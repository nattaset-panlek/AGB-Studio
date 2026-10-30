#include "agb/render/map.hpp"

#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/memory.hpp"

namespace agb::render {

namespace {
// Visible area plus one tile on each side.
constexpr int kViewTilesX = 32, kViewTilesY = 22;

int floorDiv8(int v) { return v >> 3; }
}  // namespace

bool MapLayerView::init(const data::MapLayer& layer, bg::Size size) {
    layer_ = &layer;
    size_ = size;
    widthMask_ = bg::widthTiles(size) - 1;
    heightMask_ = bg::heightTiles(size) - 1;
    if (layer.type == data::LayerType::CollisionOnly) return true;
    if (layer.tileset) placement_ = bg::loadTileset(layer.tileset);
    if (layer.tileset && !placement_.valid()) return false;
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
    const int x0 = image ? mx : mx * 2, y0 = image ? my : my * 2, n = image ? 1 : 2;
    for (int y = y0; y < y0 + n; ++y)
        for (int x = x0; x < x0 + n; ++x)
            if (x >= drawnX0_ && x <= drawnX1_ && y >= drawnY0_ && y <= drawnY1_) drawTile(x, y);
}

u16 MapLayerView::cell(int tileX, int tileY) const { return shadow_ ? shadow_[shadowIndex(tileX, tileY)] : 0; }


void MapLayerView::setCamera(int x, int y) {
    if (!layer_ || layer_->type == data::LayerType::CollisionOnly) return;
    cameraX_ = x;
    cameraY_ = y;
    scrollX_ = (x * layer_->parallaxX) >> 8;
    scrollY_ = (y * layer_->parallaxY) >> 8;
    bg::setScroll(layer_->bg, scrollX_, scrollY_);
    const int nx0 = floorDiv8(scrollX_) - 1, ny0 = floorDiv8(scrollY_) - 1;
    const int nx1 = nx0 + kViewTilesX, ny1 = ny0 + kViewTilesY;
    // A big jump (respawn, Camera.SetPosition) redraws whole rows on the fast path rather than many columns.
    const int dx = nx0 - drawnX0_, dy = ny0 - drawnY0_;
    if (drawnX1_ < drawnX0_ || dx > 16 || dx < -16 || dy > 10 || dy < -10) {
        for (int ty = ny0; ty <= ny1; ++ty) drawRow(ty, nx0, nx1);
    } else {
        // Only the columns and rows that entered the window.
        for (int tx = nx0; tx < drawnX0_; ++tx) drawColumn(tx, ny0, ny1);
        for (int tx = drawnX1_ + 1; tx <= nx1; ++tx) drawColumn(tx, ny0, ny1);
        for (int ty = ny0; ty < drawnY0_; ++ty) drawRow(ty, nx0, nx1);
        for (int ty = drawnY1_ + 1; ty <= ny1; ++ty) drawRow(ty, nx0, nx1);
    }
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
