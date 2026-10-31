# Build ROM · Failed

Figma: T-D23 · Dialog

> สถานะ build ไม่ผ่าน: บอกว่าล้มที่ขั้นไหน รายการ error ที่คลิกไปยังบรรทัดในโค้ดได้ และปุ่มไปที่ error แรก

The build did not pass. This window says which step failed, lists the errors (click one to go to that line in the
code) and has a button to go to the first error.

## Opens from

- Build ROM when a step fails (it replaces the Build ROM window)

## What is on it

- **Title**: "Build failed · 1 error" or "Build failed · N errors".
- **Steps**: the step that ran just before the failure (with a tick), the failed step in red with where it stopped
  (for example "stopped at PlayerController.cpp"), and the steps after it marked "skipped".
- **Messages**: the build's errors and warnings with the time, for example
  "PlayerController.cs(22,9): ';' expected". Click a message to open its file: a C# script opens in the Script Editor
  (or your outside editor, as set in Preferences) at that line; any other file is shown in Explorer.
- **Hint** (bottom left): "Fix them, then build again."
- **Close**: closes the window.
- **Go to first error**: opens the first error that has a file, at its line.

## Related

- [Build ROM](T-D06-build-rom.md)
- [Script Editor](T-P04-script-editor.md)
- [Build Report](T-P11-build-report.md)
- [Console panel](T-P05-console-panel.md)
- [Preferences · Editor](T-S07-preferences-editor.md)
