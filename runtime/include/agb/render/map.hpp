// Draws a map layer (metatiles) into a background layer and streams new rows / columns as the camera
// moves (ring buffer, blueprint 08). A shadow of the BG map lives in EWRAM; changed rows are copied in
// VBlank through the commit queue, so scrolling never tears.
#pragma once
#include "agb/data.hpp"
#include "agb/render/bg.hpp"

namespace agb::render {

class MapLayerView {
public:
    // Loads the tileset, picks screen blocks and configures `layer.bg`. Call while the screen is blank.
    bool init(const data::MapLayer& layer, bg::Size size = bg::Size::S64x32);
    void release();
    // Camera position in pixels (top-left of the screen) in map space; parallax is applied here.
    void setCamera(int x, int y);
    // Redraws the visible area immediately (staged loads).
    void drawAll();
    // Copies changed rows to VRAM through the commit queue; returns false if the budget deferred some.
    bool commit();

    const data::MapLayer* layer() const { return layer_; }
    int bgIndex() const { return layer_ ? layer_->bg : -1; }
    int screenBlock() const { return screenBlock_; }
    const bg::TilesetPlacement& placement() const { return placement_; }
    u16 cell(int tileX, int tileY) const;  // shadow screen entry at hardware tile coordinates (tests)
    int scrollX() const { return scrollX_; }
    int scrollY() const { return scrollY_; }

private:
    u16 screenEntry(int tileX, int tileY) const;
    void drawTile(int tileX, int tileY);
    void drawColumn(int tileX, int tileY0, int tileY1);
    void drawRow(int tileY, int tileX0, int tileX1);
    int shadowIndex(int tileX, int tileY) const;

    const data::MapLayer* layer_ = nullptr;
    bg::TilesetPlacement placement_;
    bg::Size size_ = bg::Size::S64x32;
    int screenBlock_ = -1;
    int widthMask_ = 63, heightMask_ = 31;
    u16* shadow_ = nullptr;  // in the screen arena
    u32 dirtyRows_[4] = {};  // per screen block: bit per row that changed
    int scrollX_ = 0, scrollY_ = 0, cameraX_ = 0, cameraY_ = 0;
    int drawnX0_ = 0, drawnX1_ = -1, drawnY0_ = 0, drawnY1_ = -1;  // drawn tile window (inclusive)
};

}  // namespace agb::render
