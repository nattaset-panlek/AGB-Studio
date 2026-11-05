# เทียบ GB Studio กับ AGB Studio

ข้อมูลตั้งต้นสำหรับ section ปรับปรุง (upgrade) จากการอ่านโค้ดทั้งสองโปรเจกต์เมื่อ 2026-11-05 **ยังไม่ได้รันทั้งสองโปรแกรม** ขั้นตอนการคลิกอนุมานจากโค้ดและเอกสาร ส่วนข้อที่บอกว่า "ไม่มี" มาจากการค้นชื่อไฟล์และข้อความ ไม่ได้อ่านทุกบรรทัด ควรยืนยันก่อนเริ่มทำแต่ละข้อ

- GB Studio: <https://github.com/chrismaltby/gb-studio> commit `eb6aa8a` (2026-10-21), `package.json` ระบุ 4.3.2
- รายการที่มี **(U)** อยู่ในหัวข้อ Unreleased ของ `CHANGELOG.md` ยังไม่ใช่ฟีเจอร์ของรุ่น 4.3.2

## ข้อสรุป

ช่องว่างไม่ได้อยู่ที่จำนวนหน้าต่าง OS เพราะ AGB Studio มีน้อยกว่าและ Play ฝังอยู่ในหน้าเดียว แต่อยู่ที่ 3 เรื่องนี้:

1. **ต้องสลับหน้าไปมา:** GB Studio ทำฉาก, วาง actor, ทาสี collision และแก้ logic ได้บน canvas เดียว ส่วน AGB Studio แยกเป็น Scene Editor, Tilemap, World Map และ Logic Graph
2. **ต้องพิมพ์อ้างอิงเอง:** ฉาก, เสียง, เพลง, ตัวแปร และ tag ใน Logic Graph เป็นข้อความอิสระ ส่วน GB Studio เลือกจาก dropdown
3. **Logic อยู่ห่างจากวัตถุ:** GB Studio เลือกวัตถุแล้วเขียน script ลงช่อง เช่น On Interact และ On Update ได้เลย ส่วน AGB Studio ต้องสร้าง graph ในอีกหน้า แล้วกลับมาแนบเป็น component

ส่วน engine ไม่ได้ตามหลังมาก ความสามารถหลายอย่างมีใน C# API แล้ว เช่น `Wait`, `Camera`, `Scene.SetTile`, `Save.Read` และเมนูตัวเลือก แต่ยังไม่มีใน Logic Graph

## เทียบ flow การใช้งาน

| งาน | GB Studio | AGB Studio | ไฟล์อ้างอิงฝั่งเรา |
|---|---|---|---|
| จำนวนหน้าจอ | 8 ส่วนในหน้าต่างเดียว | 27 หน้า, 24 dialog, 13 popover, 9 หน้าตั้งค่า | `docs/user/windows` |
| ติดตั้งก่อน build | ไม่พบขั้นตอนใน repo (ยังไม่ได้ตรวจตัวติดตั้งจริง) | ต้องติดตั้ง devkitPro แยก | `ToolchainHelp.cs` |
| สร้างโปรเจกต์ | template, ชื่อ, ที่เก็บ | เพิ่ม ROM title, game code, save type และตัวเลือก 4 ช่อง | `NewProjectDialog.cs` |
| เห็นฉากและวัตถุพร้อมกัน | canvas เดียว | แยก Scene Editor กับ World Map | `WorldMapPage.cs` |
| วาง actor | กด `a` แล้วคลิกบนฉาก | เมนู Add Actor, Hierarchy หรือลากจาก Assets | `Commands/Catalog.cs` |
| ทาสี collision | บน canvas | หน้า Tilemap | `Windows/Tilemap` |
| ใส่ logic | เลือกวัตถุ แล้วเขียนลงช่อง script | component 8 แบบ, C# หรือ Logic Graph | `Behaviours.cs` |
| ประตูพร้อมข้อความ 1 บรรทัด | Display Text และ Change Scene พิมพ์ในรายการ | Show Text ต้องใช้ string key, Load Scene พิมพ์ชื่อ | `LogicGraph.cs` |
| Wait และบทสนทนาต่อเนื่อง | event | C# `[Cutscene] async Task`, ไม่มี node Wait หรือ Choice | `AgbStudio.Api/Behaviour.cs` |
| เชื่อมฉาก | เกิดเองจาก Change Scene และลากปลายลูกศรได้ | ใช้ Link tool แล้วกรอก Link Settings | `LinkSettingsPopover.cs` |
| เงื่อนไขของ link | If event | พิมพ์โค้ด เช่น `Game.coins >= 10` | T-O09 |
| Play | build แบบเว็บ แล้วเปิดหน้าต่างเล่น | `F5` เล่นในแท็บ Game | T-P02 |

