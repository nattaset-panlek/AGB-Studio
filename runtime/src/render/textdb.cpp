#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/input.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/palette.hpp"
#include "agb/text.hpp"

namespace agb::text {

namespace {
const Table* const* g_tables = nullptr;
u8 g_count = 0, g_fallback = 0, g_lang = 0;

BoxStyle g_style;
render::TextBox g_box;
bool g_open = false;
bool g_awaitA = false;
void (*g_blip)() = nullptr;

// Choice state (options drawn under the text)
const u16* g_options = nullptr;
u8 g_optionCount = 0, g_cursor = 0;
s8 g_chosen = -1;
bool g_choosing = false;

void loadPalette() {
    const u16 colors[16] = {0, g_style.ink, g_style.ink2, g_style.ink3, g_style.shadow, g_style.paper, g_style.frame};
    render::palettes::setBank(false, g_style.paletteBank, colors);
}

const data::TextRun* g_current = nullptr;

void drawChoices() {
    if (!g_current) return;
    const int lh = g_current->font->lineHeight;
    for (u8 i = 0; i < g_optionCount; ++i) {
        const data::TextRun* r = run(g_options[i]);
        const int y = (g_current->lines + i) * lh;
        g_box.fillRect(0, y, g_box.widthPx(), lh, 0);
        // Cursor: a small right-pointing triangle beside the selected option.
        if (i == g_cursor)
            for (int k = 0; k < 3; ++k) g_box.fillRect(2 + k, y + lh / 2 - 3 + k, 1, 7 - 2 * k, 1);
        if (!r) continue;
        for (u16 k = 0; k < r->count; ++k) {
            const data::PlacedGlyph& pg = r->glyphs[k];
            if (pg.line == 0) g_box.drawGlyph(*r->font, pg.glyph, pg.x + 10, y + pg.dy);
        }
    }
}
}  // namespace

void setTables(const Table* const* languages, u8 count, u8 fallback) {
    g_tables = languages;
    g_count = count;
    g_fallback = fallback < count ? fallback : 0;
    if (g_lang >= count) g_lang = 0;
}

void setLanguage(u8 l) { g_lang = l < g_count ? l : g_lang; }
u8 language() { return g_lang; }
u8 languageCount() { return g_count; }
const char* languageName(u8 l) { return l < g_count ? g_tables[l]->language : ""; }

bool translated(u16 key, u8 l) { return l < g_count && key < g_tables[l]->count && g_tables[l]->runs[key]; }

const data::TextRun* run(u16 key) {
    if (!g_tables) return nullptr;
    if (translated(key, g_lang)) return g_tables[g_lang]->runs[key];
    if (translated(key, g_fallback)) return g_tables[g_fallback]->runs[key];
    debug::logf("warning: string %u has no text in any language", key);
    return nullptr;
}

void configureBox(const BoxStyle& style) { g_style = style; }

bool open() { return g_open; }

render::TextBox& box() { return g_box; }

namespace {
bool g_layerReady = false;
constexpr int kMaxLabels = 6;
render::TextBox g_labels[kMaxLabels];
int g_labelCount = 0;

u16 g_blank = 0;

// Text layer: highest priority, 32x32 map in its own screen block, shared by the box, labels and menus.
// Its first tile is a blank tile of its own: every map entry without text points at it (tile 0 of the char
// block may belong to a tileset).
bool ensureLayer() {
    if (g_layerReady) return true;
    const int sb = render::bg::allocScreenBlocks(render::bg::Size::S32x32);
    if (sb < 0) return false;
    const auto blank = render::bg::reserveTiles(1);
    if (!blank.valid()) return false;
    commit::fillNow(reinterpret_cast<u8*>(commit::bgTiles(blank.charBlock)) + blank.firstTile * 32, 0, 32);
    render::bg::Config c;
    c.charBlock = static_cast<u8>(blank.charBlock);
    c.screenBlock = static_cast<u8>(sb);
    c.priority = 0;
    render::bg::configure(g_style.bg, c);
    g_blank = blank.firstTile;
    commit::fillNow(commit::bgMap(sb), g_blank * 0x00010001u, 2048);
    loadPalette();
    render::bg::setScroll(g_style.bg, 0, 0);
    render::bg::setVisible(g_style.bg, true);
    g_layerReady = true;
    return true;
}
}  // namespace

int layer() { return ensureLayer() ? g_style.bg : -1; }
u16 blankEntry() { return g_blank; }

int addLabel(int x, int y, int widthPx, const data::TextRun* r) {
    if (g_labelCount >= kMaxLabels || !ensureLayer()) return -1;
    const int lh = r ? r->font->lineHeight : 16;
    render::TextBox& b = g_labels[g_labelCount];
    if (!b.init(g_style.bg, x / 8, y / 8, (widthPx + 7) / 8, (lh * (r ? r->lines : 1) + 7) / 8, g_style.paletteBank, render::TextBox::Style{1, 4, 0}))
        return -1;
    if (r) b.show(r, 0);
    return g_labelCount++;
}

void setLabelText(int label, const data::Font& font, const char* ascii) {
    if (label < 0 || label >= g_labelCount) return;
    render::TextBox& b = g_labels[label];
    b.clear();
    b.drawAscii(font, ascii, 0, 0);
    b.update();
}

void clearLabels() {
    for (int i = 0; i < g_labelCount; ++i) g_labels[i].release();
    g_labelCount = 0;
    g_layerReady = false;
    g_open = false;
}

static bool ensureOpen() {
    if (g_open) return true;
    if (!ensureLayer()) return false;
    if (!g_box.init(g_style.bg, g_style.tileX, g_style.tileY, g_style.widthTiles, g_style.heightTiles, g_style.paletteBank,
                    render::TextBox::Style{1, 4, 5}))
        return false;
    g_open = true;
    return true;
}

bool sayRun(const data::TextRun* r, void (*blip)(), s8 speed) {
    if (!r || !ensureOpen()) return false;
    g_blip = blip;
    g_box.setBlip(blip);
    g_box.show(r, speed >= 0 ? static_cast<u8>(speed) : g_style.charsPerFrame);
    g_current = r;
    g_awaitA = false;
    g_choosing = false;
    g_chosen = -1;
    return true;
}

bool say(u16 key, void (*blip)(), s8 speed) { return sayRun(run(key), blip, speed); }

void choose(const u16* optionKeys, u8 count) {
    g_options = optionKeys;
    g_optionCount = count;
    g_cursor = 0;
    g_chosen = -1;
    g_choosing = true;
    drawChoices();
}

int choice() { return g_chosen; }

void update() {
    for (int i = 0; i < g_labelCount; ++i) g_labels[i].update();
    if (!g_open) return;
    if (!g_box.done()) {
        if (input::pressed(Key::A) || input::pressed(Key::B)) g_box.skip();
        g_box.update();
        return;
    }
    if (g_choosing) {
        const u8 before = g_cursor;
        if (input::pressed(Key::Up) && g_cursor > 0) --g_cursor;
        if (input::pressed(Key::Down) && g_cursor + 1 < g_optionCount) ++g_cursor;
        if (before != g_cursor) drawChoices();
        if (input::pressed(Key::A)) {
            g_chosen = static_cast<s8>(g_cursor);
            g_choosing = false;
        }
        g_box.update();
        return;
    }
    if (!g_awaitA) {
        g_awaitA = true;
        g_box.showContinueMarker(true);
    } else if (input::pressed(Key::A)) {
        g_awaitA = false;
        g_box.showContinueMarker(false);
    }
    g_box.update();
}

bool waiting() { return g_open && (!g_box.done() || g_awaitA || g_choosing); }

void close() {
    if (!g_open) return;
    g_box.clear();
    g_box.update();
    g_box.release();
    g_open = false;
}

}  // namespace agb::text
