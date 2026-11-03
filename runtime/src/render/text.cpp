#include "agb/render/text.hpp"

#include "agb/commit.hpp"
#include "agb/memory.hpp"
#include "agb/render/bg.hpp"

namespace agb::render {

namespace detail {
// glyph.iwram.cpp (ARM code in IWRAM)
void blitGlyph(u32* tiles, int wTiles, int hTiles, const u8* bits, int gw, int gh, int bpp, int x0, int y0, u8 ink, u8 shadow);
void fillRect(u32* tiles, int wTiles, int hTiles, int x, int y, int w, int h, u8 index);
}  // namespace detail

bool TextBox::init(int bg, int tileX, int tileY, int widthTiles, int heightTiles, u8 paletteBank, Style style) {
    bg_ = bg;
    tileX_ = tileX;
    tileY_ = tileY;
    w_ = widthTiles;
    h_ = heightTiles;
    bank_ = paletteBank;
    style_ = style;
    const auto p = bg::reserveTiles(static_cast<u16>(w_ * h_));
    if (!p.valid()) return false;
    charBlock_ = p.charBlock;
    firstTile_ = p.firstTile;
    maxTiles_ = static_cast<u16>(w_ * h_);
    tiles_ = static_cast<u32*>(screenArena().alloc(static_cast<u32>(w_ * h_) * 32));
    if (!tiles_) return false;
    // Tiles in a later char block are reachable from the layer's char block while the index stays below
    // 1024; otherwise the layer moves to this box's char block (the first box on a fresh layer).
    const bg::Config& c = bg::config(bg);
    const int offset = (charBlock_ - c.charBlock) * 512 + firstTile_;
    if (charBlock_ >= c.charBlock && offset + w_ * h_ <= 1024) {
        mapBase_ = static_cast<u16>(offset);
    } else {
        bg::Config nc = c;
        nc.charBlock = static_cast<u8>(charBlock_);
        bg::configure(bg, nc);
        mapBase_ = firstTile_;
    }
    remap();
    clear();
    commitTiles();
    return true;
}

bool TextBox::resize(int tileX, int tileY, int widthTiles, int heightTiles) {
    if (!tiles_ || widthTiles * heightTiles > maxTiles_) return false;
    tileX_ = tileX;
    tileY_ = tileY;
    w_ = widthTiles;
    h_ = heightTiles;
    clear();
    remap();
    return true;
}

// Map writes go through the commit queue, so a panel opened or closed during play (pause, options, dialogue) changes
// the screen in VBlank.
void TextBox::remap() {
    if (bg_ < 0 || !tiles_) return;
    // Map entries: sequential tiles row by row (32-wide screen block).
    u16* map = commit::bgMap(bg::config(bg_).screenBlock);
    commit::queueSequence(map + tileY_ * 32 + tileX_, static_cast<u16>(mapBase_ | (bank_ << 12)), static_cast<u32>(w_), static_cast<u32>(h_), 64);
}

void TextBox::unmap(u16 blank) {
    if (bg_ < 0) return;
    u16* map = commit::bgMap(bg::config(bg_).screenBlock);
    commit::queueFill(map + tileY_ * 32 + tileX_, blank, static_cast<u32>(w_) * 2, static_cast<u32>(h_), 64);
}

void TextBox::drawRun(const data::TextRun& run, int x, int y) {
    if (!tiles_) return;
    for (u16 i = 0; i < run.count; ++i) {
        const data::PlacedGlyph& pg = run.glyphs[i];
        drawGlyph(*run.font, pg.glyph, x + pg.x, y + pg.line * run.font->lineHeight + pg.dy);
    }
}

void TextBox::release() {
    tiles_ = nullptr;
    run_ = nullptr;
    dirtyLo_ = dirtyHi_ = 0xFFFF;
}

void TextBox::clear() {
    if (!tiles_) return;
    const u32 fill = style_.paper * 0x11111111u;
    for (int i = 0; i < w_ * h_ * 8; ++i) tiles_[i] = fill;
    markDirty(0, h_ * 8 - 1);
}

void TextBox::markDirty(int y0, int y1) {
    // Whole tile rows: the commit copies one contiguous range of the box's tiles.
    y0 = y0 < 0 ? 0 : y0;
    y1 = y1 >= h_ * 8 ? h_ * 8 - 1 : y1;
    if (y1 < y0) return;
    const u16 lo = static_cast<u16>((y0 >> 3) * w_), hi = static_cast<u16>(((y1 >> 3) + 1) * w_ - 1);
    if (lo < dirtyLo_) dirtyLo_ = lo;
    if (hi > dirtyHi_ || dirtyHi_ == 0xFFFF) dirtyHi_ = hi;
}

void TextBox::setPixel(int x, int y, u8 index) {
    if (x < 0 || y < 0 || x >= w_ * 8 || y >= h_ * 8) return;
    const int tile = (y >> 3) * w_ + (x >> 3);
    u32& word = tiles_[tile * 8 + (y & 7)];
    const int shift = (x & 7) * 4;
    word = (word & ~(0xFu << shift)) | (static_cast<u32>(index & 15) << shift);
}

u8 TextBox::pixel(int x, int y) const {
    if (!tiles_ || x < 0 || y < 0 || x >= w_ * 8 || y >= h_ * 8) return 0;
    const int tile = (y >> 3) * w_ + (x >> 3);
    return static_cast<u8>((tiles_[tile * 8 + (y & 7)] >> ((x & 7) * 4)) & 15);
}

void TextBox::fillRect(int x, int y, int w, int h, u8 index) {
    if (!tiles_) return;
    detail::fillRect(tiles_, w_, h_, x, y, w, h, index);
    markDirty(y, y + h - 1);
}

void TextBox::blit(const u8* pixels, int stride, int w, int h, int x, int y) {
    if (!tiles_) return;
    for (int r = 0; r < h; ++r)
        for (int c = 0; c < w; ++c)
            if (const u8 index = pixels[r * stride + c]) setPixel(x + c, y + r, index);
    markDirty(y, y + h - 1);
}

void TextBox::drawPanelFrame(Style style) {
    if (!tiles_) return;
    const int w = w_ * 8;
    const int h = h_ * 8;
    const u8 paper = style.paper != 0 ? style.paper : 5;
    fillRect(0, 0, w, h, paper);
    fillRect(0, 0, w, 1, style.ink);
    fillRect(0, h - 1, w, 1, style.ink);
    fillRect(0, 0, 1, h, style.ink);
    fillRect(w - 1, 0, 1, h, style.ink);
    markDirty(0, h - 1);
}

int TextBox::drawAscii(const data::Font& font, const char* text, int x, int y) {
    if (!font.ascii) return x;
    for (const char* p = text; *p; ++p) {
        const u8 c = static_cast<u8>(*p);
        if (c >= 128 || font.ascii[c] == 0xFFFF) continue;
        drawGlyph(font, font.ascii[c], x, y);
        x += font.glyphs[font.ascii[c]].advance;
    }
    return x;
}

void TextBox::drawGlyph(const data::Font& font, u16 index, int px, int py) {
    if (index >= font.glyphCount || !tiles_) return;
    const data::Glyph& g = font.glyphs[index];
    detail::blitGlyph(tiles_, w_, h_, font.bitmaps + g.offset, g.width, g.height, font.bpp, px + g.left, py + g.top, style_.ink,
                      font.shadow ? style_.shadow : 0);
    markDirty(py + g.top, py + g.top + g.height);
}

void TextBox::show(const data::TextRun* run, u8 charsPerFrame, u8 firstLine) {
    clear();
    run_ = run;
    speed_ = charsPerFrame;
    firstLine_ = firstLine;
    next_ = 0;
    step_ = 0;
    marker_ = false;
    if (!charsPerFrame) skip();
}

void TextBox::update() {
    if (run_ && next_ < run_->count) {
        const u16 target = static_cast<u16>(step_ + speed_);
        bool revealed = false;
        while (next_ < run_->count && run_->glyphs[next_].reveal < target) {
            const data::PlacedGlyph& pg = run_->glyphs[next_];
            if (pg.line >= firstLine_) {
                const int y = (pg.line - firstLine_) * run_->font->lineHeight + pg.dy;
                if (y < h_ * 8) drawGlyph(*run_->font, pg.glyph, pg.x, y);
            }
            ++next_;
            revealed = true;
        }
        step_ = target;
        if (revealed && blip_) blip_();
    }
    commitTiles();
}

void TextBox::skip() {
    if (!run_) return;
    step_ = 0xFFFF - speed_;
    const u8 saved = speed_;
    speed_ = 0;
    while (next_ < run_->count) {
        const data::PlacedGlyph& pg = run_->glyphs[next_++];
        if (pg.line < firstLine_) continue;
        const int y = (pg.line - firstLine_) * run_->font->lineHeight + pg.dy;
        if (y < h_ * 8) drawGlyph(*run_->font, pg.glyph, pg.x, y);
    }
    speed_ = saved;
    commitTiles();
}

bool TextBox::done() const { return !run_ || next_ >= run_->count; }

void TextBox::showContinueMarker(bool on) {
    if (marker_ == on || !tiles_) return;
    marker_ = on;
    // A small down-pointing triangle in the bottom-right corner (5 px wide).
    const int x0 = w_ * 8 - 9, y0 = h_ * 8 - 6;
    for (int r = 0; r < 3; ++r)
        for (int c = r; c < 5 - r; ++c) setPixel(x0 + c, y0 + r, on ? style_.ink : style_.paper);
    markDirty(y0, y0 + 2);
}

// Copies the changed tile rows to VRAM, at most 8 KB per frame so a full panel shares the frame's DMA budget
// with the rest of the game; the remainder goes on the next update().
void TextBox::commitTiles() {
    if (dirtyHi_ == 0xFFFF || dirtyLo_ > dirtyHi_ || !tiles_) return;
    constexpr u16 kMaxTiles = 256;
    const u16 count = static_cast<u16>(dirtyHi_ - dirtyLo_ + 1 > kMaxTiles ? kMaxTiles : dirtyHi_ - dirtyLo_ + 1);
    u8* dst = reinterpret_cast<u8*>(commit::bgTiles(charBlock_)) + static_cast<u32>(firstTile_ + dirtyLo_) * 32;
    if (!commit::queueCopy(dst, tiles_ + dirtyLo_ * 8, static_cast<u32>(count) * 32)) return;
    dirtyLo_ = static_cast<u16>(dirtyLo_ + count);
    if (dirtyLo_ > dirtyHi_) {
        dirtyLo_ = 0xFFFF;
        dirtyHi_ = 0xFFFF;
    }
}

}  // namespace agb::render
