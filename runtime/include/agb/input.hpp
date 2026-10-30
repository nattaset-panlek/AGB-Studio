// Keypad input read once per frame (C# API: Input.Pressed / Held / Released / AxisX / AxisY / AnyKey).
#pragma once
#include "agb/fixed.hpp"
#include "agb/types.hpp"

namespace agb {

// Bit values match the KEYINPUT register.
enum class Key : u16 {
    A = 1 << 0, B = 1 << 1, Select = 1 << 2, Start = 1 << 3,
    Right = 1 << 4, Left = 1 << 5, Up = 1 << 6, Down = 1 << 7,
    R = 1 << 8, L = 1 << 9,
};

namespace input {

constexpr u16 kAllKeys = 0x03FF;

void update();               // reads the keypad; called once per frame
void injectState(u16 keys);  // tests and replays: use this state on the next update() instead of the keypad
bool held(Key k);
bool pressed(Key k);         // true on the frame the key goes down
bool released(Key k);        // true on the frame the key goes up
bool repeated(Key k);        // pressed, then again every repeatInterval frames after repeatDelay
bool anyKey();               // any of the 10 keys pressed this frame
int axisX();                 // -1, 0 or 1 from the D-pad
int axisY();
u16 heldMask();
void setRepeat(u8 delayFrames, u8 intervalFrames);

// Action names from Project Settings · Input (generated table: action index -> key mask).
void setActionMap(const u16* actionKeys, int count);
bool actionHeld(int action);
bool actionPressed(int action);
// Runtime remap (Project Settings · Input "Let players remap buttons").
void remapAction(int action, u16 keys);

}  // namespace input
}  // namespace agb
