#include "agb/ui.hpp"

#include <new>

#include "agb/api.hpp"
#include "agb/audio.hpp"
#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/game.hpp"
#include "agb/input.hpp"
#include "agb/memory.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/effects.hpp"
#include "agb/render/palette.hpp"
#include "agb/save.hpp"
#include "agb/script.hpp"
#include "agb/text.hpp"

namespace agb::ui {

namespace {
// Palette bank 14 belongs to menus: 1-3 ink shades (2bpp fonts use ink, ink+1, ink+2), 5 paper, 6 rule,
// 7-9 muted shades, 10 slider track, 11 slider fill.
constexpr u8 kBank = 14;
constexpr u8 kInk = 1, kPaper = 5, kRule = 6, kMuted = 7, kTrack = 10, kFill = 11;
constexpr u8 kTextBank = 15;  // dialogue box and labels (text::BoxStyle default)

const Kit* g_kit = nullptr;

void loadPalette(const Kit& k) {
    using render::palettes::blend;
    const u16 c[16] = {0, k.ink, blend(k.ink, k.paper, 6), blend(k.ink, k.paper, 11), 0, k.paper, k.rule,
                       k.muted, blend(k.muted, k.paper, 6), blend(k.muted, k.paper, 11), k.barTrack, k.barFill};
    render::palettes::setBank(false, kBank, c);
}

int fontLineHeight(const Kit& k) { return k.font ? k.font->lineHeight : 8; }
}  // namespace

// ---- Menu ---------------------------------------------------------------------------------------------

bool Menu::open(const MenuDef& def, const Kit& kit) {
    def_ = nullptr;
    const int bg = text::layer();
    if (bg < 0) return false;
    loadPalette(kit);
    if (!box_.init(bg, def.tileX, def.tileY, def.widthTiles, def.heightTiles, kBank, render::TextBox::Style{kInk, 0, static_cast<u8>(def.framed ? kPaper : 0)}))
        return false;
    def_ = &def;
    kit_ = &kit;
    first_ = 0;
    disabled_ = 0;
    waiting_ = -1;
    for (int i = 0; i < kMaxItems; ++i) values_[i] = i < def.count && def.items[i].kind == ItemKind::Slider ? static_cast<u8>(def.items[i].arg) : 0;
    cursor_ = 0;
    while (cursor_ < def.count - 1 && !selectable(cursor_)) ++cursor_;
    redraw();
    return true;
}

void Menu::close() {
    if (!def_) return;
    box_.unmap(text::blankEntry());
    box_.release();
    def_ = nullptr;
}

bool Menu::selectable(int i) const {
    return i >= 0 && i < def_->count && def_->items[i].kind != ItemKind::Heading && !(disabled_ & (1u << i));
}

void Menu::setCursor(int item) {
    if (def_ && selectable(item)) {
        cursor_ = static_cast<s8>(item);
        redraw();
    }
}

void Menu::setValue(int item, int v) {
    if (item < 0 || item >= kMaxItems) return;
    values_[item] = static_cast<u8>(v < 0 ? 0 : v);
    refreshRow(item);
}

void Menu::setEnabled(int item, bool enabled) {
    if (item < 0 || item >= kMaxItems) return;
    disabled_ = static_cast<u16>(enabled ? disabled_ & ~(1u << item) : disabled_ | (1u << item));
    if (def_ && !enabled && cursor_ == item) {
        for (int i = 0; i < def_->count; ++i)
            if (selectable(i)) {
                cursor_ = static_cast<s8>(i);
                break;
            }
    }
    if (def_) redraw();
}

void Menu::setWaiting(int item) {
    const int old = waiting_;
    waiting_ = static_cast<s8>(item);
    refreshRow(old);
    refreshRow(item);
}

void Menu::forget() {
    box_.release();
    def_ = nullptr;
}

void Menu::remap() {
    if (def_) box_.remap();
}

int Menu::labelWidth(const Label& l) const {
    if (l.key >= 0) {
        if (const data::TextRun* r = text::run(static_cast<u16>(l.key))) {
            int w = 0;
            for (u16 i = 0; i < r->count; ++i) {
                const int e = r->glyphs[i].x + r->font->glyphs[r->glyphs[i].glyph].advance;
                if (e > w) w = e;
            }
            return w;
        }
    }
    const data::Font* f = kit_->font;
    if (!l.ascii || !f || !f->ascii) return 0;
    int w = 0;
    for (const char* p = l.ascii; *p; ++p) {
        const u8 c = static_cast<u8>(*p);
        if (c < 128 && f->ascii[c] != 0xFFFF) w += f->glyphs[f->ascii[c]].advance;
    }
    return w;
}

void Menu::drawLabel(const Label& l, int x, int y, u8 ink, bool rightAlign, int right) {
    box_.setInk(ink);
    if (rightAlign) x = right - labelWidth(l);
    const data::TextRun* r = l.key >= 0 ? text::run(static_cast<u16>(l.key)) : nullptr;
    if (r) box_.drawRun(*r, x, y);
    else if (l.ascii && kit_->font) box_.drawAscii(*kit_->font, l.ascii, x, y);
    box_.setInk(kInk);
}

int Menu::rowY(int item) const {
    const int lh = fontLineHeight(*kit_);
    int top = def_->framed ? 8 : 0;
    if (def_->title.key >= 0 || def_->title.ascii) top += lh + 14;
    const int spacing = def_->spacing > lh ? def_->spacing : lh;
    return top + (item - first_) * spacing;
}

void Menu::redraw() {
    if (!def_) return;
    const MenuDef& d = *def_;
    const int w = box_.widthPx(), h = box_.heightPx(), lh = fontLineHeight(*kit_);
    box_.clear();
    if (d.framed) {
        box_.fillRect(0, 0, w, 2, kRule);
        box_.fillRect(0, h - 2, w, 2, kRule);
    }
    if (d.title.key >= 0 || d.title.ascii) drawLabel(d.title, (w - labelWidth(d.title)) / 2, d.framed ? 8 : 0, kInk);
    // Keep the cursor row visible when the list is taller than the panel.
    const int bottom = h - (d.framed ? 8 : 0) - lh;
    while (cursor_ < first_) --first_;
    while (rowY(cursor_) > bottom && first_ < cursor_) ++first_;
    for (int i = first_; i < d.count && rowY(i) <= bottom; ++i) drawRow(i, false);
    box_.update();
}

bool Menu::visible(int i) const {
    const int bottom = box_.heightPx() - (def_->framed ? 8 : 0) - fontLineHeight(*kit_);
    return i >= first_ && i < def_->count && rowY(i) <= bottom;
}

// One row: cursor, label and value. `erase` clears the row's band first (cursor moves, value changes), so a
// change costs one row of glyphs instead of the whole panel.
void Menu::drawRow(int i, bool erase) {
    const MenuDef& d = *def_;
    const int lh = fontLineHeight(*kit_), y = rowY(i);
    const int spacing = d.spacing > lh ? d.spacing : lh;
    const int labelX = d.framed ? 20 : 10, valueX = box_.widthPx() - 88;
    if (erase) box_.fillRect(d.framed ? 2 : 0, y, box_.widthPx() - (d.framed ? 4 : 0), spacing, d.framed ? kPaper : 0);
    const ItemDef& it = d.items[i];
    const bool off = (disabled_ & (1u << i)) != 0;
    const bool binding = it.kind == ItemKind::Binding;
    if (i == cursor_ && selectable(i)) {
        // Cursor: a small right-pointing triangle, 7 px tall (Figma "In-game" screens).
        for (int k = 0; k < 3; ++k) box_.fillRect(labelX - 8 + k, y + lh / 2 - 4 + k, 1, 7 - 2 * k, kInk);
    }
    drawLabel(it.label, labelX + (binding ? 16 : 0), y, off || binding ? kMuted : kInk);
    switch (it.kind) {
        case ItemKind::Slider: {
            const int steps = it.arg ? it.arg : 1;
            const int by = y + (lh - 4) / 2;
            box_.fillRect(valueX, by, 80, 4, kTrack);
            box_.fillRect(valueX, by, 80 * values_[i] / steps, 4, kFill);
            break;
        }
        case ItemKind::Choice:
            if (it.options && it.optionCount) drawLabel(it.options[values_[i] % it.optionCount], valueX, y, kInk);
            break;
        case ItemKind::Binding: {
            Label key{-1, keyName(input::actionKeys(it.arg))};
            if (i == waiting_) key = kit_->pressKey.key >= 0 || kit_->pressKey.ascii ? kit_->pressKey : Label{-1, "..."};
            drawLabel(key, valueX, y, kInk);
            break;
        }
        default:
            break;
    }
}

void Menu::refreshRow(int i) {
    if (!def_ || !visible(i)) return;
    drawRow(i, true);
    box_.update();
}

Event Menu::update() {
    if (!def_) return Event::None;
    box_.update();  // copies tiles a large redraw left for this frame
    const MenuDef& d = *def_;
    const int dir = input::repeated(Key::Down) ? 1 : input::repeated(Key::Up) ? -1 : 0;
    if (dir) {
        int i = cursor_;
        for (int n = 0; n < d.count; ++n) {
            i = (i + dir + d.count) % d.count;
            if (selectable(i)) break;
        }
        if (i != cursor_) {
            const int old = cursor_;
            cursor_ = static_cast<s8>(i);
            if (visible(i)) {
                drawRow(old, true);
                drawRow(i, true);
                box_.update();
            } else {
                redraw();  // scrolls
            }
        }
        return Event::None;
    }
    const ItemDef& it = d.items[cursor_];
    if (it.kind == ItemKind::Slider || it.kind == ItemKind::Choice) {
        int change = (input::repeated(Key::Right) ? 1 : 0) - (input::repeated(Key::Left) ? 1 : 0);
        if (it.kind == ItemKind::Choice && input::pressed(Key::A)) change = 1;
        if (change) {
            int v = values_[cursor_] + change;
            if (it.kind == ItemKind::Slider) v = v < 0 ? 0 : v > it.arg ? it.arg : v;
            else v = it.optionCount ? (v + it.optionCount) % it.optionCount : 0;
            if (v != values_[cursor_]) {
                values_[cursor_] = static_cast<u8>(v);
                refreshRow(cursor_);
                return Event::Change;
            }
            return Event::None;
        }
    }
    if (input::pressed(Key::A) && it.kind != ItemKind::Slider && it.kind != ItemKind::Choice && selectable(cursor_)) return Event::Activate;
    if (input::pressed(Key::B) && d.cancel) return Event::Back;
    return Event::None;
}

// ---- key names --------------------------------------------------------------------------------------

const char* keyName(u16 keys) {
    static const char* const kNames[10] = {"A", "B", "SELECT", "START", "RIGHT", "LEFT", "UP", "DOWN", "R", "L"};
    static char buf[32];
    int n = 0;
    for (int b = 0; b < 10; ++b) {
        if (!(keys & (1u << b))) continue;
        if (n && n < 30) buf[n++] = '+';
        for (const char* p = kNames[b]; *p && n < 31; ++p) buf[n++] = *p;
    }
    buf[n] = 0;
    return n ? buf : "-";
}

// ---- overlays -----------------------------------------------------------------------------------------

namespace {
AGB_EWRAM_DATA Menu g_sceneMenu;

// Saves what an overlay changes on top of a running scene and puts it back on close. The overlay's panels share the
// text layer with the scene's labels, dialogue box and title menu: closing a panel points its map entries at the blank
// tile, so whatever it covered is mapped again here (the HUD labels vanished after Options).
struct Restore {
    render::bg::Mark vram;
    u32 arena;
    u16 bldCnt, bldY, bldAlpha;
    u16 textColors[16];
    int textBg;
    render::bg::Config textConfig;  // a large panel may move the text layer to its own char block

