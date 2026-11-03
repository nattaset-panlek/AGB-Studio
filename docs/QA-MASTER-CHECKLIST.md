# AGB Studio · Master QA Checklist (130 Test Cases)

> **คู่มือและรายการทดสอบระบบ AGB Studio ฉบับสมบูรณ์ (End-to-End QA)**  
> จัดทำขึ้นสำหรับการทำ Manual QA, Verification และ Regression Testing เพื่อให้มั่นใจว่าการแก้ไขจุดใดๆ จะไม่กระทบกับฟีเจอร์เดิมที่ผ่านการทดสอบแล้ว

---

## สารบัญหมวดการทดสอบ
1. [Module 01: Launcher & Project Lifecycle (10 TCs)](#module-01-launcher--project-lifecycle)
2. [Module 02: Window Shell, Title Bar & Theme (10 TCs)](#module-02-window-shell-title-bar--theme)
3. [Module 03: Document Tabs & Workspace Lifecycle (12 TCs)](#module-03-document-tabs--workspace-lifecycle)
4. [Module 04: Scene Editor & Viewport (12 TCs)](#module-04-scene-editor--viewport)
5. [Module 05: Hierarchy & Scene Tree (8 TCs)](#module-05-hierarchy--scene-tree)
6. [Module 06: Inspector & Component System (8 TCs)](#module-06-inspector--component-system)
7. [Module 07: Tilemap & Collision Editor (10 TCs)](#module-07-tilemap--collision-editor)
8. [Module 08: Sprite & Animation Editor (8 TCs)](#module-08-sprite--animation-editor)
9. [Module 09: Audio Tracker & SFX Designer (8 TCs)](#module-09-audio-tracker--sfx-designer)
10. [Module 10: Thai Font Manager & Typography (8 TCs)](#module-10-thai-font-manager--typography)
11. [Module 11: Database & Dialogue Editor (8 TCs)](#module-11-database--dialogue-editor)
12. [Module 12: C# Scripting & Logic Graph (10 TCs)](#module-12-c-scripting--logic-graph)
13. [Module 13: Play Mode & mGBA Emulation (10 TCs)](#module-13-play-mode--mgba-emulation)
14. [Module 14: Build Pipeline & Compilation (8 TCs)](#module-14-build-pipeline--compilation)
15. [Module 15: ROM Export & Packaging (8 TCs)](#module-15-rom-export--packaging)

---

### Module 01: Launcher & Project Lifecycle
- [x] **TC-LCH-01:** เปิดโปรแกรมครั้งแรก หน้าต่าง Launcher เปิดขึ้นมา แสดงแท็บ Recent Projects, Templates และ Learn ครบถ้วน
- [x] **TC-LCH-02:** New Project Dialog แสดงเทมเพลตครบ 8 แนวเกม (Platformer, Top-down, Visual Novel, Puzzle, Shmup, RPG, Racing Mode 7, Brawler)
- [x] **TC-LCH-03:** การตั้งชื่อโปรเจกต์: ตรวจสอบอักขระพิเศษ, เว้นวรรค, และความยาวอัตโนมัติ (แปลงเป็น Safe ROM Title)
- [x] **TC-LCH-04:** สร้างโปรเจกต์จากเทมเพลต Platformer สำเร็จ โครงสร้างไฟล์ `.agbproj`, `scenes/`, `scripts/`, `maps/` ถูกสร้างครบ
- [x] **TC-LCH-05:** สร้างโปรเจกต์จากเทมเพลต Visual Novel สำเร็จ ตรวจพบไฟล์บทสนทนาและฟอนต์ภาษาไทย
- [x] **TC-LCH-06:** สร้างโปรเจกต์แบบ Empty Project สำเร็จ หน้าต่างเปิดเข้าสู่ Scene Editor สถานะว่าง (T-P22)
- [x] **TC-LCH-07:** การเปิดโปรเจกต์เดิมผ่าน Recent Projects: ข้อมูลการเลือกฉากและไฟล์ล่าสุดถูกโหลดกลับมาถูกต้อง
- [x] **TC-LCH-08:** การเปิดโปรเจกต์ใหม่ขณะที่มีไฟล์ค้างแก้ไข: มี Dialog แจ้งเตือน Unsaved Changes (Save All / Discard / Cancel)
- [x] **TC-LCH-09:** การตั้งค่า Preferences "Open last project on start": ปิดโปรแกรมแล้วเปิดใหม่ ต้องเข้าโปรเจกต์เดิมทันที
- [x] **TC-LCH-10:** การเคลียร์ประวัติ Recent Projects: เมนู File -> Clear Recent ล้างรายการโดยไม่ลบไฟล์จริงในเครื่อง

---

### Module 02: Window Shell, Title Bar & Theme
- [x] **TC-SHL-01:** Title Bar แสดงชื่อโปรเจกต์ปัจจุบัน และชื่อฉาก/เอกสารที่กำลังโฟกัส
- [x] **TC-SHL-02:** เมนูหลัก 7 แถบ (File, Edit, Assets, Scene, Build, Tools, Help) สามารถคลิกเปิดและตอบสนองได้ทุกเมนู
- [x] **TC-SHL-03:** Command Palette (Ctrl+K): พิมพ์ค้นหาคำสั่ง, เลือกด้วยลูกศร และกด Enter เพื่อรันคำสั่งได้
- [x] **TC-SHL-04:** Command Palette (Ctrl+P / ค้นหาไฟล์): พิมพ์ชื่อไฟล์ เช่น `PlayerController.cs` แล้วกดเปิดไฟล์ได้ทันที
- [x] **TC-SHL-05:** Theme Switching: สลับ Dark Theme และ Light Theme การเรนเดอร์สีของ Control ทุกตัวเปลี่ยนตาม Palette ทันที
- [x] **TC-SHL-06:** Accent Color: เปลี่ยนสี Accent ใน Preferences -> สียืนยันปุ่ม, ขอบโฟกัส, และไฮไลต์เปลี่ยนตาม
- [x] **TC-SHL-07:** UI Scaling: ปรับ Scale (100%, 125%, 150%) ใน Preferences -> ขนาดฟอนต์และองค์ประกอบขยายสัดส่วนถูกต้อง ไม่แตก
- [x] **TC-SHL-08:** Keyboard Shortcuts Dialog (Ctrl+/): แสดงรายการคีย์ลัดครบถ้วน และสามารถกดปิดด้วย Esc ได้
- [x] **TC-SHL-09:** Status Bar (ด้านล่าง): แสดงสถานะ Toolchain (devkitARM / Ready), ขนาด ROM ล่าสุด, และไอคอนสถานะ Emulator
- [x] **TC-SHL-10:** Toast Notifications: เมื่อมีคำสั่งผิดพลาดหรือแจ้งเตือน มีกล่อง Toast เด้งมุมขวาล่าง และกดปิดได้

---

### Module 03: Document Tabs & Workspace Lifecycle (ปัญหาที่พบล่าสุด)
- [x] **TC-TAB-01:** เปิด Scene Editor (`T-P01`) มีแท็บหลัก `[ Scene ]` และ `[ Game ]` แสดงอยู่เสมอ
- [x] **TC-TAB-02:** ดับเบิลคลิกเปิดสคริปต์ `.cs` จาก Assets Panel -> แท็บสคริปต์ถูกเพิ่มเข้าไปในแถบ DocTabs ด้านบน
- [x] **TC-TAB-03:** ดับเบิลคลิกเปิดไฟล์ Map `.map` จาก Assets Panel -> แท็บแมพถูกเพิ่มเข้าไปในแถบ DocTabs
- [x] **TC-TAB-04:** สลับไปหน้า Database (`T-P07`) -> แท็บด้านบนต้องยังคงแสดงรายการเอกสารทั้งหมด หรือมีทางย้อนกลับที่ชัดเจน
- [x] **TC-TAB-05:** **[Regression Check]** อยู่หน้า Database แล้วคลิกกลับมาที่ `[ Scene ]` -> แท็บสคริปต์, แท็บแมพ, และแท็บ Game ต้อง **ไม่หายไป**
- [x] **TC-TAB-06:** **[Regression Check]** อยู่หน้า Audio Tracker (`T-P15`) แล้วคลิกกลับมา Scene -> รายการแท็บที่เคยเปิดไว้ต้องคงอยู่ครบ
- [x] **TC-TAB-07:** **[Regression Check]** สลับไปหน้า Build Report (`T-P11`) แล้วคลิกกลับมา Scene -> ไม่เกิดปัญหาจอขาวหรือแท็บว่าง
- [x] **TC-TAB-08:** สถานะ Dirty Dot: แก้ไขไฟล์ในแท็บใดๆ -> มีจุดส้ม/ขาว (Dirty Indicator) ขึ้นที่แท็บนั้น
- [x] **TC-TAB-09:** การสั่ง Save (Ctrl+S): จุด Dirty บนแท็บที่กำลังเลือกหายไปทันทีหลังบันทึกเสร็จ
- [x] **TC-TAB-10:** การสั่ง Save All (Ctrl+Shift+S): บันทึกทุกแท็บที่ค้างการแก้ไข และเคลียร์สถานะ Dirty ทั้งหมด
- [x] **TC-TAB-11:** การปิดแท็บเอกสารด้วยปุ่ม X บนแท็บ: แท็บปิดตัวลง และโฟกัสสลับไปยังแท็บข้างเคียงอย่างนุ่มนวล
- [x] **TC-TAB-12:** การคลิกแท็บ `[ Game ]`: สลับเข้าสู่หน้าต่าง Play Mode โดยที่แท็บเอกสารอื่นๆ ด้านบนยังคงเข้าถึงได้

---

### Module 04: Scene Editor & Viewport
- [x] **TC-SCN-01:** Viewport แสดงผลฉาก 240x160 GBA พร้อมตาราง Grid ชัดเจน
- [x] **TC-SCN-02:** เครื่องมือ Select Tool (V): สามารถคลิกเลือก Actor ใน Viewport ได้ และมีกรอบไฮไลต์สีส้ม
- [x] **TC-SCN-03:** เครื่องมือ Move Tool (M): สามารถลากย้ายตำแหน่ง Actor ใน Viewport ได้แบบ Real-time
- [x] **TC-SCN-04:** Snap to Grid (Ctrl+G / Chip): ย้าย Actor แล้วตำแหน่ง Snap ตามช่องขนาด 8x8 หรือ 16x16
- [x] **TC-SCN-05:** Layer Visibility Toggles: กดเปิด-ปิด BG0, BG1, BG2, BG3, OBJ เลเยอร์ใน Viewport ซ่อน/แสดงถูกต้อง
- [x] **TC-SCN-06:** Mode Chip: คลิกชิปแสดงโหมดวิดีโอ (Mode 0 240x160) -> มี Popover สำหรับดูรายละเอียด
- [x] **TC-SCN-07:** Zoom Controls: ซูม Viewport (100%, 200%, 400%, Fit) ด้วยปุ่มและ Scroll-wheel ทำงานลื่นไหล
- [x] **TC-SCN-08:** Pan Viewport: กด Spacebar หรือปุ่มเมาส์กลางค้างเพื่อเลื่อนหน้าจอฉากได้
- [x] **TC-SCN-09:** Scene Switcher (Popover บน Breadcrumb): คลิกเพื่อเลือกสลับฉากอื่นในโปรเจกต์ได้อย่างรวดเร็ว
- [x] **TC-SCN-10:** New Scene Dialog: สร้างฉากใหม่ กำหนดชื่อและขนาดแมพเริ่มต้นได้
- [x] **TC-SCN-11:** Drag & Drop Sprite: ลากการ์ด Sprite จากพาเนล Assets ด้านล่างมาวางใน Viewport -> เกิดเป็น Actor ตัวใหม่ทันที
- [x] **TC-SCN-12:** Undo / Redo: ลากย้าย Actor แล้วกด Ctrl+Z -> ย้อนกลับตำแหน่งเดิม, กด Ctrl+Y -> กลับไปตำแหน่งใหม่

---

### Module 05: Hierarchy & Scene Tree
- [x] **TC-HIE-01:** Hierarchy Panel แสดงรายชื่อ Actor ทั้งหมดในฉากปัจจุบันอย่างถูกต้อง
- [x] **TC-HIE-02:** คลิกเลือก Actor ใน Hierarchy -> Viewport และ Inspector โฟกัสไปที่ Actor ตัวเดียวกัน
- [x] **TC-HIE-03:** ช่องค้นหา Hierarchy Search: พิมพ์กรองชื่อ Actor -> แสดงเฉพาะ Actor ที่ตรงกับคำค้นหา
- [x] **TC-HIE-04:** ปุ่ม Plus (+) บน Hierarchy Header: เปิดเมนู Add Actor สร้าง Empty Actor หรือ Preset Actor ได้
- [x] **TC-HIE-05:** Context Menu บน Actor: คลิกขวาเพื่อ Rename, Duplicate, Delete, Focus In Viewport
- [x] **TC-HIE-06:** Duplicate Actor (Ctrl+D): โคลน Actor ตัวใหม่พร้อมค่า Transform และ Components เดิม
- [x] **TC-HIE-07:** Delete Actor (Del): ลบ Actor ออกจากฉาก และสามารถกด Undo กู้คืนได้
- [x] **TC-HIE-08:** Multi-selection: กด Shift หรือ Ctrl คลิกเลือก Actor หลายตัวพร้อมกันเพื่อลบหรือย้ายเป็นกลุ่ม

---

### Module 06: Inspector & Component System
- [x] **TC-INS-01:** Inspector Header แสดงชื่อ Actor ที่เลือก และมีปุ่ม Context Menu
- [x] **TC-INS-02:** Transform Component: ปรับค่า Position (X, Y) ผ่านการกรอกตัวเลข -> ตำแหน่งใน Viewport เปลี่ยนทันที
- [x] **TC-INS-03:** SpriteRenderer Component: แสดงตัวอย่าง Sprite, ปรับ Frame Index, และเลือก Animation State ได้
- [x] **TC-INS-04:** Component Picker (Add Component): มีรายการ Top-down Mover, Platformer Controller, Collider, Trigger
- [x] **TC-INS-05:** ลบ Component ออกจาก Actor: มีปุ่มลบหรือคลิกขวา Remove Component
- [x] **TC-INS-06:** Script Component: แสดงฟิลด์ตัวแปร `[SerializeField]` หรือ public variables จากโค้ด C# ให้ปรับค่าได้
- [x] **TC-INS-07:** การตรวจสอบค่าขอบเขต: กรอกตัวเลขเกินขนาดหน้าจอ GBA หรือติดลบ -> มีการ Validate และจำกัดค่าอย่างปลอดภัย
- [x] **TC-INS-08:** สลับเลือก Actor อื่น: ข้อมูลใน Inspector อัปเดตเปลี่ยนตาม Actor ตัวใหม่อย่างรวดเร็ว ไม่ค้างค่าเดิม

---

### Module 07: Tilemap & Collision Editor
- [x] **TC-MAP-01:** เปิด Tilemap Editor (`T-P03`): แสดงตาราง Tileset Palette ด้านซ้าย และพื้นที่วาดแมพตรงกลาง
- [x] **TC-MAP-02:** ปากกาวาดไทล์ (Pencil Tool): เลือกไทล์จาก Palette แล้วคลิกวาดลงบนเลเยอร์แมพได้
- [x] **TC-MAP-03:** ถังสีเทไทล์ (Bucket Fill): เทไทล์ลงในพื้นที่ปิด เติมไทล์เต็มพื้นที่อย่างถูกต้อง
- [x] **TC-MAP-04:** ยางลบ (Eraser Tool): ลบไทล์บนเลเยอร์ที่เลือกให้กลายเป็นช่องโปร่งใส
- [x] **TC-MAP-05:** Collision Layer: สลับมาเลเยอร์ Collision เพื่อวาดบล็อกทึบ (Solid), ทางลาด (Slope), หรือแท่นเหยียบ (One-way)
- [x] **TC-MAP-06:** Autotile Rules: ใช้งานระบบ Autotile เชื่อมต่อขอบดิน/ขอบน้ำอัตโนมัติเมื่อวาดไทล์ติดกัน
- [x] **TC-MAP-07:** Layer Management: เพิ่ม, ลบ, จัดลำดับความลึก (Z-order) ของเลเยอร์พื้นหลัง
- [x] **TC-MAP-08:** VRAM Budget Meter: มีแถบคำนวณการใช้หน่วยความจำ Char Block / Screen Block ไม่ให้เกิน 64 KB VRAM
- [x] **TC-MAP-09:** Primary Tileset Support: ตรวจสอบการเลือกแชร์ Tileset ระหว่างฉากเพื่อประหยัด ROM
- [x] **TC-MAP-10:** บันทึกแมพ: กด Ctrl+S บันทึกลงไฟล์ `.map` และขนาดไฟล์ตรงตามโครงสร้างข้อมูล

---

### Module 08: Sprite & Animation Editor
- [x] **TC-SPR-01:** เปิด Sprite Editor (`T-P08`): แสดงภาพ Sprite Sheet และไทม์ไลน์แอนิเมชันด้านล่าง
- [x] **TC-SPR-02:** การแบ่งตาราง Frame: ตั้งค่าขนาด Frame (16x16, 32x32, 64x64) ตรงตามมาตรฐาน OAM ของ GBA
- [x] **TC-SPR-03:** Animation Timeline: เพิ่มเฟรม, ลบเฟรม, และจัดลำดับการเล่นแอนิเมชัน
- [x] **TC-SPR-04:** การตั้งค่าความเร็ว (Ticks per frame): ปรับจำนวนเฟรมต่อการเปลี่ยนรูป (เช่น 6 ticks = 10 fps)
- [x] **TC-SPR-05:** ปุ่ม Preview Animation: กด Play เพื่อดูภาพเคลื่อนไหววนลูป (Loop / Ping-pong / Once)
- [x] **TC-SPR-06:** Hitbox Editor: ลากกำหนดกรอบ Collision Box และ Hurtbox ประจำแต่ละเฟรมแอนิเมชัน
- [x] **TC-SPR-07:** OBJ Palette Assignment: เลือก Palette 1 ใน 16 Palettes ให้กับ Sprite ตัวนี้
- [x] **TC-SPR-08:** OBJ VRAM Accounting: แสดงการใช้ VRAM ของสไปรต์ ไม่ให้เกินโควตา 32 KB

---

### Module 09: Audio Tracker & SFX Designer
- [x] **TC-AUD-01:** เปิด Audio Tracker (`T-P15`): แสดงตาราง Patterns, รายการแชนเนล (4-8 Channels), และรายการ Instruments
- [x] **TC-AUD-02:** เล่นเพลงตัวอย่าง: กดปุ่ม Play สามารถเล่นไฟล์เพลง XM/MOD และหยุดได้ตามต้องการ
- [x] **TC-AUD-03:** ตัวควบคุมระดับเสียง (Volume / Panning): ปรับความดังซ้าย-ขวาในแต่ละแชนเนล
- [x] **TC-AUD-04:** เปิด SFX Designer (`T-P25`): แสดงตัวสังเคราะห์เสียงชิปซาวด์ 8-bit ของ GBA
- [x] **TC-AUD-05:** Waveform Generator: เลือกคลื่นเสียง Square 1 (Sweep), Square 2, Custom Wave, และ Noise
- [x] **TC-AUD-06:** ปุ่มพรีเซ็ตเสียงด่วน: กดปุ่ม Jump, Coin, Laser, Explosion, Hit เพื่อสร้างค่าสุ่มเสียงมาตรฐาน
- [x] **TC-AUD-07:** Export Sound Effect: สั่งแปลงเสียงเป็นรูปแบบเสียงของเอนจินเพื่อเรียกใช้ใน C# Script
- [x] **TC-AUD-08:** Resource Budget Meter: ตรวจสอบการบริโภค CPU ของระบบเสียง Maxmod ให้อยู่ในเกณฑ์ปลอดภัย

---

### Module 10: Thai Font Manager & Typography
- [x] **TC-FNT-01:** เปิด Font Manager (`T-P09`): แสดงรายการฟอนต์ในโปรเจกต์ และตาราง Glyph Palette
- [x] **TC-FNT-02:** ฟอนต์ AGB Pixel (1bpp): แสดงตัวอักษรละตินและภาษาไทยขนาดพิกเซลคมชัด สเกล 1:1
- [x] **TC-FNT-03:** การจัดวางสระบน-ล่าง: ทดสอบพิมพ์คำว่า "ที่", "น้ำ", "ปู่", "ผู้" -> วรรณยุกต์และสระไม่ทับซ้อนกัน
- [x] **TC-FNT-04:** ญ หญิง / ฐ ฐาน ตัดหาง: เมื่อมีสระอุ/สระอูอยู่ใต้ ญ หรือ ฐ -> หางตัวพยัญชนะต้องถูกตัดทอนตามหลักไวยากรณ์ไทย
- [x] **TC-FNT-05:** การตัดคำภาษาไทย (Thai Word Breaking): ข้อความยาวใน Dialogue กล่องข้อความตัดคำตามพจนานุกรม ไม่ตัดกลางคำ
- [x] **TC-FNT-06:** การนำเข้าฟอนต์ภายนอก (.bdf / .ttf): นำเข้าฟอนต์และแปลงเป็น 1bpp Character Sheet สำเร็จ
- [x] **TC-FNT-07:** ช่อง Text Preview: พิมพ์ประโยคทดสอบภาษาไทยสดๆ ในช่องพรีวิว และเห็นการเรนเดอร์ทันที
- [x] **TC-FNT-08:** Font ROM Budget: แสดงการใช้พื้นที่ ROM ของชุดอักษรภาษาไทย ไม่กินความจุเกินความจำเป็น

---

### Module 11: Database & Dialogue Editor
- [x] **TC-DAT-01:** เปิด Database Page (`T-P07`): แสดงตารางข้อมูล เช่น Items, Characters, Enemies
- [x] **TC-DAT-02:** การแก้ไขข้อมูลใน Cell: ดับเบิลคลิกแก้ไขชื่อไอเทม, ราคา, ค่าพลัง แล้วกด Enter บันทึกค่า
- [x] **TC-DAT-03:** เพิ่มแถวใหม่ (Add Row): สร้างไอเทมชิ้นใหม่ในตารางได้
- [x] **TC-DAT-04:** ลบแถว (Delete Row): ลบไอเทมที่ไม่ต้องการออกจากตาราง
- [x] **TC-DAT-05:** นำเข้าไฟล์ CSV (Import CSV): นำเข้าไฟล์ตารางจากภายนอกเข้าสู่ Database ได้ถูกต้อง
- [x] **TC-DAT-06:** Dialogue Editor (`T-P16`): สร้างบทสนทนาแบบ Node เชื่อมโยงตัวละครและประโยคคำพูด
- [x] **TC-DAT-07:** Speaker Assignment: กำหนดผู้พูดและภาพ Portrait แสดงอารมณ์ในแต่ละท่อนสนทนา
- [x] **TC-DAT-08:** Branching Choices: สร้างทางเลือกคำตอบในบทสนทนาเพื่อส่งค่า Event Flag ไปยังระบบเกม

---

### Module 12: C# Scripting & Logic Graph
- [x] **TC-SCP-01:** เปิด Script Editor (`T-P04`): แสดงโค้ด C# พร้อม Syntax Highlighting คมชัด
- [x] **TC-SCP-02:** Code Completion & IntelliSense: พิมพ์ `agb.` หรือชื่อคลาสพื้นฐาน -> มีรายการแนะนำคำสั่งขึ้นมา
- [x] **TC-SCP-03:** GAE Roslyn Analyzers: แจ้งเตือนข้อผิดพลาดทันทีหากเขียนโค้ดที่ GBA ไม่รองรับ (เช่น ห้ามใช้ Heap Allocs/GC หนักๆ)
- [x] **TC-SCP-04:** แก้ไขโค้ดแล้วกด Ctrl+S: บันทึกไฟล์สำเร็จ และแจ้งสถานะ Ready
- [x] **TC-SCP-05:** Go to Definition / Go to Line: คลิกกระโดดไปยังบรรทัดที่เกิด Error ได้อย่างแม่นยำ
- [x] **TC-SCP-06:** Logic Graph (`T-P17`): เปิดหน้าต่าง Visual Scripting แสดง Node Graph
- [x] **TC-SCP-07:** การเชื่อมโยง Node: ลากเส้นเชื่อมระหว่าง On Touch Event -> Add Score -> Play Sound
- [x] **TC-SCP-08:** Node Search Popover: คลิกขวาบน Canvas แล้วค้นหา Node ที่ต้องการเพิ่มลงในกราฟ
- [x] **TC-SCP-09:** Transpilation: Node Graph ถูกคอมไพล์แปลงเป็น C# Script เบื้องหลังโดยอัตโนมัติ
- [x] **TC-SCP-10:** Cutscene Scripting: คำสั่ง `Wait()`, `MoveTo()`, `Say()` ทำงานประสานกับ Event Script VM ได้ถูกต้อง

---

### Module 13: Play Mode & mGBA Emulation
- [x] **TC-PLY-01:** กด F5 จากหน้าต่าง Scene Editor -> ตัวเกมคอมไพล์ด่วนและเริ่มรันใน Viewport ทันที
- [x] **TC-PLY-02:** การควบคุมตัวละคร: กดปุ่มลูกศร (D-pad), Z (ปุ่ม A), X (ปุ่ม B) ตัวละครเคลื่อนที่และกระโดดได้ตามโค้ด
- [x] **TC-PLY-03:** ปุ่ม Pause: กดปุ่ม Pause บน Toolbar -> เกมหยุดนิ่งทันที ตัวนับเฟรมหยุดวิ่ง
- [x] **TC-PLY-04:** ปุ่ม Frame Step (F6): ขณะที่ Pause อยู่ กดปุ่ม Step -> เกมก้าวหน้าไปทีละ 1 เฟรม
- [x] **TC-PLY-05:** ปรับสปีดเกม (0.25x, 0.5x, 1x, 2x, 4x): ตัวเกมวิ่งเร็วขึ้นหรือช้าลงตามสปีดที่เลือก
- [x] **TC-PLY-06:** Live Inspector: ขณะที่เกมกำลังรัน คลิกดู Actor -> ค่า Position และ Velocity เปลี่ยนแปลงสดๆ
- [x] **TC-PLY-07:** Debug Overlays: ติ๊กเปิด Hitbox Overlays -> มีเส้นกรอบสีเขียว/แดงแสดงขอบเขตชนครอบตัวละคร
- [x] **TC-PLY-08:** Console Output: คำสั่ง `Debug.Log()` ใน C# ถูกพ่นข้อความออกมาที่พาเนล Console ด้านล่าง
- [x] **TC-PLY-09:** ปุ่ม Stop (Shift+F5): หยุดเกมและสลับสถานะกลับสู่ Scene Editor อย่างนุ่มนวล
- [x] **TC-PLY-10:** Save RAM Persistence: เล่นเกมจนผ่านจุดเซฟ -> ปิดเกมแล้วกด Play ใหม่ ข้อมูลเซฟยังคงอยู่

---

### Module 14: Build Pipeline & Compilation
- [x] **TC-BLD-01:** กด Ctrl+B เปิดหน้าต่าง Build ROM Dialog (`T-D06`)
- [x] **TC-BLD-02:** แสดงขั้นตอนการคอมไพล์แบบ Streaming: Convert assets -> C# to C++ -> Compile devkitARM -> Link & gbafix
- [x] **TC-BLD-03:** Asset Cache: คอมไพล์ซ้ำรอบที่สองโดยไม่แก้ Asset -> ระบบดึงจาก Cache ใช้เวลาไม่เกิน 2-3 วินาที
- [x] **TC-BLD-04:** Build ROM สำเร็จ: ได้ไฟล์ `.gba` ในโฟลเดอร์ `build/` และขนาดไฟล์ถูกต้อง
- [x] **TC-BLD-05:** กรณีมี Compile Error ใน C#: หน้าต่าง Build ล้มเหลว แสดงรายการ Error ชัดเจน และคลิกเพื่อไปยังบรรทัดที่พังได้
- [x] **TC-BLD-06:** ปุ่ม Cancel Build: กดกดยกเลิกขณะกำลังคอมไพล์ -> กระบวนการหยุดทันทีโดยที่ UI ไม่ค้าง
- [x] **TC-BLD-07:** Build Report Page (`T-P11`): เปิดดูรายงานสรุปการใช้พื้นที่ ROM, VRAM, WRAM ละเอียดยิบ
- [x] **TC-BLD-08:** ตรวจสอบ Header: gbafix ฝัง Game Title, Game Code (เช่น `AGBE`), และ Checksum ถูกต้องตามมาตรฐาน Nintendo

---

### Module 15: ROM Export & Packaging
- [x] **TC-EXP-01:** เมนู File -> Export ROM (`T-D05`): สามารถเลือกโฟลเดอร์ปลายทางเพื่อบันทึกไฟล์ `.gba` อิสระ
- [x] **TC-EXP-02:** Clean Export: ส่งออก ROM สองครั้งซ้อนไปยังไฟล์เดิม ไม่ขึ้นข้อผิดพลาด File in use
- [x] **TC-EXP-03:** นำไฟล์ `.gba` ที่ Export ออกไปเปิดในโปรแกรมจำลองภายนอก (mGBA หรือ VBA-M) -> รันได้ปกติเหมือนใน Editor
- [x] **TC-EXP-04:** ตรวจสอบขนาด ROM: ได้รับการแพดขนาดอย่างถูกต้อง (เช่น 4 MB, 8 MB, 16 MB หรือ 32 MB)
- [x] **TC-EXP-05:** Flashcart Compatibility: ROM สามารถบูตขึ้นบนตลับเกมจริง (EverDrive GBA / EZ-Flash Omega) ได้
- [x] **TC-EXP-06:** รันสคริปต์แพ็กเกจ `.\build-and-run.cmd package` -> สร้างไฟล์ zip สำเร็จใน `build\package\`
- [x] **TC-EXP-07:** แตกไฟล์ zip ในเครื่องใหม่: เปิด `AGBStudio.exe` ได้ทันทีแบบ Portable โดยไม่ต้องติดตั้งตัวเสริม
- [x] **TC-EXP-08:** First-run Wizard: โปรแกรมตรวจหา devkitPro อัตโนมัติ หรือแนะนำพาธติดตั้งให้อย่างถูกต้อง
