# Build ROM

Figma: T-D06 · Dialog

> หน้าต่างแสดงความคืบหน้าตอนกด Build ROM ทุกขั้นตอนเห็นได้ (C# → C++ → asset → คอมไพล์ → ลิงก์) ถ้ามี error คลิกเพื่อไปที่บรรทัดนั้น

Shows the progress of a build after you press Build ROM. You can follow every step (C# to C++, assets, compile, link),
and if there is an error you can click it to go to that line.

## Opens from

- Build button in the title bar (#10)
- Build › Build ROM (Ctrl+B)
- Build Report › Build again

Export ROM also uses this window for its Release build.

## What is on it

- **Title**: "Building <game>.gba…" while it runs, then "Built <game>.gba".
- **Steps**: four rows, each with a status icon, a name and a detail line:
  **C# → C++** (your scripts turned into C++), **Convert assets** (images, maps, fonts and sounds turned into GBA
  data), **Compile (devkitARM g++ -O2)** and **Link + gbafix**. A tick means done, a turning arrow means running (with a
  progress bar and "file · done / total"), a clock means waiting, a cross means failed.
- **Log**: the build messages as they happen, with the time; warnings and errors are coloured.
- **Hint** (bottom left): how long builds usually take for this project, for example "Usually under a second.".
- **Close**: closes the window. The build keeps its result.
- **Run in Emulator**: opens Play Mode with the new ROM. Pressed during the build, it runs the game as soon as the build
  succeeds.
- **Run after build**: when Project Settings › Build says to run after building, a plain Build ROM opens Play Mode by
  itself.
- **When a step fails**: this window closes and Build ROM · Failed opens with the errors, where you click an error to
  go to its line.
- If no project is open, or another build is running, the log says so and nothing is built.

## Related

- [Build ROM · Failed](T-D23-build-rom-failed.md)
- [Build Report](T-P11-build-report.md)
- [Export ROM](T-D05-export-rom.md)
- [Play Mode](T-P02-play-mode.md)
- [Project Settings · Build](T-S03-project-settings-build.md)
- [Console panel](T-P05-console-panel.md)
