# UI changes since the Figma frames

Windows whose UI differs from the Figma file on purpose, with the reason. Each branch adds its own section.

## Templates

| Window id | Before | After | Reason |
|---|---|---|---|
| T-P01 Hierarchy | Actor rows showed the kind icon, the name and a meta word only; nothing told which components an actor had. | Actor rows show up to four small component badges after the name (behaviours first: rocket physics, sword combat, bug AI, sound audio, code script; then image sprite, type text, flag zone, link scene link, camera followed by the camera). The row tooltip lists every component ("Components: Sprite, Animation idle, Platformer Body, PlayerController (script), Collider, Camera follows"). | The owner could not tell which objects of the New Project templates use which components, or how they work. |
| T-P01 Inspector | Header, then Transform, Sprite, one section per behaviour, Collider, Add Component. | A "Components" section under the header lists every part of the actor as pills (sprite, animation, built-in and script behaviours, collider, text label, trigger zone, camera follow, scene links) with the count; scripts use the accent colour, the tooltip says what each kind is, a click scrolls to the section that edits it. | Same: one place that names everything an actor is made of. |
| T-D12 New Project | A project made from a genre template opened on its Title scene (one text label, no components). | It opens on the template's main scene (Level_01, Village, Story, Board, Stage_01, Meadow, Circuit, Pier); the game still starts on the title screen. The project folder has a README.md that lists every scene, object, component and script and what to try changing. | The title screen showed nothing to learn from, so the templates looked as if no object used components. |
