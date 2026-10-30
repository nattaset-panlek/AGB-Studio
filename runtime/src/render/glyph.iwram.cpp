// Glyph blitting for text windows, compiled as ARM code into IWRAM because of the .iwram.cpp suffix: opening
// a menu or a full dialogue page draws thousands of glyph pixels in one frame.
#include "agb/types.hpp"

namespace agb::render::detail {

// Draws a glyph bitmap (rows MSB first, 1 or 2 bpp) into a 4bpp tile buffer `wTiles` x `hTiles` tiles.
// 1bpp pixels use `ink`; 2bpp value v uses ink + 3 - v. A non-zero `shadow` first draws the shape at +1,+1.
// Pixels of one glyph row are gathered per tile and written with one read-modify-write of the tile row word
// (the buffer is in EWRAM, where every access waits).
void blitGlyph(u32* tiles, int wTiles, int hTiles, const u8* bits, int gw, int gh, int bpp, int x0, int y0, u8 ink, u8 shadow) {
    const int rowBytes = (gw * bpp + 7) >> 3;
    const int wPx = wTiles * 8, hPx = hTiles * 8;
    const int perByte = bpp == 1 ? 8 : 4;
    const u32 top = bpp == 1 ? 0x80u : 0xC0u;  // the next pixel sits in the top bits of the shifted byte
    const int down = 8 - bpp;
    for (int pass = shadow ? 0 : 1; pass < 2; ++pass) {
        const int ox = x0 + (pass == 0 ? 1 : 0), oy = y0 + (pass == 0 ? 1 : 0);
        for (int y = 0; y < gh; ++y) {
            const int py = oy + y;
            if (py < 0 || py >= hPx) continue;
            const u8* row = bits + y * rowBytes;
            u32* line = tiles + (py >> 3) * wTiles * 8 + (py & 7);
            int tile = -1;
            u32 mask = 0, value = 0;
            int x = 0;
            for (int b = 0; b < rowBytes; ++b, x += perByte) {
                u32 byte = row[b];
                if (!byte) continue;  // most of a glyph box is empty
                for (int k = 0; k < perByte; ++k, byte <<= bpp) {
                    const u32 v = (byte & top) >> down;
                    if (!v) continue;
                    const int px = ox + x + k;
                    if (x + k >= gw || px < 0 || px >= wPx) continue;
                    const int tx = px >> 3;
                    if (tx != tile) {
                        if (tile >= 0) line[tile * 8] = (line[tile * 8] & ~mask) | value;
                        tile = tx;
                        mask = value = 0;
                    }
                    const u32 c = pass == 0 ? shadow : bpp == 1 ? ink : ink + 3 - v;
                    const int sh = (px & 7) * 4;
                    mask |= 0xFu << sh;
                    value |= (c & 15) << sh;
                }
            }
            if (tile >= 0) line[tile * 8] = (line[tile * 8] & ~mask) | value;
        }
    }
}

// Fills a pixel rectangle with one palette index, a whole 8-pixel tile row word at a time where it can.
void fillRect(u32* tiles, int wTiles, int hTiles, int x, int y, int w, int h, u8 index) {
    int x1 = x + w, y1 = y + h;
    if (x < 0) x = 0;
    if (y < 0) y = 0;
    if (x1 > wTiles * 8) x1 = wTiles * 8;
    if (y1 > hTiles * 8) y1 = hTiles * 8;
    if (x >= x1 || y >= y1) return;
    const u32 fill = (index & 15) * 0x11111111u;
    for (int py = y; py < y1; ++py) {
        u32* line = tiles + (py >> 3) * wTiles * 8 + (py & 7);
        int px = x;
        while (px < x1) {
            const int tx = px >> 3, first = px & 7;
            const int last = (x1 - (tx << 3)) > 8 ? 8 : x1 - (tx << 3);  // exclusive, within this tile
            u32& word = line[tx * 8];
            if (first == 0 && last == 8) {
                word = fill;
            } else {
                const u32 mask = (last == 8 ? 0xFFFFFFFFu : (1u << (last * 4)) - 1) & ~((1u << (first * 4)) - 1);
                word = (word & ~mask) | (fill & mask);
            }
            px = (tx + 1) << 3;
        }
    }
}

}  // namespace agb::render::detail
