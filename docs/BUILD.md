# Building AGB Studio

Requirements: Windows 10/11 x64, devkitPro with devkitARM, libtonc and libgba (maxmod) in `D:\devkitPro` (or `DEVKITPRO`), .NET SDK 10, Ninja on `PATH`. Versions used are in `TOOLCHAIN.md`.

```
ci.cmd
```

builds the solution, runs the lint (`agb lint`), and runs every test suite. The C# tests also build and run every runtime test ROM in the embedded mGBA core.

Useful commands (after `dotnet build tools/agb/agb.csproj -c Release`):

| Command | What it does |
|---|---|
| `agb runtime-test [name ...] [--verbose] [--shots]` | builds `runtime/tests/*` and runs each ROM headless |
| `agb romtest <rom.gba> [--frames N] [--shot out.png] [--dumpsave]` | runs any ROM headless |
| `agb sample mini|skyhopper <folder>` | creates a sample project from generated original content |
| `agb build <project folder> [--release] [--clean] [--verbose]` | builds a project to `.gba` |
| `agb text-preview <font.ttf> <size> <width> <out.png> <text>` | previews Thai / Latin text layout |
| `agb lint` | checks the repository rules |

Build output of a project goes to `<project>/build/` (generated C++ in `build/gen`, objects in `build/obj`, `report.json`). Delete it or use `--clean` for a full rebuild.
