# ส่งต่องาน AGB-HANDOFF-1105 · Upgrade จาก GB Studio

เขียนเมื่อ 2026-11-05 จากเครื่อง `C:\Personal\AGB-Studio` เจ้าของย้ายไปทำต่อที่ PC เครื่องอื่น Claude ที่เครื่องใหม่อ่านไฟล์นี้ก่อน

## เริ่มที่เครื่องใหม่

```bash
git clone https://github.com/nattaset-panlek/AGB-Studio.git
```

```bash
git fetch origin
```

```bash
git switch handoff/2026-11-05
```

branch นี้มีงานทั้งหมดของเครื่องเดิม รวมงานที่ยังไม่ commit บน `main` ของเครื่องเดิม ส่วน `main` บน GitHub ยังอยู่ที่ `cf4bfa2` อย่า merge เข้า `main` จนกว่าเจ้าของจะสั่ง

ก่อนทำงานให้เช็ก 2 เรื่องนี้กับเครื่องใหม่:

1. รัน `dotnet build AGB-Studio.sln -c Release` แล้วรัน test สักชุด ถ้าเจอ `0x800711C7` แปลว่า Smart App Control บล็อก DLL **ห้ามแก้การตั้งค่าความปลอดภัยเอง** ให้ใช้ CI แทน
2. เช็กว่ามี devkitPro หรือไม่ ถ้าไม่มี build ROM และ test ROM จะไม่ผ่าน ให้ใช้ CI หรือถามเจ้าของ

## สถานะ

| Phase | สถานะ | ค้างอะไร |
|---|---|---|
| 17 เลือกอ้างอิงแทนการพิมพ์ | เสร็จ 7/7 มี test | P17.X1 เจ้าของยังไม่ได้ลองในโปรแกรม |
| 18 วางและทาสีบนฉาก | เขียนแล้ว **ยังไม่ติ๊กและยังไม่รู้ผล test** | P18.E1, ผล CI, Scope log, P18.04, P18.X1 |
| 19 Node ที่เรียก API ได้ทันที | เขียนแล้ว **ยังไม่ติ๊กและยังไม่รู้ผล test** | P19.E1, ผล CI, Scope log, P19.X1 |
| 20-23 | ยังไม่เริ่ม | เจ้าของยังไม่ได้เลือก, 21-23 ต้องออกแบบใน Figma ก่อนตาม D-28 |
| 24 Toolchain ในตัว (Arm) | D-30 ตัดสินแล้ว ยังไม่เริ่ม | P24.E1 รอผล test ของ Phase 18 และ 19 |

รายการเต็มและวิธีตรวจอยู่ที่ `docs/ENGINE-CHECKLIST.md` หัวข้อ "Upgrade section" ถึง Phase 24 ส่วนรายงานเปรียบเทียบอยู่ที่ `docs/GB-STUDIO-COMPARE.md` ซึ่งยังมี 3 ข้อที่คลาดเคลื่อน ฉบับแก้แล้วอยู่ใน Upgrade section

## ทำอะไรต่อ ตามลำดับ

1. **อ่านผล CI ของ `ci/phase18`** (วิธีอยู่ด้านล่าง) ถ้ารอบล่าสุดยังรันหรือถูกยกเลิก ให้รอหรือ push ใหม่
2. **แก้ test ของ Phase 18 และ 19 ที่ล้ม** โดยเฉพาะ test คีย์ลัดและรายการคำสั่ง เพราะมีคำสั่ง `scene.place.*` เพิ่มมา ผล `MenuActionTests` และ `DesignSystemTests` ยังไม่รู้
3. **ติ๊กตามหลักฐาน** และเพิ่มแถว Scope log (ดูหัวข้อถัดไป) ตั้งแต่ Phase 18 เป็นต้นไปยังไม่มีข้อไหนติ๊ก
4. **เริ่ม P24.01:** ลอง build runtime และเกมตัวอย่างด้วย Arm GNU Toolchain เทียบขนาด ROM และเวลาเฟรมกับ devkitARM 16.1 ข้อนี้ตัดสินว่าทางนี้ไปต่อได้หรือไม่ ควรทำผ่าน CI ใน branch แยก
5. Phase อื่นต้องถามเจ้าของก่อนย้าย Phase

## Scope log ที่ยังต้องเพิ่ม

- **T-P01:** ป้าย START ใต้ Player, หัวข้อ Collision ใน Inspector และโหมดวางด้วยคีย์ `A` / `T` ไม่มีใน Figma, tick "matches Figma" ต้องถ่ายภาพใหม่
- **T-D10 Keyboard Shortcuts:** คำสั่ง `scene.place.*` เพิ่มจาก frame
- **T-P17, T-O08:** Node ใหม่ 5 ตัว และชนิด `PropKind.Integer` ไม่มีใน frame

ทั้งหมดสร้างก่อนแล้วค่อยแก้ frame ตาม D-28

## อ่านผล CI

repo เป็น public แต่ log และ artifact ต้อง login ผล test จึงเขียนลง annotation ซึ่งอ่านได้โดยไม่ต้อง login:

