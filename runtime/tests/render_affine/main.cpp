// Affine layer (rotate / scale) and the Mode 7 floor table.
#include <tonc.h>

#include "../test.hpp"
#include "agb/commit.hpp"
#include "agb/memory.hpp"
#include "agb/render/affine.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/palette.hpp"

using namespace agb;
using namespace agb::render;

namespace {
AGB_EWRAM_BSS u32 g_tiles8[2 * 16];  // two 8bpp tiles
u16 g_cells[32 * 32];
const u16 kColors[16] = {0, 0x7FFF, 0x001F, 0x03E0, 0x7C00};
const data::Palette kPal{kColors, 1};
data::Tileset g_ts;
data::MapLayer g_layer;

void setup() {
    for (int i = 0; i < 16; ++i) {
        g_tiles8[i] = 0x02020202u;       // tile 0: colour 2 (red)
        g_tiles8[16 + i] = 0x03030303u;  // tile 1: colour 3 (green)
    }
    for (int y = 0; y < 32; ++y)
        for (int x = 0; x < 32; ++x) g_cells[y * 32 + x] = static_cast<u16>(((x / 2) + (y / 2)) & 1);
    g_ts = {};
    g_ts.gfx = {g_tiles8, sizeof g_tiles8, 2, 8, false};
    g_ts.palette = &kPal;
    g_layer = {2, data::LayerType::Affine, 0, true, 256, 256, 32, 32, g_cells, &g_ts};
}
}  // namespace

AGB_TEST(affine_layer_rotates_and_scales) {
    screenArena().reset();
    bg::reset();
    palettes::reset();
    setup();
    bg::setVideoMode(1);
    CHECK(affine::load(2, g_layer));
    CHECK_EQ(commit::reg(commit::Bg2Cnt) >> 14, 1);  // 32x32 tiles (affine layers are always 8bpp)
    affine::setTransform(2, fixed(2), 32, fixed(128), fixed(128), 120, 80);
    // 45 degrees at scale 2: pa = cos / 2 = 0.3535 * 256 = 90
    CHECK_EQ(static_cast<s16>(commit::reg(commit::Bg2Pa)), 90);
    CHECK_EQ(static_cast<s16>(commit::reg(commit::Bg2Pb)), -90);
    palettes::update();
    for (int i = 0; i < 3; ++i) system::waitVBlank();
    // The map reached VRAM as bytes: first two cells are tile 0, then tile 1.
    const u8* map = reinterpret_cast<const u8*>(commit::bgMap(commit::reg(commit::Bg2Cnt) >> 8 & 31));
    CHECK_EQ(map[0], 0);
    CHECK_EQ(map[2], 1);
}

AGB_TEST(mode7_table_has_perspective) {
    affine::Mode7 m;
    m.camX = fixed(128);
    m.camY = fixed(128);
    m.camHeight = 24;
    m.angle = 0;
    m.horizon = 40;
    affine::mode7(m);
    const s32* t = affine::mode7Table();
    // Lines closer to the horizon cover more texture per pixel (larger |pc|) than lines at the bottom.
    const s32 near = static_cast<s16>(t[159 * 4 + 1] & 0xFFFF), far = static_cast<s16>(t[45 * 4 + 1] & 0xFFFF);
    debug::logf("mode7 pc at line 45: %ld, line 159: %ld", far, near);
    CHECK(far > near);
    CHECK(near > 0);
    // Above the horizon nothing is sampled.
    CHECK_EQ(t[10 * 4 + 0], 0);
    for (int i = 0; i < 30; ++i) {
        m.angle = static_cast<u8>(m.angle + 2);
        affine::mode7(m);
        system::waitVBlank();
    }
    const fixed2 g = affine::mode7Ground(m, 120, 159);
    CHECK(g.x.raw != 0 || g.y.raw != 0);
}

int main() {
    system::init();
    test::runAll();
}