    void take() {
        vram = render::bg::mark();
        arena = screenArena().used();
        bldCnt = commit::reg(commit::BldCnt);
        bldY = commit::reg(commit::BldY);
        bldAlpha = commit::reg(commit::BldAlpha);
        for (int i = 0; i < 16; ++i) textColors[i] = render::palettes::baseColor(kTextBank * 16 + i);
        textBg = text::layer();
        if (textBg >= 0) textConfig = render::bg::config(textBg);
    }
    // blend false: leave the blend registers: a page that did not touch them may have opened during a scene's fade in.
    void give(bool blend = true) const {
        render::palettes::setBank(false, kTextBank, textColors);
        if (blend) {
            commit::setReg(commit::BldCnt, bldCnt);
            commit::setReg(commit::BldY, bldY);
            commit::setReg(commit::BldAlpha, bldAlpha);
        }
        screenArena().rewind(arena);
        render::bg::rewind(vram);
        if (textBg >= 0) render::bg::configure(textBg, textConfig);
        text::remapPanels();
        g_sceneMenu.remap();
    }
};

class PauseScreen final : public Screen {
public:
    const char* name() const override { return "pause menu"; }
    bool overlay() const override { return true; }

    void enter() override {
        const Kit& k = *g_kit;
        const int textBg = text::layer();  // before take(): creating the layer loads its palette
        restore_.take();
        u8 targets = render::effects::kObj | render::effects::kBackdrop;
        for (int b = 0; b < 4; ++b)
            if (b != textBg) targets = static_cast<u8>(targets | (1u << b));
        render::effects::setBrightness(targets, -k.dim);
        // HUD labels share the text layer, which the blend leaves alone: darken their colours by hand.
        u16 dimmed[16];
        for (int i = 0; i < 16; ++i) dimmed[i] = render::palettes::blend(restore_.textColors[i], 0, k.dim);
        render::palettes::setBank(false, kTextBank, dimmed);
        items_[0] = {ItemKind::Action, k.resume, Command::Resume, 0, nullptr, 0};
        items_[1] = {ItemKind::Action, k.options, Command::Options, 0, nullptr, 0};
        items_[2] = {ItemKind::Action, k.quit, Command::QuitToTitle, 0, nullptr, 0};
        // 112 x 80 box in the middle of the screen (Figma "In-game · Pause menu"), taller for taller fonts.
        const int lh = fontLineHeight(k), spacing = lh > 14 ? lh : 14;
        const int rows = (8 + lh + 14 + 2 * spacing + lh + 8 + 7) / 8;
        const u8 h = static_cast<u8>(rows < 10 ? 10 : rows > 20 ? 20 : rows);
        def_ = {items_, 3, 8, static_cast<u8>((20 - h) / 2), 14, h, 14, k.paused, true, true};
        menu_.open(def_, k);
        debug::log("Pause menu opened");
    }

    void update() override {
        const Event e = menu_.update();
        render::palettes::update();
        if (e == Event::Back || (e == Event::None && g_kit->pauseAction >= 0 && input::actionPressed(g_kit->pauseAction))) {
            close();
            return;
        }
        if (e != Event::Activate) return;
        switch (items_[menu_.cursor()].command) {
            case Command::Resume:
                close();
                break;
            case Command::Options:
                openOptions();
                break;
            case Command::QuitToTitle:
                close();
                game::loadScene(g_kit->titleScene);
                break;
            default:
                break;
        }
    }

    void exit() override {
        menu_.close();
        restore_.give();
    }

    // The options screen covered the pause box: point the map at its tiles again.
    void resume() override { menu_.remap(); }

private:
    void close() {
        debug::log("Pause menu closed");
        game::pop();
    }

    Restore restore_;
    ItemDef items_[3];
    MenuDef def_;
    Menu menu_;
};

class OptionsScreen final : public Screen {
public:
    const char* name() const override { return "options"; }
    bool overlay() const override { return true; }

    void enter() override {
        const Kit& k = *g_kit;
        const int textBg = text::layer();
        restore_.take();
        backdrop_ = render::palettes::baseColor(0);
        for (int b = 0; b < 4; ++b) shown_[b] = render::bg::visible(b);
        objShown_ = true;
        for (int b = 0; b < 4; ++b)
            if (b != textBg) render::bg::setVisible(b, false);
        render::bg::setObjVisible(false);
        commit::setReg(commit::BldCnt, 0);
        render::palettes::setBackdrop(k.screen);
        // Labels and the dialogue box share the text layer: paint them in the screen colour to hide them.
        u16 hidden[16];
        for (int i = 0; i < 16; ++i) hidden[i] = k.screen;
        render::palettes::setBank(false, kTextBank, hidden);

        int n = 0;
        items_[n++] = {ItemKind::Slider, k.music, Command::None, 10, nullptr, 0};
        musicItem_ = 0;
        items_[n++] = {ItemKind::Slider, k.effects, Command::None, 10, nullptr, 0};
        languageItem_ = -1;
        if (text::languageCount() > 1 && k.languageNames) {
            languageItem_ = static_cast<s8>(n);
            items_[n++] = {ItemKind::Choice, k.language, Command::None, 0, k.languageNames, text::languageCount()};
        }
        firstBinding_ = -1;
        if (k.actionCount) {
            items_[n++] = {ItemKind::Heading, k.controls, Command::None, 0, nullptr, 0};
            firstBinding_ = static_cast<s8>(n);
            for (int a = 0; a < k.actionCount && n < Menu::kMaxItems; ++a)
                items_[n++] = {ItemKind::Binding, k.actionNames[a], Command::None, static_cast<u16>(a), nullptr, 0};
        }
        def_ = {items_, static_cast<u8>(n), 2, 1, 26, 17, 12, k.optionsTitle, false, true};
        // Light text on the screen colour (Figma "In-game · Options").
        kit_ = k;
        kit_.ink = k.screenInk;
        kit_.paper = k.screen;
        menu_.open(def_, kit_);
        menu_.setValue(0, (audio::musicVolume() * 10).round());
        menu_.setValue(1, g_effects);
        if (languageItem_ >= 0) menu_.setValue(languageItem_, text::language());
        if (!k.remap)
            for (int i = firstBinding_; firstBinding_ >= 0 && i < n; ++i) menu_.setEnabled(i, false);
        hint_.init(textBg, 2, 18, 26, 2, kBank, render::TextBox::Style{7, 0, 0});
        drawHint();
        waiting_ = -1;
        debug::log("Options opened");
    }

    void update() override {
        render::palettes::update();
        hint_.update();
        if (waiting_ >= 0) {
            waitForKey();
            return;
        }
        const Event e = menu_.update();
        const int c = menu_.cursor();
        if (e == Event::Change) {
            if (c == musicItem_) audio::setMusicVolume(fixed(menu_.value(c)) / 10);
            if (c == 1) {
                g_effects = static_cast<u8>(menu_.value(c));
                audio::setEffectsVolume(fixed(g_effects) / 10);
            }
            if (c == languageItem_) {
                text::setLanguage(static_cast<u8>(menu_.value(c)));
                menu_.redraw();
                drawHint();
            }
            dirty_ = true;
        } else if (e == Event::Activate && items_[c].kind == ItemKind::Binding) {
            waiting_ = static_cast<s8>(c);
            menu_.setWaiting(c);
        } else if (e == Event::Back) {
            game::pop();
        }
    }