## เทียบเครื่องมือ

**ใช้กับ GBA:** ตรง = ใช้แนวคิดเดิมได้, ปรับ = ต้องออกแบบตามฮาร์ดแวร์ GBA, ไม่ใช้ = ผูกกับ Game Boy, เรานำหน้า = เรามีมากกว่าอยู่แล้ว

| เครื่องมือ | GB Studio | AGB Studio | สิ่งที่ขาด | ใช้กับ GBA | ไฟล์ GB Studio (`src/`) |
|---|---|---|---|---|---|
| World canvas | มี | บางส่วน | การ์ดอย่างเดียว ยังไม่มี actor หรือการแก้ไขบน canvas | ปรับ: ฉากใหญ่และ 4 BG ต้องใช้ thumbnail หรือ LOD | `components/world/WorldView.tsx` |
| วางและลากวัตถุบนฉาก | มี | บางส่วน | ทำได้ทีละฉาก ไม่มีคลิกเพื่อวาง | ตรง | `world/entities/scenes/*` |
| Inspector | มี | มี | | เรานำหน้า | `world/inspector/*` |
| ลูกศรเชื่อมฉาก | เกิดจาก event | บางส่วน | ไม่ได้สร้างจาก script, จุดเกิดลากไม่ได้ | ตรง | `world/connections/Connections.tsx` |
| จุดเริ่มผู้เล่น | ลากได้ | บางส่วน | มีแค่สวิตช์ start scene ไม่พบ marker | ตรง | `PlayerStartMarker.tsx` |
| Script แบบรายการ | มี | บางส่วน | เรามี node graph 45 node แต่ไม่มีช่อง script ต่อวัตถุ | ปรับ: ให้ลงเป็น VM หรือ C# เดิม | `components/script/*` |
| เมนูเพิ่ม event | ค้นหา, favorite, preset | บางส่วน | ค้นหาได้อย่างเดียว | ตรง | `script/menus/AddScriptEventMenu.tsx` |
| Navigator ทั้งโปรเจกต์ | มี | บางส่วน | Hierarchy เห็นทีละฉาก | ตรง | `world/navigator/*` |
| ตัวแปร | จัดการและสร้างในบรรทัด (U) | บางส่วน | อยู่ใน Project Settings, ใน graph พิมพ์ชื่อ, ไม่พบรายการจุดที่ใช้ | ตรง | `navigator/VariableNavigatorPane.tsx` |
| ค่าคงที่ | มี | บางส่วน | มีแค่ใน C# | ตรง | `ConstantNavigatorPane.tsx` |
| Script ใช้ซ้ำพร้อมพารามิเตอร์ | มี | บางส่วน | ใช้ class C# ได้ ไม่พบ subgraph | ตรง | `CustomEventNavigatorPane.tsx` |
| Prefab | override และจุดที่ใช้ | บางส่วน | ไม่พบ inspector ของ prefab และรายการจุดที่ใช้ | ตรง | `inspector/prefabs/*` |
| Sprite และ animation | ประกอบ metasprite | มี | เราวาดพิกเซลได้ ยังไม่ได้เช็กการประกอบหลาย OBJ | ปรับ: ขนาด OBJ, 128 OAM, affine | `components/sprites/*` |
| ดูรูปและ tileset | มี | มี | | ตรง | `components/images/*` |
| Autotile (U) | มี | มี | | ตรง | `inspector/scenes/tilemap/*` |
| ทาสี tile บน canvas | มี | บางส่วน | ทำในหน้า Tilemap | ปรับ: bank ต่อ tile และหลายชั้น | `toolbar/BrushToolbar.tsx` |
| ทาสี collision บน canvas | มี | บางส่วน | ทำในหน้า Tilemap | ตรง | `SceneCollisions.tsx` |
| Music editor | piano roll, tracker, MIDI | บางส่วน | มี tracker, mixer ไม่พบ piano roll | ปรับ: ใช้กับ Maxmod และ sample | `components/music/*` |
| Sound effect | ฟัง WAV, VGM, FX Hammer | มี | | เรานำหน้า: มี SFX Designer | `components/sounds/*` |
| Palette | แก้จาก dropdown ได้ (U) | มี | ไม่มีการสร้างจาก dropdown | ปรับ: 16 สีต่อ bank | `components/palettes/*` |
| Preview บทสนทนาใน script | มี | บางส่วน | มีเฉพาะหน้า Dialogue | ตรง | `script/previews/*` |
| Debugger | ตัวแปร, VRAM, breakpoint, ROM usage (U) | มี | ของเราเป็นหน้าแยก ไม่ได้ติดกับหน้าทำงาน | เรานำหน้าบางส่วน | `components/debugger/*` |
| โน้ตบนแผนที่ | มี | ไม่มี | ทั้งฟีเจอร์ | ตรง | `entities/notes/NoteView.tsx` |
| ประเภทฉาก | 5 แบบ | บางส่วน | มี behaviour และ Start from | ตรง | `settings/section/SettingsSectionSceneTypes.tsx` |
| ปรับค่า engine, eject engine | มี | บางส่วน | มีฟิลด์ component ไม่พบ eject | ตรง | `SettingsSectionEngineFields.tsx` |
| Plugin | 6 ประเภท | ไม่มี | ทั้งฟีเจอร์ (Out of scope เดิม) | ปรับ: engine เป็น C# และ C++ | `lib/pluginManager/types.ts` |
| Export เว็บ | มี | ไม่มี | ทั้งฟีเจอร์ | ปรับ: ต้องหา emulator GBA แบบ WASM และเช็กลิขสิทธิ์ | `lib/compiler/webBuild.ts` |
| Export Pocket | มี | ไม่มี | ทั้งฟีเจอร์ | ยังไม่รู้ว่ามีทางสำหรับ GBA หรือไม่ | `apps/gb-studio-cli` |
| CLI | มี | มี | | เรานำหน้า: มี MCP ด้วย | `apps/gb-studio-cli` |
| Template | 3 แบบ | มี 9 แบบ | ยังไม่ได้เทียบความลึกของแต่ละแบบ | เรานำหน้า | `appData/templates` |
| ภาษาของ UI | 21 ไฟล์ | ไม่มี | ทั้งฟีเจอร์ (Out of scope เดิม) | ตรง | `src/lang` |
| Undo | จำกัด 20 ครั้ง | มี | | เรานำหน้า: ค่าเริ่มต้น 200 | `store/rootReducer.ts` |
| ค้นหา | ฉากและ navigator | มี | ไม่มีตัวกรองบน canvas | เรานำหน้า: Command Palette | `AppToolbar.tsx` |
| ลากไฟล์เข้า import | มี | มี | | ตรง | `App.tsx` |
| Reload asset อัตโนมัติ | มี | บางส่วน | ยังไม่ได้ตรวจว่าครอบคลุมแค่ไหน | ตรง | `lib/project/watchProject.ts` |

