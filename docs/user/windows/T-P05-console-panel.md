# Console panel

Figma: T-P05 · Page

> แผงล่างของ Scene Editor เมื่อกดแท็บ Console: log จากสคริปต์และการ build กรองตามระดับได้ คลิกบรรทัดเพื่อไปที่โค้ด

The bottom panel of the Scene Editor when you click the Console tab: messages from your scripts and from builds. You can
filter by level, double-click a line to go to the code, and copy lines as text to report a problem without a screenshot.

Click a line to select it. Ctrl+click adds or removes a line, Shift+click selects from the last line clicked, and dragging
selects what it passes over; Ctrl+A selects all and Esc clears. The **...** button at the top right and a right click offer
**Copy Selected** (Ctrl+C), **Copy All**, **Select All**, **Save to File** (`build/console.log`) and **Clear**. A copied line has
its time, level, message and the file it points at.

## Opens from

- The Console tab of the bottom panel (#21)

## What is on it

- **Console tab badge**: the number of messages in the Console.
- **Message lines**: each line has the time, an icon for its level and the text. The Console collects:
  - build results: "Build OK in 0.80 s · sky_hopper.gba 412 KB", "Build failed" with the step, the number of errors
    above it and what to do, or "Build cancelled · the last built ROM is unchanged";
  - build warnings and errors, each with the file and line it points at;
  - the running game's own lines, the same as in Play Mode's Console: `Debug.Log` output, engine warnings (counted
    under Warnings) and failed asserts (counted under Errors);
  - "Scene Forest_01 loaded (BG 3 · OBJ 9)" when a scene opens;
  - the results of Find Usages and Reimport from the Asset Context Menu;
  - each file that Import Asset did not import, with the reason.
- **All**: shows every message.
- **Warnings · n**: shows only warnings. The chip turns orange when there are warnings.
- **Errors · n**: shows only errors. The chip turns red when there are errors.
- The filter chips sit at the right end of the bottom panel's tab row.
- **Click a line with a file**: opens that script at that line, in the Script Editor or in your external editor, as
  chosen in Preferences › Editor.
- The list scrolls to the newest message. The Console keeps the last 1000 messages.

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [Script Editor](T-P04-script-editor.md)
- [Play Mode](T-P02-play-mode.md)
- [Build ROM](T-D06-build-rom.md)
- [Build ROM · Failed](T-D23-build-rom-failed.md)
- [Preferences · Editor](T-S07-preferences-editor.md)
