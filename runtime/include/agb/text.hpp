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
};

void configureBox(const BoxStyle& style);
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

// Text Label actors and HUD text: small text boxes on the text layer at screen pixel positions.
int addLabel(int x, int y, int widthPx, const data::TextRun* run);
void setLabelText(int label, const data::Font& font, const char* ascii);  // runtime numbers ("SCORE 0120")
void clearLabels();

}  // namespace agb::text
