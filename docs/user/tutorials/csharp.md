# Scripting in C# for GBA

fixed-point, Update(), OnTouch() · 20 min · 3 steps

Game logic is written as C# behaviours. AGB Studio checks them against the GBA rules (GAE001 to GAE006) and translates
them to C++ when you build.

## 1. Open a script

Behaviours are C# classes with Update() and OnTouch(). Open PlayerController.cs from Assets.

- [ ] Open PlayerController.cs (the Script Editor opens)

## 2. Read the API

Numbers on the GBA are fixed-point: fixed is 24.8. The Scripting API lists every class and method.

- [ ] Open the Scripting API (F1, or Help › Scripting API)

## 3. Compile

Building translates the scripts to C++ and reports problems in the Console.

- [ ] Build the ROM (Ctrl+B)
- [ ] Open the Console tab

Click an error in the Console to open the script at its line.

## See also

- [Script Editor](../windows/T-P04-script-editor.md), [Scripting API](../windows/T-P18-scripting-api.md),
  [Console panel](../windows/T-P05-console-panel.md), [Logic Graph](../windows/T-P17-logic-graph.md)
