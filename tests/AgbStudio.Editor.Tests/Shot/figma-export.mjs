// Read-only Figma export for the SHOT procedure (checklist 2.5) through the Talk To Figma fork relay.
// Usage: node figma-export.mjs [tokens] [icons] [art] [menus] [shots] [brand]   (no argument = all but brand)
// Writes: docs/evidence/figma/tokens.json, menus.json, <id>.png + manifest.json,
//         editor/AgbStudio.Editor/Assets/Icons/<name>.svg, editor/AgbStudio.Editor/Assets/Art/<name>.svg,
//         brand: docs/brand/<banner>.png (T-R03) and docs/brand/app-icon-<size>.png (T-R01) from "AGB Studio · Brand"
// Never creates, edits or deletes Figma nodes. Needs Node 22+ (global WebSocket) and the relay on ws://localhost:3055.
import fs from "node:fs";
import path from "node:path";
import { randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..", "..", "..");
const figDir = path.join(root, "docs", "evidence", "figma");
const iconDir = path.join(root, "editor", "AgbStudio.Editor", "Assets", "Icons");
const artDir = path.join(root, "editor", "AgbStudio.Editor", "Assets", "Art");
const PAGE_ID = "3400:3217";
const CHANNEL = "flowux";
const steps = process.argv.slice(2).length ? process.argv.slice(2) : ["tokens", "icons", "art", "menus", "shots"];

// windows exported as whole frames: evidence id -> frame name in "AGB Studio · Screens" (every window of checklist
// section 4, the light copies T-L01..T-L03 and the in-game screens IG-01..IG-04)
const FRAMES = {"T-P01": "Scene Editor", "T-P02": "Play Mode", "T-P03": "Tilemap Editor", "T-P04": "Script Editor", "T-P05": "Console panel", "T-P06": "Animation panel", "T-P07": "Database", "T-P08": "Sprite Editor", "T-P09": "Font Manager", "T-P10": "Palette Manager", "T-P11": "Build Report", "T-P12": "Launcher", "T-P13": "World Map", "T-P14": "Localization", "T-P15": "Audio", "T-P16": "Dialogue Editor", "T-P17": "Logic Graph", "T-P18": "Scripting API", "T-P19": "Debugger", "T-P20": "Launcher · Templates", "T-P21": "Launcher · Learn", "T-P22": "Scene Editor · Empty project", "T-P23": "Debugger · Memory", "T-P24": "Debugger · Palettes", "T-P25": "SFX Designer", "T-P26": "Auto-tile Rules panel", "T-D01": "New Project", "T-D02": "New Scene", "T-D03": "Scene Settings", "T-D04": "Import Asset", "T-D05": "Export ROM", "T-D06": "Build ROM", "T-D07": "Flash to Cartridge", "T-D08": "Project Settings", "T-D09": "Preferences", "T-D10": "Keyboard Shortcuts", "T-D11": "About", "T-D12": "Unsaved Changes", "T-D13": "New Sprite", "T-D14": "New Script", "T-D15": "New Font", "T-D16": "Add Characters", "T-D17": "New Table", "T-D18": "Add Field", "T-D19": "Import CSV", "T-D20": "Add Language", "T-D21": "New Speaker", "T-D22": "Opening Project", "T-D23": "Build ROM · Failed", "T-D24": "Import Asset · Errors", "T-S01": "Project Settings · General", "T-S02": "Project Settings · Save data", "T-S03": "Project Settings · Build", "T-S04": "Project Settings · Scripting", "T-S05": "Project Settings · Localization", "T-S06": "Preferences · Appearance", "T-S07": "Preferences · Editor", "T-S08": "Preferences · Paths", "T-S09": "Preferences · Shortcuts", "T-O01": "Scene Switcher", "T-O02": "Video Mode", "T-O03": "Grid & Snap", "T-O04": "Asset Picker", "T-O05": "Component Picker", "T-O06": "Budget Details", "T-O07": "Command Palette", "T-O08": "Add Node", "T-O09": "Link Settings", "T-O10": "Palette Effect", "T-O11": "Find & Replace", "T-O12": "Record Shortcut", "T-O13": "Tutorial Steps", "T-L01": "Scene Editor · Light", "T-L02": "New Project · Light", "T-L03": "Preferences · Appearance · Light", "IG-01": "In-game · Boot splash", "IG-02": "In-game · Title screen", "IG-03": "In-game · Pause menu", "IG-04": "In-game · Options"};
// menus from the Editor Map: evidence id -> group frame holding a "menu" frame
const MENUS = {"T-M01": "File menu", "T-M02": "Edit menu", "T-M03": "Assets menu", "T-M04": "Scene menu", "T-M05": "Build menu", "T-M06": "Tools menu", "T-M07": "Help menu", "T-M08": "Open Recent", "T-M09": "Create", "T-M10": "Add Actor", "T-M11": "Add to Scene", "T-M12": "Actor Context Menu", "T-M13": "Asset Context Menu", "T-M14": "Component Menu", "T-M15": "Zoom", "T-M16": "Animation List", "T-M17": "Project Context Menu", "T-M18": "Play Speed", "T-M19": "OAM Size", "T-M20": "Add Layer", "T-M21": "Node Context Menu", "T-M22": "Scene Context Menu", "T-M23": "Add to Dialogue", "T-M24": "Command"};

const ws = new WebSocket("ws://localhost:3055");
const pending = new Map();
function send(command, params, timeout = 300000) {
  const id = randomUUID();
  ws.send(JSON.stringify({ id, type: command === "join" ? "join" : "message", channel: CHANNEL, message: { id, command, params: { ...params, commandId: id } } }));
  return new Promise((res, rej) => {
    const timer = setTimeout(() => { pending.delete(id); rej(new Error(command + " timed out")); }, timeout);
    pending.set(id, { res, rej, timer });
  });
}
ws.onmessage = ev => {
  const msg = JSON.parse(String(ev.data)).message;
  if (!msg || typeof msg !== "object" || !pending.has(msg.id) || (msg.result === undefined && msg.error === undefined)) return;
  const p = pending.get(msg.id); pending.delete(msg.id); clearTimeout(p.timer);
  msg.error ? p.rej(new Error(msg.error)) : p.res(msg.result);
};
// a plugin call can stall while Figma is in the background; a tiny script every 3 s keeps it pumping
async function run(code) {
  const p = send("run_script", { code, args: {} });
  let done = false;
  p.then(() => (done = true), () => (done = true));
  while (!done) {
    await Promise.race([p.catch(() => {}), new Promise(r => setTimeout(r, 3000))]);
    if (!done) send("run_script", { code: "return 1;" }, 60000).catch(() => {});
  }
  return p;
}

const slug = s => s.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
const variantSlug = name => name.split(",").map(p => p.split("=").pop().trim()).map(slug).join("-");

const LIST = `
const pg = await figma.getNodeByIdAsync("${PAGE_ID}"); await pg.loadAsync();
const sec = n => pg.children.find(s => s.name === n);
const cs = sec("AGB Studio · Components"), scr = sec("AGB Studio · Screens"), map = sec("AGB Studio · Editor Map");
const comps = [];
for (const n of cs.findAllWithCriteria({ types: ["COMPONENT_SET", "COMPONENT"] })) {
  if (n.type === "COMPONENT" && n.parent && n.parent.type === "COMPONENT_SET") continue;
  comps.push({ id: n.id, name: n.name, type: n.type, w: n.width, h: n.height, variants: n.type === "COMPONENT_SET" ? n.children.map(v => ({ id: v.id, name: v.name, w: v.width, h: v.height })) : null });
}
const frames = {}; for (const [k, v] of Object.entries(${JSON.stringify(FRAMES)})) { const f = scr.findChild(n => n.name === v && n.type === "FRAME"); if (f) frames[k] = { id: f.id, name: v, w: f.width, h: f.height }; }
const menus = {}; for (const [k, v] of Object.entries(${JSON.stringify(MENUS)})) { const g = map.findOne(n => n.name === v && n.type === "FRAME" && !!n.findChild(c => c.name === "menu")); const m = g && g.findChild(c => c.name === "menu"); if (m) menus[k] = { id: m.id, name: v, w: m.width, h: m.height }; }
return { comps, frames, menus };`;

async function exportPng(items) {
  // one await for the whole batch: sequential awaits on exportAsync stall in this plugin
  const code = `const ids = ${JSON.stringify(items.map(i => i.node))};
const nodes = await Promise.all(ids.map(id => figma.getNodeByIdAsync(id)));
const bytes = await Promise.all(nodes.map(n => n.exportAsync({ format: "PNG", useAbsoluteBounds: true, constraint: { type: "SCALE", value: 1 } })));
return bytes.map(b => figma.base64Encode(b));`;
  const out = await run(code);
  items.forEach((it, i) => fs.writeFileSync(path.join(figDir, it.id + ".png"), Buffer.from(out[i], "base64")));
}

async function main() {
  fs.mkdirSync(figDir, { recursive: true });
  if (steps.includes("tokens")) {
    const t = await run(`const hex = c => "#" + [c.r, c.g, c.b].map(x => Math.round(x * 255).toString(16).padStart(2, "0")).join("");
const out = { source: "Figma file givb0hmQYmmaOy7p7DGtb2, page ${PAGE_ID}", collections: [], textStyles: [], effectStyles: [] };
for (const col of await figma.variables.getLocalVariableCollectionsAsync()) {
  const vars = await Promise.all(col.variableIds.map(id => figma.variables.getVariableByIdAsync(id)));
  out.collections.push({ name: col.name, modes: col.modes.map(m => m.name), variables: vars.map(v => { const val = v.valuesByMode[col.modes[0].modeId]; return { name: v.name, hex: hex(val), alpha: Math.round((val.a == null ? 1 : val.a) * 1000) / 1000 }; }) });
}
for (const s of await figma.getLocalTextStylesAsync()) { const fn = typeof s.fontName === "symbol" ? null : s.fontName; out.textStyles.push({ name: s.name, family: fn && fn.family, style: fn && fn.style, size: s.fontSize, lineHeight: s.lineHeight.unit === "AUTO" ? null : s.lineHeight.value, letterSpacing: s.letterSpacing.unit === "PIXELS" ? Math.round(s.letterSpacing.value * 100) / 100 : null, letterSpacingPercent: s.letterSpacing.unit === "PERCENT" ? s.letterSpacing.value : null, textCase: s.textCase }); }
for (const s of await figma.getLocalEffectStylesAsync()) out.effectStyles.push({ name: s.name, effects: s.effects.map(e => ({ type: e.type, alpha: Math.round(e.color.a * 100) / 100, x: e.offset.x, y: e.offset.y, radius: e.radius, spread: e.spread })) });
return out;`);
    fs.writeFileSync(path.join(figDir, "tokens.json"), JSON.stringify(t, null, 2) + "\n");
    console.log("tokens:", t.collections.map(c => c.name + " " + c.variables.length).join(", "), "· text styles", t.textStyles.length);
  }
  const list = await run(LIST);
  if (steps.includes("icons") || steps.includes("art")) {
    const pick = steps.includes("icons") ? list.comps.filter(c => c.type === "COMPONENT" && c.name.startsWith("icon/")) : [];
    const art = steps.includes("art") ? list.comps.filter(c => /^(art|thumb|brand)\//.test(c.name)) : [];
    const nodes = [...pick.map(c => ({ id: c.id, file: path.join(iconDir, c.name.slice(5) + ".svg") }))];
    for (const c of art) {
      if (c.variants) for (const v of c.variants) nodes.push({ id: v.id, file: path.join(artDir, slug(c.name) + "-" + variantSlug(v.name) + ".svg") });
      else nodes.push({ id: c.id, file: path.join(artDir, slug(c.name) + ".svg") });
    }
    for (const d of [iconDir, artDir]) fs.mkdirSync(d, { recursive: true });
    const svgs = await run(`const ids = ${JSON.stringify(nodes.map(n => n.id))}; const nodes = await Promise.all(ids.map(id => figma.getNodeByIdAsync(id))); return await Promise.all(nodes.map(n => n.exportAsync({ format: "SVG_STRING" })));`);
    nodes.forEach((n, i) => fs.writeFileSync(n.file, svgs[i]));
    console.log("svg:", pick.length, "icons,", nodes.length - pick.length, "art");
  }
  if (steps.includes("menus")) {
    // every menu of Editor Map boards 2 and 4: items with kind, label, shortcut and icon (for the menu tests)
    const menus = await run(`const pg = await figma.getNodeByIdAsync("${PAGE_ID}"); await pg.loadAsync();
const map = pg.children.find(s => s.name === "AGB Studio · Editor Map");
const groups = map.findAll(n => n.type === "FRAME" && !!n.findChild(c => c.name === "menu"));
const out = [];
for (const g of groups) {
  const m = g.findChild(c => c.name === "menu");
  const items = m.children.map(i => {
    if (i.name === "Menu Separator") return { separator: true };
    const p = {}; for (const [k, v] of Object.entries(i.componentProperties || {})) p[k.split("#")[0]] = v.value;
    return { kind: p.Kind, state: p.State, label: p.Label, shortcut: p["Show Shortcut"] ? p.Shortcut : null, iconId: p["Show Icon"] ? p.Icon : null };
  });
  out.push({ title: g.name, items });
}
const ids = [...new Set(out.flatMap(m => m.items.map(i => i.iconId).filter(Boolean)))];
const nodes = await Promise.all(ids.map(id => figma.getNodeByIdAsync(id)));
const names = {}; ids.forEach((id, k) => names[id] = nodes[k] ? nodes[k].name.replace("icon/", "") : null);
for (const m of out) for (const i of m.items) if (i.iconId) { i.icon = names[i.iconId]; delete i.iconId; } else delete i.iconId;
return out;`);
    fs.writeFileSync(path.join(figDir, "menus.json"), JSON.stringify(menus, null, 1) + "\n");
    console.log("menus:", menus.length);
  }
  if (steps.includes("brand")) {
    // T-R03 banners ("Plus Pad · Banners": frames named "Banner · <use> <w>×<h>") and the T-R01 app icon at 1x
    const brandDir = path.join(root, "docs", "brand");
    fs.mkdirSync(brandDir, { recursive: true });
    const found = await run(`const pg = await figma.getNodeByIdAsync("${PAGE_ID}"); await pg.loadAsync();
const brand = pg.children.find(s => s.name === "AGB Studio · Brand");
const banners = brand.findAll(n => n.type === "FRAME" && n.name.startsWith("Banner · ")).map(n => ({ id: n.id, name: n.name }));
const icon = pg.findOne(n => (n.type === "COMPONENT" || n.type === "COMPONENT_SET") && n.name === "brand/app-icon");
const icons = icon ? (icon.type === "COMPONENT_SET" ? icon.children : [icon]).map(n => ({ id: n.id, name: n.name })) : [];
return { banners, icons };`);
    const items = [
      ...found.banners.map(b => ({ node: b.id, file: slug(b.name.replace("Banner · ", "banner ")) })),
      ...found.icons.map(i => ({ node: i.id, file: "app-icon-" + variantSlug(i.name) })),
    ];
    const code = `const ids = ${JSON.stringify(items.map(i => i.node))};
const nodes = await Promise.all(ids.map(id => figma.getNodeByIdAsync(id)));
const bytes = await Promise.all(nodes.map(n => n.exportAsync({ format: "PNG", useAbsoluteBounds: true, constraint: { type: "SCALE", value: 1 } })));
return bytes.map(b => figma.base64Encode(b));`;
    const out = await run(code);
    items.forEach((it, i) => fs.writeFileSync(path.join(brandDir, it.file + ".png"), Buffer.from(out[i], "base64")));
    console.log("brand:", found.banners.length, "banners,", found.icons.length, "app icons");
  }
  if (steps.includes("shots")) {
    const items = [];
    for (const c of list.comps) {
      if (c.name.startsWith("icon/") || c.name.startsWith("art/") || c.name.startsWith("thumb/") || c.name.startsWith("Doc/")) continue;
      if (c.variants) for (const v of c.variants) items.push({ id: "C-" + slug(c.name) + "-" + variantSlug(v.name), node: v.id, set: c.name, variant: v.name, w: v.w, h: v.h });
      else items.push({ id: "C-" + slug(c.name), node: c.id, set: c.name, variant: "", w: c.w, h: c.h });
    }
    for (const [k, f] of Object.entries(list.frames)) items.push({ id: k, node: f.id, set: "frame", variant: f.name, w: f.w, h: f.h });
    for (const [k, f] of Object.entries(list.menus)) items.push({ id: k, node: f.id, set: "menu", variant: f.name, w: f.w, h: f.h });
    for (let i = 0; i < items.length; i += 40) await exportPng(items.slice(i, i + 40));
    fs.writeFileSync(path.join(figDir, "manifest.json"), JSON.stringify(items, null, 1) + "\n");
    console.log("png:", items.length, "exports");
  }
}

ws.onopen = async () => {
  try { await send("join", { channel: CHANNEL }); await main(); }
  catch (e) { console.error("FAILED:", e.message); process.exitCode = 1; }
  finally { ws.close(); }
};
ws.onerror = () => { console.error("relay not reachable on ws://localhost:3055"); process.exit(1); };
