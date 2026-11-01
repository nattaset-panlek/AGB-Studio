# UI changes since Figma

Visual and UX changes made in the editor after the Figma frames were signed off. The owner allowed them on
2026-11-01 (packaged-app test on a fresh Windows account); Figma is updated from this list later. One row per change:
the window, what Figma (and the app) showed before, what the app shows now, and why.

| Window | Before | After | Reason |
|---|---|---|---|
| T-P02 Play Mode | "Frame N" chip hugged its text; Inter proportional digits | Chip 128 px wide, text left-aligned, tabular digits (OpenType tnum) | The Stop / Pause / Step group in the middle of the toolbar moved sideways every frame while the number changed width |
| T-P19, T-P23, T-P24 Debugger | "Paused at frame N" chip hugged its text and took no space while hidden | Chip 184 px wide in a fixed 184 px slot, tabular digits | Same jitter, and the play controls jumped when the chip showed or hid |
| T-P19, T-P23, T-P24 Debugger | Doc Tabs: Game, Debugger | Doc Tabs: Scene, Game, Debugger | Without a running game the only way out was the Game tab, which starts a build |
| T-P19, T-P23, T-P24 Debugger | Pause enabled with no game | Pause disabled until a game runs, as on every other page | Toolbar state did not match the Play state |
| T-P18 Scripting API | No Doc Tabs: the page had no way back except the title bar menus | Doc Tabs row (34 px) over the reference: every open document, then "Scripting API" selected | Owner report: after entering the Scripting page the user could not exit or go back |
| T-P13 World Map | No Doc Tabs over the canvas | Doc Tabs row: open documents, then "World Map" selected | Same dead end: the only way back was double-clicking a scene |
| T-P17 Logic Graph | No Doc Tabs over the canvas | Doc Tabs row with the open documents (the graph selected) | Same dead end |
| T-P11 Build Report | No Doc Tabs over the report | Doc Tabs row with the open documents (Build Report selected) | Same dead end |
| T-P16 Dialogue Editor (and the other Database-frame pages) | With no .dlg open the Doc Tabs row was empty | The Scene tab is always there | Dead end with an empty project |
| T-P12, T-P20, T-P21 Launcher | With a project open (File › Open Project…, Help › Getting Started) the title bar is hidden and nothing leads back | "Back to <project>" ghost button at the left of Open… / New Project, shown only while a project is open; it returns to the page shown before | Dead end: the only way back was opening the same project again |
| Startup (new dialog, no Figma frame) | Running AGBStudio.exe from inside a zip (%TEMP%\Temp1_*.zip) started normally and failed later | "Extract AGB Studio first" dialog (520 px, warn icon): says to Extract All and start from the extracted folder; Continue anyway / Close AGB Studio | Owner report: the zip's temporary folder lacks the bundled files and Windows may delete it |
| Crash notice (toast) | "... Details are in crash-<time>.log (crash folder)." | "... Details are in <full path of the log>." | Users could not find the crash folder |
| All pages, title bar breadcrumb | Debugger, World Map and Scripting API set an invented "document" to show their name | The breadcrumb shows the page name while the page is shown; the active document stays one that is open | The invented document stayed active after leaving the page: Save, other pages' Doc Tabs and the breadcrumb pointed at a page that was gone |
| All pages, Doc Tabs | The Game tab could stay selected on other pages (a project opened without a scene made Game the active document) | Game is selected only in Play Mode; the Scene Editor always shows its scene tab selected | Owner report: the UI looked stuck in Play after switching pages |
| T-P01 Scene Editor | Returning from the Tilemap, Font or Sprite pages through the Scene tab rebuilt the page | The same Scene Editor comes back with its tool, bottom panel tab and selection | State lost on switch |
| T-P03, T-P08, T-P09 tab rows | A tab of another editor's document (Database, Dialogue, Audio, SFX, Palettes, Logic Graph) made it active without showing its page | The tab opens that document's page | Panels and tabs disagreed after a switch |
| Play (all pages) | Stop from another page (Command Palette) jumped to the Scene Editor | Stop returns to the Scene Editor only from Play Mode or the Debugger; elsewhere the page stays | Play hijacked the page the user was on |

SHOT renders that change with these rows: T-P02 (frame chip width), T-P11, T-P13, T-P17, T-P18 (new Doc Tabs row),
T-P19 / T-P23 / T-P24 (Scene tab, paused chip slot). The Launcher frames change only when a project is open.
# UI changes since the Figma frames

Windows whose UI differs from the Figma file on purpose, with the reason. Each branch adds its own section.

## Templates

| Window id | Before | After | Reason |
|---|---|---|---|
| T-P01 Hierarchy | Actor rows showed the kind icon, the name and a meta word only; nothing told which components an actor had. | Actor rows show up to four small component badges after the name (behaviours first: rocket physics, sword combat, bug AI, sound audio, code script; then image sprite, type text, flag zone, link scene link, camera followed by the camera). The row tooltip lists every component ("Components: Sprite, Animation idle, Platformer Body, PlayerController (script), Collider, Camera follows"). | The owner could not tell which objects of the New Project templates use which components, or how they work. |
| T-P01 Inspector | Header, then Transform, Sprite, one section per behaviour, Collider, Add Component. | A "Components" section under the header lists every part of the actor as pills (sprite, animation, built-in and script behaviours, collider, text label, trigger zone, camera follow, scene links) with the count; scripts use the accent colour, the tooltip says what each kind is, a click scrolls to the section that edits it. | Same: one place that names everything an actor is made of. |
| T-D12 New Project | A project made from a genre template opened on its Title scene (one text label, no components). | It opens on the template's main scene (Level_01, Village, Story, Board, Stage_01, Meadow, Circuit, Pier); the game still starts on the title screen. The project folder has a README.md that lists every scene, object, component and script and what to try changing. | The title screen showed nothing to learn from, so the templates looked as if no object used components. |
