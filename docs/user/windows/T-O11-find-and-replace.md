# Find & Replace

Figma: T-O11 · Popover

> แถบค้นหาและแทนที่ในตัวแก้โค้ด เลือกได้ว่าจะค้นในไฟล์นี้หรือทั้งโปรเจกต์ รองรับตัวพิมพ์เล็กใหญ่ ทั้งคำ และ regex

A find and replace bar for the code editor. You choose whether to search this file or the whole project, and it can
match case, whole words and regular expressions.

## Opens from

- Script Editor › Ctrl+F (Find) / Ctrl+H (Replace), or the search button in its toolbar

## What is on it

- **Search box**: starts with the selected text, or the word at the caret. Matches update as you type.
- **Match count**: "2 of 5", "No results", or "Invalid" for a regular expression that does not work.
- **Aa**: match upper and lower case exactly.
- **ab**: match whole words only.
- **.\***: treat the search as a regular expression.
- **Previous / Next**: move to the previous or next match and select it in the code. Enter goes to the next match,
  Shift+Enter to the previous one.
- **Close**: closes the bar. Esc does the same.
- **Replace box**: the new text. With Ctrl+H the cursor starts here. In regular expression mode, `$1` and similar insert
  the matched groups.
- **Replace**: replaces the current match and moves to the next. Enter in the replace box does the same.
- **Replace all**: replaces every match in the chosen place and shows how many were replaced.
- **Search in**: This file, All scripts (every `.cs` file of the project) or Whole project (every text file outside
  the build folder). Open scripts are changed in the editor, so you can undo and must save; files that are not open
  are changed on disk straight away.

## Related

- [Script Editor](T-P04-script-editor.md)
- [Command Palette](T-O07-command-palette.md)
