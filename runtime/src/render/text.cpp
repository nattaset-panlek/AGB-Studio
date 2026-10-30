#include "agb/render/text.hpp"

#include "agb/commit.hpp"
#include "agb/memory.hpp"
#include "agb/render/bg.hpp"

namespace agb::render {

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
    tiles_ = static_cast<u32*>(screenArena().alloc(static_cast<u32>(w_ * h_) * 32));
    if (!tiles_) return false;
    // The BG layer must use this char block for the tile indices below to be right.
    const bg::Config& c = bg::config(bg);
    if (c.charBlock != charBlock_) {
        bg::Config nc = c;
        nc.charBlock = static_cast<u8>(charBlock_);
        bg::configure(bg, nc);
    }
    // Map entries: sequential tiles row by row (32-wide screen block).
    u16* map = commit::bgMap(c.screenBlock);
    u16 row[32];
    for (int y = 0; y < h_; ++y) {
        for (int x = 0; x < w_; ++x) row[x] = static_cast<u16>((firstTile_ + y * w_ + x) | (bank_ << 12));
        commit::copyNow(map + (tileY_ + y) * 32 + tileX_, row, static_cast<u32>(w_) * 2);
    }
    clear();
    commitTiles();
    return true;
}

void TextBox::release() {
    tiles_ = nullptr;
    run_ = nullptr;
}

void TextBox::clear() {
    if (!tiles_) return;
    const u32 fill = style_.paper * 0x11111111u;
    for (int i = 0; i < w_ * h_ * 8; ++i) tiles_[i] = fill;
    dirty_ = true;
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
    for (int yy = y; yy < y + h; ++yy)
        for (int xx = x; xx < x + w; ++xx) setPixel(xx, yy, index);
    dirty_ = true;
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
    if (index >= font.glyphCount) return;
    const data::Glyph& g = font.glyphs[index];
    const u8* bits = font.bitmaps + g.offset;
    const int bpp = font.bpp;
    const int rowBytes = (g.width * bpp + 7) / 8;
    for (int pass = (font.shadow && style_.shadow) ? 0 : 1; pass < 2; ++pass) {
        for (int y = 0; y < g.height; ++y)
            for (int x = 0; x < g.width; ++x) {
                const int bit = x * bpp;
                const int v = (bits[y * rowBytes + bit / 8] >> (8 - bpp - bit % 8)) & ((1 << bpp) - 1);
                if (!v) continue;
                const int dx = px + g.left + x, dy = py + g.top + y;
                if (pass == 0) setPixel(dx + 1, dy + 1, style_.shadow);
                else setPixel(dx, dy, static_cast<u8>(bpp == 1 ? style_.ink : style_.ink + (3 - v)));
            }
    }
    dirty_ = true;
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
    dirty_ = true;
}

void TextBox::commitTiles() {
    if (!dirty_ || !tiles_) return;
    u8* dst = reinterpret_cast<u8*>(commit::bgTiles(charBlock_)) + static_cast<u32>(firstTile_) * 32;
    if (commit::queueCopy(dst, tiles_, static_cast<u32>(w_ * h_) * 32)) dirty_ = false;
}

}  // namespace agb::render
