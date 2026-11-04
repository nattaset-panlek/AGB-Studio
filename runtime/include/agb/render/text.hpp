// Text windows: variable-width glyphs drawn into a block of BG tiles (Font Manager "Draw to BG 2 text
// layer"). Layout (Thai stacking, line breaks) was done by the build; this places pixels and reveals
// glyphs over time for the dialogue typewriter (chars per frame, blip sound, continue marker).
#pragma once
#include "agb/data.hpp"

namespace agb::render {

// A value for a {0}..{3} token: a run (a localized name), or a number when run is null. chars overrides both.
struct TextVar {
    const data::TextRun* run = nullptr;
    s32 number = 0;
    s8 digits = 0;            // pads with zeros
    char chars[13] = {};      // typed text (a player name), used when not empty
};
constexpr int kTextVars = 4;
void numberText(s32 value, int digits, char* out);  // decimal with zero padding, into 12 bytes
TextVar& textVar(int index);  // the game-wide values (C# Text.SetVar), used unless a box has its own

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

    // Space between the box's edge and text shown with show(); 0 by default. The continue marker stays inside it.
    Padding padding() const { return padding_; }
    void setPadding(Padding p) { padding_ = p; }
    // A border of this palette index drawn by show() and clear() (0 = none).
    void setFrame(u8 index, u8 width) { frame_ = index; frameWidth_ = width; }
    // Glyphs only touch this pixel rectangle (RectMask2D); clearClip goes back to the whole box.
    void setClip(int x, int y, int right, int bottom) { clip_[0] = x; clip_[1] = y; clip_[2] = right; clip_[3] = bottom; clipped_ = true; }
    void clearClip() { clipped_ = false; }
    // Values for {0}..{3} tokens (nullptr = the game-wide ones) and the inks of <c1>..<c3>.
    void setVars(const TextVar* vars) { vars_ = vars; }
    void setRichInks(const u8* inks) { richInks_ = inks; }
    // Widest line of a run with its tokens filled in.
    int runWidth(const data::TextRun& run) const;
    int varWidth(const data::Font& font, const TextVar& v) const;
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
    bool uploading() const { return dirtyHi_ != 0xFFFF; }  // tiles changed here are not all in VRAM yet
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
    struct Pen {
        u16 mark = 0;
        s16 shift = 0;
        u8 line = 0;
        u8 ink = 1;
    };
    void applyMarks(const data::TextRun& run, u16 glyph, int x, int y, u8 firstLine, Pen& pen);
    void placeGlyph(const data::TextRun& run, u16 glyph, int x, int y, u8 firstLine, Pen& pen);
    void drawVar(const data::Font& font, const TextVar& v, int x, int y);
    void drawFrame();
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
    Padding padding_{0, 0, 0, 0};
    Pen pen_;
    u8 frame_ = 0, frameWidth_ = 0;
    bool clipped_ = false;
    int clip_[4] = {};
    const TextVar* vars_ = nullptr;
    const u8* richInks_ = nullptr;
};

}  // namespace agb::render
