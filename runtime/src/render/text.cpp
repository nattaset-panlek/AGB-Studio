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
    // From the layer's own char block on, so the tiles stay within the 1024 its map entries reach.
    auto p = bg::reserveTilesFrom(static_cast<u16>(w_ * h_), bg::config(bg).charBlock);
    if (!p.valid()) p = bg::reserveTiles(static_cast<u16>(w_ * h_));
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

namespace {
AGB_EWRAM_BSS TextVar g_textVars[kTextVars];
const u8 kDefaultRichInks[3] = {7, 10, 13};  // the dialogue palette (text::BoxStyle::rich)

int asciiWidth(const data::Font& font, const char* text) {
    if (!font.ascii) return 0;
    int w = 0;
    for (const char* p = text; *p; ++p) {
        const u8 c = static_cast<u8>(*p);
        if (c < 128 && font.ascii[c] != 0xFFFF) w += font.glyphs[font.ascii[c]].advance;
    }
    return w;
}

int plainRunWidth(const data::TextRun& r) {
    int w = 0;
    for (u16 i = 0; i < r.count; ++i) {
        const int e = r.glyphs[i].x + r.font->glyphs[r.glyphs[i].glyph].advance;
        if (e > w) w = e;
    }
    return w;
}
}  // namespace

// Decimal text of a number with zero padding, into 12 bytes.
void numberText(s32 value, int digits, char* out) {
    char buf[12];
    int n = 0;
    u32 v = value < 0 ? static_cast<u32>(-value) : static_cast<u32>(value);
    do {
        buf[n++] = static_cast<char>('0' + v % 10);
        v /= 10;
    } while (v && n < 10);
    while (n < digits && n < 10) buf[n++] = '0';
    if (value < 0) buf[n++] = '-';
    for (int i = 0; i < n; ++i) out[i] = buf[n - 1 - i];
    out[n] = 0;
}

TextVar& textVar(int index) { return g_textVars[index < 0 || index >= kTextVars ? 0 : index]; }

int TextBox::varWidth(const data::Font& font, const TextVar& v) const {
    if (v.chars[0]) return asciiWidth(font, v.chars);
    if (v.run) return plainRunWidth(*v.run);
    char text[12];
    numberText(v.number, v.digits, text);
    return asciiWidth(font, text);
}

void TextBox::drawVar(const data::Font& font, const TextVar& v, int x, int y) {
    if (v.chars[0]) {
        drawAscii(font, v.chars, x, y);
    } else if (v.run) {
        for (u16 i = 0; i < v.run->count; ++i) {
            const data::PlacedGlyph& pg = v.run->glyphs[i];
            if (pg.line == 0) drawGlyph(*v.run->font, pg.glyph, x + pg.x, y + pg.dy);
        }
    } else {
        char text[12];
        numberText(v.number, v.digits, text);
        drawAscii(font, text, x, y);
    }
}

int TextBox::runWidth(const data::TextRun& r) const {
    if (!r.markCount) return plainRunWidth(r);
    // Per line: the last glyph's end, moved by what the values on that line took or gave back.
    int widest = 0;
    int m = 0;
    int shift = 0, line = -1;
    for (u16 i = 0; i <= r.count; ++i) {
        for (; m < r.markCount && r.marks[m].glyph <= i; ++m) {
            const data::TextMark& mk = r.marks[m];
            if (mk.line != line) {
                line = mk.line;
                shift = 0;
            }
            if (mk.kind == data::TextMarkKind::Var) {
                const int vw = varWidth(*r.font, (vars_ ? vars_ : g_textVars)[mk.arg % kTextVars]);
                if (mk.x + shift + vw > widest) widest = mk.x + shift + vw;
                shift += vw - data::kTextVarReserve;
            }
        }
        if (i == r.count) break;
        if (r.glyphs[i].line != line) {
            line = r.glyphs[i].line;
            shift = 0;
        }
        const int e = r.glyphs[i].x + shift + r.font->glyphs[r.glyphs[i].glyph].advance;
        if (e > widest) widest = e;
    }
    return widest;
}

void TextBox::applyMarks(const data::TextRun& r, u16 glyph, int x, int y, u8 firstLine, Pen& pen) {
    while (pen.mark < r.markCount && r.marks[pen.mark].glyph <= glyph) {
        const data::TextMark& m = r.marks[pen.mark++];
        if (m.line != pen.line) {
            pen.line = m.line;
            pen.shift = 0;
        }
        if (m.kind == data::TextMarkKind::Color) {
            const u8* inks = richInks_ ? richInks_ : kDefaultRichInks;
            style_.ink = m.arg >= 1 && m.arg <= 3 ? inks[m.arg - 1] : pen.ink;
            continue;
        }
        const TextVar& v = (vars_ ? vars_ : g_textVars)[m.arg % kTextVars];
        if (m.line >= firstLine) drawVar(*r.font, v, x + m.x + pen.shift, y + (m.line - firstLine) * r.font->lineHeight);
        pen.shift = static_cast<s16>(pen.shift + varWidth(*r.font, v) - data::kTextVarReserve);
    }
}