**มีเฉพาะเรา:** Font Manager, UI Editor, Database, Flash to Cartridge, MCP, budget meter, C# editor พร้อม analyzer, การแปลภาษาในเกม, Play ในแท็บเดียว

**ไม่ต้องทำเพราะผูกกับ Game Boy:** SGB, `IfDeviceSGB`, `IfDeviceGBA`, `IfColorSupported`, hUGE และ `.uge`, ข้อจำกัด 4 ช่องเสียง, ตั้งค่าตลับแบบ GB

## เทียบชุดคำสั่ง Logic

GB Studio มี 155 event (เลิกใช้แล้ว 15) ส่วน AGB Studio มี 45 node นับตามกลุ่มแรกของแต่ละ event โดยจับคู่ข้อความ

| กลุ่ม GB Studio | จำนวน | ที่เรามีใน Logic Graph | ใช้กับ GBA |
|---|---|---|---|
| Actor | 38 | Compare Tag, Destroy, Self; ไม่มี move, sprite, animation, hide, emote, projectile | ตรง, emote และ projectile ต้องดูทรัพยากร |
| Control flow | 24 | Branch; ไม่มี loop, switch, thread, lock, call script | ตรง |
| Variables | 16 | Get, Set, Add; ไม่มี array, flag | ตรง |
| Dialogue | 9 | Show Text | ตรง |
| Engine fields | 8 | ไม่มี | ตรง (ค่า runtime ของเรา) |
| Scene | 7 | Load Scene; ไม่มี push/pop, เปลี่ยน tile | ตรง |
| Camera | 7 | Shake Camera | ตรง |
| Timer | 6 | ไม่มี (รวม Wait) | ตรง |
| Screen | 6 | ไม่มี (fade, overlay) | ปรับ: ใช้ brightness, blend, BG หรือ window |
| Colour | 6 | ไม่มี | ปรับ: bank 16 สี, สี 15 บิต |
| Save data | 5 | Save Game, Checkpoint; ไม่มี load, clear, peek | ตรง |
| Music and sound | 5 | Play Sound, Play Music, Stop Music | ตรง |
| Math | 4 | Arithmetic, logic, Compare; ไม่มี RNG | ตรง |
| Input | 4 | ค่าปุ่ม; ไม่มี await, attach | ตรง |
| Misc | 3 | Comment; ไม่มี raw script | ปรับ: native C++ หรือ VM op |
| ไม่มีกลุ่ม (Label, Link, Print) | 7 | ไม่มี | Link ปรับ: Out of scope เดิม |

