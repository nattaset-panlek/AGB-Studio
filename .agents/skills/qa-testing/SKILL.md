---
name: qa-testing
description: "Run QA testing on a page, feature, or full application at one of three depth tiers (smoke, standard, full). Use this skill whenever the user asks to QA a page or app, run a smoke test after a build/deploy, verify before release, or run a regression sweep. Triggers on QA, QA sweep, smoke test, regression test, post-deploy check, pre-launch check, verify the build, test this feature."
category: qa
catalog_summary: "Pre-launch QA, regression testing, multi-tier test matrix"
display_order: 1
---

# QA Testing (Multi-Tier Verification Protocol)

Verify that an application, feature, or subsystem is working properly before declaring it shipped.

---

## When to use

- After every code modification (Tier 1: Smoke test)
- After completing a feature or fixing a bug (Tier 2: Standard audit)
- Before a major release or milestone gate (Tier 3: Full release matrix)
- Investigating an unexpected UI/UX or behavioral defect
- Pre-launch verification of a release build

---

## The Framework: 3 Tiers

QA scales with the stakes. Pick the tier that matches the context.

| Tier | When to run | Time | Coverage |
|---|---|---|---|
| **Tier 1: Smoke** | After every code edit | ~1-2 minutes | Critical signals only (Compilation, Quick E2E sanity) |
| **Tier 2: Standard** | New feature / bugfix | ~5-10 minutes | Component test suites, Headless UI tour, Edge cases |
| **Tier 3: Full** | Major release, pre-launch | ~20-30 minutes | Full test matrix (810+ tests, ROM export, Self-contained package) |

### Tier 1: Smoke Test
The quick sanity check to confirm the build isn't broken:
- Solution builds with zero errors: `dotnet build -c Release -v q`
- Navigation E2E sanity: `dotnet test --filter "FullyQualifiedName~NavigationE2ETests"`
- Pass criteria: Zero compiler errors, 100% passing core navigation tests.

### Tier 2: Standard Audit
The component-level audit for touched modules:
- Automated UI Tour: `.\run-ui-tour.ps1 -AutoTour` (Navigation, Play Mode, Live Inspector, Emulation)
- CLI pipeline check: `agb template` -> `agb build` -> `agb romtest`
- Scoped test suites covering affected functional modules.
- Pass criteria: 31/31 UI Tour steps pass, zero visual or state regressions.

### Tier 3: Full Release Matrix
Comprehensive verification across all dimensions before release:
- Full test suite: `ci.cmd` (all unit, scripting, and editor test suites, 810+ tests)
- 130-item checklist: `docs/QA-MASTER-CHECKLIST.md`
- Clean standalone release package: `agb package build/package`
- Release gate sign-off in `docs/ENGINE-CHECKLIST.md`.

---

## Workflow

1. **Pick the tier:** Smoke for quick edits, Standard for completed features, Full for release sign-off.
2. **Execute tests:** Run the automated test suites / UI tour for the chosen tier.
3. **Triage failures:** Use `qa-test-runner` protocol to triage and patch.
4. **Report & Document:** Provide clear pass/fail counts, files patched, and remaining blockers.