    void exit() override {
        menu_.close();
        hint_.unmap(text::blankEntry());
        hint_.release();
        for (int b = 0; b < 4; ++b) render::bg::setVisible(b, shown_[b]);
        render::bg::setObjVisible(objShown_);
        render::palettes::setBackdrop(backdrop_);
        restore_.give();
        if (dirty_) saveOptions();
        dirty_ = false;
        debug::log("Options closed");
    }

    static u8 g_effects;

private:
    void drawHint() {
        hint_.clear();
        const Kit& k = *g_kit;
        const data::TextRun* r = k.hint.key >= 0 ? text::run(static_cast<u16>(k.hint.key)) : nullptr;
        if (r) {
            int w = 0;
            for (u16 i = 0; i < r->count; ++i) {
                const int e = r->glyphs[i].x + r->font->glyphs[r->glyphs[i].glyph].advance;
                if (e > w) w = e;
            }
            hint_.drawRun(*r, (hint_.widthPx() - w) / 2, 0);
        } else if (k.hint.ascii && k.font) {
            const int w = hint_.drawAscii(*k.font, k.hint.ascii, 0, 0);
            hint_.clear();
            hint_.drawAscii(*k.font, k.hint.ascii, (hint_.widthPx() - w) / 2, 0);
        }
        hint_.update();
    }

    // Remap: the next face, shoulder, Start or Select key becomes the action's button; an action that had
    // that key gets this action's old key (swap). Up / Down cancel.
    void waitForKey() {
        const int item = waiting_;
        const int action = items_[item].arg;
        if (input::pressed(Key::Up) || input::pressed(Key::Down)) {
            waiting_ = -1;
            menu_.setWaiting(-1);
            return;
        }
        constexpr u16 kBindable = static_cast<u16>(Key::A) | static_cast<u16>(Key::B) | static_cast<u16>(Key::L) | static_cast<u16>(Key::R) |
                                  static_cast<u16>(Key::Start) | static_cast<u16>(Key::Select);
        u16 key = 0;
        for (int b = 0; b < 10 && !key; ++b) {
            const u16 bit = static_cast<u16>(1u << b);
            if ((kBindable & bit) && input::pressed(static_cast<Key>(bit))) key = bit;
        }
        if (!key) return;
        const u16 old = input::actionKeys(action);
        for (int a = 0; a < input::actionCount(); ++a)
            if (a != action && (input::actionKeys(a) & key)) input::remapAction(a, static_cast<u16>((input::actionKeys(a) & ~key) | old));
        input::remapAction(action, key);
        debug::logf("Remapped action %d to %s", action, keyName(key));
        dirty_ = true;
        waiting_ = -1;
        menu_.setWaiting(-1);
    }

    // Writes the options block to the save chip (volumes, language, one nibble per action for the remap).
    static void saveOptions() {
        save::Options o;
        o.music = static_cast<u8>((audio::musicVolume() * 10).round());
        o.effects = g_effects;
        o.language = text::language();
        for (int a = 0; a < input::actionCount() && a < 8; ++a) {
            const u16 keys = input::actionKeys(a);
            u8 nib = 0xF;
            for (u8 k = 0; k < 6; ++k)
                if (keys == kBindKeys[k]) nib = k;
            o.remap[a / 2] = static_cast<u8>((o.remap[a / 2] & ~(0xF << (a % 2 * 4))) | (nib << (a % 2 * 4)));
        }
        save::writeOptions(o);
    }

    static constexpr u16 kBindKeys[6] = {static_cast<u16>(Key::A), static_cast<u16>(Key::B), static_cast<u16>(Key::Select),
                                         static_cast<u16>(Key::Start), static_cast<u16>(Key::R), static_cast<u16>(Key::L)};
    friend void agb::ui::loadOptions();

    bool dirty_ = false;
    Restore restore_;
    Kit kit_{};
    u16 backdrop_ = 0;
    bool shown_[4] = {};
    bool objShown_ = true;
    ItemDef items_[Menu::kMaxItems];
    MenuDef def_;
    Menu menu_;
    render::TextBox hint_;
    s8 musicItem_ = 0, languageItem_ = -1, firstBinding_ = -1, waiting_ = -1;
};
u8 OptionsScreen::g_effects = 10;

}  // namespace

void loadOptions() {
    save::Options o;
    if (!save::readOptions(o)) return;
    audio::setMusicVolume(fixed(o.music) / 10);
    OptionsScreen::g_effects = o.effects;
    audio::setEffectsVolume(fixed(o.effects) / 10);
    if (o.language < text::languageCount()) text::setLanguage(o.language);
    for (int a = 0; a < input::actionCount() && a < 8; ++a) {
        const u8 nib = (o.remap[a / 2] >> (a % 2 * 4)) & 0xF;
        if (nib < 6) input::remapAction(a, OptionsScreen::kBindKeys[nib]);
    }
}

namespace {
AGB_EWRAM_BSS u8 g_pauseStorage[sizeof(PauseScreen)] AGB_ALIGN4;
AGB_EWRAM_BSS u8 g_optionsStorage[sizeof(OptionsScreen)] AGB_ALIGN4;

bool isPage(const Screen* s);

bool isOverlay(const Screen* s) {
    return s && (isPage(s) || s == reinterpret_cast<const Screen*>(g_pauseStorage) || s == reinterpret_cast<const Screen*>(g_optionsStorage));
}
}  // namespace

void setKit(const Kit* k) {
    g_kit = k;
    if (k && k->box) text::configureBox(*k->box);
    g_sceneMenu.forget();
}
const Kit* kit() { return g_kit; }

void openSceneMenu(const MenuDef& def) {
    if (!g_kit) {
        debug::log("warning: this scene's menu can't be shown because the game has no font for menus. Add a font in Font Manager and build again.");
        return;
    }
    g_sceneMenu.open(def, *g_kit);
    // "Continue" needs a save.
    for (int i = 0; i < def.count; ++i)
        if (def.items[i].command == Command::Continue && !(game::state() && save::exists(0))) g_sceneMenu.setEnabled(i, false);
}

void closeSceneMenu() {
    // The scene's arena and VRAM are reset by the load; only forget the menu.
    g_sceneMenu.forget();
}

bool sceneMenuOpen() { return g_sceneMenu.isOpen(); }
Menu& sceneMenu() { return g_sceneMenu; }

void updateSceneMenu() {
    if (game::depth() && isOverlay(game::top())) return;
    if (g_sceneMenu.update() != Event::Activate) return;
    const ItemDef& it = g_sceneMenu.def()->items[g_sceneMenu.cursor()];
    switch (it.command) {
        case Command::LoadScene:
            game::loadScene(it.arg);
            break;
        case Command::Continue:
            if (api::Save::Read(0)) {
                const SaveHeader& h = api::Save::header();
                game::loadScene(h.scene, h.x, h.y);
            }
            break;
        case Command::Options:
            openOptions();
            break;
        case Command::QuitToTitle:
            if (g_kit) game::loadScene(g_kit->titleScene);
            break;
        default:
            break;
    }
}

bool pauseRequested() {
    return g_kit && g_kit->pauseAction >= 0 && !overlayOpen() && !g_sceneMenu.isOpen() && !text::open() && !game::loading() && !script::cutsceneRunning() &&
           render::effects::transitionDone() && input::actionPressed(g_kit->pauseAction);
}

void openPause() {
    if (!g_kit) return;
    game::push(new (g_pauseStorage) PauseScreen());
}

void openOptions() {
    if (!g_kit) return;
    game::push(new (g_optionsStorage) OptionsScreen());
}

bool overlayOpen() { return game::depth() && isOverlay(game::top()); }

// ---- UI pages -------------------------------------------------------------------------------------------

namespace {
const PageSet* g_pages = nullptr;
const char* g_pageChosen = nullptr;
// What a cutscene waiting in UI.Choose hears.
bool g_pageWaiting = false, g_pageWakeOnMove = false, g_pageResultReady = false;
s32 g_pageResult = 0;

bool sameName(const char* a, const char* b) {
    while (*a && *a == *b) {
        ++a;
        ++b;
    }
    return *a == *b;
}

void setPageResult(s32 result) {
    g_pageResult = result;
    g_pageResultReady = true;
}

void remapOpenPages();
void pumpOpenPages();

// The on-screen keyboard of an Input Field, in the rectangle its KeyboardDef gives (the build keeps it in the page's panel).
constexpr int kKeyCols = 13, kKeyRows = 6;
const char* const kKeyRowsText[kKeyRows - 1] = {"ABCDEFGHIJKLM", "NOPQRSTUVWXYZ", "abcdefghijklm", "nopqrstuvwxyz", "0123456789.-'"};

class PageScreen final : public Screen {
public:
    PageScreen(const PageDef& def, int data) : def_(def), data_(data) {
        for (int i = 0; i < def.count && i < kMaxPageElements; ++i) {
            const ElementDef& e = def.elements[i];
            amount_[i] = e.kind == ElementKind::Dropdown && e.amount >= e.optionCount ? 0 : e.amount;  // a Dropdown keeps its option here
            if (e.hidden) hidden_ |= 1ull << i;
            if (e.on) on_ |= 1ull << i;
            if (!e.interactable) disabled_ |= 1ull << i;
            if (e.kind == ElementKind::ScrollRect && scrollCount_ < kMaxScrollRects) scrollElement_[scrollCount_++] = static_cast<s8>(i);
        }
        focus_ = def.firstFocus;
    }