เรามีกลุ่ม Physics 9 node ซึ่ง GB Studio ไม่มีกลุ่มนี้

## ลิขสิทธิ์: ลอกอะไรได้บ้าง

| ส่วน | สถานะ | ทำอย่างไร |
|---|---|---|
| แนวคิด flow และ UI | ไม่ได้ลอกโค้ด | เรียนรู้และออกแบบเองได้ |
| โค้ดในตัวโปรแกรม | MIT (`LICENSE`) | ถ้าคัดลอก ต้องเก็บประกาศลิขสิทธิ์และเพิ่มใน `docs/THIRD_PARTY.md` แต่ส่วนใหญ่ต้องเขียนใหม่อยู่ดี เพราะเราใช้ C#/Avalonia |
| รูปใน template, เพลง, เสียง, ฟอนต์ PublicPixel | **ไม่พบเงื่อนไข** | ห้ามนำมาใช้ มีเครดิตผู้แต่ง แต่ไม่พบการให้สิทธิ์ และ `docs/user/licence-for-your-games.md` ของเราระบุว่า template ไม่มีของบุคคลที่สาม |
| hUGEDriver, GBDK, GBVM | ยังไม่ทราบ | ไม่อยู่ใน clone หรือไม่มีข้อความลิขสิทธิ์ และไม่ควรต้องใช้กับ GBA |

ยังไม่ได้ตรวจเงื่อนไขบนเว็บ gbstudio.dev

## ลำดับที่แนะนำ

ความเห็นเบื้องต้น รอเจ้าของตัดสินใจ

| ลำดับ | งาน | เหตุผล | ขนาด |
|---|---|---|---|
| 1 | เลือกจาก dropdown แทนการพิมพ์ใน Logic Graph, Link และ Scene Settings | ผิดพลาดน้อยลงทันที ไม่กระทบรูปแบบไฟล์ | เล็ก |
| 2 | คลิกเพื่อวาง actor และ trigger ใน Scene Editor | ลดขั้นตอนที่ใช้บ่อยที่สุด | เล็ก |
| 3 | เพิ่ม node ที่ engine รองรับแล้ว: Wait, Show Choice, Fade, Camera, Move, Random, Load Save | C# API มีอยู่แล้วเป็นส่วนใหญ่ | กลาง |
| 4 | ช่อง script บนวัตถุใน Inspector | แก้เรื่อง logic อยู่ห่างจากวัตถุ | กลาง ถึงใหญ่ |
| 5 | ทาสี collision ใน Scene Editor, Navigator ทั้งโปรเจกต์, รายการจุดที่ใช้ตัวแปรและ prefab | ลดการสลับหน้า | กลาง |
| 6 | World canvas ที่แก้ไขฉากได้ในตัว, ลูกศรจาก event, โน้ตบนแผนที่ | เปลี่ยนวิธีทำงานมากที่สุด จึงควรทำหลังพื้นฐานนิ่ง | ใหญ่ |
| ภายหลัง | Piano roll, Export เว็บ, Plugin, ภาษา UI | งานใหญ่ และไม่ใช่ปัญหา flow หลัก | ใหญ่ |

คำถามที่ควรถามเจ้าของก่อนเริ่ม: Logic Graph จะคงแบบ node ไว้หรือเพิ่มแบบรายการ event แบบ GB Studio ถ้าจะเพิ่ม ทั้งสองแบบต้องแปลงกันได้ หรือเลือกแบบเดียวต่อ script
