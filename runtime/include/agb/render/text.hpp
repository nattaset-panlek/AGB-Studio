// Text windows: variable-width glyphs drawn into a block of BG tiles (Font Manager "Draw to BG 2 text
// layer"). Layout (Thai stacking, line breaks) was done by the build; this places pixels and reveals
// glyphs over time for the dialogue typewriter (chars per frame, blip sound, continue marker).
#pragma once
#include "agb/data.hpp"

namespace agb::render {

class TextBox {
public:
    struct Style {
        u8 ink = 1;     // palette index for the darkest shade; 2bpp fonts use ink, ink+1, ink+2
        u8 shadow = 4;  // palette index of the drop shadow (0 = none)
        u8 paper = 0;   // background fill index (0 = transparent)
    };

    struct Padding {
        u8 left = 4;
        u8 top = 2;
        u8 right = 4;
        u8 bottom = 2;
    };

    Padding padding() const { return padding_; }
    void setPadding(Padding p) { padding_ = p; }
    void drawPanelFrame(Style style);
    void drawPanelFrame() { drawPanelFrame(style_); }

    // Reserves w x h tiles on background `bg` (already configured) at tile position (x, y) of its map.
    bool init(int bg, int tileX, int tileY, int widthTiles, int heightTiles, u8 paletteBank, Style style);
    void release();
    void clear();
    // Starts showing a run; `charsPerFrame` 0 shows everything at once. `firstLine` scrolls long runs.
    void show(const data::TextRun* run, u8 charsPerFrame = 2, u8 firstLine = 0);
    void update();  // reveals more glyphs and queues the changed tiles
    void skip();    // reveal everything now
    // The player pressed A while typing: reveal the rest quickly (12 characters a frame keeps a long Thai line
    // within the frame budget and still looks instant).
    void fastForward() { if (speed_ < 12) speed_ = 12; }
    bool done() const;
    void setBlip(void (*blip)()) { blip_ = blip; }
    void showContinueMarker(bool on);

    // Draws one glyph immediately (tests and editor previews).
    void drawGlyph(const data::Font& font, u16 glyph, int x, int y);
    u8 pixel(int x, int y) const;  // palette index in the tile buffer (tests)
    void fillRect(int x, int y, int w, int h, u8 index);
    // Copies w x h pixels of an 8-bit indexed image (`stride` pixels a row) to (x, y); index 0 stays see-through.
    void blit(const u8* pixels, int stride, int w, int h, int x, int y);
    // Runtime ASCII text (HUD numbers); returns the pen x after the text.
    int drawAscii(const data::Font& font, const char* text, int x, int y);
    // Draws every glyph of a run now with its top-left at (x, y) (menus and titles).
    void drawRun(const data::TextRun& run, int x, int y);
    void setInk(u8 ink) { style_.ink = ink; }
    // Points the box's map entries at `blank` (closing a menu); the tiles stay reserved. Both map writes are queued
    // for the next VBlank (commit::queueFill / queueSequence).
    void unmap(u16 blank = 0);
    void remap();  // writes the map entries again (another panel covered this one)
    bool resize(int tileX, int tileY, int wTiles, int hTiles);
    int widthPx() const { return w_ * 8; }
    int heightPx() const { return h_ * 8; }
    int tileX() const { return tileX_; }
    int tileY() const { return tileY_; }
    int wTiles() const { return w_; }
    int hTiles() const { return h_; }
    u16 maxTiles() const { return maxTiles_; }

private:
    void setPixel(int x, int y, u8 index);
    void markDirty(int y0, int y1);  // pixel rows
    void commitTiles();

    int bg_ = -1, tileX_ = 0, tileY_ = 0, w_ = 0, h_ = 0;
    u16 firstTile_ = 0;
    u16 mapBase_ = 0;  // tile index of the first tile as seen from the layer's char block
    u16 maxTiles_ = 0;
    s8 charBlock_ = -1;
    u8 bank_ = 0;
    Style style_;
    u32* tiles_ = nullptr;  // 4bpp tile buffer in the screen arena, w_*h_*8 words
    const data::TextRun* run_ = nullptr;
    u8 firstLine_ = 0;
    u16 next_ = 0;          // next glyph index in the run
    u16 step_ = 0;          // reveal step reached
    u8 speed_ = 2;
    u16 dirtyLo_ = 0xFFFF, dirtyHi_ = 0xFFFF;  // changed tiles not yet copied to VRAM (inclusive)
    bool marker_ = false;
    void (*blip_)() = nullptr;
    Padding padding_{4, 2, 4, 2};
};

}  // namespace agb::render
