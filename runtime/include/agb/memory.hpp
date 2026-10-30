// Memory arenas (reset per Screen) and fixed-size pools with generation-checked handles.
// There is no heap: everything a Screen allocates lives in its arena and is dropped on exit.
#pragma once
#include <new>

#include "agb/assert.hpp"
#include "agb/debug.hpp"
#include "agb/types.hpp"

namespace agb {

class Arena {
public:
    Arena(void* buffer, u32 size, const char* name) : base_(static_cast<u8*>(buffer)), size_(size), name_(name) {}

    void* alloc(u32 bytes, u32 align = 4) {
        const u32 start = (used_ + align - 1) & ~(align - 1);
        if (start + bytes > size_) {
            debug::logf("warning: arena '%s' is full (%lu of %lu bytes); allocation of %lu failed", name_, used_, size_, bytes);
            return nullptr;
        }
        used_ = start + bytes;
        if (used_ > peak_) peak_ = used_;
        return base_ + start;
    }

    template <typename T, typename... Args>
    T* make(Args&&... args) {
        void* p = alloc(sizeof(T), alignof(T));
        return p ? new (p) T(static_cast<Args&&>(args)...) : nullptr;
    }

    void reset() { used_ = 0; }
    void rewind(u32 used) { if (used < used_) used_ = used; }  // frees everything allocated after used()
    u32 used() const { return used_; }
    u32 peak() const { return peak_; }
    u32 size() const { return size_; }

private:
    u8* base_;
    u32 size_, used_ = 0, peak_ = 0;
    const char* name_;
};

// The per-Screen arena in EWRAM (blueprint 10: 112 KB).
Arena& screenArena();

template <typename T>
struct Handle {
    u16 index = 0xFFFF;
    u16 gen = 0;
    bool valid() const { return index != 0xFFFF; }
    bool operator==(const Handle&) const = default;
};

template <typename T, int N>
class Pool {
public:
    explicit Pool(const char* name) : name_(name) {
        for (int i = 0; i < N; ++i) gen_[i] = 1, live_[i] = false;
    }

    template <typename... Args>
    Handle<T> create(Args&&... args) {
        for (int i = 0; i < N; ++i) {
            if (live_[i]) continue;
            live_[i] = true;
            new (&slot(i)) T(static_cast<Args&&>(args)...);
            ++count_;
            return Handle<T>{static_cast<u16>(i), gen_[i]};
        }
        debug::logf("warning: pool '%s' is full (%d); the new object was not created", name_, N);
        return {};
    }

    T* get(Handle<T> h) {
        if (!h.valid() || h.index >= N) return nullptr;
        if (!live_[h.index] || gen_[h.index] != h.gen) {
            AGB_ASSERT(false, "stale handle used after the object was destroyed");
            return nullptr;
        }
        return &slot(h.index);
    }

    void destroy(Handle<T> h) {
        if (!h.valid() || h.index >= N || !live_[h.index] || gen_[h.index] != h.gen) {
            AGB_ASSERT(false, "double free or stale handle");
            return;
        }
        slot(h.index).~T();
        live_[h.index] = false;
        ++gen_[h.index];
        if (gen_[h.index] == 0) gen_[h.index] = 1;
        --count_;
    }

    void clear() {
        for (int i = 0; i < N; ++i)
            if (live_[i]) destroy(Handle<T>{static_cast<u16>(i), gen_[i]});
    }

    int count() const { return count_; }
    static constexpr int capacity() { return N; }

    template <typename F>
    void forEach(F&& f) {
        for (int i = 0; i < N; ++i)
            if (live_[i]) f(slot(i), Handle<T>{static_cast<u16>(i), gen_[i]});
    }

private:
    T& slot(int i) { return *reinterpret_cast<T*>(&storage_[i * sizeof(T)]); }
    alignas(T) u8 storage_[N * sizeof(T)];
    u16 gen_[N];
    bool live_[N];
    int count_ = 0;
    const char* name_;
};

}  // namespace agb
