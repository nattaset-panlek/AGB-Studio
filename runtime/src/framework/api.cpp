#include "agb/api.hpp"

namespace agb::api {

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
