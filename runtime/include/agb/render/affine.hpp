// Affine backgrounds (Add Layer "Rotate / scale layer (Mode 1-2)") and the Mode 7 floor used by the Racing
// template: 8bpp tiles with one-byte map entries, rotated and scaled by the BG2 / BG3 matrices.
#pragma once
#include "agb/data.hpp"
#include "agb/fixed.hpp"

namespace agb::render::affine {

// Loads an affine layer: `layer.cells` holds one tile index (0..255) per cell, the map is square with a side of
// 16, 32, 64 or 128 tiles, tiles are 8bpp. Call while the screen is blank.
bool load(int bg, const data::MapLayer& layer);

// Rotates (0..255 = full turn) and scales the layer around texture point (cx, cy), shown at screen (sx, sy).
void setTransform(int bg, fixed scale, u8 angle, fixed cx, fixed cy, int sx, int sy);

struct Mode7 {
    fixed camX, camY;   // camera position on the floor texture
    int camHeight = 32; // pixels above the floor
    u8 angle = 0;       // heading, 0..255
    int horizon = 40;   // screen line of the horizon
};

// Computes the per-line matrices for BG2 and starts the HBlank DMA that applies them (call once per frame).
void mode7(const Mode7& m);
void stopMode7();
// Floor texture position under screen pixel (x, y) for the current Mode 7 setup (hit tests, sprite placement).
fixed2 mode7Ground(const Mode7& m, int x, int y);
const s32* mode7Table();  // 161 lines x 4 words: PA|PB, PC|PD, X, Y

}  // namespace agb::render::affine
