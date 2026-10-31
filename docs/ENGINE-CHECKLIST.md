# AGB Studio · Engine + Editor Master Checklist

- สถานะ: **ยังไม่เริ่ม build** รอผู้ใช้สั่งเริ่ม (ห้ามเขียนโค้ด engine/editor ก่อนหน้านั้น)
- ฉบับ: 0.1 · 2026-10-31
- ขอบเขต (scope): ดีไซน์ใน Figma file `givb0hmQYmmaOy7p7DGtb2` page "GBA GameEngine Project" (`3400:3217`) sections "AGB Studio · Components", "AGB Studio · Editor Map", "AGB Studio · Screens", "AGB Studio · Brand"
- สถาปัตยกรรม: blueprint `D:\GBA-Research\agb-studio-blueprint.html` (published: https://claude.ai/artifact/DnpkUdKGZWyD7EK4L5qpvW)
- อ้างอิงรูปแบบ (ศึกษาเท่านั้น ห้ามคัดลอก): `D:\GBA-Research\pokeemerald`
- Inventory นี้อ่านจาก Figma จริงเมื่อ 2026-10-31 ผ่าน Talk To Figma fork (read-only): 75 windows ใน Screens, 24 เมนู, ปุ่ม 35 + 52 รายการใน Editor Map, component 47 ชิ้น, token สี 29 ตัวต่อธีม, prototype 120 links

## Phase overview

| Phase | ชื่อ | เข้าได้เมื่อ | ทำคู่ขนานกับ |
|---|---|---|---|
| 0 | Environment and toolchain check | ผู้ใช้สั่งเริ่ม build | - |
| 1 | Open decisions and foundations | Phase 0 ผ่าน | - |
| 2 | Runtime: Platform + Core services | Phase 1 ผ่าน | 10 |
| 3 | Runtime: Rendering and media | Phase 2 ผ่าน | 10 |
| 4 | Runtime: Game framework | Phase 3 ผ่าน | 10 |
| 5 | World model: map / layout / tileset / metatile | Phase 4 ผ่าน | 6, 10 |
| 6 | Scripting: 3 tiers + Event Script VM | Phase 4 ผ่าน | 5, 7, 10 |
| 7 | Asset pipeline + build pipeline (hash cache) | Phase 2 ผ่าน | 3-6, 10 |
| 8 | Font manager + Thai text | Phase 3 และ 7 ผ่าน | 9 |
| 9 | Database + Localization | Phase 6 (tier 3) และ 7 ผ่าน | 8 |
| 10 | Editor shell + design system | Phase 1 ผ่าน (D-01 ตัดสินแล้ว) | 2-9 |
| 11 | Editor windows (ทุกหน้าต่างใน Figma) | Phase 10 ผ่าน และ runtime ของหน้าต่างนั้นพร้อม | 12-14 |
| 12 | Play / Simulate mode | Phase 7 และ 10 ผ่าน | 11, 13 |
| 13 | Dev tools (Debugger, profiler, budgets) | Phase 12 ผ่าน | 11, 14 |
| 14 | External IDE support | Phase 6 (tier 1) และ 10 ผ่าน | 11-13 |
| 15 | Genre kits, templates, tutorials, samples | Phase 4-9 และ 11 ผ่าน | 16 |
| 16 | Packaging and release | ทุก phase และทุก gate ผ่าน | - |

---

## 1. Scope guard

**ดีไซน์ใน Figma คือขอบเขตของงาน** (ผู้ใช้กำหนด: checklist ต้องคุมงานให้อยู่ในสิ่งที่ออกแบบไว้ใน Figma เสมอ)

1. ทุก feature item ต้องระบุหน้าต่างใน Figma ที่มัน implement (page, dialog, popover, menu หรือ state) ด้วยรหัส `T-...` จากหัวข้อ 4 หรือชื่อ frame ใน Figma
2. ห้ามสร้างสิ่งที่ไม่มีใน Figma ยกเว้น **infrastructure ที่ดีไซน์ต้องใช้** เช่น build system, file formats, CI, test harness, emulator integration, debug channel, logging, installer ทุก infrastructure item ต้องเขียนว่า `(infra: <ดีไซน์ไหนต้องใช้>)`
3. Feature ที่มีใน blueprint แต่ไม่มีใน Figma **ไม่ถือว่าอยู่ใน scope** จนกว่าจะผ่านกฎ Scope change (ดูรายการใน 1.3)
4. ถ้าพบว่า Figma ขัดกันเอง หรือขัดกับ blueprint ให้หยุดและถามผู้ใช้ ห้ามเดาเอง (รายการที่พบแล้วอยู่ใน 1.4)

### 1.1 Scope change rule

- [ ] กฎนี้ถูกอ่านและยอมรับโดยทุก session ที่ทำงานกับ checklist นี้ (ติ๊กเมื่อผู้ใช้ยืนยันตอนเริ่ม build)

ขั้นตอนเมื่อมีของใหม่ที่ไม่อยู่ใน Figma:
1. หยุดงานส่วนนั้น บันทึกใน "Scope log" (หัวข้อ 8)
2. ส่งให้ผู้ใช้ตัดสิน (ถามใน chat หรือ SendMessage ไปยัง session หลัก) ห้ามสร้างก่อนได้คำตอบ
3. ถ้าผู้ใช้อนุมัติ: ออกแบบใน Figma ก่อน (โดย design session, ใช้ Talk To Figma fork เท่านั้น) แล้วเพิ่มรหัส `T-...` ใหม่ในหัวข้อ 4
4. จากนั้นจึงเพิ่ม item ใน phase ที่เกี่ยวข้องและเริ่ม build
5. ถ้าผู้ใช้ไม่อนุมัติ: ย้ายไป "Out of scope / not now"

### 1.2 Out of scope / not now

สิ่งเหล่านี้ห้ามสร้างจนกว่าจะผ่าน Scope change rule:

- ทุกอย่างใน section "Concepts" ของ Figma และ logo options อื่นนอกจาก "Plus Pad"
- Editor บน macOS / Linux / web / mobile (Figma ใช้ path แบบ Windows, Show in Explorer, Alt+F4) จนกว่า D-10 จะบอกเป็นอย่างอื่น
- แปล UI ของตัว editor เป็นภาษาอื่น (UI ใน Figma เป็นภาษาอังกฤษ) ภาษาไทยรองรับในเนื้อหาเกมและช่องกรอกข้อความ
- Link cable / multiplayer / netplay (blueprint มี "Link" เป็น input source ของ command bus แต่ไม่มี UI ใน Figma)
- UI สำหรับบันทึก replay และ test runner สำหรับผู้ใช้ (blueprint section 13) ส่วน test runner ภายในของทีมเป็น infra ได้
- Seamless streaming ข้ามแมพที่เชื่อมติดกันแบบ pokeemerald (World Map ใน Figma มีแค่ link แบบประตู / trigger / goal)
- Flag แบบ Daily ที่ใช้ RTC และฮาร์ดแวร์เสริมของตลับ (RTC, rumble, tilt, solar), SIO, multiboot
- Save ระหว่างกลาง cutscene script
- Video mode 4 และ 5 (Video Mode popover มีแค่ Mode 0-3)
- Module แบบ IT / S3M, MIDI, MP3, OGG (Figma: รองรับ MOD / XM / WAV และปฏิเสธ MP3) จนกว่า D-14 จะบอกเป็นอย่างอื่น
- Kanji และ CJK นอกเหนือจาก Japanese kana (Font Manager มีชุด "Japanese kana")
- UI ตั้งขนาด pool (blueprint บอกว่าตั้งใน Project Settings แต่ Figma ไม่มีช่องนี้)
- รูปแบบบีบอัดอื่นนอกจาก LZ77 (blueprint: ZX0 / LZ4 หลังวัดผลบนเครื่องจริงเท่านั้น)
- ระบบ plugin ของ editor, asset store, cloud sync, บัญชีผู้ใช้, telemetry, ทำงานร่วมกันแบบ real-time
- Lua scripting, GDB console UI นอกเหนือจากที่หน้า Debugger แสดง
- หน้าจอในเกมที่ยังไม่ได้ออกแบบ (boot splash art, title screen, pause menu, options menu) ดู 1.4 ข้อ 6

### 1.3 Blueprint items that have no Figma UI (need a decision before they are built)

- [ ] Pool sizes per project (blueprint 02): ใช้ค่าคงที่ในไฟล์โปรเจกต์ไปก่อน หรือออกแบบ UI เพิ่ม
- [ ] Genre kit "Menu / UI kit" (blueprint 14): ไม่มีใน Figma เป็นชื่อแยก แต่ template ต้องใช้ title screen และ pause menu
- [ ] Grid movement controller (blueprint 08 / 14): Component Picker มีแค่ "Top-down Mover · 8-way movement"
- [ ] Event Script VM state store แบบ Daily (RTC) และ save กลาง script (blueprint 07)
- [ ] Seamless map streaming (blueprint 08)
- [ ] Replay / AI / test input sources ของ command bus นอกเหนือจาก Player และ AI ที่ template ต้องใช้ (blueprint 09)

### 1.4 Design issues found while reading Figma (confirm or fix in Figma before the related phase)

- [ ] 1. Editor Map board 1: ภาพจำลองแสดงแท็บ "PlayerController.gs" แต่ Screens และ legend ใช้ "PlayerController.cs"
- [ ] 2. Analyzer rule IDs "GAE001"-"GAE006" น่าจะมาจากชื่อเก่า "GBA GameEngine" (ดู D-07)
- [ ] 3. Editor Map board 4 มี "File menu › New Project (Empty)" แต่ File menu ใน board 2 มีแค่ "New Project…" และ New Project dialog ไม่มีการ์ด "Empty"
- [ ] 4. New Project dialog แสดง template 6 แบบ แต่ Launcher · Templates แสดง 8 แบบ (เพิ่ม Beat 'em up, Racing (Mode 7)) (ดู D-09)
- [ ] 5. Opening Project แสดงไฟล์ "SkyHopper.agbproj" แต่ blueprint ใช้ `project.json`; Figma ยังใช้นามสกุล `.scene`, `.map`, `.font`, `.db`, `.dlg`, `.sfx` (ดู D-03)
- [ ] 6. หน้าจอในเกมที่ Figma อ้างถึงแต่ยังไม่ได้ออกแบบ: boot splash "AGB Studio logo · 2 s" (Project Settings · General), "Title screen and pause menu" (Launcher · Templates, Platformer), options menu สำหรับ remap ปุ่ม (Project Settings · Input) ต้องออกแบบใน Figma ก่อนสร้าง
- [ ] 7. Import CSV caption พูดถึง "CSV / Google Sheets" แต่ UI แสดงแค่ไฟล์ CSV (ดู D-13)
- [ ] 8. Build ROM dialog เรียง "C# -> C++" ก่อน "Convert assets" ส่วน blueprint section 11 แปลง asset ก่อน (ไม่ block: UI แสดงตามลำดับ Figma, pipeline อาจรันคู่ขนาน)
- [ ] 9. Script API namespace ในโค้ดตัวอย่างคือ `using GBA;` (ดู D-08)

---

## 2. How to use this checklist

### 2.1 รูปแบบ item

- `- [ ]` ยังไม่เสร็จ, `- [x]` เสร็จพร้อมหลักฐาน (GitHub task list มีแค่สองสถานะ) ถ้าติดขัดให้เติม `(blocked: <เหตุผล>)` ท้ายบรรทัด
- รหัส: `D-xx` decision, `T-xx` traceability (หน้าต่างใน Figma), `Px.yy` item ใน phase x, `G-xx` cross-cutting gate
- ทุก item ใน phase มี `Verify:` (วิธีตรวจ) และใช้ Definition of Done มาตรฐาน (2.4) บาง item มี `Done:` เพิ่มเฉพาะเรื่อง

### 2.2 ใครติ๊ก

- **Implemented / phase items:** session หรือคนที่ทำงานนั้นเสร็จเป็นคนติ๊ก พร้อมหลักฐาน
- **Matches Figma (visual check):** คนที่ไม่ได้เขียนส่วนนั้น (session อื่น หรือผู้ใช้) เป็นคนติ๊ก หลังทำ SHOT procedure (2.5)
- **Entry / exit gates และ decisions:** ติ๊กได้เมื่อผู้ใช้ยืนยันใน chat เท่านั้น (`user:<วันที่>`)
- ห้ามติ๊กโดยไม่มีหลักฐาน ถ้าของที่ติ๊กแล้วพังภายหลัง ให้เอาติ๊กออกและบันทึกใน Scope log (หัวข้อ 8) ห้ามเอาออกเงียบ ๆ

### 2.3 กฎหลักฐาน (evidence)

ทุกบรรทัดที่ติ๊กต้องต่อท้ายด้วยหลักฐานในรูปแบบ:

```
- [x] P2.04 Commit queues ... — evidence: test:CommitQueue.Overflow, rom:build/tests/core_commit.gba@3f2a1c0, commit:3f2a1c0 (2026-11-05)
```

| ชนิด | ตัวอย่าง |
|---|---|
| `test:` | ชื่อ unit test / ROM test ที่ผ่านใน CI |
| `rom:` | path ของ ROM ที่ build + commit hash |
| `shot:` | `docs/evidence/app/<T-ID>.png` เทียบกับ `docs/evidence/figma/<T-ID>.png` |
| `report:` | `build/report.json` ของ build ที่อ้าง |
| `clip:` | วิดีโอหรือ GIF สั้นจาก emulator / editor |
| `commit:` / `pr:` | hash หรือลิงก์ PR |
| `doc:` | path ของเอกสาร |
| `user:` | วันที่ผู้ใช้ยืนยันใน chat |

### 2.4 Definition of Done มาตรฐาน (ใช้กับทุก item)

1. โค้ดหรือเนื้อหา merge แล้ว build ผ่าน ไม่มี warning ใหม่
2. การตรวจใน `Verify:` ผ่าน และแนบหลักฐานแล้ว
3. Feature item อ้างหน้าต่างใน Figma และไม่เพิ่มอะไรนอกเหนือจากนั้น
4. ไม่มี emoji ในโค้ด, UI string, เอกสาร, commit message (G-EMOJI)
5. เอกสารที่ผู้ใช้เห็นอัปเดตแล้วถ้าพฤติกรรมเปลี่ยน
6. ผ่าน G-LEGAL (ไม่มีโค้ดหรือ asset ของ pokeemerald)

### 2.5 คำย่อวิธีตรวจ (Verify)

| คำย่อ | ความหมาย |
|---|---|
| `UT` | unit test บนเครื่อง PC รันใน CI |
| `RT` | ROM test: test ROM รันแบบไม่มีจอใน mgba-rom-test แล้วจบด้วยรหัส pass/fail ใน CI |
| `EMU` | ROM บูตและทำงานถูกใน mGBA และ VBA-M (มีภาพหรือคลิป) |
| `HW` | ทดสอบบนเครื่องจริงผ่าน flash cart (ถ้ายังไม่มีเครื่อง ให้เขียน `n/a until hardware`) |
| `BUD` | ตัวเลขงบจาก `report.json` หรือ profiler (ROM, VRAM, OAM, palette, IWRAM, EWRAM, เวลาเฟรม) |
| `SHOT` | ภาพหน้าต่างของ editor เทียบกับ frame ใน Figma ตามขั้นตอนด้านล่าง |
| `E2E` | test อัตโนมัติที่กด UI ของ editor จริง |
| `REV` | review ตามกฎที่ระบุใน item |
| `DOC` | มีหน้าเอกสารและลิงก์ถึงได้ |
| `USER` | ผู้ใช้ยืนยันใน chat |

**SHOT procedure (matches Figma):**
1. เปิด fixture project "Sky Hopper" (P1.03) เพื่อให้ข้อมูลในหน้าต่างตรงกับใน Figma
2. ตั้งขนาดหน้าต่างให้เท่า frame ใน Figma (page 1440 x 900, dialog / popover ตามขนาดที่ระบุในหัวข้อ 4)
3. Export frame จาก Figma เป็น PNG 1x ผ่าน Talk To Figma fork แบบ read-only (ห้ามใช้ official Figma MCP, ห้ามแก้ node) เก็บที่ `docs/evidence/figma/<T-ID>.png`
4. จับภาพ editor ที่ 100% DPI เก็บที่ `docs/evidence/app/<T-ID>.png`
5. เทียบแบบวางข้างกันและ overlay: layout และระยะห่างคลาดได้ไม่เกิน 2 px, สีตรงกับ token, ฟอนต์ ไอคอน ข้อความ และ state ตรงกัน
6. ความต่างที่เหลือต้องแก้ หรือให้ผู้ใช้ยอมรับและบันทึกใน Scope log
7. frame ธีมสว่าง (T-L01 ถึง T-L03) ตรวจในธีม Light

### 2.6 รายงาน scope drift

- เมื่อเจอสิ่งที่ต้องทำแต่ไม่อยู่ใน Figma หรือ Figma ขัดกันเอง: เพิ่มแถวใน Scope log (หัวข้อ 8) แล้วแจ้งผู้ใช้ทันที อย่าทำต่อในส่วนนั้น
- ทุก commit / PR ต้องระบุ `T-...` หรือ `infra: <เหตุผล>` (G-SCOPE) reviewer ปฏิเสธงาน feature ที่ไม่มีรหัสอ้างอิง
- ทุกครั้งที่ Figma เปลี่ยน ให้ design session อัปเดตหัวข้อ 4 ก่อน แล้วจึงเริ่ม build ส่วนที่เปลี่ยน

---

## 3. Open decisions (settle with the user in Phase 1)

> Scope: ข้อเหล่านี้ยังไม่ได้ตัดสิน ห้ามถือว่าตัดสินแล้ว บันทึกคำตอบของผู้ใช้ลง `docs/DECISIONS.md` (ทำตอนเริ่ม build) แล้วจึงติ๊ก

- [ ] **D-01 Editor UI tech stack.** ข้อจำกัดจากดีไซน์: ต้องฝัง mGBA core (C), ต้องเรียก Roslyn (.NET) ได้, ธีม token มืด/สว่าง, docking panels, node graph, tracker view, code editor ที่มี analyzer ตัวเลือกที่ต้องเทียบ: .NET (Avalonia / WPF / WinUI), web UI ใน shell (Electron / Tauri) + .NET sidecar, Qt, Dear ImGui
- [ ] **D-02 C#-like scripting front end.** ยืนยันแนว blueprint: Roslyn parse + analyzer, แปล C# subset -> C++20, async -> bytecode; กำหนดรายการ feature ของ C# ที่รองรับ, ชนิด `fixed` (Figma: 24.8) และ `fixed2`, generics ที่อนุญาต (`Get<T>()`, `Is<T>()`)
- [ ] **D-03 Project file format.** ชื่อและนามสกุลไฟล์ (`.agbproj` ใน Figma vs `project.json` ใน blueprint; `.scene`, `.map`, `.font`, `.db`, `.dlg`, `.sfx`, `.graph`), เป็น JSON ข้อความที่ diff ได้ทั้งหมดหรือไม่, versioning ของ format
- [ ] **D-04 Repo layout.** ตำแหน่ง repo (เสนอ `D:\devkitPro\AGB-Studio`), โครง (`runtime/`, `toolchain/`, `editor/`, `templates/`, `samples/`, `docs/`, `tests/`), git hosting, branch policy
- [ ] **D-05 Licences.** licence ของ AGB Studio, licence ของ runtime ที่ติดไปในเกมของผู้ใช้ (ผู้ใช้ขายเกมได้หรือไม่, ต้องให้เครดิตหรือไม่), การปฏิบัติตาม licence ของ third-party (mGBA, libtonc, maxmod, libgba ถ้าใช้, Roslyn, ฟอนต์, Thai dictionary)
- [ ] **D-06 Emulator integration.** วิธีฝัง mGBA core (libmgba DLL, เวอร์ชัน), บทบาทของ VBA-M ที่มีอยู่ (`D:\devkitPro\visualboyadvance-m.exe`) เช่นใช้ตรวจความเข้ากันได้, อนุญาตให้ติดตั้ง mGBA + mgba-rom-test สำหรับ CI
- [ ] **D-07 Analyzer rule prefix.** เก็บ "GAE001"-"GAE006" ตาม Figma หรือเปลี่ยน (เช่น "AGB001") ถ้าเปลี่ยนต้องแก้ Figma ก่อน
- [ ] **D-08 Script API namespace.** เก็บ `using GBA;` (ใช้เชิงบรรยาย) หรือเปลี่ยน ถ้าเปลี่ยนต้องแก้ Figma ก่อน
- [ ] **D-09 Templates and kits for 1.0.** Figma มี 8 template (Platformer, Top-down Adventure, Turn-based RPG, Shoot 'em up, Puzzle, Visual Novel, Beat 'em up, Racing (Mode 7)) ตัวไหนต้องมีใน 1.0 และลำดับการสร้าง
- [ ] **D-10 Editor OS target.** Windows อย่างเดียวสำหรับ 1.0 หรือไม่
- [ ] **D-11 Logic Graph <-> C# sync.** Figma บอก "Graph ⇄ C# in sync": Graph -> C# แน่นอน ส่วน C# -> Graph ต้องรองรับแค่โค้ดที่ graph สร้าง หรือ C# ทั่วไป
- [ ] **D-12 Cartridge flashing hardware.** Figma แสดง GBxCart RW ผ่าน USB และการคัดลอกลง SD ของ flash cart (Launcher พูดถึง EZ-Flash / EverDrive) อุปกรณ์ไหนรองรับใน 1.0
- [ ] **D-13 Online services.** ปลายทางของ Help › Report a Bug…, Check for Updates, Website, Community, Release notes และจะรองรับ Google Sheets ใน Import CSV หรือไม่
- [ ] **D-14 Audio formats and rates.** MOD / XM (Figma) เพิ่ม IT ตาม blueprint หรือไม่; ค่าเริ่มต้น Maxmod 21 kHz 8 ch และ WAV -> 11 kHz mono 8-bit ตาม Figma
- [ ] **D-15 Hot reload.** Figma: "Hot reload in Play mode" และ "Hot reload: PlayerController updated in the running game" ต้องเลือกวิธี (เช่น data / bytecode / ค่า Inspector reload สด, ส่วน C++ behaviour ต้อง rebuild แล้วคืน state) และขอบเขตที่สัญญากับผู้ใช้
- [ ] **D-16 Meaning of "Simulate".** Figma แสดง "Simulating · mGBA" เท่านั้น: Simulate = รัน ROM จริงใน mGBA core ใน editor หรือมี simulation ฝั่ง PC แยกต่างหาก
- [ ] **D-17 Thai data sources.** word list สำหรับตัดคำ (Figma: "Dictionary · 30k words") และฟอนต์ไทยตั้งต้น (Figma: NotoSansThai-Regular.ttf) พร้อม licence
- [ ] **D-18 Editor viewport renderer.** Scene / Tilemap / Sprite preview ใช้ renderer ของ editor เองที่เลียนแบบ GBA หรือให้ mGBA วาด เกณฑ์: ภาพต้องตรงกับ ROM แบบ pixel ต่อ pixel
- [ ] **D-19 Distribution.** ชนิด installer, จะรวม devkitPro มาด้วยหรือให้ผู้ใช้ติดตั้งเอง (Figma: Preferences · Paths ตรวจหา devkitPro), code signing, ช่องทาง update
- [ ] **D-20 Template and sample content.** ใครทำ art / เพลง / ข้อความต้นฉบับของ template และ sample projects (ต้องเป็นของใหม่ทั้งหมด) และออกแบบหน้าจอในเกมที่ขาด (1.4 ข้อ 6) เมื่อไร
- [ ] **D-21 C# source-level debugging.** Debugger ใน Figma หยุดที่ `PlayerController.cs:19` และแสดง call stack ชื่อ C# ต้องเลือกวิธี (#line + DWARF + mGBA GDB stub หรืออื่น ๆ) และยอมรับความเสี่ยงหรือจำกัดขอบเขตใน 1.0

---

## 4. Traceability: every Figma window, menu, state and component

> Scope: รายการนี้คือขอบเขตทั้งหมด ถ้าไม่อยู่ในนี้ ห้ามสร้าง (ยกเว้น infra) แต่ละหน้าต่างมีสองช่อง: **implemented** (เปิดได้จากทุกที่ที่ Figma ระบุ และ item ฟังก์ชันใน Phase 11 ผ่าน) และ **matches Figma** (SHOT ผ่าน)
>
> Editor Map board 3 ("หน้าต่างทั้งหมดที่ต้องออกแบบ · 54 รายการ") = T-P01..P19 + T-D01..D12 + T-O01..O07 + T-M01..M16 ส่วน settings tabs, round 2, light theme และ board 4 อยู่ในหัวข้อย่อยถัดไป

### 4.1 Pages (Editor Map board 3 · 19)

- **T-P01 Scene Editor** · 1440 x 900 · from: default workspace
  - [ ] implemented
  - [ ] matches Figma
- **T-P02 Play Mode** · 1440 x 900 · from: #11 Play, #15 Tab Game, Build › Play, Build › Play from Here, Build › Build & Run in Emulator, Build Report › Run in mGBA
  - [ ] implemented
  - [ ] matches Figma
- **T-P03 Tilemap Editor** · 1440 x 900 · from: #16 Tab Forest_01.map, Scene › Background Layers…, Create › Tileset / Tilemap
  - [ ] implemented
  - [ ] matches Figma
- **T-P04 Script Editor** · 1440 x 900 · from: #17 Tab PlayerController.cs, #31 Script component, Create › C# Script, Actor Context Menu › Edit Script, Component Menu › Edit Script
  - [ ] implemented
  - [ ] matches Figma
- **T-P05 Console panel** · 860 x 250 · from: #21 Console tab
  - [ ] implemented
  - [ ] matches Figma
- **T-P06 Animation panel** · 860 x 250 · from: #22 Animation tab
  - [ ] implemented
  - [ ] matches Figma
- **T-P07 Database** · 1440 x 900 · from: #23 Database tab, Tools › Database, Create › Data Table
  - [ ] implemented
  - [ ] matches Figma
- **T-P08 Sprite Editor** · 1440 x 900 · from: #24 Asset (double-click), Create › Sprite, Asset Context Menu › Open, Animation List › Edit Animations…
  - [ ] implemented
  - [ ] matches Figma
- **T-P09 Font Manager** · 1440 x 900 · from: #26 Font asset, Tools › Font Manager, Create › Font
  - [ ] implemented
  - [ ] matches Figma
- **T-P10 Palette Manager** · 1440 x 900 · from: #30 Palette, Assets › Optimize Palettes…, Tools › Palette Manager, Create › Palette, Sprite Editor › Palette slots
  - [ ] implemented
  - [ ] matches Figma
- **T-P11 Build Report** · 1440 x 900 · from: #34 Build status, Build › Build Report, Budget Details › Open Build Report
  - [ ] implemented
  - [ ] matches Figma
- **T-P12 Launcher** · 1440 x 900 · from: app start, File › Open Project…, Help › Getting Started
  - [ ] implemented
  - [ ] matches Figma
- **T-P13 World Map** · 1440 x 900 · from: Scene › World Map
  - [ ] implemented
  - [ ] matches Figma
- **T-P14 Localization** · 1440 x 900 · from: Tools › Localization
  - [ ] implemented
  - [ ] matches Figma
- **T-P15 Audio** · 1440 x 900 · from: Tools › Audio, Create › Sound Effect, Create › Music
  - [ ] implemented
  - [ ] matches Figma
- **T-P16 Dialogue Editor** · 1440 x 900 · from: Tools › Dialogue Editor, Create › Dialogue
  - [ ] implemented
  - [ ] matches Figma
- **T-P17 Logic Graph** · 1440 x 900 · from: Tools › Logic Graph, Create › Logic Graph
  - [ ] implemented
  - [ ] matches Figma
- **T-P18 Scripting API** · 1440 x 900 · from: Help › Scripting API, Script Editor › Open Scripting API
  - [ ] implemented
  - [ ] matches Figma
- **T-P19 Debugger** (Profiler view) · 1440 x 900 · from: #9 Debug, Tools › Debugger
  - [ ] implemented
  - [ ] matches Figma

### 4.2 Dialogs (Editor Map board 3 · 12)

- **T-D01 New Project** · 1180 x 668 · from: File › New Project…, Launcher › New Project, Launcher · Templates › Use this template
  - [ ] implemented
  - [ ] matches Figma
- **T-D02 New Scene** · 520 x 470 · from: Scene › New Scene…, Scene Switcher › New Scene…, World Map › New Scene
  - [ ] implemented
  - [ ] matches Figma
- **T-D03 Scene Settings** · 600 x 646 · from: #18 Scene size chip, Scene › Scene Settings…
  - [ ] implemented
  - [ ] matches Figma
- **T-D04 Import Asset** · 900 x 460 · from: File › Import Asset…, Assets › Import…, Sprite Editor › Drop zone, Palette Manager › Import .pal / .gpl, Audio › Music +
  - [ ] implemented
  - [ ] matches Figma
- **T-D05 Export ROM** · 560 x 488 · from: File › Export ROM (.gba)…
  - [ ] implemented
  - [ ] matches Figma
- **T-D06 Build ROM** (progress) · 600 x 440 · from: #10 Build ROM, Build › Build ROM, Build Report › Build again
  - [ ] implemented
  - [ ] matches Figma
- **T-D07 Flash to Cartridge** · 560 x 368 · from: Build › Flash to Cartridge…, Build Report › Flash to cartridge
  - [ ] implemented
  - [ ] matches Figma
- **T-D08 Project Settings** (Input tab) · 900 x 620 · from: File › Project Settings…
  - [ ] implemented
  - [ ] matches Figma
- **T-D09 Preferences** (Emulator tab) · 900 x 620 · from: #35 Emulator status, File › Preferences…, Launcher › Settings tab
  - [ ] implemented
  - [ ] matches Figma
- **T-D10 Keyboard Shortcuts** · 760 x 390 · from: Help › Keyboard Shortcuts
  - [ ] implemented
  - [ ] matches Figma
- **T-D11 About** · 460 x 304 · from: Help › About AGB Studio
  - [ ] implemented
  - [ ] matches Figma
- **T-D12 Unsaved Changes** · 460 x 262 · from: File › Exit, closing the app or opening another project with unsaved files
  - [ ] implemented
  - [ ] matches Figma

### 4.3 Settings tabs (9 frames; with T-D08 and T-D09 this makes 21 dialogs)

- **T-S01 Project Settings · General** · 900 x 620
  - [ ] implemented
  - [ ] matches Figma
- **T-S02 Project Settings · Save data** · 900 x 620
  - [ ] implemented
  - [ ] matches Figma
- **T-S03 Project Settings · Build** · 900 x 620
  - [ ] implemented
  - [ ] matches Figma
- **T-S04 Project Settings · Scripting** · 900 x 620
  - [ ] implemented
  - [ ] matches Figma
- **T-S05 Project Settings · Localization** · 900 x 620
  - [ ] implemented
  - [ ] matches Figma
- **T-S06 Preferences · Appearance** · 900 x 620
  - [ ] implemented
  - [ ] matches Figma
- **T-S07 Preferences · Editor** · 900 x 620
  - [ ] implemented
  - [ ] matches Figma
- **T-S08 Preferences · Paths** · 900 x 620
  - [ ] implemented
  - [ ] matches Figma
- **T-S09 Preferences · Shortcuts** · 900 x 620
  - [ ] implemented
  - [ ] matches Figma

### 4.4 Popovers (Editor Map board 3 · 7)

- **T-O01 Scene Switcher** · 320 x 356 · from: #8 Breadcrumb
  - [ ] implemented
  - [ ] matches Figma
- **T-O02 Video Mode** · 380 x 400 · from: #12 Video mode chip
  - [ ] implemented
  - [ ] matches Figma
- **T-O03 Grid & Snap** · 300 x 328 · from: #19 Grid chip
  - [ ] implemented
  - [ ] matches Figma
- **T-O04 Asset Picker** · 440 x 368 · from: #28 Sheet dropdown, Add to Scene › Prefab…, Tilemap Editor › Tileset dropdown
  - [ ] implemented
  - [ ] matches Figma
- **T-O05 Component Picker** · 400 x 464 · from: #32 Add Component, Actor Context Menu › Add Behaviour…
  - [ ] implemented
  - [ ] matches Figma
- **T-O06 Budget Details** · 420 x 398 · from: #33 GBA Budget
  - [ ] implemented
  - [ ] matches Figma
- **T-O07 Command Palette** · 640 x 325 · from: Edit › Command Palette…, Edit › Find in Project…, Ctrl+K
  - [ ] implemented
  - [ ] matches Figma

### 4.5 Menus (Editor Map board 2 · 16 and board 4 · 8)

Menu ใน Figma อยู่ใน Editor Map board 2 และ 4 (ไม่ใช่ใน Screens) ตรวจ SHOT กับ board นั้น รายการที่ไม่ได้เปิดหน้าต่างทำงานทันที (เช่น Save, Copy, Reimport)

- **T-M01 File menu** (#1): New Project…, Open Project…, Open Recent, Save, Save All, Import Asset…, Export ROM (.gba)…, Project Settings…, Preferences…, Exit
  - [ ] implemented
  - [ ] matches Figma
- **T-M02 Edit menu** (#2): Undo, Redo, Cut, Copy, Paste, Duplicate, Delete, Select All, Find in Project…, Command Palette…
  - [ ] implemented
  - [ ] matches Figma
- **T-M03 Assets menu** (#3): Create, Import…, Reimport, Show in Explorer, Find Unused Assets, Optimize Palettes…
  - [ ] implemented
  - [ ] matches Figma
- **T-M04 Scene menu** (#4): New Scene…, Scene Settings…, Set as Start Scene, Add Actor, Background Layers…, World Map
  - [ ] implemented
  - [ ] matches Figma
- **T-M05 Build menu** (#5): Play, Play from Here, Build ROM, Build & Run in Emulator, Flash to Cartridge…, Build Report, Clean Build
  - [ ] implemented
  - [ ] matches Figma
- **T-M06 Tools menu** (#6): Palette Manager, Font Manager, Database, Localization, Audio, Dialogue Editor, Logic Graph, Debugger, Screenshot / GIF
  - [ ] implemented
  - [ ] matches Figma
- **T-M07 Help menu** (#7): Getting Started, Scripting API, Keyboard Shortcuts, Report a Bug…, Check for Updates, About AGB Studio
  - [ ] implemented
  - [ ] matches Figma
- **T-M08 Open Recent** (File › Open Recent): recent projects, Clear Recent
  - [ ] implemented
  - [ ] matches Figma
- **T-M09 Create** (Assets › Create): Sprite, Tileset / Tilemap, Font, Palette, Sound Effect, Music, Data Table, C# Script, Logic Graph, Dialogue
  - [ ] implemented
  - [ ] matches Figma
- **T-M10 Add Actor** (Scene › Add Actor): Empty Actor, Player, Enemy, Pickup, Trigger Zone, Camera Zone, Text Label
  - [ ] implemented
  - [ ] matches Figma
- **T-M11 Add to Scene** (#13 Hierarchy +): the 7 actor types, Tilemap Layer, Prefab…
  - [ ] implemented
  - [ ] matches Figma
- **T-M12 Actor Context Menu** (#14): Rename, Duplicate, Delete, Add Behaviour…, Edit Script, Make Prefab, Focus in View
  - [ ] implemented
  - [ ] matches Figma
- **T-M13 Asset Context Menu** (#25): Open, Rename, Duplicate, Delete, Find Usages, Reimport, Show in Explorer, Create Animation
  - [ ] implemented
  - [ ] matches Figma
- **T-M14 Component Menu** (#27 Inspector …): Copy Component, Paste Values, Reset, Edit Script, Remove Component
  - [ ] implemented
  - [ ] matches Figma
- **T-M15 Zoom** (#20): 100%, 200%, 300%, 400%, Fit to Screen, Pixel-perfect
  - [ ] implemented
  - [ ] matches Figma
- **T-M16 Animation List** (#29): animations of the selected sprite, Edit Animations…
  - [ ] implemented
  - [ ] matches Figma
- **T-M17 Project Context Menu** (Launcher, right-click a project): Open, Show in Explorer, Duplicate, Rename, Remove from list, Delete project…
  - [ ] implemented
  - [ ] matches Figma
- **T-M18 Play Speed** (Play Mode speed chip): 1/4x, 1/2x, 1x, 2x, 4x, Frame step
  - [ ] implemented
  - [ ] matches Figma
- **T-M19 OAM Size** (Sprite Editor): Square 8x8 / 16x16 / 32x32 / 64x64, Wide 16x8 / 32x8 / 32x16 / 64x32, Tall 8x16 / 8x32 / 16x32 / 32x64
  - [ ] implemented
  - [ ] matches Figma
- **T-M20 Add Layer** (Tilemap Editor): Tile layer, Image layer, Rotate / scale layer (Mode 1-2), Collision-only layer
  - [ ] implemented
  - [ ] matches Figma
- **T-M21 Node Context Menu** (Logic Graph, right-click a node): Duplicate, Disable, Group into comment, Collapse, Show generated C#, Delete
  - [ ] implemented
  - [ ] matches Figma
- **T-M22 Scene Context Menu** (World Map, right-click a scene): Open, Rename, Duplicate, Set as start scene, Delete
  - [ ] implemented
  - [ ] matches Figma
- **T-M23 Add to Dialogue** (Dialogue Editor › Add line · choice · command): Line, Choice, Command, Condition, Paste
  - [ ] implemented
  - [ ] matches Figma
- **T-M24 Command** (Add to Dialogue › Command): Go to scene…, Set flag…, Give item…, Play sound…, Wait…, Shake camera…
  - [ ] implemented
  - [ ] matches Figma

### 4.6 Design round 2: extra pages (7)

- **T-P20 Launcher · Templates** · 1440 x 900 · from: Launcher › Templates tab
  - [ ] implemented
  - [ ] matches Figma
- **T-P21 Launcher · Learn** · 1440 x 900 · from: Launcher › Learn tab
  - [ ] implemented
  - [ ] matches Figma
- **T-P22 Scene Editor · Empty project** (empty state) · 1440 x 900 · from: File › New Project (Empty)
  - [ ] implemented
  - [ ] matches Figma
- **T-P23 Debugger · Memory** · 1440 x 900 · from: Debugger › Views › Memory
  - [ ] implemented
  - [ ] matches Figma
- **T-P24 Debugger · Palettes** · 1440 x 900 · from: Debugger › Views › Palettes
  - [ ] implemented
  - [ ] matches Figma
- **T-P25 SFX Designer** · 1440 x 900 · from: Audio › Sound effects +
  - [ ] implemented
  - [ ] matches Figma
- **T-P26 Auto-tile Rules panel** · 860 x 250 · from: Tilemap Editor › Auto-tile rules tab
  - [ ] implemented
  - [ ] matches Figma

### 4.7 Design round 2: extra dialogs (12, includes loading and error states)

- **T-D13 New Sprite** · 520 x 506 · from: Sprite Editor › Sprites +
  - [ ] implemented
  - [ ] matches Figma
- **T-D14 New Script** · 560 x 564 · from: Script Editor › Scripts +, Component Picker › New C# script…
  - [ ] implemented
  - [ ] matches Figma
- **T-D15 New Font** · 560 x 506 · from: Font Manager › Fonts +
  - [ ] implemented
  - [ ] matches Figma
- **T-D16 Add Characters** · 600 x 388 · from: Font Manager › Add characters from text…
  - [ ] implemented
  - [ ] matches Figma
- **T-D17 New Table** · 520 x 440 · from: Database › Tables +
  - [ ] implemented
  - [ ] matches Figma
- **T-D18 Add Field** · 520 x 516 · from: Database › Schema +
  - [ ] implemented
  - [ ] matches Figma
- **T-D19 Import CSV** · 780 x 478 · from: Database › Import CSV
  - [ ] implemented
  - [ ] matches Figma
- **T-D20 Add Language** · 520 x 416 · from: Project Settings › Localization › Add language, Localization › Languages +
  - [ ] implemented
  - [ ] matches Figma
- **T-D21 New Speaker** · 520 x 438 · from: Dialogue Editor › Speakers +
  - [ ] implemented
  - [ ] matches Figma
- **T-D22 Opening Project** (loading state) · 480 x 322 · from: Launcher › Project card, File › Open Recent
  - [ ] implemented
  - [ ] matches Figma
- **T-D23 Build ROM · Failed** (error state) · 600 x 382 · from: Build ROM when a step fails
  - [ ] implemented
  - [ ] matches Figma
- **T-D24 Import Asset · Errors** (error state) · 720 x 436 · from: Import Asset when a file can't be converted
  - [ ] implemented
  - [ ] matches Figma

### 4.8 Design round 2: extra popovers (6)

- **T-O08 Add Node** · 380 x 269 · from: Logic Graph › Right-click canvas, Nodes › Add node…
  - [ ] implemented
  - [ ] matches Figma
- **T-O09 Link Settings** · 360 x 330 · from: World Map › Link chip (click a link)
  - [ ] implemented
  - [ ] matches Figma
- **T-O10 Palette Effect** · 380 x 372 · from: Palette Manager › Fade / Cycle
  - [ ] implemented
  - [ ] matches Figma
- **T-O11 Find & Replace** · 540 x 124 · from: Script Editor › Ctrl+F / Ctrl+H
  - [ ] implemented
  - [ ] matches Figma
- **T-O12 Record Shortcut** · 340 x 264 · from: Preferences › Shortcuts › pencil
  - [ ] implemented
  - [ ] matches Figma
- **T-O13 Tutorial Steps** · 380 x 466 · from: Launcher › Learn › Tutorial Start / Continue
  - [ ] implemented
  - [ ] matches Figma

### 4.9 States (error / empty / loading) and where they are designed

ทุก state ต้องเกิดขึ้นจริงจากเงื่อนไขจริง ไม่ใช่แค่หน้าจอแสดงผล

- [ ] Empty: whole project empty -> T-P22 (Hierarchy "Nothing selected", Inspector "Select an actor or a layer", Assets "No assets yet", viewport "This scene is empty", Add player / Import images)
- [ ] Loading: opening a project -> T-D22 (Read project, Load assets with count, Compile scripts, Open last scene, Cancel)
- [ ] Loading: build progress -> T-D06 (per-step time, per-file progress, log)
- [ ] Loading: flashing a cartridge -> T-D07 (Writing x / y KB, keep connected)
- [ ] Error: build failed -> T-D23 (failed step, skipped steps, clickable errors, Go to first error)
- [ ] Error: import failed -> T-D24 (per-file problem in plain language, fix buttons, files with errors skipped)
- [ ] Error / warning inline: analyzer diagnostic popup with quick fixes -> T-P04
- [ ] Warning: shortcut conflict -> T-S09, T-O12
- [ ] Warning: localization missing / too long -> T-P14 ("missing", "long")
- [ ] Warning: missing glyphs -> T-P09, T-D16 ("3 missing")
- [ ] Warning: CSV row problems -> T-D19
- [ ] Warning: location inside devkitPro -> T-D01 ("A folder outside D:\devkitPro is safer for updates")
- [ ] Status found / not set for tools and paths -> T-S08, T-D09
- [ ] Unsaved changes -> T-D12, dirty dot on Doc Tab

### 4.10 Light theme

- **T-L01 Scene Editor · Light** · 1440 x 900 · from: Preferences › Appearance › Light
  - [ ] implemented
  - [ ] matches Figma
- **T-L02 New Project · Light** · 1180 x 668
  - [ ] implemented
  - [ ] matches Figma
- **T-L03 Preferences · Appearance · Light** · 900 x 620
  - [ ] implemented
  - [ ] matches Figma
- [ ] T-L04 Every other window switches to "AGB Studio · Light" tokens with no hard-coded colours (game pixels and code colours stay as designed) · Verify: `REV` + automated scan for colour literals outside the token file

### 4.11 System windows from Editor Map board 4 (operating-system dialogs, no visual check)

- [ ] T-Y01 Launcher › Open… -> folder picker
- [ ] T-Y02 Localization › Export .csv -> save dialog
- [ ] T-Y03 Script Editor › Open externally -> external editor process at the right file and line
- [ ] T-Y04 Preferences › Paths › Browse… -> file picker

### 4.12 Editor Map board 1: Scene Editor buttons (35)

Verify ทุกข้อด้วย `E2E`: คลิกแล้วหน้าต่างที่ถูกต้องเปิด

- [ ] T-B01 File -> File menu
- [ ] T-B02 Edit -> Edit menu
- [ ] T-B03 Assets -> Assets menu
- [ ] T-B04 Scene -> Scene menu
- [ ] T-B05 Build -> Build menu
- [ ] T-B06 Tools -> Tools menu
- [ ] T-B07 Help -> Help menu
- [ ] T-B08 Breadcrumb -> Scene Switcher
- [ ] T-B09 Debug -> Debugger
- [ ] T-B10 Build -> Build ROM
- [ ] T-B11 Play -> Play Mode
- [ ] T-B12 Video mode chip -> Video Mode
- [ ] T-B13 Hierarchy + -> Add to Scene
- [ ] T-B14 Actor (right-click) -> Actor Context Menu
- [ ] T-B15 Tab Game -> Play Mode
- [ ] T-B16 Tab Forest_01.map -> Tilemap Editor
- [ ] T-B17 Tab PlayerController.cs -> Script Editor
- [ ] T-B18 Scene size chip -> Scene Settings
- [ ] T-B19 Grid chip -> Grid & Snap
- [ ] T-B20 Zoom chip -> Zoom
- [ ] T-B21 Console tab -> Console panel
- [ ] T-B22 Animation tab -> Animation panel
- [ ] T-B23 Database tab -> Database
- [ ] T-B24 Asset (double-click) -> Sprite Editor (or the editor for that asset type)
- [ ] T-B25 Asset (right-click) -> Asset Context Menu
- [ ] T-B26 Font asset -> Font Manager
- [ ] T-B27 Inspector … -> Component Menu
- [ ] T-B28 Sheet dropdown -> Asset Picker
- [ ] T-B29 Animation dropdown -> Animation List
- [ ] T-B30 Palette -> Palette Manager
- [ ] T-B31 Script component -> Script Editor
- [ ] T-B32 Add Component -> Component Picker
- [ ] T-B33 GBA Budget -> Budget Details
- [ ] T-B34 Build status -> Build Report
- [ ] T-B35 Emulator status -> Preferences (Emulator tab)

### 4.13 Editor Map board 4: buttons on other pages (52)

Verify ทุกข้อด้วย `E2E`

- [ ] T-C01 Launcher › New Project -> New Project
- [ ] T-C02 Launcher › Open… -> folder picker
- [ ] T-C03 Launcher › Templates tab -> Launcher · Templates
- [ ] T-C04 Launcher › Learn tab -> Launcher · Learn
- [ ] T-C05 Launcher › Settings tab -> Preferences
- [ ] T-C06 Launcher › Project card -> Opening Project
- [ ] T-C07 Launcher › Project card (right-click) -> Project Context Menu
- [ ] T-C08 Launcher › Tutorial Start / Continue -> Tutorial Steps
- [ ] T-C09 Launcher › Use this template -> New Project
- [ ] T-C10 Play Mode › Speed chip 1x -> Play Speed
- [ ] T-C11 Play Mode › Stop -> Scene Editor
- [ ] T-C12 Sprite Editor › Sprites + -> New Sprite
- [ ] T-C13 Sprite Editor › Drop zone -> Import Asset
- [ ] T-C14 Sprite Editor › OAM size -> OAM Size
- [ ] T-C15 Sprite Editor › Palette slots -> Palette Manager
- [ ] T-C16 Tilemap Editor › Tileset dropdown -> Asset Picker
- [ ] T-C17 Tilemap Editor › Add layer -> Add Layer
- [ ] T-C18 Tilemap Editor › Auto-tile rules tab -> Auto-tile Rules panel
- [ ] T-C19 Script Editor › Scripts + -> New Script
- [ ] T-C20 Script Editor › Open externally -> external editor
- [ ] T-C21 Script Editor › Ctrl+F / Ctrl+H -> Find & Replace
- [ ] T-C22 Script Editor › Open Scripting API -> Scripting API
- [ ] T-C23 Logic Graph › Right-click canvas -> Add Node
- [ ] T-C24 Logic Graph › Right-click node -> Node Context Menu
- [ ] T-C25 World Map › New Scene -> New Scene
- [ ] T-C26 World Map › Scene card (right-click) -> Scene Context Menu
- [ ] T-C27 World Map › Link chip -> Link Settings
- [ ] T-C28 Palette Manager › Fade / Cycle -> Palette Effect
- [ ] T-C29 Palette Manager › Import .pal / .gpl -> Import Asset
- [ ] T-C30 Font Manager › Fonts + -> New Font
- [ ] T-C31 Font Manager › Add characters from text… -> Add Characters
- [ ] T-C32 Database › Tables + -> New Table
- [ ] T-C33 Database › Schema + -> Add Field
- [ ] T-C34 Database › Import CSV -> Import CSV
- [ ] T-C35 Localization › Languages + -> Add Language
- [ ] T-C36 Localization › Export .csv -> save dialog
- [ ] T-C37 Audio › Sound effects + -> SFX Designer
- [ ] T-C38 Audio › Music + -> Import Asset
- [ ] T-C39 Dialogue Editor › Speakers + -> New Speaker
- [ ] T-C40 Dialogue Editor › Add line · choice · command -> Add to Dialogue
- [ ] T-C41 Debugger › Views › Memory -> Debugger · Memory
- [ ] T-C42 Debugger › Views › Palettes -> Debugger · Palettes
- [ ] T-C43 Build Report › Build again -> Build ROM
- [ ] T-C44 Build ROM (a step fails) -> Build ROM · Failed
- [ ] T-C45 Import Asset (a file can't be converted) -> Import Asset · Errors
- [ ] T-C46 Build Report › Run in mGBA -> Play Mode
- [ ] T-C47 Build Report › Flash to cartridge -> Flash to Cartridge
- [ ] T-C48 Component Picker › New C# script… -> New Script
- [ ] T-C49 Preferences › Shortcuts › pencil -> Record Shortcut
- [ ] T-C50 Preferences › Paths › Browse… -> file picker
- [ ] T-C51 Budget Details › Open Build Report -> Build Report
- [ ] T-C52 File menu › New Project (Empty) -> Scene Editor · Empty project (see 1.4 item 3)

### 4.14 Prototype links

- [ ] T-X01 Flow "AGB Studio · start" (from Launcher) works end to end in the app · Verify: `E2E` script that follows the flow
- [ ] T-X02 Flow "AGB Studio · editor" (from Scene Editor) works end to end · Verify: `E2E`
- [ ] T-X03 All 120 prototype click links in Screens go to the same window in the app; every Cancel / Close closes its overlay; tabs and Stop lead back · Verify: export the link list from Figma read-only at test time, generate an `E2E` case per link

### 4.15 Design tokens, type, icons, art

- [ ] T-K01 29 colour tokens, same names in "AGB Studio" (Dark) and "AGB Studio · Light": bg/canvas, bg/base, bg/panel, bg/raised, bg/hover, bg/sunken, border/default, border/strong, text/primary, text/secondary, text/muted, text/on-accent, text/on-selected, accent/default, accent/selected, accent/subtle, accent/hover, accent/pressed, status/success, status/warning, status/danger, status/danger-subtle, status/danger-hover, status/danger-pressed, kind/script, kind/data, kind/audio, axis/x, axis/y · Verify: `UT` compares a read-only Figma export with the app token file (names and values, both themes)
- [ ] T-K02 20 text styles: Header 1, Header 2, Body, Brand/13, Title/18, Title/14, Body/12, Body/12 Medium, Body/12 Semibold, Label/11, Label/11 Medium, Caption/10, Overline/11, Mono/10, Mono/11, Mono/11 Bold, Mono/13, Thai/13, Thai/13 Medium, Pixel/10 · Verify: `UT` style table vs Figma export, `SHOT` type specimen
- [ ] T-K03 Fonts bundled with licence files: Inter, JetBrains Mono, IBM Plex Sans Thai (and Looped), Press Start 2P, Nunito ExtraBold (wordmark) · Verify: `REV` against G-LEGAL
- [ ] T-K04 70 icons (icon/play … icon/gift) exported as vector and used by name · Verify: `UT` every icon name referenced in UI exists, none missing
- [ ] T-K05 Art used inside the UI (art/scene x4, art/sprite x6, art/tileset x1, art/genre x8) and 19 thumbnails (thumb/*) · Verify: `REV` each is original design work, `SHOT` on the windows that show them

### 4.16 Components (47 buildable)

แต่ละชิ้นต้องมีครบทุก variant และ state ใน Figma รวม Hover / Pressed / Disabled / Focus ตรวจด้วย `SHOT` ต่อ variant

- **Menu Item** (Kind: Default, Submenu, Checked, Danger · State: Default, Hover, Disabled · shortcut, icon)
  - [ ] implemented
  - [ ] matches Figma
- **Menu Bar Item** (Default, Open)
  - [ ] implemented
  - [ ] matches Figma
- **Button** (Primary, Secondary, Ghost, Danger · SM, LG · Default, Hover, Pressed, Disabled · icon)
  - [ ] implemented
  - [ ] matches Figma
- **Icon Button** (Default, Active, Disabled, Hover, Pressed)
  - [ ] implemented
  - [ ] matches Figma
- **Play Control** (Play, Pause, Step, Stop)
  - [ ] implemented
  - [ ] matches Figma
- **Layer Toggle** (On, Off, Active)
  - [ ] implemented
  - [ ] matches Figma
- **Chip** (Default, Accent, Success, Warning, Danger, Script · icon)
  - [ ] implemented
  - [ ] matches Figma
- **Switch** (On, Off)
  - [ ] implemented
  - [ ] matches Figma
- **Checkbox** (On, Off · label)
  - [ ] implemented
  - [ ] matches Figma
- **Segment** (Default, Active, Hover)
  - [ ] implemented
  - [ ] matches Figma
- **Progress** (OK, Warn, Danger, Accent · 0-100)
  - [ ] implemented
  - [ ] matches Figma
- **Slider** (0-100, knob with border/strong ring)
  - [ ] implemented
  - [ ] matches Figma
- **Tree Item** (Default, Selected, Hover · toggle None, Open, Closed · meta, icon)
  - [ ] implemented
  - [ ] matches Figma
- **Doc Tab** (Default, Active, Hover · dirty dot)
  - [ ] implemented
  - [ ] matches Figma
- **Panel Tab** (Default, Active, Hover · count)
  - [ ] implemented
  - [ ] matches Figma
- **List Item** (Default, Selected, Hover)
  - [ ] implemented
  - [ ] matches Figma
- **Nav Item** (Default, Active, Hover)
  - [ ] implemented
  - [ ] matches Figma
- **Section Header** (Open, Closed · meta)
  - [ ] implemented
  - [ ] matches Figma
- **Field** (Text, Dropdown, XY, Slider, Toggle)
  - [ ] implemented
  - [ ] matches Figma
- **Swatch** (Default, Selected, Empty, Transparent)
  - [ ] implemented
  - [ ] matches Figma
- **Status Item** (Default, Dot)
  - [ ] implemented
  - [ ] matches Figma
- **Console Line** (Info, Success, Warning, Error)
  - [ ] implemented
  - [ ] matches Figma
- **Toast** (Success, Error)
  - [ ] implemented
  - [ ] matches Figma
- **Asset Card** (Default, Selected)
  - [ ] implemented
  - [ ] matches Figma
- **Frame Thumb** (Default, Active)
  - [ ] implemented
  - [ ] matches Figma
- **Scene Card** (Default, Selected)
  - [ ] implemented
  - [ ] matches Figma
- **Template Card** (Default, Selected)
  - [ ] implemented
  - [ ] matches Figma
- **Glyph Cell** (Default, Selected, Missing)
  - [ ] implemented
  - [ ] matches Figma
- **Table Cell** (Header, Default, Mono, Selected)
  - [ ] implemented
  - [ ] matches Figma
- **Text Input** (Default, Focus · help text)
  - [ ] implemented
  - [ ] matches Figma
- **Select** (Default, Focus · help text)
  - [ ] implemented
  - [ ] matches Figma
- **Tab** (Default, Active)
  - [ ] implemented
  - [ ] matches Figma
- **Node Header** (Event, Input, Physics, Flow, Data, Sound, Actor, Math)
  - [ ] implemented
  - [ ] matches Figma
- **Node Pin** (Exec, Bool, Fixed, Actor, Text · In, Out)
  - [ ] implemented
  - [ ] matches Figma
- **brand/app-icon** (Light, Dark, Indigo)
  - [ ] implemented
  - [ ] matches Figma
- **Menu Separator**
  - [ ] implemented
  - [ ] matches Figma
- **Badge**
  - [ ] implemented
  - [ ] matches Figma
- **Keycap**
  - [ ] implemented
  - [ ] matches Figma
- **Panel Header**
  - [ ] implemented
  - [ ] matches Figma
- **Search Field**
  - [ ] implemented
  - [ ] matches Figma
- **Meter**
  - [ ] implemented
  - [ ] matches Figma
- **Tooltip**
  - [ ] implemented
  - [ ] matches Figma
- **Title Bar** (no brand text; starts at the File menu)
  - [ ] implemented
  - [ ] matches Figma
- **Dialog Header**
  - [ ] implemented
  - [ ] matches Figma
- **Dialog Footer** (hint hugs its text so a long hint never pushes the buttons out)
  - [ ] implemented
  - [ ] matches Figma
- **Popover Header**
  - [ ] implemented
  - [ ] matches Figma
- **brand/mark** (Plus Pad)
  - [ ] implemented
  - [ ] matches Figma

Doc-only components, not built in the app: Doc/Callout, Doc/Target Chip (annotation of the Editor Map)

### 4.17 Brand (section "AGB Studio · Brand")

- [ ] T-R01 App icon from "Plus Pad" (5-square cross, ink arms, indigo #3b4fd8 centre) for exe, installer, taskbar, About (64 px) · Verify: `SHOT` vs Plus Pad · Logo sheet
- [ ] T-R02 Wordmark "AGB Studio" in Nunito ExtraBold in the Launcher brand block · Verify: `SHOT` vs T-P12
- [ ] T-R03 Banners (4) used for the release page and installer · Verify: `REV`
- [ ] T-R04 ROM boot splash "AGB Studio logo · 2 s", skippable in Debug builds (Project Settings · General) · Blocked until the GBA-size splash art is designed (1.4 item 6)

---

## 5. Phases

### Phase 0 · Environment and toolchain check

> Scope: ตรวจเครื่องมือเท่านั้น ไม่เขียนโค้ด engine ห้ามแตะ `D:\devkitPro\GBAStudio.spec`, `D:\devkitPro\build\` และโฟลเดอร์ toolchain

Entry gate:
- [ ] P0.E1 User has said building may start · Verify: `USER`

Items (ค่าที่พบเมื่อ 2026-10-31 อยู่ในวงเล็บ):
- [ ] P0.01 devkitARM present, version recorded (arm-none-eabi-gcc 16.1.0) · Verify: command output saved to `docs/TOOLCHAIN.md`
- [ ] P0.02 libtonc present (`libtonc/lib/libtonc.a`) · Verify: hello ROM links against it
- [ ] P0.03 libgba and maxmod present (`libgba/lib/libgba.a`, `libmm.a`, `libfat.a`); confirm the runtime links libtonc + libmm without duplicate symbols from libgba · Verify: link map of the hello ROM
- [ ] P0.04 devkitPro tools present: gbafix, grit, mmutil, gbalzss, padbin, bin2s · Verify: each tool runs, versions recorded
- [ ] P0.05 Hello ROM builds (devkitPro `cmake/GBA.cmake` or Makefile, with Ninja) and `gbafix` writes a valid header · Verify: `EMU` boots in VBA-M with a coloured screen and text
- [ ] P0.06 mGBA and mgba-rom-test installed (not present today; install needs user OK under D-06) · Verify: hello test ROM exits with the pass code headless
- [ ] P0.07 VBA-M (`D:\devkitPro\visualboyadvance-m.exe`) runs the hello ROM · Verify: `EMU`
- [ ] P0.08 Host tools recorded: .NET SDK 10.0.400, Ninja 1.12.1, CMake 4.4.3, git 2.39.1, Node 22.20.0, Python 3.10.11; mark which ones the chosen stack needs after D-01 · Verify: versions in `docs/TOOLCHAIN.md`
- [ ] P0.09 Real hardware availability recorded (flash cart, GBxCart RW); if none, every `HW` check is marked `n/a until hardware` · Verify: `USER`
- [ ] P0.10 Untouched folders are still unchanged after Phase 0 · Verify: `REV` directory listing and timestamps before and after

Exit gate:
- [ ] P0.X1 All P0 items ticked, install list approved · Verify: `USER`

### Phase 1 · Open decisions and foundations

> Scope: ตัดสิน D-01..D-21 กับผู้ใช้และวางรากฐาน (infra) ไม่มี feature

Entry gate:
- [ ] P1.E1 Phase 0 exit ticked

Items:
- [ ] P1.01 All decisions D-01..D-21 answered and recorded in `docs/DECISIONS.md` (one entry each: question, options, answer, date) · Verify: `USER`
- [ ] P1.02 Design issues 1.4 items 1-9 resolved in Figma or accepted · Verify: `USER`
- [ ] P1.03 Repo created per D-04 with the agreed layout and a README · Verify: `REV`
- [ ] P1.04 File format spec per D-03 (project, scene, map, layout, tileset, sprite meta, palette, font, database, strings, dialogue, logic graph, sfx) with JSON schemas and a format version field · Verify: `UT` load / save round trip of every schema
- [ ] P1.05 "Sky Hopper" fixture project that reproduces the data shown in Figma (5 scenes incl. Forest_01 30x20, 6 links, 12 C# scripts, Items table 24 rows / 9 fields, 340 string keys in English / Thai / Japanese, sprites hero / slime / coin / block / heart / cloud, forest_tiles, fonts thai_vwf / pixel8 / title_big, music and SFX) using original placeholder content · Verify: `UT` counts match the Figma frames
- [ ] P1.06 CI pipeline: host unit tests, runtime test ROMs through mgba-rom-test, editor build, artifact upload (infra: every Verify step needs it) · Verify: green run on the empty skeleton
- [ ] P1.07 Lint rules: no emoji (codepoint scan), no direct hardware writes outside commit queues in runtime code, no float / double in runtime C++ (infra: G-EMOJI, G-HW) · Verify: CI fails on a planted violation of each
- [ ] P1.08 Evidence folders `docs/evidence/figma/` and `docs/evidence/app/` with the naming rule from 2.3 · Verify: `REV`
- [ ] P1.09 Third-party licence register `docs/THIRD_PARTY.md` (per D-05) · Verify: `REV` against G-LEGAL
- [ ] P1.10 Developer docs skeleton: `docs/ARCHITECTURE.md` mapping blueprint sections 03-14 to code folders, `docs/BUILD.md` · Verify: `DOC`
- [ ] P1.11 PR / commit template with "Figma ref: T-... or infra: ..." field (G-SCOPE) · Verify: `REV`

Exit gate:
- [ ] P1.X1 Decisions recorded, fixture spec approved, CI green · Verify: `USER`

### Phase 2 · Runtime: Platform + Core services

> Scope: infra ที่ทุกหน้าต่างใน Figma ต้องใช้ผ่าน ROM (Play Mode, Debugger, Build) อ้างรูปแบบจาก pokeemerald `main.c`, `gpu_regs.c`, `dma3_manager.c`, `task.c`, `malloc.c`, `random.c`, `save.c` โดยศึกษาเท่านั้น

Entry gate:
- [ ] P2.E1 Phase 1 exit ticked, runtime language and build system fixed

Items:
- [ ] P2.01 Boot code and linker sections for IWRAM / EWRAM / ROM, section macros for hot ARM code and EWRAM data · Verify: `RT` boot test, map file shows sections where expected
- [ ] P2.02 Interrupts and frame loop that sleeps with `VBlankIntrWait()` (no busy wait) · Verify: `RT` 600 frames counted at 59.73 Hz; `BUD` CPU halted when idle
- [ ] P2.03 Frame order from blueprint 04: input, Screen.Update, behaviours, Event Script VM, tasks, physics, animation, build OAM, palette fade, mark dirty tilemaps · Verify: `RT` trace log order
- [ ] P2.04 Commit queues (OAM shadow, palette buffer, GPU register shadow, DMA queue with per-frame byte budget, sprite frame copy) flushed only in VBlank · Verify: `RT` overflow defers to the next frame and logs; lint P1.07 passes; Done: no runtime module writes OAM / PAL / VRAM / registers directly
- [ ] P2.05 Input: held, pressed, released, repeat, AxisX / AxisY, AnyKey, action names from Project Settings · Input [T-D08] · Verify: `UT` with recorded key sequences
- [ ] P2.06 Tasks and coroutines in a fixed pool · Verify: `UT`
- [ ] P2.07 Memory arenas (reset per Screen) and fixed pools with checked handles; pool full = Console warning [T-P05] · Verify: `UT` stale handle and double free caught in Debug
- [ ] P2.08 RNG streams (gameplay, AI, effect) with xorshift or PCG · Verify: `UT` same seed gives the same sequence on host and ROM
- [ ] P2.09 Fixed-point `fixed` (24.8, per T-P04 text) and `fixed2` vector math · Verify: `UT` against a reference table; `BUD` cycles per multiply / divide
- [ ] P2.10 Save system [T-S02]: types None / SRAM 32 KB / Flash 64 KB / Flash 128 KB / EEPROM 8 KB, N slots, two copies per slot with counter, CRC32, format version + migration, autosave on scene change, checksum toggle · Verify: `RT` corrupt one copy -> the other loads; `EMU` per save type; `HW` on one cart
- [ ] P2.11 Log and assert: `Debug.Log` to the mGBA debug-print interface, asserts in Debug, both stripped in Release unless "Keep asserts" [T-S03] · Verify: `RT` log captured headless; Release ROM string scan finds no log text
- [ ] P2.12 BIOS wrappers: LZ77 to VRAM / WRAM, Div, Halt, CpuFastSet · Verify: `RT`
- [ ] P2.13 Debug channel for the editor in Debug builds: symbol and actor-table export used by live Inspector and Debugger [T-P02, T-P19, T-P23] (infra) · Verify: `RT` addresses in the export match the map file

Exit gate:
- [ ] P2.X1 `core_*` test ROMs pass in CI; IWRAM / EWRAM within blueprint 10 defaults · Verify: `BUD`, `USER` sees the ROM boot

### Phase 3 · Runtime: Rendering and media

> Scope: สิ่งที่หน้าต่าง Video Mode, Tilemap Editor, Sprite Editor, Palette Manager, Font Manager, Audio, SFX Designer แสดงผลออกมาใน ROM อ้างรูปแบบจาก `bg.c`, `sprite.c`, `palette.c`, `window.c`, `scanline_effect.c`, `m4a.c` (ศึกษาเท่านั้น)

Entry gate:
- [ ] P3.E1 Phase 2 exit ticked

Items:
- [ ] P3.01 BG manager: automatic char block (4 x 16 KB) and screen block (32 x 2 KB) allocation, shown per layer in T-P03 layer table · Verify: `UT` allocator; `RT` 4 layers in Mode 0
- [ ] P3.02 Video modes 0, 1, 2, 3 with alpha blending and mosaic toggles [T-O02] · Verify: `RT` per mode, screenshot vs reference image
- [ ] P3.03 Affine BG layers for Mode 1-2 [T-M20 Rotate / scale layer] · Verify: `RT` rotate and scale sweep
- [ ] P3.04 Metatile map renderer with ring-buffer scroll (rows / columns drawn as the camera moves; BG size 512 x 256) · Verify: `RT` scroll test, no tearing; `BUD` DMA bytes per frame
- [ ] P3.05 Per-layer parallax (x0.2, x0.5, x1.0, x1.2 as in T-D03 and T-P03) · Verify: `RT`
- [ ] P3.06 Sprite / OAM manager: 128 entries, priority sort, 32 affine matrices, all 12 OAM shapes [T-M19], sprite frame streaming to OBJ VRAM · Verify: `RT` 128 sprites; `BUD` OAM count
- [ ] P3.07 Sprite animation: frames, ticks per frame, loop, ping-pong [T-P08, T-P06] · Verify: `UT` timing
- [ ] P3.08 Palette manager: 16 BG + 16 OBJ banks, fade to black over N frames, tint, cycle (range, speed, direction Left / Right / Ping-pong), flash [T-P10, T-O10] · Verify: `RT` palette RAM checks per frame
- [ ] P3.09 Text windows: VWF renderer 1bpp / 2bpp (4 shades) to a BG text layer, drop shadow, typewriter speed (chars per frame), blip sound, continue marker [T-P09, T-P16] · Verify: `RT` reference string renders pixel-identical to the golden image
- [ ] P3.10 Scanline effects (per-line DMA) (infra: Racing (Mode 7) template in T-P20) · Verify: `RT` Mode 7 plane test
- [ ] P3.11 Music with maxmod: soundbank from MOD / XM, play / stop / volume / loop start / fade on scene change, 8 ch at 21 kHz default [T-P15] · Verify: `EMU` listen check; `BUD` mixer CPU near the 6% shown in Figma
- [ ] P3.12 DMG sound effects from register data: Square 1 (sweep), Square 2, Wave, Noise [T-P25] · Verify: `EMU` sounds match the editor preview; 0 bytes of sample ROM
- [ ] P3.13 Channel usage report (DSound A / B for music, DMG channels for SFX) [T-P15 GBA channels] · Verify: `UT`
- [ ] P3.14 Screen transitions Cut / Fade / Slide with duration [T-O09] · Verify: `RT`

Exit gate:
- [ ] P3.X1 4 scrolling BGs + 128 sprites + music at 60 fps on mGBA · Verify: `BUD` profiler capture, `EMU`, `USER`

### Phase 4 · Runtime: Game framework

> Scope: สิ่งที่ Scene Editor, Component Picker, Add Actor, Scripting API และ template ใน Figma ต้องใช้ อ้างรูปแบบจาก `main.c` (callback / state), `overworld.c`, `battle_controllers.c` (ศึกษาเท่านั้น)

Entry gate:
- [ ] P4.E1 Phase 3 exit ticked

Items:
- [ ] P4.01 Game and Screen stack: push / pop / replace, coroutine `Load()` in 7 staged steps (blueprint 05), `Update()`, `Exit()`, return continuation · Verify: `RT` no step longer than one frame, overruns logged with the step name
- [ ] P4.02 Scene / Actor / Behaviour: Start, Update, OnTouch, `Get<T>()`, `Is<T>()`, tag, Destroy [T-P04, T-P17] · Verify: `RT`
- [ ] P4.03 Built-in behaviours from Component Picker [T-O05]: Platformer Body (gravity, jump force, max speed, slopes, one-way), Top-down Mover (8-way), Projectile, Health (hearts, invincibility), Damage on Touch, Patrol, Chase Player, Sound on Event · Verify: `RT` one scenario per behaviour
- [ ] P4.04 Collision: tile types Solid / One-way / Ladder / Hazard / Water / None [T-P03]; actor colliders with size and offset [T-P01 Inspector] · Verify: `UT` + `RT`
- [ ] P4.05 Camera: follow target, dead zone, clamp to map [T-D03], Camera Zone actor [T-M10], shake [T-M24] · Verify: `RT`
- [ ] P4.06 Actor presets: Empty Actor, Player, Enemy, Pickup, Trigger Zone, Camera Zone, Text Label [T-M10, T-M11] · Verify: `RT` each spawns and behaves
- [ ] P4.07 Prefabs as const templates in ROM [T-M12 Make Prefab, T-M11 Prefab…] · Verify: `UT`
- [ ] P4.08 Flags / vars store with stable IDs: persistent (in save), per-map, session · Verify: `UT` adding a flag does not shift existing save data
- [ ] P4.09 Game state fields (score, coins, lives) declared in scripts and reachable from Logic Graph "Add to Variable" [T-P04, T-P17] · Verify: `UT`
- [ ] P4.10 Command bus: rules emit commands, presentation answers at declared sync points, input source Player or AI (infra: Turn-based RPG and Puzzle templates in T-P20) · Verify: `UT` rules run headless; `RT` same inputs give the same result
- [ ] P4.11 Scene API: `Scene.Load`, `Scene.Pause`, `Save.Checkpoint()`, start scene from settings [T-S01, T-D03] · Verify: `RT`
- [ ] P4.12 Runtime button remap [T-D08 "Let players remap buttons in the options menu"] · Blocked until the options menu is designed (1.4 item 6)

Exit gate:
- [ ] P4.X1 A Forest_01 slice of the fixture runs at 60 fps with player, slimes, coins, HUD · Verify: `EMU`, `BUD`, `USER`

### Phase 5 · World model: map / layout / tileset / metatile

> Scope: ข้อมูลที่ Tilemap Editor, Auto-tile Rules, World Map, Scene Settings และ Link Settings สร้าง อ้างรูปแบบจาก `fieldmap.c`, `event_object_movement.c` และ `data/maps` / `data/layouts` (โครงสร้างเท่านั้น)

Entry gate:
- [ ] P5.E1 Phase 4 exit ticked

Items:
- [ ] P5.01 Tilesets (primary / secondary) with 4bpp tiles, dedupe including flips, counts shown as "Unique tiles 58 / 1024" [T-P03] · Verify: `UT`; `BUD`
- [ ] P5.02 Internal metatile format (2 x 2 tiles x 2 layers + behaviour id + layer type) behind the tile and stamp UI of T-P03 (infra) · Verify: `UT` round trip
- [ ] P5.03 Layout stored apart from map, shareable between maps (infra) · Verify: `UT`
- [ ] P5.04 Map header: size, video mode, backgrounds and parallax, music, camera, links [T-D03, T-P13] · Verify: `UT` round trip
- [ ] P5.05 Map events: entities, portals (door -> spawn point), triggers (area + condition), interactables [T-P01 Hierarchy, T-O09] · Verify: `RT`
- [ ] P5.06 Map scripts OnEnter / OnExit (and OnLoad as infra) [T-D03] · Verify: `RT`
- [ ] P5.07 Scene links from World Map: kinds on touch / door / respawn, spawn point, transition, condition "Only if" [T-P13, T-O09] · Verify: `RT` warp Forest_01 -> Forest_02 and Cave_01
- [ ] P5.08 Tile animation and random brush data [T-P03 tile properties] · Verify: `RT`
- [ ] P5.09 Auto-tile 3x3 rules resolved at edit time and stored [T-P26] · Verify: `UT` rule set "Grass edges (16)" gives the expected tiles
- [ ] P5.10 Background layer types: tile, image, rotate / scale, collision-only [T-M20] · Verify: `RT`

Exit gate:
- [ ] P5.X1 All 5 fixture scenes load through their links with transitions · Verify: `EMU`, `USER`

### Phase 6 · Scripting: 3 tiers + Event Script VM

> Scope: Script Editor, Project Settings · Scripting, Logic Graph, Dialogue Editor, Scripting API, Database (tier 3) อ้างรูปแบบ VM จาก `script.c`, `scrcmd.c` (ศึกษาเท่านั้น ไม่ใช้ opcode หรือชื่อเดิม)

Entry gate:
- [ ] P6.E1 Phase 4 exit ticked, D-02 decided

Tier 1 · C# -> C++:
- [ ] P6.01 C# subset spec documented (types, classes, virtual, generics allowed, attributes, what is forbidden and why) · Verify: `DOC`, `USER`
- [ ] P6.02 Roslyn analyzer with rules GAE001-GAE006 (or renamed per D-07): allocation in Update / OnTouch, float / double, new inside Update, try / throw, string concat in a loop, capturing lambdas; severity per rule and "Treat warnings as errors" [T-S04] · Verify: `UT` positive and negative case per rule
- [ ] P6.03 Code fixes "Change to fixed" and "Ignore" [T-P04 diagnostic popup] · Verify: `UT`
- [ ] P6.04 C# -> C++20 translator (no exceptions, no RTTI), output to `build/gen`, files written only when content changes [T-S04 Generated] · Verify: `UT` golden files; `RT` translated PlayerController behaves like Figma's code sample
- [ ] P6.05 Line mapping so g++ errors and debugging point at `.cs` lines [T-D06 log, T-D23] · Verify: `UT` planted error reports `PlayerController.cs(22,9)`
- [ ] P6.06 Public fields and `[Range]` exposed to the Inspector [T-P04 Exposed fields, T-P01] · Verify: `E2E`
- [ ] P6.07 Project namespace setting [T-S04 Namespace] · Verify: `UT`
- [ ] P6.08 Script templates Behaviour / Static helper / Scene script [T-D14] · Verify: `UT` each template compiles clean
- [ ] P6.09 Scripting API v0.1 exactly as T-P18 and the Figma code samples show: Input (Pressed, Held, Released, AxisX / AxisY, AnyKey, Key enum), Behaviour, Actor, Sound, Music, Camera, Save, Text & Dialogue (`Text.Show`), Items (Database), fixed (math), Scene, Game, Debug.Log · Verify: `UT` per member; `DOC` generated page per member

Tier 2 · async C# -> bytecode, Event Script VM:
- [ ] P6.10 `[Cutscene] async Task` with `await Say / Move / Choice / Wait` and `LockAll()` compiled to bytecode + string table (blueprint 06) · Verify: `UT` golden bytecode
- [ ] P6.11 One opcode table source that generates the compiler side and the VM handlers · Verify: `UT` consistency check
- [ ] P6.12 VM: several contexts by priority (cutscene, NPC / trigger, immediate), pc, call stack, locals, return value, `Wait(frames)`, `WaitUntil`, `Continue()` from natives (blueprint 07) · Verify: `RT`
- [ ] P6.13 Native table declared with `[ScriptNative(waits: ...)]` · Verify: `UT`
- [ ] P6.14 VM debug hooks for breakpoints, step and variable view [T-P19 Breakpoints] · Verify: `RT`
- [ ] P6.15 Dialogue compiler: lines, speakers, choices, commands (Go to scene, Set flag, Give item, Play sound, Wait, Shake camera), conditions "Show if" and "Then set" [T-P16, T-M23, T-M24] · Verify: `UT` golden; `RT` forest_intro plays with both choices
- [ ] P6.16 Logic Graph -> C# generator for node categories Events, Input, Physics, Flow, Math, Actor, Sound, Data & Save, Text & UI, Other [T-P17, T-O08] · Verify: `UT` the Figma graph generates the C# shown in its Generated C# panel
- [ ] P6.17 C# -> Graph direction per D-11 [T-P17 "in sync"] · Verify: `UT` round trip
- [ ] P6.18 Hot reload per D-15 [T-S04, T-P04 Output line] · Verify: `E2E`

Tier 3 · data -> ROM const tables:
- [ ] P6.19 Database tables -> `constexpr` tables + enum IDs, one generated unit per table [T-P07] · Verify: `UT`; changing one row rebuilds one unit
- [ ] P6.20 Typed access (`Items.Get(ItemId.Potion)`) generated per table [T-P07, T-D17] · Verify: `UT`

Exit gate:
- [ ] P6.X1 All fixture scripts compile, analyzer flags `coyoteTime` float as in Figma, ROM runs · Verify: `RT`, `USER`

### Phase 7 · Asset pipeline + build pipeline (hash cache)

> Scope: Import Asset, Import Asset · Errors, Build ROM, Build ROM · Failed, Export ROM, Build Report, Project Settings · Build, Preferences · Paths (cache) ห้ามเพิ่มรูปแบบไฟล์ที่ Figma ไม่ได้ระบุ

Entry gate:
- [ ] P7.E1 Phase 2 exit ticked, D-03 decided

Items:
- [ ] P7.01 Per-asset `meta.json` options (frame size, palette slot, compression, etc.) edited in the editor (blueprint 11) · Verify: `UT` schema
- [ ] P7.02 Image converter: PNG / Aseprite -> 4bpp tiles, reduce to 16 colours, share palette, split frames, trim empty frames, create animation [T-D04, T-D13] · Verify: `UT` golden output
- [ ] P7.03 Image errors in plain language with fixes: "Reduce to 16" and "Split into 3 palettes" [T-D24] · Verify: `UT` 48-colour image gives that message
- [ ] P7.04 Tiled `.tmx` import -> map / layout [T-D04] · Verify: `UT`
- [ ] P7.05 Palette import / export `.pal` / `.gpl` [T-P10] · Verify: `UT`
- [ ] P7.06 Audio: MOD / XM -> maxmod soundbank via mmutil; WAV -> 11 kHz mono 8-bit; MP3 rejected with the Figma message [T-D04, T-D24] · Verify: `UT`
- [ ] P7.07 `.sfx` (DMG) -> register tables; Bake to `.wav` [T-P25] · Verify: `UT`
- [ ] P7.08 LZ77 compression compatible with the BIOS [T-S03, T-D05] · Verify: `RT` BIOS decompresses converter output exactly
- [ ] P7.09 Hash cache: key = input bytes + options + converter version; `build/cache/manifest.json`; unchanged inputs skipped (blueprint 11) · Verify: `UT` no-change rebuild converts 0 assets, one option change reconverts 1
- [ ] P7.10 Build graph with Ninja: g++ `-O2 -mthumb -mcpu=arm7tdmi`, hot code as ARM in IWRAM, `ld --gc-sections`, objcopy, gbafix with title (12), game code (4), maker (2), version [T-D06, T-S01] · Verify: `RT` ROM boots; header bytes checked
- [ ] P7.11 Debug / Release configs with optimise level, strip Debug.Log, keep asserts, warn when ROM > N MB (16 MB default), output folder, run in mGBA after build [T-S03] · Verify: `UT` config matrix
- [ ] P7.12 `build/report.json`: ROM / RAM / VRAM per asset, ROM breakdown (graphics, code, audio, data), largest assets, warnings, time per stage, history [T-P11, T-O06] · Verify: `UT` schema; numbers match the linker map
- [ ] P7.13 Build progress and errors streamed to the editor: steps, per-file progress, timings, `file(line,col)` links, first error [T-D06, T-D23] · Verify: `E2E`
- [ ] P7.14 Clean Build [T-M05] and cache clear with size display [T-S08] · Verify: `UT`
- [ ] P7.15 Export ROM: file name, folder, pad to power of two, estimated size [T-D05] · Verify: `UT` padded size; `EMU` padded ROM boots
- [ ] P7.16 Asset reference graph for Find Usages and Find Unused Assets [T-M13, T-M03] and "Used by" lists [T-P07, T-P10] · Verify: `UT`
- [ ] P7.17 Reimport [T-M03, T-M13] · Verify: `UT`
- [ ] P7.18 Incremental build budget: "Usually under a second" [T-D06] for the fixture after a one-line script change · Verify: `BUD` timing in CI

Exit gate:
- [ ] P7.X1 Fixture builds clean -> `.gba`; a second build with no changes does no work; report matches the map file · Verify: `RT`, `BUD`, `USER`

### Phase 8 · Font manager + Thai text

> Scope: Font Manager, New Font, Add Characters, Project Settings · Localization (Thai wrap, font per language) และกล่องข้อความ preview ใน Font Manager / Localization / Dialogue Editor

Entry gate:
- [ ] P8.E1 Phase 3 and 7 exits ticked, D-17 decided

Items:
- [ ] P8.01 Font sources TTF / OTF, BDF, image sheet [T-D15] · Verify: `UT`
- [ ] P8.02 Fixed (8x8) and variable width fonts, sizes (12 px, 16x16), metrics: height, base, spacing, line, advance per glyph [T-P09] · Verify: `UT`
- [ ] P8.03 Character sets Basic Latin (95), Thai (87), Symbols (12), Japanese kana [T-P09, T-D15] · Verify: `UT` counts
- [ ] P8.04 Glyph data editable per pixel; missing glyphs flagged (Glyph Cell "Missing") [T-P09] · Verify: `UT`
- [ ] P8.05 Thai layout at build time: stack upper vowels and tone marks, shift marks on ascender consonants (ป ฝ ฟ ฬ), place lower vowels ุ ู and handle descender consonants (ฎ ฏ ฐ ญ), sara am ำ [T-P09 Thai layout] · Verify: `UT` full combination corpus vs golden bitmaps
- [ ] P8.06 Thai word wrap by dictionary at build time; never splits a word; line breaks precomputed [T-P09, T-S05, T-P14] · Verify: `UT` known sentences; `RT` wrapped output
- [ ] P8.07 Rendering 2bpp / 4 shades, drop shadow, draw to a BG text layer, VRAM text budget (4 / 16 KB) [T-P09] · Verify: `RT`; `BUD`
- [ ] P8.08 Add Characters: paste text, list missing glyphs with code points, render them from the source font, option to scan all dialogue and localization strings [T-D16] · Verify: `UT` the Figma sample finds 7 missing
- [ ] P8.09 Editor preview uses the same layout and rasteriser as the ROM [T-P09, T-P14, T-P16] · Verify: `UT` editor bitmap equals an `RT` ROM capture of the same string (pixel diff 0)
- [ ] P8.10 Default font and font per language [T-S01, T-S05, T-D20] · Verify: `UT`
- [ ] P8.11 ROM size estimate per font ("about 6 KB of ROM") [T-D15, T-P09] · Verify: `BUD` estimate within 10% of the built size

Exit gate:
- [ ] P8.X1 G-THAI passes · Verify: `USER` reviews the Thai screenshots

### Phase 9 · Database + Localization

> Scope: Database, New Table, Add Field, Import CSV, Localization, Add Language, Project Settings · Localization ไม่มี import สำหรับ Localization (Figma มีแค่ Export .csv)

Entry gate:
- [ ] P9.E1 Phase 6 tier 3 and Phase 7 exits ticked

Database:
- [ ] P9.01 Tables with enum key field `id`, rows add / edit / filter, record panel [T-P07] · Verify: `UT`
- [ ] P9.02 Field types int, fixed, bool, text, enum (with values), sprite, sound, script, reference; default; localized; bytes per row shown [T-D18] · Verify: `UT` "rarity" enum = 1 B per row
- [ ] P9.03 New Table from empty / CSV / copy of a table; localize text fields; keep in ROM as const [T-D17] · Verify: `UT`
- [ ] P9.04 Import CSV: column mapping, type check, skip column, per-row warnings with defaults [T-D19] · Verify: `UT` the fixture CSV warns on rows 7 and 15
- [ ] P9.05 "Used by" references (shop slot, chest, script drop table) [T-P07] · Verify: `UT`
- [ ] P9.06 Script-typed fields (e.g. `Heal(power)`) compiled into callable code [T-P07 effect column] · Verify: `RT`
- [ ] P9.07 ROM size per table in the status bar [T-P07] · Verify: `BUD`

Localization:
- [ ] P9.08 String keys, source language, one table per language [T-P14] · Verify: `UT`
- [ ] P9.09 Languages with progress, fallback language, Add Language with "translate from", font, mark all missing, fall back until translated [T-S05, T-D20] · Verify: `UT`
- [ ] P9.10 Fit check in px against the box width and line count (fits 2 lines, max 3), "long" flag, auto-check [T-P14] · Verify: `UT` uses the runtime layout engine
- [ ] P9.11 Missing strings: list, "Missing only" filter, `[key]` shown in Debug, fail the build on missing [T-S05, T-P14] · Verify: `UT`
- [ ] P9.12 Groups (Dialogue, UI & menus, Items), notes and speaker per string [T-P14] · Verify: `UT`
- [ ] P9.13 Export .csv through the save dialog [T-P14, T-Y02] · Verify: `UT`
- [ ] P9.14 `Text.Show(key)` and dialogue lines pick the current language at runtime · Verify: `RT` switch language, same scene

Exit gate:
- [ ] P9.X1 Fixture builds in English and Thai, Japanese shows fallback · Verify: `RT`, `USER`

### Phase 10 · Editor shell + design system

> Scope: โครงของ editor และ design system ตาม Figma section "AGB Studio · Components" ไม่มีหน้าต่าง feature เว้นแต่ Launcher, Preferences, Project Settings ในส่วนที่เป็นโครง

Entry gate:
- [ ] P10.E1 Phase 1 exit ticked, D-01 and D-10 decided

Items:
- [ ] P10.01 App skeleton on the chosen stack: Title Bar (no brand text), menu bar with 7 menus, status bar [T-M01..T-M07] · Verify: `SHOT` vs Scene Editor chrome
- [ ] P10.02 Token system with both collections and identical names [T-K01] · Verify: `UT`
- [ ] P10.03 Text styles and bundled fonts [T-K02, T-K03] · Verify: `SHOT` specimen
- [ ] P10.04 Icon set [T-K04] · Verify: `UT`
- [ ] P10.05 Component library [4.16] · Verify: `SHOT` per component and state
- [ ] P10.06 Docking layout: Hierarchy, Inspector, bottom panel tabs (Assets, Console, Animation, Database), document tabs with dirty dot [T-P01] · Verify: `E2E`
- [ ] P10.07 Theme Dark / Light / System, accent colour, UI scale, reduce motion, pixel grid at 400% and above, code font size [T-S06, T-L01..T-L04] · Verify: `E2E`, `SHOT`
- [ ] P10.08 Project open / save / save all, dirty tracking, Unsaved Changes on exit, close or opening another project [T-D12] · Verify: `E2E`
- [ ] P10.09 Undo / redo with the history limit from Preferences (200 steps) [T-S07] · Verify: `UT`
- [ ] P10.10 Autosave every N minutes [T-S07] · Verify: `UT`
- [ ] P10.11 Cut, copy, paste, duplicate, delete, select all across actors, assets, nodes, dialogue lines, table rows [T-M02] · Verify: `E2E`
- [ ] P10.12 Command system with Command Palette (Ctrl+K, commands and files, `>` for commands only, keyboard navigation) and Find in Project [T-O07] · Verify: `E2E`
- [ ] P10.13 Shortcut system: defaults as in Figma menus and T-D10, rebinding, conflict detection, reset to defaults [T-S09, T-O12, T-D10] · Verify: `UT` conflict "Ctrl+G used by Go to line"
- [ ] P10.14 Storage: Preferences per user, Project Settings per project [T-D09, T-D08] · Verify: `UT`
- [ ] P10.15 Launcher as start page; "Open last project on start" [T-P12, T-S07] · Verify: `E2E`
- [ ] P10.16 Toolchain detection with found / not found status: devkitPro, devkitARM version, libtonc, maxmod, gbafix, script editor, image editor, Tiled [T-D09, T-S08, T-S03, T-P12] · Verify: `UT` with fake folders
- [ ] P10.17 OS dialogs and Show in Explorer [T-Y01..T-Y04, T-M03, T-M13, T-M17] · Verify: `E2E`
- [ ] P10.18 Toasts and tooltips · Verify: `SHOT`
- [ ] P10.19 Recent projects (Open Recent, Clear Recent, Launcher list with times) [T-M08, T-P12] · Verify: `UT`
- [ ] P10.20 Thai typing and display in every editor text field (Localization, Dialogue, Database, Font Manager) · Verify: `E2E` with Thai input

Exit gate:
- [ ] P10.X1 Shell, tokens, type, icons and all components pass SHOT · Verify: `USER`

### Phase 11 · Editor windows (every Figma page, dialog, popover, menu)

> Scope: หน้าต่างละรายการตามหัวข้อ 4 เท่านั้น ห้ามเพิ่มปุ่ม แผง หรือตัวเลือกที่ไม่มีใน frame เมื่อเสร็จแต่ละกลุ่ม ให้ติ๊ก "implemented" ของ T-ID ในหัวข้อ 4 แล้วส่งให้ reviewer ทำ SHOT

Entry gate:
- [ ] P11.E1 Phase 10 exit ticked; for each window, the runtime phase it depends on is done

11.A Launcher [T-P12, T-P20, T-P21, T-O13, T-M17, T-D22]
- [ ] P11.01 Projects: recent list with search, thumbnails, times; Open…; New Project · Verify: `E2E`
- [ ] P11.02 Templates: filters All / Action / Adventure / RPG / Puzzle / Story / Racing, details "Includes", Use this template · Verify: `E2E`
- [ ] P11.03 Learn: tutorials with progress (Start / Continue / Review), documentation links, sample projects · Verify: `E2E`
- [ ] P11.04 Tutorial Steps: step text, sub-steps that tick themselves when the action is done in the editor, Back / Skip / Next step · Verify: `E2E` "Your first platformer" step 3
- [ ] P11.05 Project Context Menu actions; "Delete project…" asks first and says what is removed · Verify: `E2E`
- [ ] P11.06 Opening Project with steps and Cancel · Verify: `E2E` cancel leaves no half-open project
- [ ] P11.07 "What's new" panel fed by release notes · Verify: `E2E`

11.B New Project [T-D01, T-L02, T-P22]
- [ ] P11.08 Template cards, project name, location with the devkitPro warning, ROM title (12) and game code (4) validation, save type, include options (sample level, Thai variable-width font, Logic Graph examples), Initialize git repository · Verify: `E2E` each option changes the created project
- [ ] P11.09 Empty project path opens Scene Editor · Empty project with all empty states [4.9] · Verify: `E2E`

11.C Scene Editor and panels [T-P01, T-P05, T-P06, T-D02, T-D03, T-O01..T-O06, T-M10..T-M16]
- [ ] P11.10 Viewport shows the scene as the ROM would draw it (per D-18), GBA screen frame 240 x 160, zoom, pan, cursor tile coordinates · Verify: `UT` viewport pixels equal an `RT` capture of the same scene
- [ ] P11.11 Layer toggles BG0-BG3 / OBJ, video mode chip, scene size chip, grid chip, zoom chip · Verify: `E2E`
- [ ] P11.12 Hierarchy tree with search, add, context menu, selection sync with viewport · Verify: `E2E`
- [ ] P11.13 Inspector: Actor header (layer, priority), Transform, Sprite (sheet, animation, frames, palette), behaviours, Collider, script fields, Add Component, component menu, GBA Budget meters · Verify: `E2E`
- [ ] P11.14 Assets panel: categories with counts, filter, cards, double-click opens the right editor, context menu, drag into the scene · Verify: `E2E`
- [ ] P11.15 Scene Switcher with search and New Scene… · Verify: `E2E`
- [ ] P11.16 Console panel: All / Warnings / Errors filters with counts, click a line to open the code · Verify: `E2E`
- [ ] P11.17 Animation panel: animations of the selected sprite, frame ticks, speed, onion skin · Verify: `E2E`
- [ ] P11.18 New Scene (template, size, video mode, copy backgrounds, follow camera) and Scene Settings (name, size, start scene, camera, backgrounds parallax, music, OnEnter / OnExit) · Verify: `E2E`
- [ ] P11.19 Video Mode, Grid & Snap (size 8 / 16 / 32, show grid, snap, tile collision, camera bounds, grid colour), Asset Picker (search, type tabs), Component Picker (categories, search, New C# script…), Budget Details (live numbers, palette-sharing hint) · Verify: `E2E`
- [ ] P11.20 Menus Add Actor, Add to Scene, Actor Context Menu, Asset Context Menu, Component Menu, Zoom, Animation List · Verify: `E2E` every item
- [ ] P11.21 Set as Start Scene, Background Layers… (opens Tilemap Editor) · Verify: `E2E`

11.D Sprite Editor [T-P08, T-D13, T-M19]
- [ ] P11.22 Pixel canvas with 8x8 tile grid, zoom 1x / 2x / 4x, drawing tools shown in the toolbar, frame index · Verify: `E2E`
- [ ] P11.23 Palette panel: 16 colours, R / G / B 0-31, hex, 15-bit value, palette slots used, share palette · Verify: `E2E`
- [ ] P11.24 Import drop zone, auto-reduce to 16 colours, split sheet into frames · Verify: `E2E`
- [ ] P11.25 Sprite settings: OAM size menu, origin, collider, mirror X, VRAM per frame · Verify: `E2E`
- [ ] P11.26 Timeline: animations, ticks per frame with fps estimate, loop, ping-pong, onion skin · Verify: `E2E`
- [ ] P11.27 New Sprite: name, GBA sizes 8-64 px, frames, palette, start from blank / copy / import, create "idle", add collider · Verify: `E2E`

11.E Tilemap Editor [T-P03, T-P26, T-M20, T-O04]
- [ ] P11.28 Tileset panel with tileset dropdown, brush, stamp, random · Verify: `E2E`
- [ ] P11.29 Tile properties: collision type, flip H, tag, animation, auto-tile set; collision view · Verify: `E2E`
- [ ] P11.30 Layers table (type, priority, parallax, VRAM char / screen block) and Add Layer menu · Verify: `E2E`
- [ ] P11.31 Map size, BG size, colours, wrap; stats unique tiles, screen blocks, BG palettes, VRAM BG · Verify: `E2E`
- [ ] P11.32 Auto-tile Rules panel: rule sets, 3x3 rules (top edge, left edge, right edge, corner, inner), Add rule · Verify: `E2E`

11.F Palette Manager [T-P10, T-O10]
- [ ] P11.33 BG and OBJ banks with used-by lists, colour editor with 15-bit note · Verify: `E2E`
- [ ] P11.34 Merge near-duplicate colours, sort by hue, lock index 0 as transparent, Optimize Palettes (Assets menu) · Verify: `UT` merge result
- [ ] P11.35 Import .pal / .gpl, Export · Verify: `E2E`
- [ ] P11.36 Palette effects Fade / Cycle / Flash with preview slider and the Palette Effect popover · Verify: `E2E`

11.G Font Manager [T-P09, T-D15, T-D16] (data and rendering in Phase 8)
- [ ] P11.37 Font list, font settings, source, glyph grid with code points, character sets, metrics, glyph detail, Thai layout toggles, rendering options, live GBA preview, ROM / VRAM stats · Verify: `E2E`
- [ ] P11.38 New Font and Add Characters dialogs · Verify: `E2E`

11.H Database [T-P07, T-D17, T-D18, T-D19] (data in Phase 9)
- [ ] P11.39 Tables list with counts, grid with filter and Add row, record panel, schema panel, "Use it in a script" sample, "Used by" · Verify: `E2E`
- [ ] P11.40 New Table, Add Field, Import CSV dialogs · Verify: `E2E`

11.I Localization [T-P14, T-D20]
- [ ] P11.41 Languages with progress, string table (key, source, target, fit), search, groups, notes, speaker, GBA preview, status of missing strings · Verify: `E2E`
- [ ] P11.42 Add Language dialog, Export .csv · Verify: `E2E`

11.J Audio and SFX Designer [T-P15, T-P25]
- [ ] P11.43 Music list, track info (BPM, speed, patterns, length, loop, loop start, format, volume, fade on scene change), tracker pattern view, preview playback · Verify: `E2E`
- [ ] P11.44 Sound effect list, GBA channel usage, mixer per channel, Preview SFX, audio ROM and mixer CPU · Verify: `E2E`
- [ ] P11.45 SFX Designer: output Hardware (DMG) or Bake to .wav, channel, duty, pitch and volume graph, presets (Jump, Coin, Hurt, Laser, Explosion, Power-up, Select), envelope and sweep fields, register view, script snippet · Verify: `E2E`; preview sounds the same as `EMU`

11.K Dialogue Editor [T-P16, T-D21, T-M23, T-M24]
- [ ] P11.46 Conversations list, lines with speaker and language toggle (Thai / English), choices with targets, speakers list, line inspector (portrait, text speed, blip, conditions), GBA preview · Verify: `E2E`
- [ ] P11.47 New Speaker dialog, Add to Dialogue and Command menus · Verify: `E2E`

11.L Logic Graph [T-P17, T-O08, T-M21]
- [ ] P11.48 Canvas with nodes, wires, pins by type, zoom (minus / plus / Fit), node palette by category, node inspector, Generated C# panel in sync, status (nodes, wires, compiled) · Verify: `E2E`
- [ ] P11.49 Add Node popover (search, Enter adds, Shift+Enter adds and connects) and Node Context Menu (incl. Show generated C#) · Verify: `E2E`

11.M World Map [T-P13, T-O09, T-M22]
- [ ] P11.50 Scene cards with links, start scene marker, inspector of the selected scene (size, video mode, backgrounds, links, music, events), New Scene, zoom · Verify: `E2E`
- [ ] P11.51 Link Settings popover and Scene Context Menu · Verify: `E2E`

11.N Script Editor [T-P04, T-D14, T-O11]
- [ ] P11.52 Script list, breadcrumb to member, code editor with line numbers, highlighting, autocomplete, format on save, tab size, inline analyzer underline and diagnostic popup with quick fixes [T-S07 built-in editor options] · Verify: `E2E`
- [ ] P11.53 Side panel: attached to, compiles to, exposed fields, outline, API quick reference, Open Scripting API · Verify: `E2E`
- [ ] P11.54 Bottom panel Problems / Terminal / Output; status line (Ln / Col, language, toolchain) · Verify: `E2E`
- [ ] P11.55 Find & Replace (case, whole word, regex; this file / all scripts / whole project), New Script dialog, Open externally · Verify: `E2E`

11.O Scripting API [T-P18]
- [ ] P11.56 API tree, search, member table, example with Copy example, see also, works offline ("Docs v0.1 · Offline") · Verify: `E2E` with network off

11.P Build windows [T-D06, T-D23, T-P11, T-D05, T-D07]
- [ ] P11.57 Build ROM and Build ROM · Failed bound to the real build stream (Phase 7) · Verify: `E2E`
- [ ] P11.58 Build Report: header, ROM size, build time split, warnings, free VRAM, ROM breakdown, largest assets, history, Export, Open build folder, Run in mGBA, Flash to cartridge, Build again · Verify: `E2E`
- [ ] P11.59 Export ROM dialog · Verify: `E2E`
- [ ] P11.60 Flash to Cartridge per D-12: device detect, cartridge info, back up save first, verify after writing, progress · Verify: `HW`

11.Q Settings dialogs [T-D08, T-D09, T-S01..T-S09, T-D10, T-D11, T-O12]
- [ ] P11.61 Project Settings tabs Input, General, Save data, Build, Scripting, Localization with every field in the frames · Verify: `E2E` each field persists and changes the build
- [ ] P11.62 Preferences tabs Emulator, Appearance, Editor, Paths, Shortcuts with every field in the frames · Verify: `E2E`
- [ ] P11.63 Keyboard Shortcuts dialog (search, groups) and About (version, date, credits, Licenses, Website) · Verify: `E2E`

11.R Import [T-D04, T-D24]
- [ ] P11.64 Import Asset: drop or browse, type detection per file, per-file options, import count; errors dialog with fix buttons · Verify: `E2E`

11.S Menus [T-M01..T-M24]
- [ ] P11.65 Every menu item in 4.5 does its action or opens its window; disabled states when not applicable · Verify: `E2E` per item
- [ ] P11.66 Tools › Screenshot / GIF captures the Game view to PNG / GIF [T-M06] · Verify: `E2E`
- [ ] P11.67 Help › Report a Bug…, Check for Updates per D-13 [T-M07] · Verify: `E2E`

Exit gate:
- [ ] P11.X1 Every T-P, T-D, T-S, T-O, T-M, T-L in section 4 has both boxes ticked; all T-B and T-C ticked · Verify: `USER`

### Phase 12 · Play / Simulate mode

> Scope: Play Mode, Play Speed menu, Preferences · Emulator, Build › Play / Play from Here / Build & Run in Emulator ความหมายของ "Simulate" ตาม D-16

Entry gate:
- [ ] P12.E1 Phase 7 and 10 exits ticked, D-06 and D-16 decided

Items:
- [ ] P12.01 mGBA core embedded in the Game tab: scale 3x, pixel-perfect filter, audio, pause when unfocused, fast-forward key (Tab, 4x) [T-D09] · Verify: `E2E`; `BUD` 59.7 fps
- [ ] P12.02 Play = incremental build + run; Play from Here; Build & Run in Emulator; Run in mGBA from Build Report [T-M05, T-P11] · Verify: `E2E`
- [ ] P12.03 Play controls Play / Pause / Step / Stop; Stop returns to Scene Editor [T-P02, T-C11] · Verify: `E2E`
- [ ] P12.04 Play Speed 1/4x to 4x and Frame step [T-M18] · Verify: `E2E`
- [ ] P12.05 Overlays Hitboxes and Tile collision drawn over the game view [T-P02] · Verify: `SHOT`
- [ ] P12.06 Live Inspector: actor state, position, velocity, on ground, script fields [T-P02] · Verify: `E2E` values change while running
- [ ] P12.07 Console receives Debug.Log and engine logs with timestamps [T-P02, T-P05] · Verify: `E2E`
- [ ] P12.08 Status bar: frame number, fps, VBlank time free, CPU %, Running / Paused, Hot reload on [T-P02] · Verify: `E2E`
- [ ] P12.09 Keyboard and gamepad mapped from Project Settings · Input [T-D08] · Verify: `E2E`
- [ ] P12.10 Save file kept per project while playing (infra: testing Save data) · Verify: `E2E` save, stop, play, load
- [ ] P12.11 Hot reload in Play mode per D-15 · Verify: `E2E`

Exit gate:
- [ ] P12.X1 The fixture plays inside the editor at full speed with live Inspector and Console · Verify: `USER`

### Phase 13 · Dev tools

> Scope: Debugger (Profiler), Debugger · Memory, Debugger · Palettes, Budget Details, Build Report, Screenshot / GIF ไม่เพิ่ม view ที่ไม่มีใน Figma

Entry gate:
- [ ] P13.E1 Phase 12 exit ticked, D-21 decided

Items:
- [ ] P13.01 Profiler: frame time for the last 120 frames against 16.7 ms, this frame split Logic / Render / Audio / Idle [T-P19] · Verify: `RT` instrumentation; `E2E`
- [ ] P13.02 VRAM tiles view by char block (BG / OBJ) [T-P19] · Verify: `E2E` matches mGBA memory
- [ ] P13.03 OAM sprites table (#, actor, X, Y, size, tile, palette, priority, flags; 12 / 128 in use) [T-P19] · Verify: `E2E`
- [ ] P13.04 Palettes view: live palette RAM, changed-this-frame outline, address, BGR555 value, RGB, used by, palette effect, Open in Palette Manager, Break when this colour changes [T-P24] · Verify: `E2E`
- [ ] P13.05 Memory view: memory map of EWRAM / IWRAM by category, watch list by name and address, hex view, Add watch [T-P23] · Verify: `E2E`
- [ ] P13.06 Breakpoints: C# line breakpoints, VM script breakpoints, "On scene load"; Paused at frame N; call stack with C# names [T-P19] · Verify: `E2E` stop at `PlayerController.cs:19`
- [ ] P13.07 Budget Details live numbers and suggestions [T-O06] · Verify: `UT` palette-sharing suggestion for the fixture
- [ ] P13.08 Build Report history across builds [T-P11] · Verify: `E2E`
- [ ] P13.09 Debug button (#9) and Tools › Debugger open the Debugger [T-B09] · Verify: `E2E`

Exit gate:
- [ ] P13.X1 All debugger views show correct live data for the fixture · Verify: `USER`

### Phase 14 · External IDE support

> Scope: Preferences · Editor, Preferences · Paths, Script Editor › Open externally, New Script "Open in the external editor after creating"

Entry gate:
- [ ] P14.E1 Phase 6 tier 1 and Phase 10 exits ticked

Items:
- [ ] P14.01 Script editor Built-in / External; external editor VS Code / JetBrains Rider / Visual Studio / Custom…; executable path with found status [T-S07, T-S08] · Verify: `UT` detection incl. `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe`
- [ ] P14.02 Generate `.csproj` (and `.vscode/` settings) for IntelliSense with the API reference assembly and the analyzer package; regenerated when scripts change [T-S07] · Verify: `UT` `dotnet build` of the generated project reports the same GAE diagnostics as the built-in editor
- [ ] P14.03 "Open .cs with" and Open externally open the file at the right line [T-S07, T-Y03] · Verify: `E2E` in each installed IDE; record `n/a` for IDEs not installed
- [ ] P14.04 Hot reload when an external save is detected [T-S07] · Verify: `E2E`
- [ ] P14.05 New Script "Open in the external editor after creating" [T-D14] · Verify: `E2E`
- [ ] P14.06 API reference assembly with XML docs shipped with the editor · Verify: `UT` autocomplete lists the members of P6.09
- [ ] P14.07 Analyzer rules and the "Change to fixed" fix work inside VS Code, Rider and Visual Studio · Verify: `E2E` per IDE

Exit gate:
- [ ] P14.X1 A script edited in VS Code reloads in the running game · Verify: `USER`

### Phase 15 · Genre kits, templates, tutorials, samples

> Scope: เฉพาะ template ที่ D-09 เลือก ตามคำอธิบายใน Launcher · Templates และ New Project หน้าจอในเกมที่ยังไม่ได้ออกแบบต้องออกแบบก่อน (1.4 ข้อ 6) art / เพลง / ข้อความต้องเป็นของใหม่ (D-20)

Entry gate:
- [ ] P15.E1 Phases 4-9 and 11 exits ticked, D-09 and D-20 decided

Items (each template: `RT` builds, `EMU` 60 fps, `HW` where possible, `E2E` New Project creates it):
- [ ] P15.01 Platformer: player controller with jump and coyote time, enemy patrol and stomp, coins / score / HUD, checkpoints and lives, title screen and pause menu (Mode 0, 3 BG layers, 9 actors, 4 scripts) [T-P20, T-D01]
- [ ] P15.02 Top-down Adventure: explore, talk to NPCs, push blocks, open doors [T-P20, T-D01]
- [ ] P15.03 Turn-based RPG: party, stats, battles, items and shops from the Database [T-P20, T-D01]
- [ ] P15.04 Shoot 'em up: scrolling stages, bullet patterns, power-ups [T-P20, T-D01]
- [ ] P15.05 Puzzle: falling blocks or grids with match rules [T-P20, T-D01]
- [ ] P15.06 Visual Novel: portraits, choices, flags and Thai text [T-P20, T-D01]
- [ ] P15.07 Beat 'em up: side-scrolling fights, combos, enemy waves [T-P20]
- [ ] P15.08 Racing (Mode 7): rotating ground plane, laps, timer [T-P20]
- [ ] P15.09 Template content is original (art, music, text) · Verify: `REV` per G-LEGAL
- [ ] P15.10 Tutorials: Your first platformer (8 steps), Scripting in C# for GBA, Making fonts with Thai, Palettes & VRAM, Sound & music [T-P21, T-O13] · Verify: `DOC`; `E2E` steps tick themselves
- [ ] P15.11 Sample projects: Sky Hopper demo, Cave Quest, Snow Run, Dusk Story [T-P21] · Verify: `RT` each builds and boots
- [ ] P15.12 Documentation hub: Scripting API, GBA hardware guide, Keyboard shortcuts, Release notes, Community, Report a bug [T-P21] · Verify: `DOC`

Exit gate:
- [ ] P15.X1 Every chosen template plays start to finish on emulator (and hardware if available) · Verify: `USER`

### Phase 16 · Packaging and release

> Scope: การส่งมอบ editor ตาม D-19 และสิ่งที่ About, Help และ Brand ใน Figma แสดง

Entry gate:
- [ ] P16.E1 All earlier exits and all gates in section 6 ticked

Items:
- [ ] P16.01 Windows package per D-19; first run detects or guides the devkitPro install; uninstall keeps user projects · Verify: install on a clean Windows user account
- [ ] P16.02 About: version, date, credits text, Licenses list (every third-party item), Website [T-D11] · Verify: `REV` against `docs/THIRD_PARTY.md`
- [ ] P16.03 Check for Updates and Report a Bug per D-13 [T-M07] · Verify: `E2E`
- [ ] P16.04 Brand applied: icon, wordmark, Launcher brand block, About icon, banners [T-R01..T-R03] · Verify: `SHOT`
- [ ] P16.05 Release notes for the version, shown in Launcher "What's new" · Verify: `DOC`
- [ ] P16.06 Release candidate passes every section 4 box and every gate; sample ROMs boot on hardware if available · Verify: CI report, `HW`
- [ ] P16.07 Code signing and update channel per D-19 · Verify: signature check

Exit gate:
- [ ] P16.X1 Release approved · Verify: `USER`

---

## 6. Cross-cutting gates

> Scope: gate เหล่านี้ใช้กับทุก phase phase ใดจะผ่าน exit gate ได้ต้องไม่ทำให้ gate เหล่านี้พัง

### G-HW · GBA hardware limits

- [ ] G-HW.01 VRAM: BG <= 64 KB in tile modes, OBJ <= 32 KB (16 KB in bitmap modes), char block and screen block accounting matches hardware · Verify: `BUD` limit checks fail the build with a clear message; `RT` limits test ROM
- [ ] G-HW.02 OAM <= 128 sprites and <= 32 affine matrices; per-scanline sprite overload warned · Verify: `BUD`, `RT`
- [ ] G-HW.03 Palettes <= 16 BG + 16 OBJ banks of 16 colours, index 0 transparent · Verify: `BUD`
- [ ] G-HW.04 IWRAM <= 32 KB including stack, EWRAM <= 256 KB, split per blueprint 10 defaults · Verify: `BUD` from the map file
- [ ] G-HW.05 ROM <= 32 MB with the warning threshold from Project Settings · Build · Verify: `BUD`
- [ ] G-HW.06 Save data fits the chosen save type [T-S02 "189 B / 32 KB"] · Verify: `UT`
- [ ] G-HW.07 60 fps: work fits in 280,896 cycles per frame, commit fits in VBlank, each staged-load step fits in one frame · Verify: `BUD` profiler on the worst scene of each template
- [ ] G-HW.08 Audio channel use is within maxmod settings and DMG channels do not clash · Verify: `UT`

### G-THAI · Thai text rendering

- [ ] G-THAI.01 Every consonant with every upper vowel, tone mark and lower vowel renders correctly, including ascender and descender consonants, sara am, mai yamok, Thai digits, mixed Thai / Latin lines · Verify: `UT` corpus vs golden bitmaps, `RT` ROM capture
- [ ] G-THAI.02 Dictionary wrap never splits a word and matches the Localization fit check · Verify: `UT`
- [ ] G-THAI.03 Editor preview equals the ROM output pixel for pixel · Verify: P8.09
- [ ] G-THAI.04 Thai works in every surface shown in Figma: dialogue box, Localization, Database localized fields, Dialogue Editor, Font Manager, New Speaker (TH name) · Verify: `E2E`
- [ ] G-THAI.05 Thai font and word list licences recorded (D-17) · Verify: `REV`
- [ ] G-THAI.06 Thai on real hardware photo or flash-cart capture · Verify: `HW` (or `n/a until hardware`)

### G-ERR · Error handling

- [ ] G-ERR.01 Every user-facing error says what happened, why, and how to fix it, in plain words (pattern of T-D24 and T-D23) · Verify: `REV` of all error strings
- [ ] G-ERR.02 Every compile or build error links to `file(line,col)` · Verify: `E2E`
- [ ] G-ERR.03 No silent failure: pool full, budget exceeded, missing string, missing glyph, failed import all reach the Console (and fail the build where a setting says so) · Verify: `UT` per case
- [ ] G-ERR.04 Long operations can be cancelled and leave a consistent state (open project, build, flash, import) · Verify: `E2E`
- [ ] G-ERR.05 Missing or wrong tool paths show which path and point to Preferences · Paths · Verify: `E2E`
- [ ] G-ERR.06 Editor crash keeps the user's work through autosave and writes a crash log (infra) · Verify: `E2E` forced crash
- [ ] G-ERR.07 Runtime Debug builds: asserts log the file and line to the Console and stop; Release behaviour per "Keep asserts" · Verify: `RT`

### G-PERF · Performance budgets

- [ ] G-PERF.01 Every template holds 60 fps on mGBA (and hardware if available) in its heaviest scene · Verify: `BUD`
- [ ] G-PERF.02 Incremental build of the fixture after a one-line change finishes in under 1 s ("Usually under a second", T-D06) · Verify: `BUD` CI timing
- [ ] G-PERF.03 Clean build, project open and Play start times have targets agreed in Phase 1 and are met · Verify: `BUD`
- [ ] G-PERF.04 Editor stays responsive while building and playing (UI thread never blocked by build or emulator) · Verify: `E2E` timing
- [ ] G-PERF.05 Audio mixer CPU stays near the Figma value (6% for 8 ch at 21 kHz) · Verify: `BUD`

### G-DOCS · Documentation

- [ ] G-DOCS.01 Scripting API docs generated from source, offline, cover 100% of public members · Verify: `UT` doc coverage
- [ ] G-DOCS.02 One user page per editor window, starting from its Figma caption text · Verify: `DOC`
- [ ] G-DOCS.03 Tutorials and the GBA hardware guide listed in Launcher · Learn · Verify: `DOC`
- [ ] G-DOCS.04 Developer docs: ARCHITECTURE, BUILD, file formats, DECISIONS, TOOLCHAIN, THIRD_PARTY kept current · Verify: `REV` at each phase exit
- [ ] G-DOCS.05 Release notes per release · Verify: `DOC`

### G-LEGAL · Legal and IP

- [ ] G-LEGAL.01 No code, assets, text, map data, data tables, music, names or opcodes copied from pokeemerald; references cite file names only · Verify: `REV` plus a scan for distinctive pokeemerald identifiers (for example `gMain`, `gSaveBlock1Ptr`, `VAR_RESULT`, `MUS_`) in the repo
- [ ] G-LEGAL.02 No Nintendo assets, BIOS files or commercial ROMs in the repo, tests or installer · Verify: `REV`
- [ ] G-LEGAL.03 Product name is "AGB Studio"; "GBA" and "Game Boy" never appear in the product name, app id, installer name, window title or icon; descriptive use only (for example "for Game Boy Advance"); never "GB Studio" or "GBA Studio" · Verify: `REV` + string scan
- [ ] G-LEGAL.04 Every third-party component and font licence recorded and shown in About › Licenses (mGBA, libtonc, maxmod, libgba if linked, Roslyn, Inter, JetBrains Mono, IBM Plex Sans Thai, Noto Sans Thai, Nunito, Press Start 2P, Thai word list) · Verify: `REV`
- [ ] G-LEGAL.05 Butano and other engines used as reference only, no code copied unless its licence is followed and recorded · Verify: `REV`
- [ ] G-LEGAL.06 Template, sample and tutorial content is original · Verify: `REV`
- [ ] G-LEGAL.07 Runtime licence for user games decided (D-05) and shown in docs · Verify: `DOC`

### G-EMOJI · No emoji (user rule)

- [ ] G-EMOJI.01 No emoji in code, UI strings, docs, commit messages, release notes, templates · Verify: CI codepoint scan (P1.07)

### G-SCOPE · Scope kept to Figma

- [ ] G-SCOPE.01 Every merged change names a `T-...` or `infra: ...` · Verify: `REV` of the PR template field
- [ ] G-SCOPE.02 No window, button, panel or setting in the app that is not in section 4 · Verify: `REV` at each phase exit against the Figma inventory
- [ ] G-SCOPE.03 Scope log reviewed with the user at each phase exit · Verify: `USER`

---

## 7. Counts at a glance (from Figma on 2026-10-31)

| Group | Count | Section |
|---|---|---|
| Pages | 19 + 7 round 2 = 26 | 4.1, 4.6 |
| Dialogs | 12 + 9 settings tabs + 12 round 2 = 33 | 4.2, 4.3, 4.7 |
| Popovers | 7 + 6 round 2 = 13 | 4.4, 4.8 |
| Light theme frames | 3 | 4.10 |
| Windows in "AGB Studio · Screens" | 75 | 4.1-4.10 |
| Menus | 16 (board 2) + 8 (board 4) = 24 | 4.5 |
| Mapped buttons | 35 (board 1) + 52 (board 4) | 4.12, 4.13 |
| Components | 47 buildable + 2 doc-only | 4.16 |
| Colour tokens | 29 per theme, 2 collections | 4.15 |
| Text styles | 20 | 4.15 |
| Icons | 70 | 4.15 |
| Prototype links | 120, flows "AGB Studio · start" and "AGB Studio · editor" | 4.14 |

---

## 8. Scope log

บันทึกทุกครั้งที่เจอสิ่งที่อยู่นอก Figma, Figma ขัดกันเอง, หรือ item ที่เคยติ๊กแล้วพัง

| Date | Who | Item / window | What was found | Proposed action | User decision |
|---|---|---|---|---|---|
| 2026-10-31 | checklist session | 1.4 items 1-9 | Design issues found while reading Figma | Settle in Phase 1 (P1.02) | pending |
| 2026-10-31 | build session | 1.4 item 6, D-20, D-23 | In-game screens (boot splash, title, pause, options) were missing from Figma | Four frames were added to "AGB Studio · Screens" (new row at y 24880) and built to match; the brief said Figma is read-only | pending: keep or remove the frames |
| 2026-10-31 | build session | T-P01, T-P05, T-P06, T-P08, T-P19, T-P22, T-P24, T-D04, T-D07, T-D09, T-D10, T-S02, T-S04, T-S06, T-S07, T-S08, T-O01, T-O04, T-O07, T-O08 | Code differs from Figma captions (list in docs/evidence/editor-gaps.md) | Fix in the editor before ticking those windows | pending |
| 2026-10-31 | build session | G-SCOPE.01 | Commits before 2026-10-31 do not all name a T-id or infra: reason | Name T-ids from now on; history is not rewritten | pending |
| 2026-10-31 | build session | P15.11 | Cave Quest, Snow Run and Dusk Story are built from the RPG, Racing and Visual Novel templates with their own colours (and a new story for Dusk Story) | Accept as samples, or ask for larger original samples | pending |
| 2026-10-31 | build session | T-P20 Racing card | Figma lists "Opponent cars"; the Racing template has two computer karts that follow the centre line (no overtaking AI) | Accept | pending |
| 2026-10-31 | build session | T-P17 On Touch Tag | Figma's code panel shows the tag as an if around the body; the generator writes a guard (if tag differs, return) so it can be told apart from a Compare Tag node | Accept | pending |
| 2026-10-31 | build session | G-SCOPE.02 | Windows in the app that section 4 does not list: delete-project confirmation, devkitPro-not-found notice (P16.01), Scripting API "See also" row, 11 operating-system file dialogs beyond 4.11 (docs/evidence/scope-inventory-review.md) | Accept as needed by Figma flows (delete asks first, first-run guidance, OS dialogs) or remove | pending |
| 2026-10-31 | build session | D-24 | Logs go to the AGB debug channel, not mGBA's debug print (P2.11) | Confirm D-24 | pending |
| 2026-10-31 | build session | P5.01 | Primary / secondary tilesets: Figma T-P03 shows one tileset per map and no control for it | Smallest design written (tileset names the primary it extends; build de-duplicates against it; runtime keeps the primary in VRAM) - build it, or drop P5.01 | pending |
| 2026-10-31 | build session | P5.02 | Two-layer metatiles: Figma paints each layer separately and the model already gives each layer its own grid | Reword P5.02 to accept per-layer grids, or pack layer pairs at build time (design written) | pending |