void TextBox::placeGlyph(const data::TextRun& r, u16 i, int x, int y, u8 firstLine, Pen& pen) {
    if (r.markCount) applyMarks(r, i, x, y, firstLine, pen);
    const data::PlacedGlyph& pg = r.glyphs[i];
    if (pg.line != pen.line) {
        pen.line = pg.line;
        pen.shift = 0;
    }
    if (pg.line < firstLine) return;
    const int gy = y + (pg.line - firstLine) * r.font->lineHeight + pg.dy;
    if (gy < h_ * 8) drawGlyph(*r.font, pg.glyph, x + pg.x + pen.shift, gy);
}

void TextBox::drawRun(const data::TextRun& run, int x, int y) {
    if (!tiles_) return;
    Pen pen;
    pen.ink = style_.ink;
    for (u16 i = 0; i < run.count; ++i) placeGlyph(run, i, x, y, 0, pen);
    if (run.markCount) applyMarks(run, run.count, x, y, 0, pen);
    style_.ink = pen.ink;
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
    drawFrame();
}

void TextBox::drawFrame() {
    if (!frame_ || !frameWidth_) return;
    const int w = w_ * 8, h = h_ * 8, b = frameWidth_;
    detail::fillRect(tiles_, w_, h_, 0, 0, w, b, frame_);
    detail::fillRect(tiles_, w_, h_, 0, h - b, w, b, frame_);
    detail::fillRect(tiles_, w_, h_, 0, b, b, h - 2 * b, frame_);
    detail::fillRect(tiles_, w_, h_, w - b, b, b, h - 2 * b, frame_);
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
    const int gx = px + g.left, gy = py + g.top;
    if (clipped_ && (gx < clip_[0] || gy < clip_[1] || gx + g.width > clip_[2] || gy + g.height > clip_[3])) {
        // Cut by the rectangle: pixel by pixel, away from the fast blit (the common case is a glyph inside it).
        const int rowBytes = (g.width * font.bpp + 7) >> 3;
        const u8* bits = font.bitmaps + g.offset;
        for (int y = 0; y < g.height; ++y)
            for (int x = 0; x < g.width; ++x) {
                const u8 byte = bits[y * rowBytes + ((x * font.bpp) >> 3)];
                const int v = font.bpp == 1 ? (byte >> (7 - x)) & 1 : (byte >> (6 - (x % 4) * 2)) & 3;
                const int dx = gx + x, dy = gy + y;
                if (!v || dx < clip_[0] || dy < clip_[1] || dx >= clip_[2] || dy >= clip_[3]) continue;
                setPixel(dx, dy, static_cast<u8>(font.bpp == 1 ? style_.ink : style_.ink + 3 - v));
            }
    } else {
        detail::blitGlyph(tiles_, w_, h_, font.bitmaps + g.offset, g.width, g.height, font.bpp, gx, gy, style_.ink, font.shadow ? style_.shadow : 0);
    }
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
    pen_ = Pen{};
    pen_.ink = style_.ink;
    if (!charsPerFrame) skip();
}

void TextBox::update() {
    if (run_ && next_ < run_->count) {
        const u16 target = static_cast<u16>(step_ + speed_);
        bool revealed = false;
        while (next_ < run_->count && run_->glyphs[next_].reveal < target) {
            placeGlyph(*run_, next_, padding_.left, padding_.top, firstLine_, pen_);
            ++next_;
            revealed = true;
        }
        if (next_ >= run_->count && run_->markCount) {
            applyMarks(*run_, run_->count, padding_.left, padding_.top, firstLine_, pen_);
            style_.ink = pen_.ink;
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
    while (next_ < run_->count) placeGlyph(*run_, next_++, padding_.left, padding_.top, firstLine_, pen_);
    if (run_->markCount) {
        applyMarks(*run_, run_->count, padding_.left, padding_.top, firstLine_, pen_);
        style_.ink = pen_.ink;
    }
    speed_ = saved;
    commitTiles();
}

bool TextBox::done() const { return !run_ || next_ >= run_->count; }

void TextBox::showContinueMarker(bool on) {
    if (marker_ == on || !tiles_) return;
    marker_ = on;
    // A small down-pointing triangle in the bottom-right corner (5 px wide).
    const int x0 = w_ * 8 - (padding_.right + 5 > 9 ? padding_.right + 5 : 9), y0 = h_ * 8 - (padding_.bottom + 3 > 6 ? padding_.bottom + 3 : 6);
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
