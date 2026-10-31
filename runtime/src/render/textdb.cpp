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
// Text state lives in EWRAM (the Text row of the RAM budget): it is touched a few times per glyph, not per pixel.
AGB_EWRAM_DATA render::TextBox g_box;
bool g_open = false;
bool g_awaitA = false;
bool g_acked = false;  // the player pressed A for the line on screen (wait only once per line)
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

namespace {
void relocalizeLabels();
}  // namespace

void setLanguage(u8 l) {
    if (l >= g_count || l == g_lang) return;
    g_lang = l;
    relocalizeLabels();
}
u8 language() { return g_lang; }
u8 languageCount() { return g_count; }
const char* languageName(u8 l) { return l < g_count ? g_tables[l]->language : ""; }

bool translated(u16 key, u8 l) { return l < g_count && key < g_tables[l]->count && g_tables[l]->runs[key]; }

const data::TextRun* run(u16 key) {
    if (!g_tables) return nullptr;
    if (translated(key, g_lang)) return g_tables[g_lang]->runs[key];
    if (translated(key, g_fallback)) return g_tables[g_fallback]->runs[key];
    debug::logf("warning: string key number %u has no text in any language, so nothing is shown. Add its text in Localization and rebuild.", key);
    return nullptr;
}

void configureBox(const BoxStyle& style) { g_style = style; }

bool open() { return g_open; }

render::TextBox& box() { return g_box; }

namespace {
bool g_layerReady = false;
int g_bg = 0;  // the BG the text layer took for this scene
constexpr int kMaxLabels = 6;
AGB_EWRAM_DATA render::TextBox g_labels[kMaxLabels];
// What each HUD label shows (setLabelValue): the same value is not redrawn, and a new value with the same text
// before it only redraws the digits.
struct LabelValue {
    const data::TextRun* prefix;
    s32 value;
    s16 digitsX;
    u8 digits;
    bool shown;
};
AGB_EWRAM_BSS LabelValue g_labelValues[kMaxLabels];
// The label's own text: the run it was made with (Actor::label, passed back as the HUD prefix), the string key it came
// from (-1 = a fixed run) and that key's run in the current language.
struct LabelText {
    const data::TextRun* made;
    const data::TextRun* current;
    s16 key;
};
AGB_EWRAM_BSS LabelText g_labelTexts[kMaxLabels];
int g_labelCount = 0;

u16 g_blank = 0;
// The dialogue box keeps its tiles for the whole scene: closing only points its map at the blank tile, so dialogue
// after dialogue does not use more VRAM.
bool g_boxReady = false;

// Text layer: highest priority, 32x32 map in its own screen block, shared by the box, labels and menus.
// Its first tile is a blank tile of its own: every map entry without text points at it (tile 0 of the char
// block may belong to a tileset).
// BoxStyle::bg when the scene leaves it free, else the highest regular BG the scene does not show: a scene layer
// on the same BG would be hidden and the text would scroll with that layer's parallax.
int pickBg() {
    const int mode = render::bg::videoMode();
    const int last = mode == 0 ? 3 : mode == 1 ? 1 : -1;  // regular (non-affine) BGs of the mode
    if (g_style.bg <= last && !render::bg::visible(g_style.bg)) return g_style.bg;
    for (int bg = last; bg >= 0; --bg)
        if (!render::bg::visible(bg)) return bg;
    debug::logf("warning: no free background layer for text, so it shares BG%d with the scene and may cover it. Leave one layer free in this scene.", g_style.bg);
    return g_style.bg;
}

bool ensureLayer() {
    if (g_layerReady) return true;
    const int sb = render::bg::allocScreenBlocks(render::bg::Size::S32x32);
    if (sb < 0) return false;
    const auto blank = render::bg::reserveTiles(1);
    if (!blank.valid()) return false;
    // The layer may be made during play (the first dialogue or pause menu): its blank tile and map go through the
    // commit queue like the panels drawn on it.
    commit::queueFill(reinterpret_cast<u8*>(commit::bgTiles(blank.charBlock)) + blank.firstTile * 32, 0, 32);
    render::bg::Config c;
    c.charBlock = static_cast<u8>(blank.charBlock);
    c.screenBlock = static_cast<u8>(sb);
    c.priority = 0;
    g_bg = pickBg();
    render::bg::configure(g_bg, c);
    g_blank = blank.firstTile;
    commit::queueFill(commit::bgMap(sb), g_blank, 2048);
    loadPalette();
    render::bg::setScroll(g_bg, 0, 0);
    render::bg::setVisible(g_bg, true);
    g_layerReady = true;
    return true;
}
}  // namespace

int layer() { return ensureLayer() ? g_bg : -1; }
u16 blankEntry() { return g_blank; }

int addLabel(int x, int y, int widthPx, const data::TextRun* r, s16 key) {
    if (g_labelCount >= kMaxLabels || !ensureLayer()) return -1;
    g_labelTexts[g_labelCount] = {r, r, r ? key : static_cast<s16>(-1)};
    const int lh = r ? r->font->lineHeight : 16;
    render::TextBox& b = g_labels[g_labelCount];
    if (!b.init(g_bg, x / 8, y / 8, (widthPx + 7) / 8, (lh * (r ? r->lines : 1) + 7) / 8, g_style.paletteBank, render::TextBox::Style{1, 4, 0}))
        return -1;
    if (r) b.show(r, 0);
    // The label already shows its text: the first HUD value only draws digits after it.
    g_labelValues[g_labelCount] = {};
    if (r) {
        int w = 0;
        for (u16 i = 0; i < r->count; ++i) {
            const int e = r->glyphs[i].x + r->font->glyphs[r->glyphs[i].glyph].advance;
            if (e > w) w = e;
        }
        g_labelValues[g_labelCount] = {r, 0, static_cast<s16>(w + 4), 0xFF, true};
    }
    return g_labelCount++;
}

