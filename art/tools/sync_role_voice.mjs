import { createRequire } from "module";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const here = dirname(fileURLToPath(import.meta.url));
const repo = join(here, "..", "..");
const require = createRequire(join(repo, "tools/config-mcp/package.json"));
const ExcelJS = require("exceljs");

const csvPath = join(repo, "art/voice/lines.csv");
const dataRoot = join(repo, "config/Config/Data");
const voiceXlsx = join(dataRoot, "语音.xlsx");
const tablesXlsx = join(dataRoot, "__tables__.xlsx");

function parseCsv(text) {
  const lines = text.replace(/^\uFEFF/, "").split(/\r?\n/).filter((l) => l.trim());
  const headers = lines[0].split(",");
  return lines.slice(1).map((line) => {
    const cols = line.split(",");
    const row = {};
    headers.forEach((h, i) => {
      row[h] = (cols[i] || "").trim();
    });
    return row;
  });
}

function lubanHeader(sheet, names, types, comments) {
  sheet.getRow(1).values = ["##var", ...names];
  sheet.getRow(2).values = ["##type", ...types];
  sheet.getRow(3).values = ["##", ...comments];
}

const rows = parseCsv(readFileSync(csvPath, "utf8"));
const fields = ["id", "who", "module", "meaning", "text_zh", "langPath", "langPath_ln", "emotion"];
const types = ["string", "string", "string", "string", "string", "string", "string", "string"];
const comments = [
  "人名_模块_意义",
  "音色/目录，对应 samples/{who}.mp3",
  "battle/profile/hub/settle",
  "skill/show/spawn/hurt/win",
  "普通话文本，TTS 只读这一列",
  "默认加载地址，Yoo：lixin/voice/lixin_battle_skill",
  "龙南话自录音地址，有文件则优先；空则用 langPath",
  "合成情绪，仅生成用",
];

const voiceWb = new ExcelJS.Workbook();
const sheet = voiceWb.addWorksheet("RoleVoice");
lubanHeader(sheet, fields, types, comments);
rows.forEach((row, i) => {
  const excelRow = sheet.getRow(4 + i);
  excelRow.values = [null, ...fields.map((f) => row[f] || "")];
});
await voiceWb.xlsx.writeFile(voiceXlsx);

const tablesWb = new ExcelJS.Workbook();
await tablesWb.xlsx.readFile(tablesXlsx);
const tables = tablesWb.worksheets[0];
let found = false;
tables.eachRow((row) => {
  if (String(row.getCell(2).value || "") === "TbRoleVoice") found = true;
});
if (!found) {
  const next = tables.rowCount + 1;
  tables.getRow(next).values = [
    null,
    "TbRoleVoice",
    "RoleVoice",
    true,
    "RoleVoice@语音.xlsx",
    "id",
    "map",
    "c",
    "人声文案",
  ];
  await tablesWb.xlsx.writeFile(tablesXlsx);
  console.log("registered TbRoleVoice in __tables__.xlsx");
}

console.log("wrote", voiceXlsx, "rows", rows.length);