    const char* name() const override { return def_.name; }
    bool overlay() const override { return true; }

    void enter() override {
        const int bg = text::layer();
        text::reserveBoxes();  // the dialogue box outlives this page: its memory must not be given back when the page closes
        restore_.take();
        render::palettes::setBank(false, kBank, g_pages->palette);
        debug::logf("UI page %s opened", def_.name);
        if (def_.dim) {
            u8 targets = render::effects::kObj | render::effects::kBackdrop;
            for (int b = 0; b < 4; ++b)
                if (b != bg) targets = static_cast<u8>(targets | (1u << b));
            render::effects::setBrightness(targets, -static_cast<int>(def_.dim));
        }
        const u8 charBlock = bg >= 0 ? render::bg::config(bg).charBlock : 0;
        if (bg < 0 || !box_.init(bg, def_.tileX, def_.tileY, def_.widthTiles, def_.heightTiles, kBank, render::TextBox::Style{kInk, 0, 0})) {
            debug::warnOnce(22, "A UI page did not open: there is no background memory left for its panel. Make the page smaller or use fewer map tiles in this scene.");
            return;
        }
        if (render::bg::config(bg).charBlock != charBlock)
            debug::warnOnce(26, "UI pages open together use more than 1024 tiles of the text layer, so the pages under this one show wrong tiles. Close a large page before opening another.");
        box_.setRichInks(g_pages->richInks);
        box_.unmap(text::blankEntry());  // it shows once its tiles are in VRAM (a large panel takes a few frames)
        ready_ = true;
        // A control that starts disabled or hidden gives the cursor away; so does a list row out of view.
        if (focus_ >= 0 && !canFocus(focus_)) moveAway();
        if (focus_ >= 0) settleScroll(focus_);
        redraw(0, 0, 240, 160);
    }

    void update() override {
        if (!ready_) {
            game::pop();
            return;
        }
        if (game::loading()) {  // Scene.Load from a script: the scene underneath has to run to change
            game::pop();
            return;
        }
        pumpOpenPages();
        render::palettes::update();
        const bool wasOpen = text::open();
        if (!text::waiting()) handleInput();
        if (game::top() != this) return;  // the page closed, or opened another
        if (def_.scenePlays) {
            game::at(0)->update();
            if (game::top() != this) return;
        } else {
            render::effects::update();  // a fade the scene started goes on under the page
            script::run();
            if (game::top() != this) return;
            text::update();
            if (text::open() && !text::waiting() && !script::cutsceneRunning()) text::close();
        }
        if (wasOpen && !text::open()) remapOpenPages();
    }

    void exit() override {
        debug::logf("UI page %s closed", def_.name);
        if (ready_) box_.unmap(text::blankEntry());
        box_.release();
        restore_.give(def_.dim != 0);
        if (g_sceneMenu.isOpen() && g_kit) loadPalette(*g_kit);
    }

    void resume() override {
        remapOpenPages();
        if (text::open()) text::box().remap();
        restorePalette();
    }

    void remapBox() {
        if (ready_ && shownOnScreen_) box_.remap();
    }

    // Copies changed tiles to VRAM; a page opening shows once all of them are there. Runs for every open page, because a
    // page that opened in the frame another opened over it still has to arrive.
    void pump() {
        if (!ready_) return;
        box_.update();
        if (!shownOnScreen_ && !box_.uploading()) {
            shownOnScreen_ = true;
            remapOpenPages();
            if (text::open()) text::box().remap();
        }
    }

    int data() const { return data_; }
    int focus() const { return focus_; }
    const PageDef& def() const { return def_; }

    int find(const char* element) const {
        for (int i = 0; i < def_.count; ++i)
            if (sameName(def_.elements[i].name, element)) return i;
        return -1;
    }

    void setHidden(int i, bool hidden) {
        const u64 bit = 1ull << i;
        if (((hidden_ & bit) != 0) == hidden) return;
        hidden_ = hidden ? hidden_ | bit : hidden_ & ~bit;
        if (hidden && focus_ >= 0 && !canFocus(focus_)) moveAway();
        if (ready_) {
            const int s = scrollOf(i);
            if (s >= 0) {
                clampScroll(s);
                if (focus_ >= 0) settleScroll(focus_);
                redrawScroll(s);
            } else {
                redrawElement(i);
            }
        }
    }

    void setInteractable(int i, bool on) {
        const u64 bit = 1ull << i;
        if (((disabled_ & bit) == 0) == on) return;
        disabled_ = on ? disabled_ & ~bit : disabled_ | bit;
        if (!on && focus_ >= 0 && !canFocus(focus_)) moveAway();
        if (ready_) redrawElement(i);
    }

    void setFocus(int i) {
        if (i < 0 || i >= def_.count || !canFocus(i) || focus_ == i) return;
        const int old = focus_;
        focus_ = static_cast<s8>(i);
        if (!ready_) return;
        if (!settleScroll(i)) {
            if (old >= 0) redrawElement(old);
            redrawElement(i);
        }
    }

    void setAmount(int i, int amount) {
        amount = amount < 0 ? 0 : amount > 100 ? 100 : amount;
        if (amount_[i] == amount) return;
        amount_[i] = static_cast<u8>(amount);
        if (ready_) redrawElement(i);
    }

    void setText(int i, const Label& label) {
        int slot = 0;
        while (slot < kMaxChangedTexts && changedText_[slot] != i + 1 && changedText_[slot]) ++slot;
        if (slot == kMaxChangedTexts) {
            debug::warnOnce(25, "UI.SetText: a page can change the text of 20 elements while it is open, so this one kept its text.");
            return;
        }
        changedText_[slot] = static_cast<u8>(i + 1);
        changedKey_[slot] = label.key;
        if (ready_) redrawElement(i);
    }

    void setImage(int i, const ImageDef* image) {
        int slot = 0;
        while (slot < kMaxChangedImages && changedImageEl_[slot] != i + 1 && changedImageEl_[slot]) ++slot;
        if (slot == kMaxChangedImages) {
            debug::warnOnce(27, "UI.SetImage: a page can change the image of 6 elements while it is open, so this one kept its image.");
            return;
        }
        changedImageEl_[slot] = static_cast<u8>(i + 1);
        changedImage_[slot] = image;
        if (ready_) redrawElement(i);
    }

    // Numbers shown in an element: its text's {0} and {1}, or after its text when it has neither token.
    void setValues(int i, s32 a, s32 b, int count, int digits) {
        int slot = 0;
        while (slot < kMaxValues && values_[slot].element != i + 1 && values_[slot].element) ++slot;
        if (slot == kMaxValues) {
            debug::warnOnce(28, "UI.SetNumber: a page can show numbers in 20 elements, so this one kept its text. Split the page.");
            return;
        }
        Values& v = values_[slot];
        if (v.element == i + 1 && v.a == a && v.b == b && v.count == count && v.digits == digits) return;
        v = Values{a, b, static_cast<u8>(i + 1), static_cast<s8>(count), static_cast<s8>(digits)};
        if (ready_) redrawElement(i);
    }

    void setOn(int i, bool on) {
        const u64 bit = 1ull << i;
        const ElementDef& e = def_.elements[i];
        if (on && e.group >= 0)
            for (int k = 0; k < def_.count; ++k)
                if (k != i && def_.elements[k].group == e.group && (on_ & (1ull << k))) {
                    on_ &= ~(1ull << k);
                    if (ready_) redrawElement(k);
                }
        on_ = on ? on_ | bit : on_ & ~bit;
        if (ready_) redrawElement(i);
    }

    bool on(int i) const { return (on_ & (1ull << i)) != 0; }
    int amount(int i) const { return amount_[i]; }

    bool shown(int i) const {
        for (; i >= 0; i = def_.elements[i].parent)
            if (hidden_ & (1ull << i)) return false;
        return true;
    }

    int inputLength() const {
        int n = 0;
        while (input_[n]) ++n;
        return n;
    }
    int inputChar(int index) const { return index >= 0 && index < inputLength() ? input_[index] : 0; }
    void setInputChar(int index, int ch) {
        if (index < 0 || index >= kMaxInput) return;
        input_[index] = static_cast<char>(ch);
        input_[index + 1] = 0;
        if (!ready_) return;
        for (int i = 0; i < def_.count; ++i)
            if (def_.elements[i].kind == ElementKind::InputField) redrawElement(i);
    }

private:
    struct Values {
        s32 a, b;
        u8 element;  // element index + 1, 0 = free
        s8 count;
        s8 digits;
    };
    static constexpr int kMaxInput = 12;

    void restorePalette() { render::palettes::setBank(false, kBank, g_pages->palette); }

    bool enabled(int i) const {
        for (; i >= 0; i = def_.elements[i].parent)
            if (disabled_ & (1ull << i)) return false;
        return true;
    }
    bool canFocus(int i) const { return shown(i) && enabled(i); }

