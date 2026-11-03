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
    void give() const {
        render::palettes::setBank(false, kTextBank, textColors);
        commit::setReg(commit::BldCnt, bldCnt);
        commit::setReg(commit::BldY, bldY);
        commit::setReg(commit::BldAlpha, bldAlpha);
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
    return g_kit && g_kit->pauseAction >= 0 && !g_sceneMenu.isOpen() && !text::open() && !game::loading() && !script::cutsceneRunning() &&
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

bool sameName(const char* a, const char* b) {
    while (*a && *a == *b) {
        ++a;
        ++b;
    }
    return *a == *b;
}

class PageScreen final : public Screen {
public:
    PageScreen(const PageDef& def, int data) : def_(def), data_(data) {
        for (int i = 0; i < def.count && i < kMaxPageElements; ++i) {
            const ElementDef& e = def.elements[i];
            amount_[i] = e.amount;
            if (e.hidden) hidden_ |= 1ull << i;
            if (e.on) on_ |= 1ull << i;
        }
        focus_ = def.firstFocus;
    }

    const char* name() const override { return def_.name; }
    bool overlay() const override { return true; }

    void enter() override {
        const int bg = text::layer();
        restore_.take();
        render::palettes::setBank(false, kBank, g_pages->palette);
        debug::logf("UI page %s opened", def_.name);
        if (bg < 0 || !box_.init(bg, def_.tileX, def_.tileY, def_.widthTiles, def_.heightTiles, kBank, render::TextBox::Style{kInk, 0, 0})) {
            debug::warnOnce(22, "A UI page did not open: there is no background memory left for its panel. Make the page smaller or use fewer map tiles in this scene.");
            return;
        }
        ready_ = true;
        redraw(0, 0, 240, 160);
    }

    void update() override {
        if (!ready_) {
            game::pop();
            return;
        }
        box_.update();
        render::palettes::update();
        const int old = focus_;
        if (input::repeated(Key::Up)) moveFocus(0);
        else if (input::repeated(Key::Down)) moveFocus(1);
        if (focus_ == old && focus_ >= 0 && def_.elements[focus_].kind == ElementKind::Slider) {
            const int change = (input::repeated(Key::Right) ? 5 : 0) - (input::repeated(Key::Left) ? 5 : 0);
            if (change) {
                setAmount(focus_, amount_[focus_] + change);
                return;
            }
        }
        if (focus_ == old) {
            if (input::repeated(Key::Left)) moveFocus(2);
            else if (input::repeated(Key::Right)) moveFocus(3);
        }
        if (focus_ != old) {
            if (old >= 0) redrawElement(old);
            if (focus_ >= 0) redrawElement(focus_);
            return;
        }
        if (input::pressed(Key::A) && focus_ >= 0) {
            activate(focus_);
            return;
        }
        if (input::pressed(Key::B) && def_.cancel) game::pop();
    }

    void exit() override {
        debug::logf("UI page %s closed", def_.name);
        if (ready_) box_.unmap(text::blankEntry());
        box_.release();
        restore_.give();
        if (g_sceneMenu.isOpen() && g_kit) loadPalette(*g_kit);
    }

    void resume() override { box_.remap(); }

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
        if (hidden && focus_ == i) moveAway();
        if (ready_) redrawElement(i);
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
            debug::warnOnce(25, "UI.SetText: a page can change the text of 16 elements while it is open, so this one kept its text.");
            return;
        }
        changedText_[slot] = static_cast<u8>(i + 1);
        changedLabel_[slot] = label;
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

private:
    void moveFocus(int way) {
        if (focus_ < 0) return;
        const ElementDef& e = def_.elements[focus_];
        const s8 next = way == 0 ? e.up : way == 1 ? e.down : way == 2 ? e.left : e.right;
        if (next >= 0 && shown(next)) focus_ = next;
    }

    // A hidden control gives the cursor to its nearest neighbour; with none showing, nothing is selected.
    void moveAway() {
        const ElementDef& e = def_.elements[focus_];
        const s8 ways[4] = {e.down, e.up, e.right, e.left};
        focus_ = -1;
        for (const s8 n : ways)
            if (n >= 0 && shown(n)) {
                focus_ = n;
                if (ready_) redrawElement(n);
                return;
            }
    }

    void activate(int i) {
        const ElementDef& e = def_.elements[i];
        if (e.kind == ElementKind::Toggle) setOn(i, e.group >= 0 ? true : !on(i));
        g_pageChosen = e.name;
        switch (e.action) {
            case ActionKind::OpenPage:
                openPage(static_cast<int>(e.target), e.data);
                break;
            case ActionKind::ClosePage:
                game::pop();
                break;
            case ActionKind::LoadScene: {
                const u16 scene = static_cast<u16>(e.target);
                while (overlayOpen()) game::pop();
                game::loadScene(scene);
                break;
            }
            default:
                break;
        }
    }

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
        clipX_ = x;
        clipY_ = y;
        clipR_ = x + w;
        clipB_ = y + h;
        box_.fillRect(x, y, w, h, 0);
        for (int i = 0; i < def_.count; ++i) {
            const ElementDef& e = def_.elements[i];
            if (e.x - ox >= clipR_ || e.y - oy >= clipB_ || e.x - ox + e.w <= clipX_ || e.y - oy + e.h <= clipY_) continue;
            if (shown(i)) draw(i, e.x - ox, e.y - oy);
        }
        box_.update();
    }

    void redrawElement(int i) {
        const ElementDef& e = def_.elements[i];
        redraw(e.x, e.y, e.w, e.h);
    }

    void fill(int x, int y, int w, int h, u8 index) {
        if (!index) return;
        if (x < clipX_) { w -= clipX_ - x; x = clipX_; }
        if (y < clipY_) { h -= clipY_ - y; y = clipY_; }
        if (w > clipR_ - x) w = clipR_ - x;
        if (h > clipB_ - y) h = clipB_ - y;
        if (w > 0 && h > 0) box_.fillRect(x, y, w, h, index);
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

    void drawImage(const ElementDef& e, int amount, int x, int y) {
        const ImageDef& im = *e.image;
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
            if (const data::TextRun* r = text::run(static_cast<u16>(l.key))) {
                int w = 0;
                for (u16 i = 0; i < r->count; ++i) {
                    const int e = r->glyphs[i].x + r->font->glyphs[r->glyphs[i].glyph].advance;
                    if (e > w) w = e;
                }
                return w;
            }
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

    void drawLabel(const ElementDef& e, const Label& l, u8 ink, int x, int y, int w) {
        if (!ink || (l.key < 0 && !l.ascii) || !g_kit || !g_kit->font) return;
        const int lw = labelWidth(l), lh = g_kit->font->lineHeight;
        const int tx = x + (e.alignX == Align::Center ? (w - lw) / 2 : e.alignX == Align::End ? w - lw : 0);
        const int ty = y + (e.alignY == Align::Center ? (e.h - lh) / 2 : e.alignY == Align::End ? e.h - lh : 0);
        box_.setInk(ink);
        const data::TextRun* r = l.key >= 0 ? text::run(static_cast<u16>(l.key)) : nullptr;
        if (r) box_.drawRun(*r, tx, ty);
        else if (l.ascii) box_.drawAscii(*g_kit->font, l.ascii, tx, ty);
        box_.setInk(kInk);
    }

    const Label& labelOf(int i) const {
        for (int slot = 0; slot < kMaxChangedTexts && changedText_[slot]; ++slot)
            if (changedText_[slot] == i + 1) return changedLabel_[slot];
        return def_.elements[i].label;
    }

    void draw(int i, int x, int y) {
        const ElementDef& e = def_.elements[i];
        const bool focused = i == focus_;
        const u8 fillColor = focused && e.focusFill ? e.focusFill : e.fill;
        const u8 border = focused && e.focusBorder ? e.focusBorder : e.border;
        const u8 ink = focused && e.focusInk ? e.focusInk : e.ink;
        fill(x, y, e.w, e.h, fillColor);
        if (e.image) drawImage(e, amount_[i], x, y);
        if (e.kind == ElementKind::Slider) {
            const int inset = e.border ? e.borderWidth : 0;
            fill(x + inset, y + inset, (e.w - 2 * inset) * amount_[i] / 100, e.h - 2 * inset, e.accent);
        }
        if (border && e.borderWidth) {
            const int b = e.borderWidth;
            fill(x, y, e.w, b, border);
            fill(x, y + e.h - b, e.w, b, border);
            fill(x, y + b, b, e.h - 2 * b, border);
            fill(x + e.w - b, y + b, b, e.h - 2 * b, border);
        }
        int textX = x, textW = e.w;
        if (e.kind == ElementKind::Toggle) {
            const int size = e.h < 10 ? e.h : 10, top = y + (e.h - size) / 2;
            const u8 frame = ink ? ink : e.accent;
            fill(x, top, size, 1, frame);
            fill(x, top + size - 1, size, 1, frame);
            fill(x, top + 1, 1, size - 2, frame);
            fill(x + size - 1, top + 1, 1, size - 2, frame);
            if (on(i)) fill(x + 2, top + 2, size - 4, size - 4, e.accent ? e.accent : ink);
            textX += size + 4;
            textW -= size + 4;
        } else if (e.kind == ElementKind::Button && e.cursor) {
            if (focused) {
                const u8 c = e.accent ? e.accent : ink;
                const int top = y + e.h / 2 - 3;
                for (int k = 0; k < 3; ++k) fill(x + 1 + k, top + k, 1, 7 - 2 * k, c);
            }
            textX += 8;
            textW -= 8;
        }
        drawLabel(e, labelOf(i), ink, textX, y, textW);
    }

    // Three pages' worth of state stays in the Text row of the RAM budget, so only the texts a script changed are kept.
    static constexpr int kMaxChangedTexts = 16;

    const PageDef& def_;
    int data_;
    Restore restore_;
    render::TextBox box_;
    bool ready_ = false;
    s8 focus_ = -1;
    int clipX_ = 0, clipY_ = 0, clipR_ = 0, clipB_ = 0;
    u64 hidden_ = 0, on_ = 0;
    u8 amount_[kMaxPageElements] = {};
    u8 changedText_[kMaxChangedTexts] = {};  // element index + 1, 0 = free
    Label changedLabel_[kMaxChangedTexts] = {};
};

alignas(8) AGB_EWRAM_BSS u8 g_pageStorage[kMaxOpenPages][sizeof(PageScreen)];

Screen* storedPage(u8* storage) { return reinterpret_cast<PageScreen*>(storage); }

bool isPage(const Screen* s) {
    for (auto& storage : g_pageStorage)
        if (s == storedPage(storage)) return true;
    return false;
}

PageScreen* topPage() {
    if (!game::depth()) return nullptr;
    Screen* top = game::top();
    for (auto& storage : g_pageStorage)
        if (top == storedPage(storage)) return static_cast<PageScreen*>(top);
    return nullptr;
}

// A storage in use is one the screen stack holds: a scene load drops open pages without telling this file.
u8* freePageStorage() {
    for (auto& storage : g_pageStorage) {
        bool used = false;
        for (int d = 0; d < game::depth(); ++d)
            if (game::at(d) == storedPage(storage)) used = true;
        if (!used) return storage;
    }
    return nullptr;
}
}  // namespace

void setPages(const PageSet* pages) { g_pages = pages; }

bool openPage(int index, int data) {
    if (!g_pages || index < 0 || index >= g_pages->count) {
        debug::warnOnce(23, "UI.Open: there is no such UI page. Check the name against the pages in the project's ui folder.");
        return false;
    }
    u8* storage = freePageStorage();
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
    if (topPage()) game::pop();
}

bool pageOpen(const char* name) {
    for (int d = 0; d < game::depth(); ++d)
        for (auto& storage : g_pageStorage)
            if (game::at(d) == storedPage(storage) && (!name || sameName(reinterpret_cast<PageScreen*>(storage)->def().name, name))) return true;
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

}  // namespace agb::ui
