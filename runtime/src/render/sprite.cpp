#include "agb/render/sprite.hpp"

#include <tonc.h>

#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/palette.hpp"
#include "agb/system.hpp"

namespace agb::render::sprites {

namespace {
struct Instance {
    const data::SpriteSheet* sheet;
    s16 x, y;
    u16 frame;
    s16 order;
    u8 priority;
    bool alive, visible, hflip, vflip, mosaic, blend;
    s8 anim;
    u8 animStep, tick;
    s8 pingDir;
    bool animDone;
    fixed speed, tickAcc;
    s8 affine;  // matrix index or -1
    bool doubleSize;
    s16 cacheSlot;  // VRAM cache entry shown now, -1 = none
    s8 palBank;
};

struct CacheEntry {
    const data::SpriteSheet* sheet;
    u16 frame;
    u16 tile;    // first OBJ tile
    u16 tiles;
    u16 refs;
    bool loaded;  // copy queued successfully
    u32 lastUse;
};

constexpr int kCacheSize = 96;
AGB_EWRAM_BSS Instance g_sprites[kMaxSprites];
CacheEntry g_cache[kCacheSize];
u32 g_tileUsed[32];  // bit per OBJ tile (1024 tiles)
bool g_affineUsed[kMaxAffine];
u32 g_useClock = 0;
u32 g_copies = 0;
int g_drawn = 0;
bool g_mosaicAll = false;
constexpr int kLineCycles = 1210;  // sprite rendering cycles per screen line (HBlank not free for OAM)

bool tilesFree(int first, int count) {
    for (int t = first; t < first + count; ++t)
        if (t >= 1024 || (g_tileUsed[t >> 5] & (1u << (t & 31)))) return false;
    return true;
}

void markTiles(int first, int count, bool used) {
    for (int t = first; t < first + count; ++t) {
        if (used) g_tileUsed[t >> 5] |= 1u << (t & 31);
        else g_tileUsed[t >> 5] &= ~(1u << (t & 31));
    }
}

int allocTiles(int count) {
    // Sizes are powers of two in practice; align so large frames do not fragment small ones.
    const int align = count >= 8 ? 8 : count >= 4 ? 4 : 1;
    // In Modes 3, 4 and 5 the bitmap covers OBJ tiles 0..511 (render/bitmap.hpp).
    for (int t = bg::videoMode() >= 3 ? 512 : 0; t + count <= 1024; t += align)
        if (tilesFree(t, count)) {
            markTiles(t, count, true);
            return t;
        }
    return -1;
}

void evict(CacheEntry& e) {
    markTiles(e.tile, e.tiles, false);
    e = {};
}

int acquireFrame(const data::SpriteSheet* sheet, u16 frame) {
    ++g_useClock;
    for (int i = 0; i < kCacheSize; ++i)
        if (g_cache[i].sheet == sheet && g_cache[i].frame == frame && g_cache[i].tiles) {
            ++g_cache[i].refs;
            g_cache[i].lastUse = g_useClock;
            return i;
        }
    const int tiles = sheet->tilesPerFrame;
    int slot = -1;
    for (int i = 0; i < kCacheSize && slot < 0; ++i)
        if (!g_cache[i].tiles) slot = i;
    int tile = allocTiles(tiles);
    // Evict unused frames, oldest first, until it fits.
    while (tile < 0 || slot < 0) {
        int oldest = -1;
        for (int i = 0; i < kCacheSize; ++i)
            if (g_cache[i].tiles && !g_cache[i].refs && (oldest < 0 || g_cache[i].lastUse < g_cache[oldest].lastUse)) oldest = i;
        if (oldest < 0) {
            debug::warnOnce(10, "sprite memory is full, so a sprite frame could not be shown. Use fewer or smaller sprites in this scene.");
            return -1;
        }
        evict(g_cache[oldest]);
        if (slot < 0) slot = oldest;
        if (tile < 0) tile = allocTiles(tiles);
    }
    g_cache[slot] = {sheet, frame, static_cast<u16>(tile), static_cast<u16>(tiles), 1, false, g_useClock};
    return slot;
}

void releaseFrame(int slot) {
    if (slot >= 0 && g_cache[slot].refs) --g_cache[slot].refs;
}

bool ensureLoaded(CacheEntry& e) {
    if (e.loaded) return true;
    const data::SpriteSheet& s = *e.sheet;
    const u32 bytes = static_cast<u32>(e.tiles) * 32;
    const u8* src = static_cast<const u8*>(s.gfx.data) + static_cast<u32>(e.frame) * bytes;
    if (!commit::queueCopy(reinterpret_cast<u8*>(commit::objTiles()) + static_cast<u32>(e.tile) * 32, src, bytes)) return false;
    ++g_copies;
    e.loaded = true;
    return true;
}

Instance* get(Id id) { return id >= 0 && id < kMaxSprites && g_sprites[id].alive ? &g_sprites[id] : nullptr; }

void applyAnimFrame(Instance& s) {
    const data::AnimDef& a = s.sheet->anims[s.anim];
    s.frame = a.frames[s.animStep];
}
}  // namespace

void reset() {
    for (auto& s : g_sprites) s = {};
    for (auto& e : g_cache) e = {};
    for (auto& w : g_tileUsed) w = 0;
    for (auto& a : g_affineUsed) a = false;
    g_mosaicAll = false;
    commit::ObjAttr* oam = commit::oam();
    for (int i = 0; i < 128; ++i) oam[i].attr0 = ATTR0_HIDE;
}

Id create(const data::SpriteSheet* sheet, u8 priority) {
    for (int i = 0; i < kMaxSprites; ++i) {
        if (g_sprites[i].alive) continue;
        Instance& s = g_sprites[i];
        s = {};
        s.sheet = sheet;
        s.alive = s.visible = true;
        s.priority = priority;
        s.anim = -1;
        s.affine = -1;
        s.cacheSlot = -1;
        s.speed = fixed(1);
        s.pingDir = 1;
        const int bank = sheet->palette ? palettes::acquire(sheet->palette, true) : 0;
        s.palBank = static_cast<s8>(bank < 0 ? 0 : bank);
        return static_cast<Id>(i);
    }
    debug::warnOnce(11, "all 128 sprites are in use, so a new sprite was not created. Destroy sprites or actors you no longer need.");
    return kNone;
}

void destroy(Id id) {
    Instance* s = get(id);
    if (!s) return;
    releaseFrame(s->cacheSlot);
    if (s->affine >= 0) g_affineUsed[s->affine] = false;
    if (s->sheet->palette) palettes::release(s->sheet->palette, true);
    s->alive = false;
}

bool valid(Id id) { return get(id) != nullptr; }

void setPosition(Id id, int x, int y) {
    if (Instance* s = get(id)) s->x = static_cast<s16>(x), s->y = static_cast<s16>(y);
}

void setFrame(Id id, u16 frame) {
    if (Instance* s = get(id)) {
        s->anim = -1;
        s->frame = frame < s->sheet->frameCount ? frame : 0;
    }
}

void play(Id id, int anim, bool restart) {
    Instance* s = get(id);
    if (!s || anim < 0 || anim >= s->sheet->animCount) return;
    if (s->anim == anim && !restart) return;
    s->anim = static_cast<s8>(anim);
    s->animStep = 0;
    s->tick = 0;
    s->tickAcc = fixed();
    s->pingDir = 1;
    s->animDone = false;
    applyAnimFrame(*s);
}

int playByName(Id id, const char* name) {
    Instance* s = get(id);
    if (!s) return -1;
    for (int i = 0; i < s->sheet->animCount; ++i) {
        const char* a = s->sheet->anims[i].name;
        const char* b = name;
        while (*a && *a == *b) ++a, ++b;
        if (*a == *b) {
            play(id, i);
            return i;
        }
    }
    return -1;
}

void setSpeed(Id id, fixed speed) {
    if (Instance* s = get(id)) s->speed = speed;
}
void setFlip(Id id, bool h, bool v) {
    if (Instance* s = get(id)) s->hflip = h, s->vflip = v;
}
void setVisible(Id id, bool v) {
    if (Instance* s = get(id)) s->visible = v;
}
void setPriority(Id id, u8 p) {
    if (Instance* s = get(id)) s->priority = p & 3;
}
void setOrder(Id id, s16 order) {
    if (Instance* s = get(id)) s->order = order;
}
void setMosaic(Id id, bool on) {
    if (Instance* s = get(id)) s->mosaic = on;
}
void setMosaicAll(bool on) { g_mosaicAll = on; }
void setBlend(Id id, bool on) {
    if (Instance* s = get(id)) s->blend = on;
}

void setAffine(Id id, u8 angle, fixed sx, fixed sy, bool doubleSize) {
    Instance* s = get(id);
    if (!s) return;
    if (s->affine < 0) {
        for (int i = 0; i < kMaxAffine; ++i)
            if (!g_affineUsed[i]) {
                g_affineUsed[i] = true;
                s->affine = static_cast<s8>(i);
                break;
            }
        if (s->affine < 0) {
            debug::warnOnce(12, "32 sprites are already rotated or scaled, so this one is drawn without it. Rotate or scale fewer sprites at once.");
            return;
        }
    }
    s->doubleSize = doubleSize;
    // Inverse transform: P = R(-a) * S^-1 in 8.8.
    const fixed c = math::cos(angle), sn = math::sin(angle);
    const fixed isx = sx.raw ? fixed(1) / sx : fixed(), isy = sy.raw ? fixed(1) / sy : fixed();
    commit::setAffine(s->affine, static_cast<s16>((c * isx).raw), static_cast<s16>((-sn * isx).raw),
                      static_cast<s16>((sn * isy).raw), static_cast<s16>((c * isy).raw));
}

void clearAffine(Id id) {
    Instance* s = get(id);
    if (!s || s->affine < 0) return;
    g_affineUsed[s->affine] = false;
    s->affine = -1;
}

u16 frame(Id id) {
    const Instance* s = get(id);
    return s ? s->frame : 0;
}
int anim(Id id) {
    const Instance* s = get(id);
    return s ? s->anim : -1;
}
bool animDone(Id id) {
    const Instance* s = get(id);
    return !s || s->animDone;
}

void update() {
    for (Instance& s : g_sprites) {
        if (!s.alive || s.anim < 0 || s.animDone) continue;
        const data::AnimDef& a = s.sheet->anims[s.anim];
        s.tickAcc += s.speed;
        while (s.tickAcc >= fixed(1)) {
            s.tickAcc -= fixed(1);
            if (++s.tick < a.ticks[s.animStep]) continue;
            s.tick = 0;
            if (a.pingPong && a.frameCount > 1) {
                int next = s.animStep + s.pingDir;
                if (next < 0 || next >= a.frameCount) {
                    s.pingDir = static_cast<s8>(-s.pingDir);
                    next = s.animStep + s.pingDir;
                }
                s.animStep = static_cast<u8>(next);
            } else if (s.animStep + 1 < a.frameCount) {
                ++s.animStep;
            } else if (a.loop) {
                s.animStep = 0;
            } else {
                s.animDone = true;
                break;
            }
            applyAnimFrame(s);
        }
    }
}

int build() {
    g_copies = 0;
    // Order by (priority asc, order desc, index): earlier OAM entries draw in front. A counting pass by priority
    // (4 buckets) then an insertion sort by order inside each bucket: with 128 sprites of mixed priorities a plain
    // insertion sort took most of a frame (render_stress).
    s16 sorted[kMaxSprites];
    s16 orders[kMaxSprites];
    int start[5] = {};
    for (const Instance& s : g_sprites)
        if (s.alive && s.visible) ++start[s.priority + 1];
    for (int p = 1; p < 5; ++p) start[p] += start[p - 1];
    const int n = start[4];
    int fill[4] = {start[0], start[1], start[2], start[3]};
    for (int i = 0; i < kMaxSprites; ++i) {
        const Instance& s = g_sprites[i];
        if (!s.alive || !s.visible) continue;
        const int at = fill[s.priority]++;
        sorted[at] = static_cast<s16>(i);
        orders[at] = s.order;
    }
    for (int p = 0; p < 4; ++p)
        for (int i = start[p] + 1; i < start[p + 1]; ++i) {
            const s16 v = sorted[i], o = orders[i];
            int j = i - 1;
            while (j >= start[p] && orders[j] < o) {
                sorted[j + 1] = sorted[j];
                orders[j + 1] = orders[j];
                --j;
            }
            sorted[j + 1] = v;
            orders[j + 1] = o;
        }
    commit::ObjAttr* oam = commit::oam();
    int out = 0;
#if defined(AGB_DEBUG)
    // Debug builds check the sprite load per line every 32 frames: the hardware has 1210 cycles per line for sprites
    // (one per pixel of width, 2 per pixel of an affine sprite's box plus 10) and silently drops what does not fit.
    const bool checkLines = (system::frame() & 31) == 0;
    u16 lineCycles[160];
    if (checkLines)
        for (u16& c : lineCycles) c = 0;
#endif
    for (int k = 0; k < n; ++k) {
        Instance& s = g_sprites[sorted[k]];
        // Swap the cached frame when the frame changed.
        const CacheEntry* cur = s.cacheSlot >= 0 ? &g_cache[s.cacheSlot] : nullptr;
        if (!cur || cur->frame != s.frame || cur->sheet != s.sheet) {
            const int slot = acquireFrame(s.sheet, s.frame);
            if (slot >= 0) {
                releaseFrame(s.cacheSlot);
                s.cacheSlot = static_cast<s16>(slot);
            }
        }
        if (s.cacheSlot < 0) continue;
        CacheEntry& e = g_cache[s.cacheSlot];
        g_cache[s.cacheSlot].lastUse = g_useClock;
        if (!ensureLoaded(e)) continue;  // budget used up: shown next frame
        const data::SpriteSheet& sh = *s.sheet;
        const int ds = s.affine >= 0 && s.doubleSize ? 2 : 1;
        const int left = s.x - sh.originX - (ds == 2 ? sh.width / 2 : 0);
        const int top = s.y - sh.originY - (ds == 2 ? sh.height / 2 : 0);
        if (left >= 240 || top >= 160 || left + sh.width * ds <= 0 || top + sh.height * ds <= 0) continue;
        u16 a0 = static_cast<u16>((top & 0xFF) | (static_cast<u16>(sh.shape) << 14));
        if (s.mosaic || g_mosaicAll) a0 |= ATTR0_MOSAIC;
        if (s.blend) a0 |= ATTR0_BLEND;
        u16 a1 = static_cast<u16>((left & 0x1FF) | (static_cast<u16>(sh.sizeCode) << 14));
        if (s.affine >= 0) {
            a0 |= ds == 2 ? ATTR0_AFF_DBL : ATTR0_AFF;
            a1 |= static_cast<u16>(s.affine << 9);
        } else {
            if (s.hflip) a1 |= ATTR1_HFLIP;
            if (s.vflip) a1 |= ATTR1_VFLIP;
        }
        const u16 a2 = static_cast<u16>(e.tile | ATTR2_PRIO(s.priority) | ATTR2_PALBANK(s.palBank));
        oam[out].attr0 = a0;
        oam[out].attr1 = a1;
        oam[out].attr2 = a2;
#if defined(AGB_DEBUG)
        if (checkLines) {
            const int cost = s.affine >= 0 ? 2 * sh.width * ds + 10 : sh.width;
            const int y0 = top < 0 ? 0 : top, y1 = top + sh.height * ds > 160 ? 160 : top + sh.height * ds;
            for (int y = y0; y < y1; ++y) lineCycles[y] = static_cast<u16>(lineCycles[y] + cost);
        }
#endif
        if (++out == 128) break;
    }
#if defined(AGB_DEBUG)
    if (checkLines) {
        int worst = 0;
        for (int y = 1; y < 160; ++y)
            if (lineCycles[y] > lineCycles[worst]) worst = y;
        if (lineCycles[worst] > kLineCycles)
            debug::logf("warning: screen line %d needs %d sprite cycles of the %d the hardware has, so some sprites on it are hidden. Use fewer on that row.",
                        worst, lineCycles[worst], kLineCycles);
    }
#endif
    for (int i = out; i < 128; ++i) oam[i].attr0 = ATTR0_HIDE;
    g_drawn = out;
    return out;
}

int count() {
    int n = 0;
    for (const auto& s : g_sprites) n += s.alive;
    return n;
}

int vramTilesUsed() {
    int n = 0;
    for (u32 w : g_tileUsed) n += __builtin_popcount(w);
    return n;
}

int affineUsed() {
    int n = 0;
    for (bool b : g_affineUsed) n += b;
    return n;
}

u32 frameCopiesThisFrame() { return g_copies; }

}  // namespace agb::render::sprites
