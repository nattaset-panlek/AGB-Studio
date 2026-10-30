// Fixed pool of tasks run once per frame, in priority order. A task is a function plus a small
// state block; long jobs step through `state` one chunk per frame (coroutine style, no heap).
#pragma once
#include "agb/types.hpp"

namespace agb::task {

constexpr int kMaxTasks = 16;

struct Task {
    void (*func)(Task&) = nullptr;
    u16 state = 0;
    u16 wait = 0;     // frames left before func runs again
    u8 priority = 0;  // lower runs first
    bool active = false;
    u8 id = 0;
    s32 data[8] = {};
};

using TaskId = int;
constexpr TaskId kNone = -1;

TaskId create(void (*func)(Task&), u8 priority = 128);
void destroy(TaskId id);
bool isActive(TaskId id);
Task* get(TaskId id);
void runAll();
void destroyAll();
int activeCount();

}  // namespace agb::task
