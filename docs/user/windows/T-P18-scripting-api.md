# Scripting API

Figma: T-P18 · Page

> คู่มือ API สำหรับเขียนสคริปต์ C# ในตัว editor ค้นหาได้ มีตัวอย่างโค้ดคัดลอกได้ทันที

The reference for writing C# scripts, built into AGB Studio. You can search it, and every page has an example you can
copy straight away.

## Opens from

- Help › Scripting API (F1)
- Script Editor › Open Scripting API

## What is on it

- **Toolbar**: Search (puts the cursor in the search box) and the Docs view button. Play (F5) runs the game; Pause
  pauses and resumes a running game and Step moves it one frame (F10); both are greyed out while no game runs. The
  "GBA · Mode 0 · 240×160" chip opens Video Mode. Undo and Redo are on the right.
- **API tree**: the reference pages: Behaviour, Actor, Input (with Key), Sound, Music, Scene, Camera, Save,
  Text & Dialogue, Items (Database) and fixed (math). Click one to show it.
- **Search API…**: type to list matching members instead of the tree, for example "Input.Pressed". Click a result to
  open its page at that member. Your project's own members (Game fields, database tables) are included.
- **Page title and overview**: what the class is for, in one or two sentences.
- **Members table**: each member with what it returns and a short description. Pairs such as AxisX and AxisY share a
  row.
- **Example**: a short script that shows the class in use, with code colours.
- **See also**: buttons that open related pages.
- **On this page**: jumps to Overview, Members, Example and See also, and to the enum page where there is one (for
  example Key enum on the Input page).
- **Copy example**: copies the example to the clipboard and shows "Example copied".
- **Status line**: the docs version and "Offline". The reference is built from the files on this computer, so it works
  without the internet.

Differs from Figma: the page also ends with a "See also" row of buttons; the Figma frame lists See also only under
On this page.

## Related

- [Script Editor](T-P04-script-editor.md)
- [New Script](T-D14-new-script.md)
- [Logic Graph](T-P17-logic-graph.md)
- [Database](T-P07-database.md)
- [Video Mode](T-O02-video-mode.md)
- [GBA hardware guide: input](../gba-hardware-guide.md#input)
- [GBA hardware guide: save data](../gba-hardware-guide.md#save-data)