void setLabelText(int label, const data::Font& font, const char* ascii) {
    if (label < 0 || label >= g_labelCount) return;
    g_labelValues[label].shown = false;
    render::TextBox& b = g_labels[label];
    b.clear();
    b.drawAscii(font, ascii, 0, 0);
    b.update();
}

void setLabelValue(int label, const data::TextRun* prefix, s32 value, u8 digits) {
    if (label < 0 || label >= g_labelCount || !prefix) return;
    // The label's own text comes back as the run it was made with: show it in the current language.
    if (prefix == g_labelTexts[label].made) prefix = g_labelTexts[label].current;
    render::TextBox& b = g_labels[label];
    LabelValue& lv = g_labelValues[label];
    if (lv.shown && lv.prefix == prefix && lv.value == value && lv.digits == digits) return;
    int x = 0;
    if (lv.shown && lv.prefix == prefix) {
        x = lv.digitsX - 4;
        b.fillRect(lv.digitsX, 0, b.widthPx() - lv.digitsX, b.heightPx(), 0);
    } else {
        b.clear();
        b.drawRun(*prefix, 0, 0);
        for (u16 i = 0; i < prefix->count; ++i) {
            const int e = prefix->glyphs[i].x + prefix->font->glyphs[prefix->glyphs[i].glyph].advance;
            if (e > x) x = e;
        }
    }
    lv = {prefix, value, static_cast<s16>(x + 4), digits, true};
    char buf[12];
    int n = 0;
    u32 v = value < 0 ? static_cast<u32>(-value) : static_cast<u32>(value);
    do {
        buf[n++] = static_cast<char>('0' + v % 10);
        v /= 10;
    } while (v && n < 10);
    while (n < digits && n < 10) buf[n++] = '0';
    if (value < 0 && n < 11) buf[n++] = '-';
    char text[12];
    for (int i = 0; i < n; ++i) text[i] = buf[n - 1 - i];
    text[n] = 0;
    b.drawAscii(*prefix->font, text, x + 4, 0);
    b.update();
}

void setLabelRun(int label, const data::TextRun* r) {
    if (label < 0 || label >= g_labelCount) return;
    g_labelValues[label].shown = false;
    render::TextBox& b = g_labels[label];
    b.clear();
    if (r) b.drawRun(*r, 0, 0);
    b.update();
}

namespace {
// The language changed: labels that show their own key (alone, or before a HUD number) are drawn in the new language.
void relocalizeLabels() {
    for (int i = 0; i < g_labelCount; ++i) {
        LabelText& t = g_labelTexts[i];
        const data::TextRun* r = t.key >= 0 ? run(static_cast<u16>(t.key)) : nullptr;
        if (!r || r == t.current) continue;
        const LabelValue lv = g_labelValues[i];
        const bool own = lv.shown && lv.prefix == t.current;
        t.current = r;
        if (!own) continue;  // replaced by Actor.SetLabelText: keep what the script set
        if (lv.digits == 0xFF) {
            g_labels[i].show(r, 0);
            int w = 0;
            for (u16 k = 0; k < r->count; ++k) {
                const int e = r->glyphs[k].x + r->font->glyphs[r->glyphs[k].glyph].advance;
                if (e > w) w = e;
            }
            g_labelValues[i] = {r, 0, static_cast<s16>(w + 4), 0xFF, true};
        } else {
            g_labelValues[i].shown = false;  // redraw the new text and the same number
            setLabelValue(i, r, lv.value, lv.digits);
        }
    }
}
}  // namespace

void clearLabels() {
    for (int i = 0; i < g_labelCount; ++i) g_labels[i].release();
    g_labelCount = 0;
    g_layerReady = false;
    g_open = false;
    g_boxReady = false;  // the scene's arena and VRAM are reset
}

void remapPanels() {
    for (int i = 0; i < g_labelCount; ++i) g_labels[i].remap();
    if (g_open) g_box.remap();
}

static bool ensureOpen() {
    if (g_open) return true;
    if (!ensureLayer()) return false;
    if (g_boxReady) {
        g_box.remap();
        g_box.clear();
        g_open = true;
        return true;
    }
    if (!g_box.init(g_bg, g_style.tileX, g_style.tileY, g_style.widthTiles, g_style.heightTiles, g_style.paletteBank,
                    render::TextBox::Style{1, 4, 5}))
        return false;
    g_open = true;
    g_boxReady = true;
    return true;
}

bool sayRun(const data::TextRun* r, void (*blip)(), s8 speed) {
    if (!r || !ensureOpen()) return false;
    g_blip = blip;
    g_box.setBlip(blip);
    g_box.show(r, speed >= 0 ? static_cast<u8>(speed) : g_style.charsPerFrame);
    g_current = r;
    g_awaitA = false;
    g_acked = false;
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
        if (input::pressed(Key::A) || input::pressed(Key::B)) g_box.fastForward();
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
    if (!g_awaitA && !g_acked) {
        g_awaitA = true;
        g_box.showContinueMarker(true);
    } else if (g_awaitA && input::pressed(Key::A)) {
        g_awaitA = false;
        g_acked = true;
        g_box.showContinueMarker(false);
    }
    g_box.update();
}

// Waiting until the line has been read: typing, then A pressed once (or a choice made).
bool waiting() { return g_open && (!g_box.done() || !g_acked || g_choosing); }

void close() {
    if (!g_open) return;
    g_box.unmap(g_blank);
    g_open = false;
}

}  // namespace agb::text
