// Video modes 0-3 (Video Mode popover T-O02) with alpha blending and mosaic, four tile layers in Mode 0 (BG manager,
// T-P03 layer table) and a sweep of rotate / scale transforms (T-M20). Each picture is held on screen after a
// "shot <name>" log line; RuntimeRomTests.VideoModesMatchReferenceImages captures it and compares it pixel for pixel
// with a reference image computed on the host from the same content. The checks in this ROM cover registers and
// VRAM placement.
#include "agb/hw.hpp"

#include "../test.hpp"
#include "agb/commit.hpp"
#include "agb/memory.hpp"
#include "agb/render/affine.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/bitmap.hpp"
#include "agb/render/effects.hpp"
#include "agb/render/map.hpp"
#include "agb/render/palette.hpp"
#include "agb/render/sprite.hpp"

using namespace agb;
using namespace agb::render;

namespace {
// ---- content shared with the host reference (RuntimeRomTests.ModeReference) ----------------------------------------
// Tile layers: 4bpp tiles 0 (clear), 1 (colour 1), 2 (colour 2); metatiles 0, 1, 2 made of them. 15 x 10 metatiles.
AGB_EWRAM_BSS u32 g_tiles4[3 * 8];
const data::Metatile kMeta[3] = {{{0, 0, 0, 0}}, {{1, 1, 1, 1}}, {{2, 2, 2, 2}}};
const data::Collision kCol[3] = {};
const u16 kLayerColors[4][16] = {
    {0, 0x001F, 0x03FF},  // BG0: red, yellow
    {0, 0x03E0, 0x7FE0},  // BG1: green, cyan
    {0, 0x7C00, 0x7C1F},  // BG2: blue, magenta
    {0, 0x4210, 0x1084},  // BG3: grey, dark grey
};
const data::Palette kLayerPal[4] = {{kLayerColors[0], 1}, {kLayerColors[1], 1}, {kLayerColors[2], 1}, {kLayerColors[3], 1}};
data::Tileset g_tilesets[4];
constexpr int kMapW = 15, kMapH = 10;
u16 g_cells[4][kMapW * kMapH];
data::MapLayer g_layers[4];

u16 layerCell(int layer, int mx, int my) {
    switch (layer) {
        case 0: return (mx <= 4 && my <= 4) ? 1 : (mx >= 12 && my >= 8) ? 2 : 0;
        case 1: return (mx >= 3 && mx <= 9 && my >= 2 && my <= 6) ? 1 : 0;
        case 2: return (mx >= 7 && my >= 4 && my <= 8) ? 1 : 0;
        default: return ((mx + my) & 1) ? 1 : 2;
    }
}

// Affine textures: 8bpp tiles 0 (colour 1), 1 (colour 2), 2 (colour 3), 3 (clear); 32 x 32 tiles.
AGB_EWRAM_BSS u32 g_tiles8[4 * 16];
const u16 kAffColors[2][16] = {
    {0, 0x03E0, 0x001F, 0x7C00},  // green, red, blue
    {0, 0x7FFF, 0x2529, 0x03FF},  // white, dark, yellow
};
const data::Palette kAffPal[2] = {{kAffColors[0], 1}, {kAffColors[1], 1}};
data::Tileset g_affTs[2];
alignas(4) u8 g_affCells[2][32 * 32];
data::MapLayer g_affLayers[2];

// Texture tile at (tx, ty) of texture 0 (asymmetric: a blue block in one corner) and texture 1 (holes).
u8 affCell(int texture, int tx, int ty) {
    if (texture == 0) return (tx < 8 && ty < 4) ? 2 : static_cast<u8>(((tx / 2) + (ty / 2)) & 1);
    return ((tx / 4 + ty / 4) & 1) ? 3 : static_cast<u8>((tx / 4) % 3);
}

// Mode 3 picture.
AGB_EWRAM_BSS u16 g_picture[bitmap::kWidth * bitmap::kHeight];
u16 picturePixel(int x, int y) { return static_cast<u16>((x >> 3) | ((y >> 3) << 5) | ((((x ^ y) >> 4) & 31) << 10)); }

// 16 x 16 sprite in colour 1 (white).
AGB_EWRAM_BSS u32 g_spriteTiles[4 * 8];
const u16 kSprColors[16] = {0, 0x7FFF};
const data::Palette kSprPal{kSprColors, 1};
const u8 kFrame0[] = {0}, kTicks[] = {1};
const data::AnimDef kSprAnim[] = {{"idle", kFrame0, kTicks, 1, true, false}};
data::SpriteSheet g_sheet;

void makeContent() {
    for (int t = 0; t < 3; ++t)
        for (int r = 0; r < 8; ++r) g_tiles4[t * 8 + r] = static_cast<u32>(t) * 0x11111111u;
    for (int i = 0; i < 4; ++i) {
        g_tilesets[i] = {};
        g_tilesets[i].gfx = {g_tiles4, sizeof g_tiles4, 3, 4, false};
        g_tilesets[i].palette = &kLayerPal[i];
        g_tilesets[i].metatiles = kMeta;
        g_tilesets[i].collision = kCol;
        g_tilesets[i].metatileCount = 3;
        for (int my = 0; my < kMapH; ++my)
            for (int mx = 0; mx < kMapW; ++mx) g_cells[i][my * kMapW + mx] = layerCell(i, mx, my);
        g_layers[i] = {static_cast<u8>(i), data::LayerType::Tile, static_cast<u8>(i), false, 256, 256, kMapW, kMapH, g_cells[i], &g_tilesets[i]};
    }
    for (int t = 0; t < 4; ++t)
        for (int w = 0; w < 16; ++w) g_tiles8[t * 16 + w] = t == 3 ? 0 : static_cast<u32>(t + 1) * 0x01010101u;
    for (int k = 0; k < 2; ++k) {
        g_affTs[k] = {};
        g_affTs[k].gfx = {g_tiles8, sizeof g_tiles8, 4, 8, false};
        g_affTs[k].palette = &kAffPal[k];
        for (int ty = 0; ty < 32; ++ty)
            for (int tx = 0; tx < 32; ++tx) g_affCells[k][ty * 32 + tx] = affCell(k, tx, ty);
        g_affLayers[k] = {2, data::LayerType::Affine, 0, true, 256, 256, 32, 32, reinterpret_cast<const u16*>(g_affCells[k]), &g_affTs[k]};
    }
    for (int y = 0; y < bitmap::kHeight; ++y)
        for (int x = 0; x < bitmap::kWidth; ++x) g_picture[y * bitmap::kWidth + x] = picturePixel(x, y);
    for (auto& w : g_spriteTiles) w = 0x11111111u;
    g_sheet = {};
    g_sheet.gfx = {g_spriteTiles, sizeof g_spriteTiles, 4, 4, false};
    g_sheet.palette = &kSprPal;
    g_sheet.width = g_sheet.height = 16;
    g_sheet.shape = data::ObjShape::Square;
    g_sheet.sizeCode = 1;
    g_sheet.tilesPerFrame = 4;
    g_sheet.frameCount = 1;
    g_sheet.anims = kSprAnim;
    g_sheet.animCount = 1;
}

void resetVideo() {
    screenArena().reset();
    effects::reset();
    sprites::reset();
    palettes::reset();
    bg::reset();
}

// Shows the picture for a few frames after logging its name (the host captures it).
void shot(const char* name) {
    palettes::update();
    sprites::build();
    system::waitVBlank();
    system::waitVBlank();
    debug::logf("shot %s", name);
    for (int i = 0; i < 4; ++i) {
        sprites::build();
        system::waitVBlank();
    }
}

void logAffine(const char* name, int bg) {
    const commit::Reg base = bg == 2 ? commit::Bg2Pa : commit::Bg3Pa;
    debug::logf("affine %s %d %d %d %d %ld %ld", name, static_cast<s16>(commit::reg(base)), static_cast<s16>(commit::reg(static_cast<commit::Reg>(base + 1))),
                static_cast<s16>(commit::reg(static_cast<commit::Reg>(base + 2))), static_cast<s16>(commit::reg(static_cast<commit::Reg>(base + 3))),
                commit::bgRef(bg, 0), commit::bgRef(bg, 1));
}

MapLayerView g_views[4];
}  // namespace

