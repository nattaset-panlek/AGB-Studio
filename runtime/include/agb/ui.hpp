// Menu / UI kit (blueprint 14), built to the "In-game" row of Figma "AGB Studio · Screens": menu lists with
// the triangle cursor, the boot splash, the pause overlay and the options screen (volumes, language and
// button remap). A title screen is an ordinary scene with a MenuDef (SceneDef::menu).
#pragma once
#include "agb/data.hpp"
#include "agb/render/text.hpp"
#include "agb/text.hpp"

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
    const text::BoxStyle* box = nullptr;  // Project Settings · Dialogue box, nullptr = the default box
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

// ---- UI pages (ui/<Name>.uipage) ------------------------------------------------------------------------
// A page is made once and opened by a script or a button. The build works out every rectangle, the layout groups and
// the D-pad neighbours, so the game only draws. All of a page is drawn into one panel on the text layer, in the colours
// of the project's UI palette (palette bank 14 while a page is open). The scene waits underneath.
enum class ElementKind : u8 { Panel, Image, Text, Button, Toggle, Slider, ScrollRect, Scrollbar, Dropdown, InputField };
enum class ImageMode : u8 { Simple, Sliced, Tiled, FilledHorizontal, FilledVertical };
enum class Align : u8 { Start, Center, End };
enum class ActionKind : u8 { None, OpenPage, ClosePage, LoadScene, OpenOptions, ContinueGame, QuitToTitle, MusicVolume, EffectsVolume, ChangeLanguage, RemapKey };

struct ImageDef {
    const u8* pixels;  // one palette index a pixel, 0 = see-through
    u16 width, height;
};

struct KeyboardDef {
    s16 x, y, w, h;
    u8 fill, border, ink, focusFill, focusInk;
};

struct ElementDef {
    const char* name;
    ElementKind kind;
    s8 parent;                       // index in the page, -1 = root; parents come before their children
    s16 x, y, w, h;                  // screen pixels
    u8 fill, border, borderWidth;    // palette indices, 0 = none
    u8 accent, focusFill, focusBorder, ink, focusInk;
    Align alignX, alignY;
    Label label;
    const ImageDef* image;
    ImageMode imageMode;
    u8 sliceL, sliceT, sliceR, sliceB;
    u8 amount;                       // 0..100
    bool on, hidden, cursor;
    s8 group;                        // Toggle Group number, -1 = none
    s8 up, down, left, right;        // D-pad neighbours, -1 = none
    ActionKind action;
    s16 target;                      // page or scene index
    s16 data;                        // what UI.Choose returns; a Scroll Rect keeps the space between its rows here
    u8 padL, padT, padR, padB;       // space between the rectangle and the text
    u8 disabledInk;                  // text colour when disabled, 0 = the normal one
    bool interactable;               // false: skipped by the cursor (with all its children, like a Canvas Group)
    s8 scrollTarget;                 // Scrollbar: its Scroll Rect
    const Label* options;            // Dropdown
    u8 optionCount;
    u8 maxLength;                    // Input Field
    const KeyboardDef* keyboard;     // Input Field
};

struct PageDef {
    const char* name;
    const ElementDef* elements;
    u8 count;
    u8 tileX, tileY, widthTiles, heightTiles;  // the panel holding the whole page
    s8 firstFocus;                             // -1 = nothing to select
    bool cancel;                               // B closes
    bool scenePlays;                           // the scene keeps running under the page (a HUD); else it waits
    u8 dim;                                    // darkens the scene under the page by dim/16
};

struct NamedImage {
    const char* name;
    const ImageDef* image;
};

struct PageSet {
    const PageDef* pages;
    u16 count;
    const u16* palette;  // 16 colours
    const NamedImage* images = nullptr;  // what UI.SetImage can show
    u16 imageCount = 0;
    const u8* richInks = nullptr;        // palette indices of <c1>..<c3>
};

constexpr int kMaxOpenPages = 3;     // one screen of the stack stays for the scene
constexpr s32 kPageMoved = -2;       // UI.Choose(true): the cursor or a value moved
constexpr int kMaxPageElements = 64;

void setPages(const PageSet* pages);
void forgetPageMemory();                        // the scene's arena was reset
bool openPage(const char* name, int data = 0);  // false (with a warning) when there is no such page or no room
bool openPage(int index, int data = 0);
void closePage();                               // the top page
bool pageOpen(const char* name = nullptr);      // nullptr = any page
int pageData();                                 // what the top page was opened with
int pageFocus();                                // element index under the cursor on the top page, -1 = none
const char* pageChosen();                       // the last element activated with A, nullptr = none; reading clears it
void setPageHidden(const char* element, bool hidden);
void setPageAmount(const char* element, int amount);
void setPageText(const char* element, const Label& label);
void setPageOn(const char* element, bool on);
bool pageOn(const char* element);
int pageAmount(const char* element);
void setPageValues(const char* element, s32 a, s32 b, int count, int digits);
void setPageFocus(const char* element);
bool pageFocused(const char* element);
int pageFocusData();                            // Data of the control under the cursor, -1 = none
void setPageInteractable(const char* element, bool on);
void setPageImage(const char* element, const char* image);
int pageInputLength();
int pageInputChar(int index);
void setPageInputChar(int index, int ch);       // ch 0 ends the text
// A cutscene waiting for the player (C# await UI.Choose): the Data of the control chosen, -1 for B, kPageMoved when asked.
void beginPageChoice(bool wakeOnMove, bool keepDialogue = false);
// A page named "Choice" shows Choice in scripts and the Dialogue Editor: false when the game has none.
bool openChoicePage(const u16* keys, u8 count);
// The page the Options page took the place of comes back when it has closed: true when that happened this frame.
bool reopenAfterOptions();
bool pollPageChoice(s32& result);

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
// Saved options (save::Options on the save chip): loadOptions applies them at game::boot (volumes, language, button
// remap); the Options screen writes them when it closes after a change.
void loadOptions();
bool overlayOpen();
// Name shown for a key mask in the options screen ("A", "START", "L+R").
const char* keyName(u16 keys);

}  // namespace agb::ui
