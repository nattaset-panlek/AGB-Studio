#include "agb/save.hpp"

#include <tonc.h>

#include "agb/debug.hpp"

namespace agb::save {

namespace {
struct Header {
    u32 magic;
    u32 counter;
    u16 version;
    u16 size;
    u32 crc;
};
static_assert(sizeof(Header) == 16);
constexpr u32 kMagic = 0x53424741;  // "AGBS"

Config g_config;
MigrateFn g_migrate = nullptr;
bool g_migrated = false;

constexpr u32 makeCrcEntry(u32 n) {
    u32 c = n;
    for (int k = 0; k < 8; ++k) c = (c & 1) ? 0xEDB88320u ^ (c >> 1) : c >> 1;
    return c;
}
struct CrcTable {
    u32 v[256];
    constexpr CrcTable() : v() {
        for (u32 i = 0; i < 256; ++i) v[i] = makeCrcEntry(i);
    }
};
constexpr CrcTable kCrc{};

volatile u8* const kSram = reinterpret_cast<volatile u8*>(0x0E000000);

// ---- Flash (64 KB / 128 KB, 4 KB sectors) ------------------------------------------------------
int g_flashBank = -1;

void flashCommand(u8 cmd) {
    kSram[0x5555] = 0xAA;
    kSram[0x2AAA] = 0x55;
    kSram[0x5555] = cmd;
}

void flashBank(int bank) {
    if (g_config.type != Type::Flash128K || bank == g_flashBank) return;
    flashCommand(0xB0);
    kSram[0] = static_cast<u8>(bank);
    g_flashBank = bank;
}

bool flashWait(volatile u8* p, u8 expect) {
    for (int i = 0; i < 200000; ++i)
        if (*p == expect) return true;
    return false;
}

void flashEraseSector(u32 offset) {
    flashBank(static_cast<int>(offset >> 16));
    volatile u8* sector = kSram + (offset & 0xF000);
    flashCommand(0x80);
    kSram[0x5555] = 0xAA;
    kSram[0x2AAA] = 0x55;
    *sector = 0x30;
    if (!flashWait(sector, 0xFF)) debug::log("warning: flash sector erase timed out");
}

void flashProgram(u32 offset, u8 value) {
    flashBank(static_cast<int>(offset >> 16));
    volatile u8* p = kSram + (offset & 0xFFFF);
    flashCommand(0xA0);
    *p = value;
    if (!flashWait(p, value)) debug::log("warning: flash program timed out");
}

// ---- EEPROM 8 KB (64-bit blocks, 14-bit addresses, bit-serial through DMA3) ----------------------
volatile u16* const kEeprom = reinterpret_cast<volatile u16*>(0x0D000000);

// The compiler cannot see DMA reading or writing memory, so the barriers make every store to the
// source happen before the transfer and every load from the destination happen after it.
void eepromDma(volatile u16* dst, const volatile u16* src, u32 halfwords) {
    asm volatile("" ::: "memory");
    REG_DMA3CNT = 0;
    REG_DMA3SAD = reinterpret_cast<u32>(src);
    REG_DMA3DAD = reinterpret_cast<u32>(dst);
    REG_DMA3CNT = halfwords | DMA_ENABLE | DMA_16;
    while (REG_DMA3CNT & DMA_ENABLE) {}
    asm volatile("" ::: "memory");
}

void eepromReadBlock(u32 block, u8* out) {
    u16 bits[17];
    bits[0] = 1;
    bits[1] = 1;
    for (int i = 0; i < 14; ++i) bits[2 + i] = (block >> (13 - i)) & 1;
    bits[16] = 0;
    eepromDma(const_cast<u16*>(kEeprom), bits, 17);
    u16 in[68];
    eepromDma(in, kEeprom, 68);
    for (int b = 0; b < 8; ++b) {
        u8 v = 0;
        for (int i = 0; i < 8; ++i) v = static_cast<u8>((v << 1) | (in[4 + b * 8 + i] & 1));
        out[b] = v;
    }
}

void eepromWriteBlock(u32 block, const u8* data) {
    u16 bits[81];
    bits[0] = 1;
    bits[1] = 0;
    for (int i = 0; i < 14; ++i) bits[2 + i] = (block >> (13 - i)) & 1;
    for (int b = 0; b < 8; ++b)
        for (int i = 0; i < 8; ++i) bits[16 + b * 8 + i] = (data[b] >> (7 - i)) & 1;
    bits[80] = 0;
    eepromDma(const_cast<u16*>(kEeprom), bits, 81);
    for (int i = 0; i < 100000; ++i)
        if (*kEeprom & 1) return;
    debug::log("warning: EEPROM write timed out");
}

// Bytes reserved per copy. It depends only on the save type and slot count, never on the payload
// size, so a newer format version still finds the copies an older version wrote.
u32 copyBytes() {
    const u32 copies = static_cast<u32>(g_config.slots ? g_config.slots : 1) * 2;
    u32 per = capacityBytes(g_config.type) / copies;
    switch (g_config.type) {
        case Type::Flash64K:
        case Type::Flash128K: per &= ~0xFFFu; break;  // whole 4 KB sectors
        case Type::Eeprom8K: per &= ~7u; break;        // whole 8-byte blocks
        default: per &= ~3u;
    }
    return per;
}

u32 copyOffset(int slot, int copy) { return (static_cast<u32>(slot) * 2 + static_cast<u32>(copy)) * copyBytes(); }

bool readHeader(int slot, int copy, Header& h) {
    deviceRead(copyOffset(slot, copy), &h, sizeof h);
    return h.magic == kMagic;
}

bool copyValid(int slot, int copy, Header& h, u8* buffer) {
    if (!readHeader(slot, copy, h)) return false;
    deviceRead(copyOffset(slot, copy) + sizeof(Header), buffer, h.size);
    return !g_config.checksum || crc32(buffer, h.size) == h.crc;
}
}  // namespace

u32 crc32(const void* data, u32 size, u32 crc) {
    crc = ~crc;
    const u8* p = static_cast<const u8*>(data);
    for (u32 i = 0; i < size; ++i) crc = kCrc.v[(crc ^ p[i]) & 0xFF] ^ (crc >> 8);
    return ~crc;
}

void configure(const Config& config, MigrateFn migrate) {
    g_config = config;
    g_migrate = migrate;
    g_flashBank = -1;
    if (config.type != Type::None && (copyBytes() < sizeof(Header) + config.payloadSize || config.payloadSize > 4096))
        debug::logf("warning: save data needs %u bytes per slot but the save type has room for %lu", config.payloadSize, maxPayload() < 4096 ? maxPayload() : 4096);
}

const Config& config() { return g_config; }

u32 capacityBytes(Type t) {
    switch (t) {
        case Type::Sram32K: return 32 * 1024;
        case Type::Flash64K: return 64 * 1024;
        case Type::Flash128K: return 128 * 1024;
        case Type::Eeprom8K: return 8 * 1024;
        default: return 0;
    }
}

u32 bytesUsed() { return g_config.type == Type::None ? 0 : (sizeof(Header) + g_config.payloadSize) * 2 * g_config.slots; }
u32 maxPayload() { return g_config.type == Type::None ? 0 : copyBytes() - sizeof(Header); }

void deviceRead(u32 offset, void* out, u32 size) {
    u8* o = static_cast<u8*>(out);
    switch (g_config.type) {
        case Type::Sram32K:
            for (u32 i = 0; i < size; ++i) o[i] = kSram[offset + i];
            break;
        case Type::Flash64K:
        case Type::Flash128K:
            for (u32 i = 0; i < size; ++i) {
                flashBank(static_cast<int>((offset + i) >> 16));
                o[i] = kSram[(offset + i) & 0xFFFF];
            }
            break;
        case Type::Eeprom8K: {
            u8 block[8];
            for (u32 i = 0; i < size; ++i) {
                const u32 at = offset + i;
                if (i == 0 || (at & 7) == 0) eepromReadBlock(at >> 3, block);
                o[i] = block[at & 7];
            }
            break;
        }
        default:
            for (u32 i = 0; i < size; ++i) o[i] = 0xFF;
    }
}

void deviceWrite(u32 offset, const void* data, u32 size) {
    const u8* d = static_cast<const u8*>(data);
    switch (g_config.type) {
        case Type::Sram32K:
            for (u32 i = 0; i < size; ++i) kSram[offset + i] = d[i];
            break;
        case Type::Flash64K:
        case Type::Flash128K:
            // Callers write whole copies that start on a sector; erase each sector first.
            for (u32 i = 0; i < size; ++i) {
                const u32 at = offset + i;
                if ((at & 0xFFF) == 0 || i == 0) flashEraseSector(at & ~0xFFFu);
                if (d[i] != 0xFF) flashProgram(at, d[i]);
            }
            break;
        case Type::Eeprom8K: {
            u8 block[8];
            u32 i = 0;
            while (i < size) {
                const u32 at = offset + i, b = at >> 3, first = at & 7;
                if (first != 0 || size - i < 8) eepromReadBlock(b, block);
                for (u32 k = first; k < 8 && i < size; ++k, ++i) block[k] = d[i];
                eepromWriteBlock(b, block);
            }
            break;
        }
        default: break;
    }
}

bool write(int slot, const void* data) {
    if (g_config.type == Type::None || slot < 0 || slot >= g_config.slots) return false;
    Header a{}, b{};
    const bool va = readHeader(slot, 0, a), vb = readHeader(slot, 1, b);
    const u32 counter = (va && (!vb || a.counter >= b.counter) ? a.counter : vb ? b.counter : 0) + 1;
    // Write over the older (or invalid) copy.
    const int target = !va ? 0 : !vb ? 1 : (a.counter <= b.counter ? 0 : 1);
    const u32 total = sizeof(Header) + g_config.payloadSize;
    AGB_EWRAM_BSS static u8 buffer[sizeof(Header) + 4096];
    if (total > sizeof buffer) {
        debug::log("warning: save payload larger than 4 KB is not supported");
        return false;
    }
    Header h{kMagic, counter, g_config.formatVersion, g_config.payloadSize, crc32(data, g_config.payloadSize)};
    __builtin_memcpy(buffer, &h, sizeof h);
    __builtin_memcpy(buffer + sizeof h, data, g_config.payloadSize);
    deviceWrite(copyOffset(slot, target), buffer, total);
    // Verify what was written.
    Header check{};
    AGB_EWRAM_BSS static u8 verify[4096];
    return copyValid(slot, target, check, verify) && check.counter == counter;
}

bool read(int slot, void* data) {
    g_migrated = false;
    if (g_config.type == Type::None || slot < 0 || slot >= g_config.slots) return false;
    AGB_EWRAM_BSS static u8 bufA[4096];
    AGB_EWRAM_BSS static u8 bufB[4096];
    Header a{}, b{};
    const bool va = copyValid(slot, 0, a, bufA), vb = copyValid(slot, 1, b, bufB);
    if (!va && !vb) return false;
    const bool useA = va && (!vb || a.counter >= b.counter);
    const Header& h = useA ? a : b;
    u8* src = useA ? bufA : bufB;
    u8* out = static_cast<u8*>(data);
    if (h.version != g_config.formatVersion || h.size != g_config.payloadSize) {
        g_migrated = true;
        if (g_migrate) return g_migrate(h.version, src, h.size, g_config.payloadSize) && (__builtin_memcpy(out, src, g_config.payloadSize), true);
        for (u32 i = 0; i < g_config.payloadSize; ++i) out[i] = i < h.size ? src[i] : 0;
        return true;
    }
    __builtin_memcpy(out, src, g_config.payloadSize);
    return true;
}

bool exists(int slot) {
    AGB_EWRAM_BSS static u8 tmp[4096];
    Header h{};
    return copyValid(slot, 0, h, tmp) || copyValid(slot, 1, h, tmp);
}

void erase(int slot) {
    static const Header blank{0xFFFFFFFFu, 0, 0, 0, 0};
    deviceWrite(copyOffset(slot, 0), &blank, sizeof blank);
    deviceWrite(copyOffset(slot, 1), &blank, sizeof blank);
}

bool lastReadMigrated() { return g_migrated; }

}  // namespace agb::save
