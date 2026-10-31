# Add Node

Figma: T-O08 · Popover

> ค้นหาแล้วเพิ่มกล่อง logic ตรงตำแหน่งที่คลิก แสดงหมวดด้วยสี และบอกสั้น ๆ ว่ากล่องนั้นทำอะไร

Search for a logic node and add it where you clicked. Categories are shown by colour, with a short note on what each
node does.

## Opens from

- Logic Graph › Right-click canvas (empty space)
- Nodes › Add node…
- Also: double-click empty canvas, or drop a wire on empty canvas (the new node is then connected to that wire)

## What is on it

- **Search box**: type part of a node name, for example "veloc". Results update as you type.
- **Esc**: closes the popover without adding anything.
- **Results**: up to six nodes, grouped under their category with its colour (for example Physics, Math). Each row
  shows a short hint of what the node does, such as "jump or fall". Up and Down move the highlight.
- **Click a row, or Enter (add here)**: adds the highlighted node where you opened the popover.
- **Shift+Enter (add and connect)**: adds the node and wires it to the pin you dragged from. If the popover was not
  opened from a wire, it connects from the selected node.
- **No node matches.**: shown when nothing fits the search.

Differs from Figma: "Nodes › Add node…" is a search box in the Nodes panel that filters the node list (Enter adds the
first match in the middle of the view); it does not open this popover.

## Related

- [Logic Graph](T-P17-logic-graph.md)
- [Command Palette](T-O07-command-palette.md)
