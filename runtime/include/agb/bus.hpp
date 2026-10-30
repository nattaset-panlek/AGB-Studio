// Command bus (blueprint 09): game rules never draw. They post commands (ShowDamage, PlayAnim, Say ...)
// that the presentation side plays and completes; rules wait only at declared sync points. Choices come
// from a source per side: the player (through the UI), an AI function, or a recorded replay.
#pragma once
#include "agb/types.hpp"

namespace agb::bus {

struct Command {
    u16 type;
    s16 a, b, c;
    s32 value;
};

enum class Source : u8 { Player, AI, Replay };
using AiChooser = int (*)(int side, const s16* options, int count);

constexpr int kQueueSize = 32;
constexpr int kSides = 4;

void reset();
bool post(const Command& cmd);      // false when the queue is full
bool idle();                        // sync point: true when every posted command was completed
int pendingCount();
const Command* front();             // presentation: next command to play (nullptr = none)
void complete();                    // presentation: front command finished
void completeAll();                 // headless runs: no presentation

void setSource(int side, Source source, AiChooser ai = nullptr);
Source source(int side);
// Asks `side` to pick one of `options`. AI and replay answer at once; the player answers later through
// submitChoice(). Returns the chosen option index or -1 while waiting.
int choose(int side, const s16* options, int count);
void submitChoice(int side, int optionIndex);

// Replay: every resolved choice is recorded; playing the record back gives the same game.
const u8* record(int* length);
void setReplay(const u8* data, int length);

}  // namespace agb::bus
