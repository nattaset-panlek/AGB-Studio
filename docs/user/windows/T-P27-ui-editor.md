# UI Editor

Figma: T-P27 · Page (built ahead of Figma, see the checklist Scope log)

> ออกแบบหน้า UI ของเกมบนหน้าจอจริงขนาด 240x160: คลิกเลือก ลากย้าย ดึงมุมปรับขนาด แล้ว Build ใช้ได้เลย

Design your game's menus and dialogs on the real 240 x 160 screen: click to select, drag to move, pull a handle to resize.
Every change is saved to the page's file and can be undone. The next build has it.

## Opens from

- Tools › UI Editor
- Double-click a page in Assets › Data, or a `.uipage` Doc Tab

## What is on it

- **Toolbar**: **Duplicate**, **Delete**, **Move up** and **Move down** for the selected element, and the language the screen
  shows text in.
- **Pages**: every page in `ui/`. **+** makes an empty one, **Preset** copies a ready-made one (TextInput, Confirm, Message,
  Menu, List) into the project to change it, **x** deletes the page.
- **Elements**: the page's elements as a tree. Drag a row to reorder it or to put it inside a Panel or Scroll List.
- **The row of kinds above the screen**: one click adds that element inside the selection.
- **The screen**: the page as the game draws it, with the same layout and font. The selected control shows in its focus
  colours, and a selected Input Field shows its keyboard. Arrow keys move the selection by a pixel, Delete removes it. An
  element inside a layout group has no handles, because the group places it: change its size in the Inspector, or turn on
  Ignore layout.
- **Inspector**: what the selected element has: position and size, anchors, layout group, colours, text, image, control
  settings and the page's own settings.
- **Status bar**: the selected element's place on the screen, how many of the 15 UI colours are in use, and a warning when
  text does not fit. Click the warning to go to that element.

## Using a page from a script

```csharp
[ScriptNative] public static void Ask() => UI.Open("TextInput");

[Cutscene]
static async Task AskName()
{
    Pages.Ask();
    await UI.Choose();     // gives the Data of the control pressed, or -1 for B
    Pages.Keep();          // read the text with UI.InputLength and UI.InputChar, then UI.Close()
}
```

A preset works with no file in the project: name it in a script and the build adds it. A page of the same name in `ui/` is
used instead. The Input Field takes English letters, digits, space and `. - '`.

## See also

[Font Manager](T-P09-font-manager.md), [Localization](T-P14-localization.md)
