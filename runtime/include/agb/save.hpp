// Save data (Project Settings · Save data): N slots, each stored twice (copy A / B) with a write
// counter, format version and CRC32. A write always goes to the older copy, so losing power
// mid-write leaves the other copy intact.
#pragma once
#include "agb/types.hpp"

namespace agb::save {

enum class Type : u8 { None, Sram32K, Flash64K, Flash128K, Eeprom8K };

struct Config {
    Type type = Type::None;
    u8 slots = 1;
    u16 payloadSize = 0;    // bytes per slot (generated from the declared save fields)
    u16 formatVersion = 1;  // bumped by the build when the field layout changes
    bool checksum = true;
};

// Called when a slot was written by an older format version. Default (nullptr): bytes that exist
// in both versions are kept and new trailing fields start at zero.
using MigrateFn = bool (*)(u16 fromVersion, u8* data, u32 storedSize, u32 currentSize);

void configure(const Config& config, MigrateFn migrate = nullptr);
const Config& config();
u32 capacityBytes(Type t);
u32 bytesUsed();   // bytes of header + payload over all slots and copies
u32 maxPayload();  // largest payload a slot can hold with this type and slot count

bool write(int slot, const void* data);
// Fills data with the newest valid copy. Returns false when the slot is empty or both copies are bad.
bool read(int slot, void* data);
bool exists(int slot);
void erase(int slot);
int lastSlot();  // the slot last read or written (0 at boot): Project Settings · Save data "Autosave" writes it
bool lastReadMigrated();

// The global options block (Options screen: music and effects volume, language, button remap), kept on the
// chip outside the slots: Flash reserves its last 4 KB sector, SRAM and EEPROM their last 8 bytes, so the
// slots never overlap it. kOptionsBytes (8) is counted in Project Settings � Save data.
constexpr u32 kOptionsBytes = 8;
struct Options {
    u8 music = 10;       // 0..10
    u8 effects = 10;     // 0..10
    u8 language = 0;
    u8 remap[4] = {0xFF, 0xFF, 0xFF, 0xFF};  // 8 nibbles, one per action: index of the key (0 A, 1 B, 2 Select, 3 Start, 4 R, 5 L) or 0xF = default
};
// readOptions: false when no options were saved (or the chip has no save type); `o` is then left unchanged.
bool readOptions(Options& o);
bool writeOptions(const Options& o);

u32 crc32(const void* data, u32 size, u32 crc = 0);

// Raw device access (tests and the hand-over snapshot of hot reload).
void deviceRead(u32 offset, void* out, u32 size);
void deviceWrite(u32 offset, const void* data, u32 size);

}  // namespace agb::save
