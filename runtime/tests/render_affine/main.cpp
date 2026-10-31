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
alignas(4) u8 g_cells[32 * 32];  // one tile byte per cell
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
        for (int x = 0; x < 32; ++x) g_cells[y * 32 + x] = static_cast<u8>(((x / 2) + (y / 2)) & 1);
    g_ts = {};
    g_ts.gfx = {g_tiles8, sizeof g_tiles8, 2, 8, false};
    g_ts.palette = &kPal;
    g_layer = {2, data::LayerType::Affine, 0, true, 256, 256, 32, 32, reinterpret_cast<const u16*>(g_cells), &g_ts};
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

AGB_TEST(affine_rotate_scale_sweep) {
    // Every 8th step of the circle at five scales (160 transforms). For each: the matrix is a rotation by the angle
    // divided by the scale, and the chosen texture point lands on the chosen screen point (hardware model: texture =
    // reference + (pa * x + pb * y, pc * x + pd * y)). RuntimeRomTests.AffineSweepMatchesReferenceImages checks pixels.
    const fixed scales[] = {fixed::lit(0.5), fixed(1), fixed::lit(1.5), fixed(2), fixed(3)};
    int bad = 0, checked = 0;
    for (const fixed sc : scales)
        for (int a = 0; a < 256; a += 8) {
            const fixed cx = fixed(128) + fixed(a % 7), cy = fixed(96) - fixed(a % 5);
            const int sx = 120 - a % 9, sy = 80 + a % 11;
            affine::setTransform(2, sc, static_cast<u8>(a), cx, cy, sx, sy);
            const s32 pa = static_cast<s16>(commit::reg(commit::Bg2Pa)), pb = static_cast<s16>(commit::reg(commit::Bg2Pb));
            const s32 pc = static_cast<s16>(commit::reg(commit::Bg2Pc)), pd = static_cast<s16>(commit::reg(commit::Bg2Pd));
            const s32 len = 65536 / sc.raw;  // 1 / scale in 8.8; 8.8 rounding keeps the length within 5 %
            const s32 c = math::cos(static_cast<u8>(a)).raw, s = math::sin(static_cast<u8>(a)).raw;
            const s32 len2 = pa * pa + pc * pc, want2 = len * len;
            const s32 cross = pa * s - pc * c, dot = pa * c + pc * s;
            const bool ok = pa == pd && pb == -pc && (len2 - want2) * 100 <= 5 * want2 && (want2 - len2) * 100 <= 5 * want2 &&
                            cross <= 3 * 256 && cross >= -3 * 256 && dot > 0 &&
                            commit::bgRef(2, 0) + pa * sx + pb * sy == cx.raw && commit::bgRef(2, 1) + pc * sx + pd * sy == cy.raw;
            ++checked;
            if (!ok && bad++ < 4)
                debug::logf("  scale %ld angle %d: pa %ld pb %ld pc %ld pd %ld ref %ld,%ld", sc.raw, a, pa, pb, pc, pd, commit::bgRef(2, 0),
                            commit::bgRef(2, 1));
        }
    CHECK_EQ(checked, 160);
    CHECK_EQ(bad, 0);
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
