// Draws a map layer (metatiles) into a background layer and streams new rows / columns as the camera
// moves (ring buffer, blueprint 08). A shadow of the BG map lives in EWRAM; changed rows are copied in
// VBlank through the commit queue, so scrolling never tears.
#pragma once
#include "agb/data.hpp"
#include "agb/render/bg.hpp"

namespace agb::render {

// A layer of a scene joined to this one, drawn past this map's edge. It uses the same tileset.
struct MapNeighbor {
    const data::MapLayer* layer;
    s16 tileX, tileY;  // its top-left corner in this map's 16 px tiles
};

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
    // Runtime map edits: switch to a RAM copy of the layer (same layout) and redraw one 16 px cell.
    void retarget(const data::MapLayer* layer) { layer_ = layer; }
    void redrawMetatile(int mx, int my);
    // Connected scenes: what to draw past the map's edges. The list must stay valid while the view uses it.
    void setNeighbors(const MapNeighbor* list, u8 count) { neighbors_ = list; neighborCount_ = count; }
    // Connected scenes: map space moves by (dx, dy) pixels, a multiple of 16, with the camera: what is on the screen and
    // in the hardware map stays where it is, so walking into the next scene draws nothing.
    void moveOrigin(int dx, int dy) { originX_ += dx; originY_ += dy; cameraX_ -= dx; cameraY_ -= dy; }
    int bgIndex() const { return layer_ ? layer_->bg : -1; }
    int screenBlock() const { return screenBlock_; }
    const bg::TilesetPlacement& placement() const { return placement_; }
    u16 cell(int tileX, int tileY) const;  // shadow screen entry at hardware tile coordinates (tests)
    // The scroll the layer shows (map pixels). After a big camera move it can stay at the old position for a frame or
    // two: setCamera() redraws at most two whole windows per frame, the others wait (never a half-drawn window).
    int scrollX() const { return scrollX_; }
    int scrollY() const { return scrollY_; }

private:
    u16 screenEntry(int tileX, int tileY) const;
    u16 neighborEntry(int tileX, int tileY) const;
    void drawTile(int tileX, int tileY);
    void drawColumn(int tileX, int tileY0, int tileY1);
    void drawRow(int tileY, int tileX0, int tileX1);
    int shadowIndex(int tileX, int tileY) const;

    const data::MapLayer* layer_ = nullptr;
    bg::TilesetPlacement placement_;
    bg::Size size_ = bg::Size::S64x32;
    int screenBlock_ = -1;
    int widthMask_ = 63, heightMask_ = 31;
    bool placeByAdd_ = false;  // map entries + first tile / palette bank cannot carry out of their fields (drawRow)
    u16* shadow_ = nullptr;  // in the screen arena
    u32 dirtyRows_[4] = {};  // per screen block: bit per row that changed
    int scrollX_ = 0, scrollY_ = 0, cameraX_ = 0, cameraY_ = 0;
    int drawnX0_ = 0, drawnX1_ = -1, drawnY0_ = 0, drawnY1_ = -1;  // drawn tile window (inclusive)
    int originX_ = 0, originY_ = 0;  // this map's corner in the space of the hardware map, in pixels (moveOrigin)
    const MapNeighbor* neighbors_ = nullptr;
    u8 neighborCount_ = 0;
};

}  // namespace agb::render