    void handleInput() {
        if (inputOpen_ >= 0) {
            updateKeyboard();
            return;
        }
        if (dropOpen_ >= 0) {
            updateDropdown();
            return;
        }
        const int old = focus_;
        if (input::repeated(Key::Up)) moveFocus(0);
        else if (input::repeated(Key::Down)) moveFocus(1);
        if (focus_ == old && focus_ >= 0) {
            const ElementDef& e = def_.elements[focus_];
            const int way = (input::repeated(Key::Right) ? 1 : 0) - (input::repeated(Key::Left) ? 1 : 0);
            if (way && e.kind == ElementKind::Slider) {
                setAmount(focus_, amount_[focus_] + way * 5);
                setMoved();
                return;
            }
            if (way && e.kind == ElementKind::Dropdown && e.optionCount) {
                setAmount(focus_, (amount_[focus_] + way + e.optionCount) % e.optionCount);
                setMoved();
                return;
            }
            if (input::repeated(Key::Left)) moveFocus(2);
            else if (input::repeated(Key::Right)) moveFocus(3);
        }
        if (focus_ != old) {
            if (focus_ < 0 || !settleScroll(focus_)) {
                if (old >= 0) redrawElement(old);
                if (focus_ >= 0) redrawElement(focus_);
            }
            setMoved();
            return;
        }
        // Nothing to select: the D-pad scrolls the page's first Scroll Rect (a long text).
        if (focus_ < 0 && scrollCount_) {
            const int way = (input::repeated(Key::Down) ? 8 : 0) - (input::repeated(Key::Up) ? 8 : 0);
            if (way) {
                const int s = scrollElement_[0];
                scroll_[0] = static_cast<s16>(scroll_[0] + way);
                clampScroll(s);
                redrawScroll(s);
                return;
            }
        }
        if (input::pressed(Key::A) && focus_ >= 0) {
            activate(focus_);
            return;
        }
        // Start on an Input Field takes what is typed, without opening the keyboard.
        if (input::pressed(Key::Start) && focus_ >= 0 && def_.elements[focus_].kind == ElementKind::InputField) {
            g_pageChosen = def_.elements[focus_].name;
            if (g_pageWaiting) setPageResult(def_.elements[focus_].data);
            return;
        }
        if (input::pressed(Key::B) && def_.cancel) {
            if (g_pageWaiting) setPageResult(-1);
            else game::pop();
        }
    }

    void setMoved() {
        if (g_pageWaiting && g_pageWakeOnMove) setPageResult(ui::kPageMoved);
    }

    // Follows the links that way past hidden and disabled controls.
    void moveFocus(int way) {
        if (focus_ < 0) return;
        int at = focus_;
        for (int n = 0; n < def_.count; ++n) {
            const ElementDef& e = def_.elements[at];
            const s8 next = way == 0 ? e.up : way == 1 ? e.down : way == 2 ? e.left : e.right;
            if (next < 0 || next == focus_) return;
            if (canFocus(next)) {
                focus_ = next;
                return;
            }
            at = next;
        }
    }

    // A hidden or disabled control gives the cursor to its nearest neighbour; with none showing, nothing is selected.
    void moveAway() {
        const s8 here = focus_;
        for (int i = 0; i < 4; ++i) {
            focus_ = here;
            const s8 before = focus_;
            moveFocus(i == 0 ? 1 : i == 1 ? 0 : i == 2 ? 3 : 2);
            if (focus_ != before) {
                if (ready_) redrawElement(focus_);
                return;
            }
        }
        focus_ = -1;
    }

    void activate(int i) {
        const ElementDef& e = def_.elements[i];
        if (e.kind == ElementKind::Dropdown && e.optionCount) {
            dropOpen_ = static_cast<s8>(i);
            dropCursor_ = amount_[i] < e.optionCount ? amount_[i] : 0;
            drawDropdown();
            box_.update();
            return;
        }
        if (e.kind == ElementKind::InputField) {
            inputOpen_ = static_cast<s8>(i);
            keyCol_ = keyRow_ = 0;
            redraw(e.keyboard->x, e.keyboard->y, e.keyboard->w, e.keyboard->h);
            return;
        }
        if (e.kind == ElementKind::Toggle) setOn(i, e.group >= 0 ? true : !on(i));
        g_pageChosen = e.name;
        switch (e.action) {
            case ActionKind::OpenPage:
                openPage(static_cast<int>(e.target), e.data);
                break;
            case ActionKind::ClosePage:
                if (g_pageWaiting) setPageResult(e.data);
                game::pop();
                break;
            case ActionKind::LoadScene: {
                const u16 scene = static_cast<u16>(e.target);
                while (overlayOpen()) game::pop();
                game::loadScene(scene);
                break;
            }
            default:
                if (g_pageWaiting) setPageResult(e.data);
                break;
        }
    }

    // ---- Scroll Rect -------------------------------------------------------------------------------------
    int scrollSlot(int s) const {
        for (int k = 0; k < scrollCount_; ++k)
            if (scrollElement_[k] == s) return k;
        return -1;
    }

    // The nearest Scroll Rect above an element, -1 = none.
    int scrollOf(int i) const {
        if (!scrollCount_) return -1;
        for (int p = def_.elements[i].parent; p >= 0; p = def_.elements[p].parent)
            if (def_.elements[p].kind == ElementKind::ScrollRect) return p;
        return -1;
    }

    int rowOf(int i, int s) const {
        while (def_.elements[i].parent != s) i = def_.elements[i].parent;
        return i;
    }

    // Space that hidden rows above this row gave back, so a list closes up when a script hides rows.
    int collapsedAbove(int row, int s) const {
        const int step = def_.elements[s].data;
        int gone = 0;
        for (int k = s + 1; k < row; ++k)
            if (def_.elements[k].parent == s && (hidden_ & (1ull << k))) gone += def_.elements[k].h + step;
        return gone;
    }

    int contentHeight(int s) const {
        const ElementDef& v = def_.elements[s];
        int h = v.padT + v.padB, rows = 0;
        for (int k = s + 1; k < def_.count; ++k)
            if (def_.elements[k].parent == s && !(hidden_ & (1ull << k))) {
                h += def_.elements[k].h;
                ++rows;
            }
        return h + (rows > 1 ? (rows - 1) * v.data : 0);
    }

    int maxScroll(int s) const {
        const int over = contentHeight(s) - def_.elements[s].h;
        return over > 0 ? over : 0;
    }

    int offsetY(int i) const {
        const int s = scrollOf(i);
        if (s < 0) return 0;
        const int k = scrollSlot(s);
        return -(k >= 0 ? scroll_[k] : 0) - collapsedAbove(rowOf(i, s), s);
    }

    void clampScroll(int s) {
        const int k = scrollSlot(s);
        if (k < 0) return;
        const int most = maxScroll(s);
        if (scroll_[k] > most) scroll_[k] = static_cast<s16>(most);
        if (scroll_[k] < 0) scroll_[k] = 0;
    }

    // Brings a list row into view; true when the list moved (and was redrawn).
    bool settleScroll(int i) {
        const int s = scrollOf(i);
        if (s < 0) return false;
        const int k = scrollSlot(s);
        if (k < 0) return false;
        const ElementDef& v = def_.elements[s];
        const int row = rowOf(i, s);
        const ElementDef& r = def_.elements[row];
        const int top = r.y - v.y - collapsedAbove(row, s);
        const int was = scroll_[k];
        if (top - v.padT < scroll_[k]) scroll_[k] = static_cast<s16>(top - v.padT);
        if (top + r.h + v.padB > scroll_[k] + v.h) scroll_[k] = static_cast<s16>(top + r.h + v.padB - v.h);
        clampScroll(s);
        if (scroll_[k] == was) return false;
        if (ready_) redrawScroll(s);
        return true;
    }

    void redrawScroll(int s) {
        const ElementDef& v = def_.elements[s];
        redraw(v.x, v.y, v.w, v.h);
        for (int i = 0; i < def_.count; ++i)
            if (def_.elements[i].kind == ElementKind::Scrollbar && def_.elements[i].scrollTarget == s) redrawElement(i);
    }

    // ---- drawing -----------------------------------------------------------------------------------------
    // Everything under a rectangle again, parents first (the build lists parents before children).
    void redraw(int x, int y, int w, int h) {
        const int ox = def_.tileX * 8, oy = def_.tileY * 8;
        x -= ox;
        y -= oy;
        if (x < 0) { w += x; x = 0; }
        if (y < 0) { h += y; y = 0; }
        if (w > box_.widthPx() - x) w = box_.widthPx() - x;
        if (h > box_.heightPx() - y) h = box_.heightPx() - y;
        if (w <= 0 || h <= 0) return;
        const int rx = x, ry = y, rr = x + w, rb = y + h;
        clipX_ = rx;
        clipY_ = ry;
        clipR_ = rr;
        clipB_ = rb;
        box_.fillRect(x, y, w, h, 0);
        for (int i = 0; i < def_.count; ++i) {
            const ElementDef& e = def_.elements[i];
            if (!shown(i)) continue;
            const int ex = e.x - ox, ey = e.y - oy + offsetY(i);
            clipX_ = rx;
            clipY_ = ry;
            clipR_ = rr;
            clipB_ = rb;
            const int s = scrollOf(i);
            if (s >= 0) {  // Mask: the Scroll Rect shows its content only inside itself, inside its border
                const ElementDef& v = def_.elements[s];
                const int b = v.border ? v.borderWidth : 0;
                const int vx = v.x - ox + b, vy = v.y - oy + b, vr = v.x - ox + v.w - b, vb = v.y - oy + v.h - b;
                if (vx > clipX_) clipX_ = vx;
                if (vy > clipY_) clipY_ = vy;
                if (vr < clipR_) clipR_ = vr;
                if (vb < clipB_) clipB_ = vb;
                if (clipX_ >= clipR_ || clipY_ >= clipB_) continue;
            }
            if (ex >= clipR_ || ey >= clipB_ || ex + e.w <= clipX_ || ey + e.h <= clipY_) continue;
            draw(i, ex, ey);
        }
        clipX_ = rx;
        clipY_ = ry;
        clipR_ = rr;
        clipB_ = rb;
        if (dropOpen_ >= 0) drawDropdown();
        if (inputOpen_ >= 0) drawKeyboard();
        box_.clearClip();
        box_.update();
    }

