// Localized text (Localization T-P14, C# Text.Show): string tables per language, built by the toolchain
// as pre-laid-out runs, and the dialogue box that shows them with the typewriter effect.
#pragma once
#include "agb/data.hpp"
#include "agb/render/text.hpp"

namespace agb::text {

struct Table {
    const data::TextRun* const* runs;  // indexed by string key id; nullptr = not translated
    u16 count;
    const char* language;              // "en", "th" ...
};

void setTables(const Table* const* languages, u8 count, u8 fallback);
// Switches the language (Options screen); labels made from a string key show it in the new language at once.
void setLanguage(u8 language);
u8 language();
u8 languageCount();
const char* languageName(u8 language);
// The run for a key in the current language, else the fallback language, else nullptr.
const data::TextRun* run(u16 key);
bool translated(u16 key, u8 language);

// ---- dialogue box (Say / Choice natives, Text.Show) ----------------------------------------------
struct BoxStyle {
    u8 bg = 0;             // background layer for the box
    u8 tileX = 1, tileY = 14, widthTiles = 28, heightTiles = 5;
    u8 paletteBank = 15;
    u8 charsPerFrame = 2;  // Dialogue Editor "Text speed · 2 ch/frame"
    u16 paper = 0x7BDE, ink = 0x0C63, ink2 = 0x294A, ink3 = 0x4E73, shadow = 0x6318, frame = 0x0C63;
    // Project Settings · Dialogue box: space inside the box, its border (0 = none), and the <c1>..<c3> colours.
    u8 padLeft = 0, padTop = 0, padRight = 0, padBottom = 0;
    u8 frameWidth = 0;
    u16 rich[3] = {0x001F, 0x7C00, 0x03E0};
};

void configureBox(const BoxStyle& style);
void setSpeed(u8 charsPerFrame);  // typing speed of the dialogue box (a Text Speed option)
// Values of {0}..{3} in strings (C# Text.SetVar): a number, the text of a string key, or typed characters.
void setVarNumber(int index, s32 value, int digits = 0);
void setVarKey(int index, u16 key);
void setVarChar(int index, int position, int ch);  // ch 0 ends the text
// Takes the dialogue box's memory now: a UI page opened over a scene gives back what was reserved after it opened.
void reserveBoxes();
// Opens the box (if needed) and starts typing `key`; returns false if the key is missing.
// `speed` overrides BoxStyle::charsPerFrame for this line (Dialogue Editor line "Text speed"), -1 = default.
bool say(u16 key, void (*blip)() = nullptr, s8 speed = -1);
bool sayRun(const data::TextRun* run, void (*blip)() = nullptr, s8 speed = -1);
void update();          // types, handles A to skip / continue; called by the scene each frame
bool waiting();         // true while text is typing or waiting for A
void close();
bool open();

// Choice list under the text: returns the chosen index once confirmed, -1 while choosing.
void choose(const u16* optionKeys, u8 count);
int choice();

render::TextBox& box();
// The shared text layer (created on first use); returns its BG index, or -1 when VRAM is full.
int layer();
u16 blankEntry();  // map entry of the text layer's blank tile (closed panels point at it)

// Text Label actors and HUD text: small text boxes on the text layer at screen pixel positions. `key` (-1 = none) is the
// string key `run` came from: the label follows language changes, and so does a HUD number drawn after it.
int addLabel(int x, int y, int widthPx, const data::TextRun* run, s16 key = -1);
void setLabelText(int label, const data::Font& font, const char* ascii);  // runtime numbers ("SCORE 0120")
// HUD value: the label's own text (localized, e.g. "SCORE" / "คะแนน"), a space, then the number padded to
// `digits` with zeros (C# Actor.SetLabelNumber).
void setLabelValue(int label, const data::TextRun* prefix, s32 value, u8 digits);
// Replaces a label's text (C# Actor.SetLabelText): speaker name tags, signs, status lines.
void setLabelRun(int label, const data::TextRun* run);
void setLabelFillRatio(int label, int current, int max);
void clearLabels();
// An overlay panel (pause menu, options screen) closed over the scene and pointed its map entries at the blank tile:
// point the labels and the open dialogue box at their tiles again.
void remapPanels();

}  // namespace agb::text
