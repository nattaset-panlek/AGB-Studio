// Save tests shared by the per-save-type ROMs. Each ROM defines AGB_SAVE_TYPE and carries the
// save-type ID string that emulators and flash carts scan for (exactly one per ROM).
#pragma once
#include "test.hpp"
#include "agb/save.hpp"

using namespace agb;

#define AGB_SAVE_STR2(x) #x
#define AGB_SAVE_STR(x) AGB_SAVE_STR2(x)
#define AGB_SAVE_TEST_NAME AGB_SAVE_STR(AGB_SAVE_TYPE)

namespace {
struct Payload {
    u32 score;
    u8 lives;
    u8 pad;
    u16 coins;
    u8 flags[16];
};

void configure(u16 version = 1, u16 size = sizeof(Payload)) {
    save::Config c;
    c.type = save::Type::AGB_SAVE_TYPE;
    c.slots = 3;
    c.payloadSize = size;
    c.formatVersion = version;
    c.checksum = true;
    save::configure(c);
}
}  // namespace

AGB_TEST(write_then_read_each_slot) {
    configure();
    for (int s = 0; s < 3; ++s) save::erase(s);
    CHECK(!save::exists(0));
    Payload p{1234, 3, 0, 77, {}};
    p.flags[5] = 0xA5;
    CHECK(save::write(1, &p));
    Payload q{};
    CHECK(save::read(1, &q));
    CHECK_EQ(q.score, 1234u);
    CHECK_EQ(q.lives, 3);
    CHECK_EQ(q.coins, 77);
    CHECK_EQ(q.flags[5], 0xA5);
    CHECK(!save::read(0, &q));
    CHECK(!save::read(2, &q));
}

AGB_TEST(newest_copy_wins_and_corruption_falls_back) {
    configure();
    save::erase(0);
    Payload p{1, 1, 0, 1, {}};
    CHECK(save::write(0, &p));  // copy A, counter 1
    p.score = 2;
    CHECK(save::write(0, &p));  // copy B, counter 2
    Payload q{};
    CHECK(save::read(0, &q));
    CHECK_EQ(q.score, 2u);
    // Corrupt the newest copy (B): the older copy must load instead.
    const u32 copy = save::maxPayload() + 16;  // bytes per copy
    u8 bad = 0x5A;
    save::deviceWrite(copy + 20, &bad, 1);
    CHECK(save::read(0, &q));
    CHECK_EQ(q.score, 1u);
    // The next write goes over the corrupt copy and wins again.
    p.score = 3;
    CHECK(save::write(0, &p));
    CHECK(save::read(0, &q));
    CHECK_EQ(q.score, 3u);
}

AGB_TEST(older_format_version_migrates) {
    configure(1, 8);  // old layout: 8 bytes
    save::erase(2);
    u8 old[8] = {1, 2, 3, 4, 5, 6, 7, 8};
    CHECK(save::write(2, old));
    configure(2, sizeof(Payload));  // new layout appends fields
    Payload q{};
    CHECK(save::read(2, &q));
    CHECK(save::lastReadMigrated());
    CHECK_EQ(q.score, 0x04030201u);
    CHECK_EQ(q.flags[0], 0);
}

AGB_TEST(slots_do_not_alias) {
    // Earlier tests left slot 0 at score 3 and slot 1 at 1234; slot 2 was written last. On Flash 128K
    // slot 2 lives in the second 64 KB bank, so this catches missing bank switches.
    configure();
    Payload q{};
    CHECK(save::read(0, &q));
    CHECK_EQ(q.score, 3u);
    CHECK(save::read(1, &q));
    CHECK_EQ(q.score, 1234u);
    Payload p{99, 9, 0, 9, {}};
    CHECK(save::write(2, &p));
    CHECK(save::write(2, &p));
    CHECK(save::read(0, &q));
    CHECK_EQ(q.score, 3u);
}