    void redrawElement(int i) {
        const ElementDef& e = def_.elements[i];
        redraw(e.x, e.y + offsetY(i), e.w, e.h);
    }

    void fill(int x, int y, int w, int h, u8 index) {
        if (!index) return;
        if (x < clipX_) { w -= clipX_ - x; x = clipX_; }
        if (y < clipY_) { h -= clipY_ - y; y = clipY_; }
        if (w > clipR_ - x) w = clipR_ - x;
        if (h > clipB_ - y) h = clipB_ - y;
        if (w > 0 && h > 0) box_.fillRect(x, y, w, h, index);
    }

    void frame(int x, int y, int w, int h, int b, u8 color) {
        if (!color || !b) return;
        fill(x, y, w, b, color);
        fill(x, y + h - b, w, b, color);
        fill(x, y + b, b, h - 2 * b, color);
        fill(x + w - b, y + b, b, h - 2 * b, color);
    }

    void blit(const ImageDef& im, int sx, int sy, int w, int h, int x, int y) {
        if (x < clipX_) { const int d = clipX_ - x; w -= d; sx += d; x = clipX_; }
        if (y < clipY_) { const int d = clipY_ - y; h -= d; sy += d; y = clipY_; }
        if (w > clipR_ - x) w = clipR_ - x;
        if (h > clipB_ - y) h = clipB_ - y;
        if (w > 0 && h > 0) box_.blit(im.pixels + sy * im.width + sx, im.width, w, h, x, y);
    }

    // Repeats a piece of the image over a rectangle.
    void tile(const ImageDef& im, int sx, int sy, int sw, int sh, int x, int y, int w, int h) {
        if (sw <= 0 || sh <= 0) return;
        for (int dy = 0; dy < h; dy += sh)
            for (int dx = 0; dx < w; dx += sw)
                blit(im, sx, sy, sw < w - dx ? sw : w - dx, sh < h - dy ? sh : h - dy, x + dx, y + dy);
    }

    const ImageDef* imageOf(int i) const {
        for (int slot = 0; slot < kMaxChangedImages && changedImageEl_[slot]; ++slot)
            if (changedImageEl_[slot] == i + 1) return changedImage_[slot];
        return def_.elements[i].image;
    }

    void drawImage(const ElementDef& e, const ImageDef& im, int amount, int x, int y) {
        const int iw = im.width, ih = im.height;
        switch (e.imageMode) {
            case ImageMode::Sliced: {
                const int l = e.sliceL, t = e.sliceT, r = e.sliceR, b = e.sliceB;
                const int mw = iw - l - r, mh = ih - t - b;
                const int cw = e.w - l - r, ch = e.h - t - b;
                blit(im, 0, 0, l, t, x, y);
                blit(im, iw - r, 0, r, t, x + e.w - r, y);
                blit(im, 0, ih - b, l, b, x, y + e.h - b);
                blit(im, iw - r, ih - b, r, b, x + e.w - r, y + e.h - b);
                tile(im, l, 0, mw, t, x + l, y, cw, t);
                tile(im, l, ih - b, mw, b, x + l, y + e.h - b, cw, b);
                tile(im, 0, t, l, mh, x, y + t, l, ch);
                tile(im, iw - r, t, r, mh, x + e.w - r, y + t, r, ch);
                tile(im, l, t, mw, mh, x + l, y + t, cw, ch);
                break;
            }
            case ImageMode::Tiled:
                tile(im, 0, 0, iw, ih, x, y, e.w, e.h);
                break;
            case ImageMode::FilledHorizontal:
                blit(im, 0, 0, iw * amount / 100, ih, x, y);
                break;
            case ImageMode::FilledVertical: {
                const int part = ih * amount / 100;  // fills upward from the bottom
                blit(im, 0, ih - part, iw, part, x, y + ih - part);
                break;
            }
            default:
                blit(im, 0, 0, iw, ih, x, y);
                break;
        }
    }

    int labelWidth(const Label& l) const {
        if (l.key >= 0) {
            if (const data::TextRun* r = text::run(static_cast<u16>(l.key))) return box_.runWidth(*r);
        }
        const data::Font* f = g_kit ? g_kit->font : nullptr;
        if (!l.ascii || !f || !f->ascii) return 0;
        int w = 0;
        for (const char* p = l.ascii; *p; ++p) {
            const u8 c = static_cast<u8>(*p);
            if (c < 128 && f->ascii[c] != 0xFFFF) w += f->glyphs[f->ascii[c]].advance;
        }
        return w;
    }

    const Values* valuesOf(int i) const {
        for (int slot = 0; slot < kMaxValues && values_[slot].element; ++slot)
            if (values_[slot].element == i + 1) return values_[slot].count ? &values_[slot] : nullptr;
        return nullptr;
    }

    // Writes the values as "a" or "a/b" into 26 bytes.
    static void valuesText(const Values& v, char* out) {
        render::numberText(v.a, v.digits, out);
        if (v.count < 2) return;
        while (*out) ++out;
        *out++ = '/';
        render::numberText(v.b, v.digits, out);
    }

    // The text of an element inside (x, y, w, h), padded; it never leaves the element's rectangle (RectMask2D).
    void drawLabel(int i, const Label& l, u8 ink, int x, int y, int w, int h, Align alignX, Align alignY, bool useValues) {
        const ElementDef& e = def_.elements[i];
        if (!ink || !g_kit || !g_kit->font) return;
        const data::Font& font = *g_kit->font;
        const data::TextRun* r = l.key >= 0 ? text::run(static_cast<u16>(l.key)) : nullptr;
        const bool hasLabel = r || (l.ascii && *l.ascii);
        const Values* values = useValues ? valuesOf(i) : nullptr;
        if (!hasLabel && !values) return;

        render::TextVar vars[render::kTextVars];
        const bool tokens = r && r->markCount;
        if (tokens) {
            for (int k = 0; k < render::kTextVars; ++k) vars[k] = render::textVar(k);
            if (values) {
                vars[0] = render::TextVar{};
                vars[0].number = values->a;
                vars[0].digits = values->digits;
                if (values->count > 1) {
                    vars[1] = render::TextVar{};
                    vars[1].number = values->b;
                    vars[1].digits = values->digits;
                }
            }
            box_.setVars(vars);
        }
        char extra[26];
        extra[0] = 0;
        if (values && !tokens) valuesText(*values, extra);
        int extraW = 0;
        if (extra[0] && font.ascii)
            for (const char* p = extra; *p; ++p)
                if (font.ascii[static_cast<u8>(*p)] != 0xFFFF) extraW += font.glyphs[font.ascii[static_cast<u8>(*p)]].advance;
        const int labelW = hasLabel ? labelWidth(l) : 0;
        const int gap = hasLabel && extra[0] ? 4 : 0;
        const int lw = labelW + gap + extraW;
        const int lines = r ? r->lines : 1;
        const int lh = font.lineHeight;
        if (inputOpen_ < 0 && (lw > w || lines * lh > h + 2) && !(warned_ & (1ull << i))) {
            warned_ |= 1ull << i;
            debug::logf("warning: UI page %s: the text of %s is %d x %d px and its space is %d x %d px, so it is cut. Make the text shorter or the element larger.", def_.name, e.name, lw, lines * lh, w, h);
        }
        const int tx = x + (alignX == Align::Center ? (w - lw) / 2 : alignX == Align::End ? w - lw : 0);
        const int ty = y + (alignY == Align::Center ? (h - lines * lh) / 2 : alignY == Align::End ? h - lines * lh : 0);
        // Mask to the element's own rectangle and to the clip of the redraw.
        const int cl = x < clipX_ ? clipX_ : x, cr = x + w > clipR_ ? clipR_ : x + w;
        box_.setClip(cl, clipY_, cr, clipB_);
        box_.setInk(ink);
        if (r) box_.drawRun(*r, tx, ty);
        else if (l.ascii) box_.drawAscii(font, l.ascii, tx, ty);
        if (extra[0]) box_.drawAscii(font, extra, tx + labelW + gap, ty);
        box_.setInk(kInk);
        box_.setVars(nullptr);
        box_.clearClip();
    }

    Label labelOf(int i) const {
        for (int slot = 0; slot < kMaxChangedTexts && changedText_[slot]; ++slot)
            if (changedText_[slot] == i + 1) return Label{changedKey_[slot], nullptr};
        return def_.elements[i].label;
    }