AGB_TEST(mode0_four_tile_layers_blend_and_mosaic) {
    makeContent();
    resetVideo();
    bg::setVideoMode(0);
    for (int i = 0; i < 4; ++i) {
        CHECK(g_views[i].init(g_layers[i]));
        g_views[i].setCamera(0, 0);
        g_views[i].drawAll();
        CHECK(g_views[i].commit());
    }
    // BG3 shows the mosaic (6 x 6 blocks); BG1 is blended over BG2 / BG3 / the backdrop at 10/16 + 6/16.
    bg::Config c3 = bg::config(3);
    c3.mosaic = true;
    bg::configure(3, c3);
    effects::setMosaic(6, 1);
    effects::setAlphaBlend(effects::kBg1, effects::kBg2 | effects::kBg3 | effects::kBackdrop, 10, 6);
    palettes::setBackdrop(0x0000);
    // P3.01: four regular layers at once, each with its own priority, palette bank and screen blocks; the screen
    // blocks of different layers and the tile data never overlap.
    CHECK_EQ(commit::reg(commit::DispCnt) & 7, 0);
    CHECK_EQ(commit::reg(commit::DispCnt) & 0x0F00, DCNT_BG0 | DCNT_BG1 | DCNT_BG2 | DCNT_BG3);
    u32 units = 0;
    int overlaps = 0;
    for (int i = 0; i < 4; ++i) {
        const u16 cnt = commit::reg(static_cast<commit::Reg>(commit::Bg0Cnt + i));
        CHECK_EQ(cnt & 3, i);              // priority = layer index
        CHECK_EQ(cnt >> 14, 1);            // 64 x 32 tiles: two screen blocks
        const int sb = (cnt >> 8) & 31, cb = (cnt >> 2) & 3;
        CHECK_EQ(sb, g_views[i].screenBlock());
        CHECK_EQ(cb, g_views[i].placement().charBlock);
        const u32 mine = 3u << sb;
        if (units & mine) ++overlaps;
        units |= mine;
        const u32 tileUnit = 1u << (cb * 8 + g_views[i].placement().firstTile * 32 / 2048);
        if (mine & tileUnit) ++overlaps;
        CHECK_EQ(g_views[i].placement().paletteBank, i);
        // The map in VRAM matches the layer: one tile per corner of a metatile, plus tileset base and palette bank.
        const u16 se = g_views[i].cell(2, 2);
        CHECK_EQ(se, static_cast<u16>(((layerCell(i, 1, 1) ? layerCell(i, 1, 1) : 0) + g_views[i].placement().firstTile) | (i << 12)));
    }
    CHECK_EQ(overlaps, 0);
    CHECK_EQ(bg::screenBlocksUsed(), 9);  // 4 x 2 map blocks + 1 unit of tile data
    shot("mode0");
}

