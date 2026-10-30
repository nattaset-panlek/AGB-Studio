#include "agb/task.hpp"

#include "agb/debug.hpp"

namespace agb::task {

namespace {
Task g_tasks[kMaxTasks];
}

TaskId create(void (*func)(Task&), u8 priority) {
    for (int i = 0; i < kMaxTasks; ++i) {
        if (g_tasks[i].active) continue;
        g_tasks[i] = Task{};
        g_tasks[i].func = func;
        g_tasks[i].priority = priority;
        g_tasks[i].active = true;
        g_tasks[i].id = static_cast<u8>(i);
        return i;
    }
    debug::warnOnce(2, "task pool is full (16 tasks); the new task was not started");
    return kNone;
}

void destroy(TaskId id) {
    if (id >= 0 && id < kMaxTasks) g_tasks[id].active = false;
}

bool isActive(TaskId id) { return id >= 0 && id < kMaxTasks && g_tasks[id].active; }

Task* get(TaskId id) { return isActive(id) ? &g_tasks[id] : nullptr; }

void runAll() {
    // Selection by priority; 16 tasks keep this cheap and avoids sorting state.
    bool ran[kMaxTasks] = {};
    for (;;) {
        int best = -1;
        for (int i = 0; i < kMaxTasks; ++i)
            if (g_tasks[i].active && !ran[i] && (best < 0 || g_tasks[i].priority < g_tasks[best].priority)) best = i;
        if (best < 0) break;
        ran[best] = true;
        Task& t = g_tasks[best];
        if (t.wait) {
            --t.wait;
            continue;
        }
        t.func(t);
    }
}

void destroyAll() {
    for (auto& t : g_tasks) t.active = false;
}

int activeCount() {
    int n = 0;
    for (auto& t : g_tasks) n += t.active;
    return n;
}

}  // namespace agb::task
