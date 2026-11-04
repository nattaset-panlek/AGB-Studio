#include "agb/render/map.hpp"

#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/memory.hpp"
#include "agb/render/affine.hpp"
#include "agb/render/effects.hpp"
#include "agb/system.hpp"

namespace agb::render {

namespace {
// Visible area plus one tile on each side.
constexpr int kViewTilesX = 32, kViewTilesY = 22;

int floorDiv8(int v) { return v >> 3; }

// Whole-window redraws (a respawn, a teleport or Camera.SetPosition across the map) allowed per frame. A layer over the
// limit keeps showing its old position and redraws on a later frame, so a jump of every layer at once never makes the
// frame overrun (each redraw costs about a tenth of a frame).
constexpr int kRedrawsPerFrame = 2;
u32 g_redrawFrame = 0xFFFFFFFFu;
int g_redraws = 0;

bool takeRedraw() {
    if (system::frame() != g_redrawFrame) {
        g_redrawFrame = system::frame();
        g_redraws = 0;
    }
    if (g_redraws >= kRedrawsPerFrame) return false;
    ++g_redraws;
    return true;
}
}  // namespace

bool MapLayerView::init(const data::MapLayer& layer, bg::Size size) {
    layer_ = &layer;
    size_ = size;
    widthMask_ = bg::widthTiles(size) - 1;
    heightMask_ = bg::heightTiles(size) - 1;
    if (layer.type == data::LayerType::CollisionOnly) return true;
    // Rotate / scale layers (Mode 1-2, the Mode 7 floor): the whole map is loaded once, the camera moves the matrix.
    if (layer.type == data::LayerType::Affine) return affine::load(layer.bg, layer);
    originX_ = originY_ = 0;
    neighbors_ = nullptr;
    neighborCount_ = 0;
    if (layer.tileset) placement_ = bg::loadTileset(layer.tileset);
    if (layer.tileset && !placement_.valid()) return false;
    // The build's entries are relative to the tileset: valid ones fit their fields after the layer's offsets.
    const data::Palette* pal = layer.tileset ? layer.tileset->palette : nullptr;
    placeByAdd_ = layer.tileset && placement_.firstTile + layer.tileset->gfx.tileCount <= 1024 && placement_.paletteBank + (pal ? pal->banks : 1) <= 16;
    screenBlock_ = bg::allocScreenBlocks(size);
    if (screenBlock_ < 0) return false;
    const int cells = (widthMask_ + 1) * (heightMask_ + 1);
    shadow_ = static_cast<u16*>(screenArena().alloc(static_cast<u32>(cells) * 2));
    if (!shadow_) return false;
    for (int i = 0; i < cells; ++i) shadow_[i] = 0;
    bg::Config c;
    c.charBlock = static_cast<u8>(placement_.charBlock < 0 ? 0 : placement_.charBlock);
    c.screenBlock = static_cast<u8>(screenBlock_);
    c.priority = layer.priority;
    c.size = size;
    bg::configure(layer.bg, c);
    bg::setVisible(layer.bg, true);
    // Nothing drawn yet: an empty window (x1 < x0) makes the first setCamera draw everything on the fast
    // path, also when this view is reused for the next scene.
    drawnX0_ = drawnY0_ = 0;
    drawnX1_ = drawnY1_ = -1;
    return true;
}

void MapLayerView::release() {
    if (layer_ && layer_->type != data::LayerType::CollisionOnly) bg::setVisible(layer_->bg, false);
    layer_ = nullptr;
    shadow_ = nullptr;
}

void MapLayerView::redrawMetatile(int mx, int my) {
    if (!layer_ || !shadow_) return;
    const bool image = layer_->type == data::LayerType::Image;
    const int x0 = image ? mx : mx * 2 + (originX_ >> 3), y0 = image ? my : my * 2 + (originY_ >> 3), n = image ? 1 : 2;
    for (int y = y0; y < y0 + n; ++y)
        for (int x = x0; x < x0 + n; ++x)
            if (x >= drawnX0_ && x <= drawnX1_ && y >= drawnY0_ && y <= drawnY1_) drawTile(x, y);
}

// A tile past the map's edge (map tile coordinates, 8 px): from the scene joined there, blank where none is. Thumb code in
// ROM: at most a row or column a frame comes here, and only in connected scenes.
u16 MapLayerView::neighborEntry(int tileX, int tileY) const {
    for (int i = 0; i < neighborCount_; ++i) {
        const MapNeighbor& n = neighbors_[i];
        const data::MapLayer& l = *n.layer;
        const int x = tileX - n.tileX * 2, y = tileY - n.tileY * 2;
        if (x < 0 || y < 0 || x >= l.width * 2 || y >= l.height * 2) continue;
        const u16 mt = l.cells[(y >> 1) * l.width + (x >> 1)];
        if (!l.tileset || mt >= l.tileset->metatileCount) return 0;
        const u16 se = l.tileset->metatiles[mt].se[(x & 1) + (y & 1) * 2];
        const u16 tile = static_cast<u16>((data::seTile(se) + placement_.firstTile) & 0x3FF);
        const u16 pal = static_cast<u16>((data::sePal(se) + placement_.paletteBank) & 15);
        return static_cast<u16>(tile | (se & (data::kSeHFlip | data::kSeVFlip)) | (pal << 12));
    }
    return 0;
}

u16 MapLayerView::cell(int tileX, int tileY) const { return shadow_ ? shadow_[shadowIndex(tileX, tileY)] : 0; }


void MapLayerView::setCamera(int x, int y) {
    if (!layer_ || layer_->type == data::LayerType::CollisionOnly) return;
    cameraX_ = x;
    cameraY_ = y;
    const int sx = ((x + originX_) * layer_->parallaxX) >> 8, sy = ((y + originY_) * layer_->parallaxY) >> 8;
    if (layer_->type == data::LayerType::Affine) {
        scrollX_ = sx;
        scrollY_ = sy;
        // While Scene.Mode7 runs, the per-line table sets the matrix instead.
        if (!effects::scanlineActive()) affine::setTransform(layer_->bg, fixed(1), 0, fixed(scrollX_), fixed(scrollY_), 0, 0);
        return;
    }
    const int nx0 = floorDiv8(sx) - 1, ny0 = floorDiv8(sy) - 1;
    const int nx1 = nx0 + kViewTilesX, ny1 = ny0 + kViewTilesY;
    // A move of more than a few tiles (respawn, teleport, Camera.SetPosition) redraws the window row by row on the fast
    // path rather than column by column; past the frame's redraw limit the layer stays where it was until a later frame.
    const int dx = nx0 - drawnX0_, dy = ny0 - drawnY0_;
    const bool first = drawnX1_ < drawnX0_;
    if (first || dx > 4 || dx < -4 || dy > 4 || dy < -4) {
        if (!first && !takeRedraw()) return;
        for (int ty = ny0; ty <= ny1; ++ty) drawRow(ty, nx0, nx1);
    } else {
        // Only the columns and rows that entered the window.
        for (int tx = nx0; tx < drawnX0_; ++tx) drawColumn(tx, ny0, ny1);
        for (int tx = drawnX1_ + 1; tx <= nx1; ++tx) drawColumn(tx, ny0, ny1);
        for (int ty = ny0; ty < drawnY0_; ++ty) drawRow(ty, nx0, nx1);
        for (int ty = drawnY1_ + 1; ty <= ny1; ++ty) drawRow(ty, nx0, nx1);
    }
    scrollX_ = sx;
    scrollY_ = sy;
    bg::setScroll(layer_->bg, sx, sy);
    drawnX0_ = nx0;
    drawnX1_ = nx1;
    drawnY0_ = ny0;
    drawnY1_ = ny1;
}

void MapLayerView::drawAll() {
    if (!layer_ || !shadow_) return;
    // setCamera() has already drawn the window when it was called since init; only redraw if not.
    if (drawnX1_ < drawnX0_) setCamera(cameraX_, cameraY_);
    const int blocks = bg::screenBlockCount(size_);
    commit::copyNow(commit::bgMap(screenBlock_), shadow_, static_cast<u32>(blocks) * 2048);
    for (auto& w : dirtyRows_) w = 0;
}

bool MapLayerView::commit() {
    if (!shadow_) return true;
    bool all = true;
    const int blocks = bg::screenBlockCount(size_);
    for (int b = 0; b < blocks; ++b) {
        u32& rows = dirtyRows_[b];
        int r = 0;
        while (rows && r < 32) {
            if (!(rows & (1u << r))) {
                ++r;
                continue;
            }
            int end = r;
            while (end + 1 < 32 && (rows & (1u << (end + 1)))) ++end;
            const u32 bytes = static_cast<u32>(end - r + 1) * 64;
            u16* dst = commit::bgMap(screenBlock_ + b) + r * 32;
            const u16* src = shadow_ + b * 1024 + r * 32;
            if (commit::queueCopy(dst, src, bytes)) {
                for (int k = r; k <= end; ++k) rows &= ~(1u << k);
            } else {
                all = false;
            }
            r = end + 1;
        }
    }
    return all;
}

}  // namespace agb::render
