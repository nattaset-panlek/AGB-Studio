# Add Node

Figma: T-O08 · Popover

> ค้นหาแล้วเพิ่มกล่อง logic ตรงตำแหน่งที่คลิก แสดงหมวดด้วยสี และบอกสั้น ๆ ว่ากล่องนั้นทำอะไร

Search for a logic node and add it where you clicked. Categories are shown by colour, with a short note on what each
node does.

## Opens from

- Logic Graph › Right-click canvas (empty space)
- Nodes › Add node… (click the box, or start typing in it: what you type goes into the search; the node is added in
  the middle of the view)
- Also: double-click empty canvas, or drop a wire on empty canvas (the new node is then connected to that wire), also
  a wire drawn with the Link tool

## What is on it

- **Search box**: type part of a node name, for example "veloc". Results update as you type.
- **Esc**: closes the popover without adding anything.
- **Results**: up to six nodes, grouped under their category with its colour (for example Physics, Math). Each row
  shows a short hint of what the node does, such as "jump or fall". Up and Down move the highlight.
- **Click a row, or Enter (add here)**: adds the highlighted node where you opened the popover.
- **Shift+Enter (add and connect)**: adds the node and wires it to the pin you dragged from. If the popover was not
  opened from a wire, it connects from the selected node.
- **No node matches.**: shown when nothing fits the search.

## Related

- [Logic Graph](T-P17-logic-graph.md)
- [Command Palette](T-O07-command-palette.md)