    void draw(int i, int x, int y) {
        const ElementDef& e = def_.elements[i];
        const bool focused = i == focus_;
        const bool off = !enabled(i);
        const u8 fillColor = focused && e.focusFill ? e.focusFill : e.fill;
        const u8 border = focused && e.focusBorder ? e.focusBorder : e.border;
        const u8 ink = off && e.disabledInk ? e.disabledInk : focused && e.focusInk ? e.focusInk : e.ink;
        fill(x, y, e.w, e.h, fillColor);
        if (const ImageDef* image = imageOf(i)) drawImage(e, *image, amount_[i], x, y);
        if (e.kind == ElementKind::Slider) {
            const int inset = e.border ? e.borderWidth : 0;
            fill(x + inset, y + inset, (e.w - 2 * inset) * amount_[i] / 100, e.h - 2 * inset, e.accent);
        }
        if (e.kind == ElementKind::Scrollbar && e.scrollTarget >= 0) {
            const int s = e.scrollTarget;
            const int content = contentHeight(s), view = def_.elements[s].h, most = maxScroll(s);
            int thumb = most > 0 ? e.h * view / content : e.h;
            if (thumb < 6) thumb = 6;
            const int k = scrollSlot(s);
            const int at = most > 0 && k >= 0 ? (e.h - thumb) * scroll_[k] / most : 0;
            fill(x, y + at, e.w, thumb, e.accent);
            return;
        }
        frame(x, y, e.w, e.h, e.borderWidth, border);
        int textX = x + e.padL, textW = e.w - e.padL - e.padR;
        const int textY = y + e.padT, textH = e.h - e.padT - e.padB;
        if (e.kind == ElementKind::Toggle) {
            const int size = e.h < 10 ? e.h : 10, top = y + (e.h - size) / 2;
            const u8 box = ink ? ink : e.accent;
            frame(textX, top, size, size, 1, box);
            if (on(i)) fill(textX + 2, top + 2, size - 4, size - 4, e.accent ? e.accent : ink);
            textX += size + 4;
            textW -= size + 4;
        } else if (e.kind == ElementKind::Button && e.cursor) {
            if (focused) {
                const u8 c = e.accent ? e.accent : ink;
                const int top = y + e.h / 2 - 3;
                for (int k = 0; k < 3; ++k) fill(textX + 1 + k, top + k, 1, 7 - 2 * k, c);
            }
            textX += 8;
            textW -= 8;
        } else if (e.kind == ElementKind::Dropdown) {
            // Label on the left, the chosen option on the right, then a small down arrow.
            const int arrowX = textX + textW - 5, top = y + e.h / 2 - 1;
            for (int k = 0; k < 3; ++k) fill(arrowX + k, top + k, 5 - 2 * k, 1, ink);
            textW -= 8;
            if (e.optionCount && amount_[i] < e.optionCount) drawLabel(i, e.options[amount_[i]], ink, textX, textY, textW, textH, Align::End, e.alignY, false);
        } else if (e.kind == ElementKind::InputField) {
            Label typed{-1, input_};
            drawLabel(i, typed, ink, textX, textY, textW, textH, Align::Start, e.alignY, false);
            if (focused && g_kit && g_kit->font) fill(textX + labelWidth(typed) + 1, textY + 2, 1, textH - 4, ink);
            return;
        }
        drawLabel(i, labelOf(i), ink, textX, textY, textW, textH, e.kind == ElementKind::Dropdown ? Align::Start : e.alignX, e.alignY, true);
    }

    // ---- Dropdown ----------------------------------------------------------------------------------------
    // The open list goes below the control, or above it when it would leave the screen.
    int dropTop(const ElementDef& e) const {
        const int h = e.optionCount * e.h + 2;
        const int y = e.y + offsetY(dropOpen_) + e.h;
        return y + h > 160 ? e.y + offsetY(dropOpen_) - h : y;
    }

    void drawDropdown() {
        const ElementDef& e = def_.elements[dropOpen_];
        const int ox = def_.tileX * 8, oy = def_.tileY * 8;
        const int x = e.x - ox, y = dropTop(e) - oy, h = e.optionCount * e.h + 2;
        fill(x, y, e.w, h, e.fill ? e.fill : e.focusFill);
        frame(x, y, e.w, h, 1, e.border ? e.border : e.ink);
        for (int k = 0; k < e.optionCount; ++k) {
            const int ry = y + 1 + k * e.h;
            const bool at = k == dropCursor_;
            if (at) fill(x + 1, ry, e.w - 2, e.h, e.focusFill);
            drawLabel(dropOpen_, e.options[k], at && e.focusInk ? e.focusInk : e.ink, x + 1 + e.padL, ry + e.padT, e.w - 2 - e.padL - e.padR, e.h - e.padT - e.padB, Align::Start, e.alignY, false);
        }
    }

    void updateDropdown() {
        const ElementDef& e = def_.elements[dropOpen_];
        const int way = (input::repeated(Key::Down) ? 1 : 0) - (input::repeated(Key::Up) ? 1 : 0);
        if (way) {
            dropCursor_ = static_cast<u8>((dropCursor_ + way + e.optionCount) % e.optionCount);
            drawDropdown();
            box_.update();
            return;
        }
        if (!input::pressed(Key::A) && !input::pressed(Key::B)) return;
        const int i = dropOpen_;
        const int top = dropTop(e), h = e.optionCount * e.h + 2;
        const bool picked = input::pressed(Key::A);
        dropOpen_ = -1;
        if (picked) amount_[i] = dropCursor_;
        redraw(e.x, top, e.w, h);
        redrawElement(i);
        if (picked) {
            g_pageChosen = e.name;
            if (g_pageWaiting) setPageResult(e.data);
        }
    }

    // ---- Input Field: an on-screen keyboard ------------------------------------------------------------
    static const char* keyLabel(int action) { return action == 0 ? "SPACE" : action == 1 ? "DEL" : "OK"; }
    static int keyAction(int col) { return col < 5 ? 0 : col < 9 ? 1 : 2; }
    static int keyActionCol(int action) { return action == 0 ? 0 : action == 1 ? 5 : 9; }
    static int keyActionCols(int action) { return action == 0 ? 5 : 4; }

    void drawKey(int col, int row) {
        const KeyboardDef& k = *def_.elements[inputOpen_].keyboard;
        const int kKeyW = (k.w - 8) / kKeyCols, kKeyH = (k.h - 6) / kKeyRows;
        const int ox = k.x - def_.tileX * 8 + (k.w - kKeyW * kKeyCols) / 2, oy = k.y - def_.tileY * 8 + (k.h - kKeyH * kKeyRows) / 2;
        const bool bottom = row == kKeyRows - 1;
        const int start = bottom ? keyActionCol(keyAction(col)) : col;
        const int cols = bottom ? keyActionCols(keyAction(col)) : 1;
        const bool at = row == keyRow_ && (bottom ? keyAction(keyCol_) == keyAction(col) : keyCol_ == col);
        const int x = ox + start * kKeyW, y = oy + row * kKeyH;
        fill(x, y, cols * kKeyW, kKeyH, at ? k.focusFill : k.fill);
        char one[2] = {};
        if (!bottom) one[0] = kKeyRowsText[row][col];
        Label label{-1, bottom ? keyLabel(keyAction(col)) : one};
        drawLabel(inputOpen_, label, at && k.focusInk ? k.focusInk : k.ink, x, y, cols * kKeyW, kKeyH, Align::Center, Align::Center, false);
    }

    void drawKeyboard() {
        const KeyboardDef& k = *def_.elements[inputOpen_].keyboard;
        const int x = k.x - def_.tileX * 8, y = k.y - def_.tileY * 8;
        fill(x, y, k.w, k.h, k.fill);
        frame(x, y, k.w, k.h, 1, k.border ? k.border : k.ink);
        for (int r = 0; r < kKeyRows - 1; ++r)
            for (int c = 0; c < kKeyCols; ++c) drawKey(c, r);
        for (int a = 0; a < 3; ++a) drawKey(keyActionCol(a), kKeyRows - 1);
    }

    void updateKeyboard() {
        const int oldCol = keyCol_, oldRow = keyRow_;
        const bool bottom = keyRow_ == kKeyRows - 1;
        if (input::repeated(Key::Up)) keyRow_ = static_cast<u8>((keyRow_ + kKeyRows - 1) % kKeyRows);
        else if (input::repeated(Key::Down)) keyRow_ = static_cast<u8>((keyRow_ + 1) % kKeyRows);
        else if (input::repeated(Key::Left)) keyCol_ = static_cast<u8>(bottom ? keyActionCol((keyAction(keyCol_) + 2) % 3) : (keyCol_ + kKeyCols - 1) % kKeyCols);
        else if (input::repeated(Key::Right)) keyCol_ = static_cast<u8>(bottom ? keyActionCol((keyAction(keyCol_) + 1) % 3) : (keyCol_ + 1) % kKeyCols);
        if (oldCol != keyCol_ || oldRow != keyRow_) {
            clipX_ = 0;
            clipY_ = 0;
            clipR_ = box_.widthPx();
            clipB_ = box_.heightPx();
            drawKey(oldCol, oldRow);
            drawKey(keyCol_, keyRow_);
            box_.update();
            return;
        }
        const ElementDef& e = def_.elements[inputOpen_];
        int n = inputLength();
        const int cap = e.maxLength && e.maxLength < kMaxInput ? e.maxLength : kMaxInput;
        const bool pressA = input::pressed(Key::A);
        bool done = input::pressed(Key::Start);
        if (pressA) {
            if (keyRow_ == kKeyRows - 1) {
                const int action = keyAction(keyCol_);
                if (action == 0 && n < cap) input_[n++] = ' ';
                else if (action == 1 && n > 0) --n;
                else if (action == 2) done = true;
            } else if (n < cap) {
                input_[n++] = kKeyRowsText[keyRow_][keyCol_];
            }
            input_[n] = 0;
            redrawElement(inputOpen_);
        } else if (input::pressed(Key::B)) {
            if (n > 0) {
                input_[n - 1] = 0;
                redrawElement(inputOpen_);
            } else {
                done = true;
            }
        }
        if (done) {
            const int i = inputOpen_;
            inputOpen_ = -1;
            redraw(e.keyboard->x, e.keyboard->y, e.keyboard->w, e.keyboard->h);
            g_pageChosen = def_.elements[i].name;
            if (g_pageWaiting) setPageResult(def_.elements[i].data);
        }
    }

