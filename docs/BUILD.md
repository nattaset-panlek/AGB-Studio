# Building AGB Studio

Requirements: Windows 10/11 x64, .NET SDK 10, devkitPro with devkitARM, libtonc, libgba (maxmod) and the GBA tools
(gbafix, mmutil), found through `DEVKITPRO`, `C:\devkitPro` or `D:\devkitPro`. Ninja is optional: when `ninja.exe` is
on `PATH` the native build runs it, otherwise AGB Studio runs the same build graph itself. Versions used are in
`TOOLCHAIN.md`.

## Build, lint and test

```
ci.cmd
```

runs, and stops at the first failure:

1. `dotnet build AGB-Studio.sln -c Release` (runtime tools, editor, tests)
2. `agb lint`: no emoji in text files, product name "AGB Studio", no pokeemerald identifiers in code, runtime code
   writes hardware only through the commit queues, no `float` / `double` in runtime C++
3. `tools/check-commit-emoji.ps1`: no emoji in any commit message (G-EMOJI.01)
4. `tools/check-commit-refs.ps1 -Strict`: fails when a commit after the baseline in `ci.cmd` (or `AGB_COMMIT_BASE`)
   names neither a Figma window id nor `infra:` (G-SCOPE.01); the earlier commits without one are in the checklist
   Scope log
5. the three test suites, one at a time (they build ROMs into shared folders under `build\`): `AgbStudio.Tests`
   (pipeline, runtime test ROMs in the embedded mGBA core, templates, packaging), `AgbStudio.Scripting.Tests`,
   `AgbStudio.Editor.Tests` (headless editor E2E and SHOT), with results as `.trx` files in `build\ci\results`

Every run, passed or failed, keeps its artifacts in `build\ci\runs\<time>-<commit>\` (`tools/ci-artifacts.ps1`,
P1.06): the `.trx` test results, the runtime test ROMs with their maps and screenshots, the SHOT report and
`summary.txt` (commit, result, the step that failed). The newest 10 runs are kept.

On a machine with little memory, set `AGB_BUILD_JOBS=1` (or 2) to limit parallel compiles of game and test ROMs.

## The `agb` command line

After `dotnet build tools/agb/agb.csproj -c Release` it is `tools\agb\bin\Release\net10.0\agb.exe`.

| Command | What it does |
|---|---|
| `agb build <project folder> [--release] [--clean] [--verbose]` | builds a project to `.gba` (all pipeline steps) |
| `agb runtime-test [name ...] [--verbose] [--shots]` | builds `runtime/tests/*` and runs each ROM headless |
| `agb romtest <rom.gba> [--frames N] [--shot out.png] [--dumpsave] [--memmap]` | runs any ROM headless until it reports pass or fail |
| `agb sample mini\|skyhopper <folder>` | creates the mini platformer or the Sky Hopper fixture from generated original content |
| `agb template <id> <folder> [name]` | creates a project from a New Project template (`platformer`, `topdown`, `visualnovel`, `puzzle`, `shmup`, `rpg`, `racing`, `brawler`) |
| `agb package [out folder]` | publishes the editor and writes the portable zip (default `build\package`) |
| `agb text-preview <font.ttf> <size> <width> <out.png> <text>` | previews Thai / Latin text layout |
| `agb lint [folder]` | checks the repository rules (default: the repository root) |
| `agb make-test-audio <folder>` | writes the small original audio files of the `audio_basic` runtime test |

## Building a game

`agb build` and the editor's Build ROM run the same pipeline (`GameBuilder`, see `ARCHITECTURE.md`): Convert assets,
C# → C++, Generate game, Compile (devkitARM g++), Link + gbafix. Output goes to `<project>/build/`:

| Path | What |
|---|---|
| `build/<rom>.gba` | the ROM, with `THIRD_PARTY.txt` (libtonc and maxmod notices) next to it |
| `build/gen/` | generated C++ (assets, scenes, strings, scripts, database) |
| `build/obj/` | `build.ninja`, objects, the linked `.elf` and its `.map` |
| `build/cache/manifest.json` | hashes of converted assets for incremental builds |
| `build/report.json`, `build/report-history.json` | the Build Report and the last 20 builds |

Generated files are rewritten only when their content changes and objects are rebuilt only when a source or header
changed, so a one-value script edit rebuilds in about a second. Delete `build/` or use `--clean` (Build › Clean Build
in the editor) for a full rebuild.

## Packaging

```
agb package [out folder]
```

runs `dotnet publish` of the editor (self-contained, win-x64), stages `AGB-Studio-<version>-win-x64/` with `bin/`,
the runtime sources, fonts, the Thai word list, the emulator core, `docs/user`, `THIRD_PARTY.md` and the release notes,
and zips it. See `ARCHITECTURE.md` › Packaging and D-19.

## Commits and pull requests

Every change names the Figma window it implements or the infrastructure reason (checklist sections 1 and 2.6,
G-SCOPE.01):

- Commit messages contain a window id such as `T-P04` or a line `infra: <reason>`, plus the checklist ids the change
  advances. The template `.gitmessage` holds the fields; use it with `git config commit.template .gitmessage`.
- Pull requests use `.github/pull_request_template.md` (field "Figma ref or infra reason"). The repository is local
  (D-04), so the template takes effect once it is hosted; until then the commit check below is the record.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-commit-refs.ps1 [-Base <rev>] [-Strict]` lists
  the non-merge commits in `<Base>..HEAD` that name neither (default base: `AGB_COMMIT_BASE`, else `main`). `-Strict`
  makes it exit 1. Merge commits are skipped because the commits they bring are checked themselves. History is not
  rewritten: commits from before the rule are listed in the checklist Scope log.
- `tools\check-commit-emoji.ps1 [-Range <rev range>]` fails on any emoji in a commit message; its code point ranges
  are those of `Lint.IsEmoji` (`tools/AgbStudio.Build/Lint.cs`), change both together.
- No emoji anywhere; the product name is "AGB Studio".
