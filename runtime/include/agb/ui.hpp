// Menu / UI kit (blueprint 14), built to the "In-game" row of Figma "AGB Studio · Screens": menu lists with
// the triangle cursor, the boot splash, the pause overlay and the options screen (volumes, language and
// button remap). A title screen is an ordinary scene with a MenuDef (SceneDef::menu).
#pragma once
#include "agb/data.hpp"
#include "agb/render/text.hpp"

namespace agb::ui {

// A label: a string key in the text tables, else ASCII drawn with the kit font's ASCII glyphs.
struct Label {
    s16 key;            // agb_text key, -1 = none
    const char* ascii;  // fallback, may be nullptr
};

enum class ItemKind : u8 { Action, Slider, Choice, Binding, Heading };
// What an Action item does when A is pressed (C# scripts can also read Menu.Chosen).
enum class Command : u8 { None, LoadScene, Continue, Options, Resume, QuitToTitle };

struct ItemDef {
    ItemKind kind;
    Label label;
    Command command;       // Action
    u16 arg;               // LoadScene: scene index · Binding: input action index · Slider: steps
    const Label* options;  // Choice
    u8 optionCount;
};

struct MenuDef {
    const ItemDef* items;
    u8 count;
    u8 tileX, tileY, widthTiles, heightTiles;  // panel on the text layer
    u8 spacing;   // pixels between rows (at least the font line height)
    Label title;  // centred above the items (key -1 and ascii nullptr = none)
    bool framed;  // paper panel with ink rules at the top and bottom (pause box)
    bool cancel;  // B leaves the menu
};

// Colours, font and labels of the built-in menus, generated from Project Settings and the template.
struct Kit {
    const data::Font* font;
    u16 ink, muted, paper, rule;  // BGR555: menu text, disabled text, pause box, its rules
    u16 screen, screenInk;        // the options screen and its text
    u16 barTrack, barFill;
    u8 dim;                // pause overlay: darkens the scene by dim/16
    s16 pauseAction;       // input action that opens the pause menu, -1 = none
    u16 titleScene;
    Label paused, resume, options, quit;
    Label optionsTitle, music, effects, language, controls, hint, pressKey;
    const Label* languageNames;  // text::languageCount() entries
    const Label* actionNames;    // one per input action (Project Settings · Input)
    u8 actionCount;
    bool remap;            // Project Settings · Input "Let players remap buttons"
};

// Boot splash (Project Settings · General · Boot splash): a full-screen image shown before the start scene.
struct Splash {
    const data::MapLayer* image;
    u16 backdrop;
    u16 frames;
};

enum class Event : u8 { None, Activate, Change, Back };

class Menu {
public:
    // Draws the menu on the text layer (palette bank 14). Values start at 0 except Slider = steps.
    bool open(const MenuDef& def, const Kit& kit);
    void close();
    bool isOpen() const { return def_ != nullptr; }
    Event update();  // D-pad moves (with key repeat), Left / Right change, A activates, B goes back
    int cursor() const { return cursor_; }
    void setCursor(int item);
    int value(int item) const { return item >= 0 && item < kMaxItems ? values_[item] : 0; }
    void setValue(int item, int value);
    void setEnabled(int item, bool enabled);
    void setWaiting(int item);  // the Binding item shows Kit::pressKey until a key is chosen (-1 = none)
    void forget();              // drops the menu without touching VRAM (the scene load already reset it)
    void remap();               // another panel covered this one: write its map entries again
    const MenuDef* def() const { return def_; }
    void redraw();

    static constexpr int kMaxItems = 16;

private:
    void drawLabel(const Label& label, int x, int y, u8 ink, bool rightAlign = false, int right = 0);
    int labelWidth(const Label& label) const;
    bool selectable(int item) const;
    bool visible(int item) const;
    int rowY(int item) const;
    void drawRow(int item, bool erase);
    void refreshRow(int item);

    const MenuDef* def_ = nullptr;
    const Kit* kit_ = nullptr;
    render::TextBox box_;
    s8 cursor_ = 0;
    u8 first_ = 0;  // first visible row when the list scrolls
    u8 values_[kMaxItems] = {};
    u16 disabled_ = 0;
    s8 waiting_ = -1;
};

// ---- game integration (called by the game framework) -----------------------------------------------
void setKit(const Kit* kit);
const Kit* kit();
// Scene menus (title screens): opened when the scene has entered, run before the scene's scripts.
void openSceneMenu(const MenuDef& def);
void closeSceneMenu();
bool sceneMenuOpen();
void updateSceneMenu();
Menu& sceneMenu();
// Pause overlay and options screen.
bool pauseRequested();  // the pause action was pressed in a scene that may pause
void openPause();
void openOptions();
bool overlayOpen();
// Name shown for a key mask in the options screen ("A", "START", "L+R").
const char* keyName(u16 keys);

}  // namespace agb::ui