    // A page keeps only what a script changed.
    static constexpr int kMaxChangedTexts = 20, kMaxChangedImages = 6, kMaxValues = 20, kMaxScrollRects = 2;

    const PageDef& def_;
    int data_;
    Restore restore_;
    render::TextBox box_;
    bool ready_ = false;
    bool shownOnScreen_ = false;
    s8 focus_ = -1;
    s8 dropOpen_ = -1, inputOpen_ = -1;
    u8 dropCursor_ = 0, keyCol_ = 0, keyRow_ = 0;
    int clipX_ = 0, clipY_ = 0, clipR_ = 0, clipB_ = 0;
    u64 hidden_ = 0, on_ = 0, disabled_ = 0, warned_ = 0;
    u8 amount_[kMaxPageElements] = {};
    u8 changedText_[kMaxChangedTexts] = {};  // element index + 1, 0 = free
    s16 changedKey_[kMaxChangedTexts] = {};
    u8 changedImageEl_[kMaxChangedImages] = {};
    const ImageDef* changedImage_[kMaxChangedImages] = {};
    Values values_[kMaxValues] = {};
    s8 scrollElement_[kMaxScrollRects] = {};
    s16 scroll_[kMaxScrollRects] = {};
    u8 scrollCount_ = 0;
    char input_[kMaxInput + 1] = {};
};

// The pages' state comes from the screen arena when a scene first opens one, so a game without pages pays nothing.
// The scene load that resets the arena forgets it (forgetPageMemory); pages never outlive their scene.
PageScreen* g_pageStorage = nullptr;

PageScreen* storedPage(int slot) { return g_pageStorage + slot; }

bool isPage(const Screen* s) {
    if (!g_pageStorage) return false;
    for (int k = 0; k < kMaxOpenPages; ++k)
        if (s == storedPage(k)) return true;
    return false;
}

PageScreen* topPage() {
    if (!game::depth() || !g_pageStorage) return nullptr;
    Screen* top = game::top();
    return isPage(top) ? static_cast<PageScreen*>(top) : nullptr;
}

// Every open page again, bottom first: a panel that closed over them pointed its map entries at the blank tile.
void remapOpenPages() {
    for (int d = 0; d < game::depth(); ++d)
        if (isPage(game::at(d))) static_cast<PageScreen*>(game::at(d))->remapBox();
}

void pumpOpenPages() {
    for (int d = 0; d < game::depth(); ++d)
        if (isPage(game::at(d))) static_cast<PageScreen*>(game::at(d))->pump();
}

// A storage in use is one the screen stack holds: a scene load drops open pages without telling this file.
PageScreen* freePageStorage() {
    if (!g_pageStorage) {
        // Before any page takes its Restore mark, so closing a page keeps this.
        void* memory = screenArena().alloc(sizeof(PageScreen) * kMaxOpenPages + 8);
        if (!memory) return nullptr;
        g_pageStorage = reinterpret_cast<PageScreen*>((reinterpret_cast<u32>(memory) + 7) & ~7u);
    }
    for (int k = 0; k < kMaxOpenPages; ++k) {
        bool used = false;
        for (int d = 0; d < game::depth(); ++d)
            if (game::at(d) == storedPage(k)) used = true;
        if (!used) return storedPage(k);
    }
    return nullptr;
}
}  // namespace

void setPages(const PageSet* pages) { g_pages = pages; }
void forgetPageMemory() { g_pageStorage = nullptr; }

bool openPage(int index, int data) {
    if (!g_pages || index < 0 || index >= g_pages->count) {
        debug::warnOnce(23, "UI.Open: there is no such UI page. Check the name against the pages in the project's ui folder.");
        return false;
    }
    if (topPage()) game::finishLoading();  // a page opened this frame enters before another covers it
    PageScreen* storage = freePageStorage();
    if (!storage || game::depth() >= game::kMaxScreens) {
        debug::warnOnce(24, "UI.Open: too many UI pages are open at once, so the new one did not open. Close one first.");
        return false;
    }
    game::push(new (storage) PageScreen(g_pages->pages[index], data));
    return true;
}

bool openPage(const char* name, int data) {
    if (g_pages)
        for (int i = 0; i < g_pages->count; ++i)
            if (sameName(g_pages->pages[i].name, name)) return openPage(i, data);
    return openPage(-1, data);
}

void closePage() {
    if (!topPage()) return;
    game::finishLoading();
    game::pop();
}

bool pageOpen(const char* name) {
    for (int d = 0; d < game::depth(); ++d)
        if (isPage(game::at(d)) && (!name || sameName(static_cast<PageScreen*>(game::at(d))->def().name, name))) return true;
    return false;
}

int pageData() {
    const PageScreen* p = topPage();
    return p ? p->data() : 0;
}

int pageFocus() {
    const PageScreen* p = topPage();
    return p ? p->focus() : -1;
}

const char* pageChosen() {
    const char* chosen = g_pageChosen;
    g_pageChosen = nullptr;
    return chosen;
}

void beginPageChoice(bool wakeOnMove) {
    g_pageWaiting = true;
    g_pageWakeOnMove = wakeOnMove;
    g_pageResultReady = false;
    // A line of dialogue that was read leaves the page.
    if (text::open() && !text::waiting()) {
        text::close();
        remapOpenPages();
    }
}

bool pollPageChoice(s32& result) {
    if (!g_pageResultReady && pageOpen()) return false;
    result = g_pageResultReady ? g_pageResult : -1;
    g_pageResultReady = false;
    g_pageWaiting = false;
    return true;
}

void setPageHidden(const char* element, bool hidden) {
    if (PageScreen* p = topPage())
        if (const int i = p->find(element); i >= 0) p->setHidden(i, hidden);
}

void setPageAmount(const char* element, int amount) {
    if (PageScreen* p = topPage())
        if (const int i = p->find(element); i >= 0) p->setAmount(i, amount);
}

void setPageText(const char* element, const Label& label) {
    if (PageScreen* p = topPage())
        if (const int i = p->find(element); i >= 0) p->setText(i, label);
}

void setPageOn(const char* element, bool on) {
    if (PageScreen* p = topPage())
        if (const int i = p->find(element); i >= 0) p->setOn(i, on);
}

bool pageOn(const char* element) {
    const PageScreen* p = topPage();
    const int i = p ? p->find(element) : -1;
    return i >= 0 && p->on(i);
}

int pageAmount(const char* element) {
    const PageScreen* p = topPage();
    const int i = p ? p->find(element) : -1;
    return i >= 0 ? p->amount(i) : 0;
}

void setPageValues(const char* element, s32 a, s32 b, int count, int digits) {
    if (PageScreen* p = topPage())
        if (const int i = p->find(element); i >= 0) p->setValues(i, a, b, count, digits);
}

void setPageFocus(const char* element) {
    if (PageScreen* p = topPage())
        if (const int i = p->find(element); i >= 0) p->setFocus(i);
}

bool pageFocused(const char* element) {
    const PageScreen* p = topPage();
    const int i = p ? p->find(element) : -1;
    return i >= 0 && p->focus() == i;
}

int pageFocusData() {
    const PageScreen* p = topPage();
    return p && p->focus() >= 0 ? p->def().elements[p->focus()].data : -1;
}

void setPageInteractable(const char* element, bool on) {
    if (PageScreen* p = topPage())
        if (const int i = p->find(element); i >= 0) p->setInteractable(i, on);
}

void setPageImage(const char* element, const char* image) {
    PageScreen* p = topPage();
    const int i = p ? p->find(element) : -1;
    if (i < 0 || !g_pages) return;
    for (int k = 0; k < g_pages->imageCount; ++k)
        if (sameName(g_pages->images[k].name, image)) {
            p->setImage(i, g_pages->images[k].image);
            return;
        }
    debug::warnOnce(29, "UI.SetImage: no UI page lists this image. Add it to the element's Image Variants.");
}

int pageInputLength() {
    const PageScreen* p = topPage();
    return p ? p->inputLength() : 0;
}

int pageInputChar(int index) {
    const PageScreen* p = topPage();
    return p ? p->inputChar(index) : 0;
}

void setPageInputChar(int index, int ch) {
    if (PageScreen* p = topPage()) p->setInputChar(index, ch);
}

}  // namespace agb::ui