AGB_TEST(checksum_off_loads_the_newest_copy_unchecked) {
    // Project Settings · Save data "Checksum" off: the CRC is still written but not checked, so a damaged newest copy
    // loads as it is. With the checksum on, the same damage makes the older copy load instead.
    configure();
    save::erase(0);
    Payload p{10, 1, 0, 1, {}};
    CHECK(save::write(0, &p));  // copy A, counter 1
    p.score = 20;
    CHECK(save::write(0, &p));  // copy B, counter 2
    save::Config c = save::config();
    c.checksum = false;
    save::configure(c);
    // Rewrite copy B whole (flash erases a sector before programming) with the low byte of the score changed.
    const u32 copy = save::maxPayload() + 16;  // bytes per copy; copy B starts here
    static u8 copyB[16 + sizeof(Payload)];
    save::deviceRead(copy, copyB, sizeof copyB);
    copyB[16] = 0x5A;
    save::deviceWrite(copy, copyB, sizeof copyB);
    Payload q{};
    CHECK(save::read(0, &q));
    CHECK_EQ(q.score, 0x5Au);
    CHECK(save::exists(0));
    configure();  // checksum on
    CHECK(save::read(0, &q));
    CHECK_EQ(q.score, 10u);
    // Writes with the checksum off still verify and the next read takes the newest copy.
    save::configure(c);
    p.score = 30;
    CHECK(save::write(0, &p));
    CHECK(save::read(0, &q));
    CHECK_EQ(q.score, 30u);
}

AGB_TEST(save_type_none_stores_nothing) {
    // Project Settings · Save data "None": nothing is written or read and no save memory is reserved.
    save::Config c;
    c.type = save::Type::None;
    c.slots = 3;
    c.payloadSize = sizeof(Payload);
    save::configure(c);
    Payload p{5, 5, 0, 5, {}};
    CHECK(!save::write(0, &p));
    CHECK(!save::read(0, &p));
    CHECK(!save::exists(0));
    CHECK_EQ(save::bytesUsed(), 0u);
    CHECK_EQ(save::maxPayload(), 0u);
    CHECK_EQ(save::capacityBytes(save::Type::None), 0u);
    configure();
}

AGB_TEST(options_block_and_a_slot_survive_a_reload) {
    // P2.10, per save type (this ROM's chip): the options block and slot 1 are written to the emulated chip, the
    // driver forgets everything it knows (configure again with another layout, then back) and both read back
    // exactly, and writing the slot leaves the block alone. The log line names the type for the host test.
    configure();
    save::erase(1);
    save::Options o;
    o.music = 7;
    o.effects = 3;
    o.language = 2;
    o.remap[1] = 0x35;
    CHECK(save::writeOptions(o));
    Payload p{4321, 2, 0, 55, {}};
    p.flags[15] = 0x3C;
    CHECK(save::write(1, &p));
    save::Config other = save::config();
    other.slots = 1;
    other.payloadSize = 4;
    save::configure(other);  // forget the layout
    configure();             // reload it from the chip
    save::Options r;
    CHECK(save::readOptions(r));
    CHECK_EQ(r.music, 7);
    CHECK_EQ(r.effects, 3);
    CHECK_EQ(r.language, 2);
    CHECK_EQ(r.remap[1], 0x35);
    Payload q{};
    CHECK(save::read(1, &q));
    CHECK_EQ(q.score, 4321u);
    CHECK_EQ(q.coins, 55);
    CHECK_EQ(q.flags[15], 0x3C);
    // the block lies outside the slots: erasing and rewriting a slot leaves it alone
    save::erase(1);
    CHECK(save::write(1, &p));
    save::Options again;
    CHECK(save::readOptions(again));
    CHECK_EQ(again.music, 7);
    debug::logf("save options+slot ok %s", AGB_SAVE_TEST_NAME);
}

AGB_TEST(capacity_and_crc) {
    configure();
    CHECK(save::bytesUsed() <= save::capacityBytes(save::Type::AGB_SAVE_TYPE));
    CHECK_EQ(save::crc32("123456789", 9), 0xCBF43926u);
}

int main() {
    AGB_SAVE_TEST_KEEP_ID();
    system::init();
    test::showResultOnScreen();  // VBA-M runs of these ROMs show green for pass
    test::runAll();
}