AGB_TEST(mode1_two_tile_layers_and_one_affine) {
    resetVideo();
    bg::setVideoMode(1);
    for (int i = 0; i < 2; ++i) {
        CHECK(g_views[i].init(g_layers[i]));
        g_views[i].setCamera(0, 0);
        g_views[i].drawAll();
        CHECK(g_views[i].commit());
    }
    g_affLayers[0].bg = 2;
    g_affLayers[0].priority = 2;
    CHECK(affine::load(2, g_affLayers[0]));
    affine::setTransform(2, fixed::lit(1.5), 32, fixed(128), fixed(128), 120, 80);
    palettes::setBackdrop(0x0000);
    CHECK_EQ(commit::reg(commit::DispCnt) & 7, 1);
    CHECK_EQ(commit::reg(commit::DispCnt) & 0x0F00, DCNT_BG0 | DCNT_BG1 | DCNT_BG2);
    CHECK_EQ(commit::reg(commit::Bg2Cnt) & BG_WRAP, BG_WRAP);
    logAffine("mode1", 2);
    shot("mode1");
}

AGB_TEST(mode2_two_affine_layers_blended) {
    resetVideo();
    bg::setVideoMode(2);
    g_affLayers[1].bg = 2;
    g_affLayers[1].priority = 0;
    g_affLayers[0].bg = 3;
    g_affLayers[0].priority = 1;
    CHECK(affine::load(2, g_affLayers[1]));  // front: white / dark / yellow with holes
    CHECK(affine::load(3, g_affLayers[0]));  // back: green / red with the blue corner
    affine::setTransform(2, fixed(1), 16, fixed(64), fixed(64), 120, 80);
    affine::setTransform(3, fixed::lit(0.5), 0, fixed(0), fixed(0), 0, 0);
    effects::setAlphaBlend(effects::kBg2, effects::kBg3, 8, 8);
    palettes::setBackdrop(0x0000);
    CHECK_EQ(commit::reg(commit::DispCnt) & 7, 2);
    CHECK_EQ(commit::reg(commit::DispCnt) & 0x0F00, DCNT_BG2 | DCNT_BG3);
    // Each affine layer has its own empty character block.
    CHECK(((commit::reg(commit::Bg2Cnt) >> 2) & 3) != ((commit::reg(commit::Bg3Cnt) >> 2) & 3));
    logAffine("mode2a", 2);
    logAffine("mode2b", 3);
    shot("mode2");
}

