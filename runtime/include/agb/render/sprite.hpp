// Sprites: instances of sprite sheets placed on screen, animated, sorted by priority and written to the
// OAM shadow once per frame. Frame graphics are streamed into OBJ VRAM through a shared cache keyed by
// (sheet, frame), so slimes on the same animation frame share one copy (Sprite Editor "VRAM/frame").
#pragma once
#include "agb/data.hpp"
#include "agb/fixed.hpp"

namespace agb::render::sprites {

using Id = s16;
constexpr Id kNone = -1;
constexpr int kMaxSprites = 128;
constexpr int kMaxAffine = 32;

void reset();
Id create(const data::SpriteSheet* sheet, u8 priority = 2);
void destroy(Id id);
bool valid(Id id);

void setPosition(Id id, int x, int y);  // screen position of the sheet's origin
void setFrame(Id id, u16 frame);
void play(Id id, int anim, bool restart = false);
int playByName(Id id, const char* name);
void setSpeed(Id id, fixed speed);       // 1 = authored speed
void setFlip(Id id, bool h, bool v);
void setVisible(Id id, bool visible);
void setPriority(Id id, u8 priority);    // 0 = front
void setOrder(Id id, s16 order);         // tie break inside a priority (higher draws in front)
void setMosaic(Id id, bool on);
void setMosaicAll(bool on);  // every sprite takes part in the mosaic (the scene's "Mosaic effect"); reset() clears it
void setBlend(Id id, bool on);
// Rotation (0..255 = full turn) and scale; uses one of 32 affine matrices. doubleSize avoids clipping.
void setAffine(Id id, u8 angle, fixed scaleX, fixed scaleY, bool doubleSize = false);
void clearAffine(Id id);

u16 frame(Id id);
int anim(Id id);
bool animDone(Id id);

// Advances animations (call once per frame, before build()).
void update();
// Writes the OAM shadow (sorted by priority then order) and queues frame copies. Returns sprites drawn.
int build();

int count();
int vramTilesUsed();
int affineUsed();
u32 frameCopiesThisFrame();

}  // namespace agb::render::sprites
