#include "agb/api.hpp"

namespace agb::api {

namespace {
u8* g_previewCopy = nullptr;  // from the screen arena, for the one frame of the preview
u32 g_previewMark = 0;
}  // namespace

bool Save::BeginPreview(int slot) {
    if (!game::state() || !save::exists(slot) || g_previewCopy) return false;
    const u32 size = save::config().payloadSize;
    g_previewMark = screenArena().used();
    g_previewCopy = static_cast<u8*>(screenArena().alloc(size));
    if (!g_previewCopy) return false;
    auto* state = static_cast<u8*>(game::state());
    for (u32 i = 0; i < size; ++i) g_previewCopy[i] = state[i];
    if (save::read(slot, state)) return true;
    EndPreview();
    return false;
}

void Save::EndPreview() {
    if (!g_previewCopy) return;
    auto* state = static_cast<u8*>(game::state());
    for (u32 i = 0; i < save::config().payloadSize; ++i) state[i] = g_previewCopy[i];
    g_previewCopy = nullptr;
    screenArena().rewind(g_previewMark);
}

void Save::Checkpoint(int slot) {
    if (!game::state()) return;
    SaveHeader& h = header();
    h.scene = game::currentScene();
    Actor* player = nullptr;
    actors::forEach([&](Actor& a) {
        if (!player && a.kind == ActorKind::Player) player = &a;
    });
    if (player) {
        h.x = static_cast<s16>(player->pos.x.floor());
        h.y = static_cast<s16>(player->pos.y.floor());
    }
    save::write(slot, game::state());
}

}  // namespace agb::api
