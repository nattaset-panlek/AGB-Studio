---
name: qa-test-runner
description: Executes automated test suites, performs root cause analysis on failures, repairs broken code or tests, and verifies edge cases autonomously. Use when asked to run tests, QA a feature, fix failing tests, or validate code changes.
---

# Autonomous QA & Test Execution Protocol

## 1. Environment & Test Discovery
- Identify the project's test runner and environment before execution (`package.json`, `pyproject.toml`, `Cargo.toml`, `build.gradle`, or Unity/Game Engine CLI).
- Run strictly scoped tests targeting only modified modules first to conserve execution time and context tokens.

## 2. Iterative Execution & Self-Healing Loop
Execute the following loop autonomously without asking for user intervention until all tests pass or a hard architectural blocker is reached (Max 5 iterations per failure):
1. **Execute:** Run the test command in the terminal with verbose/stack-trace flags enabled (e.g., `--tb=short`, `--reporter=list`).
2. **Triage Failure:**
   - **Code Regression:** Production logic violates expected behavior -> Fix the source code.
   - **Stale Test:** Intentional architectural/API change broke the test -> Update the test assertions/mocks.
   - **Environment/Flaky:** Port conflict, race condition, or missing mock -> Isolate dependencies and enforce deterministic waits.
3. **Patch & Re-verify:** Apply the minimal code change required and immediately re-run the failing test.
4. **Full Suite Gate:** Once targeted tests pass, run the full regression suite for the affected domain.

## 3. Edge-Case & QA Verification Checklist
When authoring new tests or QAing a feature, enforce:
- Boundary values (null, empty, max/min limits, zero, negative values).
- Asynchronous race conditions, timeouts, and error/exception handling paths.
- Strict mocking of external I/O (network, database, file system) for unit tests.
- Zero tolerance for trivial assertions (`expect(true).toBe(true)`) or commenting out failing tests to force a pass.

## 4. Final Output Format
Report concise results after loop completion:
- **Status:** PASS / FAIL (with coverage delta if available)
- **Root Causes Fixed:** Bullet list of exact files/methods patched and why.
- **Remaining Blockers:** Any unresolvable architectural issues requiring user decision.
