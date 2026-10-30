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

    // Reserves w x h tiles on background `bg` (already configured) at tile position (x, y) of its map.
    bool init(int bg, int tileX, int tileY, int widthTiles, int heightTiles, u8 paletteBank, Style style);
    void release();
    void clear();
    // Starts showing a run; `charsPerFrame` 0 shows everything at once. `firstLine` scrolls long runs.
    void show(const data::TextRun* run, u8 charsPerFrame = 2, u8 firstLine = 0);
    void update();  // reveals more glyphs and queues the changed tiles
    void skip();    // reveal everything now
    bool done() const;
    void setBlip(void (*blip)()) { blip_ = blip; }
    void showContinueMarker(bool on);

    // Draws one glyph immediately (tests and editor previews).
    void drawGlyph(const data::Font& font, u16 glyph, int x, int y);
    u8 pixel(int x, int y) const;  // palette index in the tile buffer (tests)
    void fillRect(int x, int y, int w, int h, u8 index);
    // Runtime ASCII text (HUD numbers); returns the pen x after the text.
    int drawAscii(const data::Font& font, const char* text, int x, int y);
    int widthPx() const { return w_ * 8; }
    int heightPx() const { return h_ * 8; }

private:
    void setPixel(int x, int y, u8 index);
    void commitTiles();

    int bg_ = -1, tileX_ = 0, tileY_ = 0, w_ = 0, h_ = 0;
    u16 firstTile_ = 0;
    s8 charBlock_ = -1;
    u8 bank_ = 0;
    Style style_;
    u32* tiles_ = nullptr;  // 4bpp tile buffer in the screen arena, w_*h_*8 words
    const data::TextRun* run_ = nullptr;
    u8 firstLine_ = 0;
    u16 next_ = 0;          // next glyph index in the run
    u16 step_ = 0;          // reveal step reached
    u8 speed_ = 2;
    bool dirty_ = false;
    bool marker_ = false;
    void (*blip_)() = nullptr;
};

}  // namespace agb::render