AGB_TEST(affine_sweep_pictures) {
    // Rotate / scale sweep on one layer: angle (0..255) and scale pairs; the host compares every picture.
    struct Step {
        u8 angle;
        fixed scale;
    };
    const Step steps[] = {{0, fixed(1)}, {16, fixed::lit(0.5)}, {64, fixed(2)}, {100, fixed::lit(1.25)}, {200, fixed(3)}, {240, fixed::lit(0.75)}};
    resetVideo();
    bg::setVideoMode(2);
    g_affLayers[0].bg = 2;
    g_affLayers[0].priority = 0;
    CHECK(affine::load(2, g_affLayers[0]));
    palettes::setBackdrop(0x0000);
    char name[8] = "sweep0";
    for (const Step& s : steps) {
        affine::setTransform(2, s.scale, s.angle, fixed(100), fixed(60), 120, 80);
        logAffine(name, 2);
        debug::logf("sweep %s angle %d scale %ld", name, s.angle, s.scale.raw);
        shot(name);
        ++name[5];
    }
}

AGB_TEST(mode3_bitmap_mosaic_and_blended_sprite) {
    resetVideo();
    bitmap::begin();
    bitmap::show(g_picture);
    CHECK_EQ(commit::reg(commit::DispCnt) & 7, 3);
    CHECK_EQ(commit::reg(commit::DispCnt) & 0x0F00, DCNT_BG2);
    // The frame buffer holds the picture; a filled rectangle changes only its pixels.
    const u16* fb = bitmap::frameBuffer();
    CHECK_EQ(fb[0], picturePixel(0, 0));
    CHECK_EQ(fb[159 * 240 + 239], picturePixel(239, 159));
    // A filled rectangle goes through the commit queue: it changes only its pixels, and only in VBlank (P2.04).
    CHECK(bitmap::fill(200, 140, 30, 10, 0x7FFF));
    CHECK_EQ(fb[145 * 240 + 210], picturePixel(210, 145));
    system::waitVBlank();
    CHECK_EQ(fb[145 * 240 + 210], 0x7FFF);
    CHECK_EQ(fb[145 * 240 + 199], picturePixel(199, 145));
    CHECK_EQ(fb[150 * 240 + 210], picturePixel(210, 150));
    // Rows queued through the commit queue arrive in VBlank: restore the rectangle from the picture.
    CHECK(bitmap::queueRows(g_picture, 140, 10));
    system::waitVBlank();
    CHECK_EQ(fb[145 * 240 + 210], picturePixel(210, 145));
    // Mosaic 4 x 4 on the bitmap, and a semi-transparent sprite (OBJ tiles from 512 in Mode 3) blended 8/16 + 8/16.
    bg::Config c2 = bg::config(2);
    c2.mosaic = true;
    bg::configure(2, c2);
    effects::setMosaic(4, 1);
    effects::setAlphaBlend(0, effects::kBg2, 8, 8);
    const auto s = sprites::create(&g_sheet, 0);
    sprites::setPosition(s, 100, 60);
    sprites::setBlend(s, true);
    sprites::build();
    CHECK_EQ(commit::oam()[0].attr2 & 0x3FF, 512);
    CHECK(commit::oam()[0].attr0 & ATTR0_BLEND);
    shot("mode3");
    sprites::destroy(s);
}

int main() {
    system::init();
    // These tests load layers directly, without a scene's staged load: the whole run counts as one (P2.04).
    commit::beginStagedLoad();
    test::runAll();
}