```bash
A=https://api.github.com/repos/nattaset-panlek/AGB-Studio; curl -s "$A/actions/runs?branch=ci/phase18&per_page=1" | grep -E '"(status|conclusion)":|^      "id": [0-9]{11}'
```

```bash
A=https://api.github.com/repos/nattaset-panlek/AGB-Studio; RUN=เลขรันจากคำสั่งก่อนหน้า; curl -s "$A/actions/runs/$RUN/jobs" | grep -E '^      "id":|"(name|conclusion)":'
```

```bash
A=https://api.github.com/repos/nattaset-panlek/AGB-Studio; JOB=เลขjob; curl -s "$A/check-runs/$JOB/annotations?per_page=50" | grep -E '"(title|message)":'
```

Workflow รันเมื่อ push ไป branch `ci/**` เท่านั้น และรอบใหม่ยกเลิกรอบเก่าของ branch เดียวกัน `handoff/*` จึงไม่รัน CI การติดตั้ง devkitPro ใน CI เคยล้มหนึ่งครั้งโดยไม่รู้สาเหตุ workflow จึงลองซ้ำและเก็บ cache

Test ที่ล้มอยู่แล้วก่อนงานนี้ 5 ตัว: `UiEditorTests.Canvas_ClickSelects...`, `UiEditorTests.DraggingATextInAGroup...`, `BreakpointUiTests.Breakpoints_From...`, `PlayModeE2ETests.P12_07...`, `TilemapE2ETests.LayersTable...` ตัวที่ผลไม่แน่นอน: `LongOperationTests.Build_CancelledFromBuildRom...` (UI ค้างเกิน 250 ms) และ `PauseAndStep_OnEveryPage(T-P17)` (ล้มหนึ่งครั้ง รันเดี่ยวผ่าน 3 รอบ ไม่รู้สาเหตุ)

## การตัดสินใจของเจ้าของ

- **D-26:** มีทั้งรายการ event และ node แต่ละ script ใช้แบบเดียว ไม่แปลงกัน ทั้งคู่สร้างเป็น C#
- **D-28:** งานเล็กสร้างก่อนแล้วแก้ frame ทีหลัง หน้าจอใหม่ออกแบบใน Figma ก่อน
- **D-29:** งานใหญ่ในชุดนี้มีแค่ World canvas ไม่รวม piano roll, export เว็บ, plugin, ภาษา UI
- **D-30:** รวม Arm GNU Toolchain ไปกับ AGB Studio ให้ผู้ใช้เปิดแล้ว build ได้เลย ไม่ต้องโหลด devkitPro **ไม่ได้เขียน compiler เอง** และห้ามรวมชุดของ devkitPro เพราะ <https://devkitpro.org/wiki/About> ขอไว้ ยังไม่ได้ตรวจ licence ของ toolchain Arm, maxmod และ mmutil

## วิธีทำงานกับเจ้าของ

- ตอบภาษาไทย สั้น ตรง และบอกตรงๆ ว่าอะไรยังไม่ได้ตรวจ
- **ทำให้จบทั้ง Phase แล้วค่อยรายงาน** ไม่หยุดถามทีละข้อ ถามเฉพาะตอนย้าย Phase หรือติดเรื่องที่เจ้าของต้องตัดสิน
- Commit และ push เมื่อเจ้าของสั่งเท่านั้น `USER` gate ต้องให้เจ้าของยืนยันเอง
- ห้ามแก้การตั้งค่าความปลอดภัย และห้ามเลี่ยง Smart App Control เช่น โหลด DLL จากหน่วยความจำ
- ลิขสิทธิ์: ห้ามใช้รูป เพลง เสียง และฟอนต์ของ GB Studio

## ข้อควรระวังทางเทคนิค

- ไฟล์ใช้ CRLF, `core.autocrlf` ของเครื่องเดิมเปิดอยู่ และ `sed -i` บางครั้งทำ CRLF หาย ให้เช็กหลังแก้
- Commit ของ Phase 18 และ 19 ใน `ci/phase18` เก็บบางไฟล์เป็น LF ทั้งไฟล์ เทียบกับ commit อื่นจึงดูเหมือนแก้ทุกบรรทัด ให้ใช้ `git diff --ignore-space-at-eol` ตอนอ่าน และจัด line ending ให้เรียบร้อยก่อน merge เข้า `main`
- รัน Editor test ทั้งชุดแล้ว `docs/evidence` จะถูกเขียนทับ ให้รัน `git restore -- docs/evidence` เว้นแต่เจ้าของจะอยากเก็บ
- Repo เปิด public แล้ว การตรวจก่อนเปิดค้นแค่รูปแบบ token และ key ในไฟล์ปัจจุบัน ยังไม่ได้ไล่ทุก commit และยังไม่ได้ตรวจสิทธิ์ของไฟล์ทั้งหมด
- บันทึกเก่าอยู่ที่ `docs/HANDOFF-2026-11-05.md` บางส่วนล้าสมัย และอาจเป็นคนละเครื่องกับเครื่องใหม่นี้
