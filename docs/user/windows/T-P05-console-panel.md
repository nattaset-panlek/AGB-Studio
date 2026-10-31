# Console panel

Figma: T-P05 · Page

> แผงล่างของ Scene Editor เมื่อกดแท็บ Console: log จากสคริปต์และการ build กรองตามระดับได้ คลิกบรรทัดเพื่อไปที่โค้ด

The bottom panel of the Scene Editor when you click the Console tab: messages from your scripts and from builds. You can
filter by level and click a line to go to the code.

## Opens from

- The Console tab of the bottom panel (#21)

## What is on it

- **Console tab badge**: the number of messages in the Console.
- **Message lines**: each line has the time, an icon for its level and the text. The Console collects:
  - build results: "Build OK in 0.80 s · sky_hopper.gba 412 KB", or "Build failed" with the step;
  - build warnings and errors, each with the file and line it points at;
  - "Scene Forest_01 loaded (BG 3 · OBJ 9)" when a scene opens;
  - the results of Find Usages and Reimport from the Asset Context Menu.
- **All**: shows every message.
- **Warnings · n**: shows only warnings. The chip turns orange when there are warnings.
- **Errors · n**: shows only errors. The chip turns red when there are errors.
- The filter chips sit at the right end of the bottom panel's tab row.
- **Click a line with a file**: opens that script at that line, in the Script Editor or in your external editor, as
  chosen in Preferences › Editor.
- The list scrolls to the newest message. The Console keeps the last 1000 messages.

Differs from Figma: Figma shows lines from the running game here (`Debug.Log`, `Player.OnTouch`). Logs from the game
appear in the Console of Play Mode instead; this Console shows build and editor messages.

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [Script Editor](T-P04-script-editor.md)
- [Play Mode](T-P02-play-mode.md)
- [Build ROM](T-D06-build-rom.md)
- [Build ROM · Failed](T-D23-build-rom-failed.md)
- [Preferences · Editor](T-S07-preferences-editor.md)
