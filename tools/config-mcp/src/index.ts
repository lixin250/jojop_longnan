import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import ExcelJS from "exceljs";
import * as z from "zod/v4";
import {
  copyFile,
  mkdir,
  readdir,
  readFile,
} from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, extname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

type CellInput = string | number | boolean | null;
type RowRecord = Record<string, CellInput>;
type ParsedRow = { excelRow: number; values: RowRecord };

const HERE = dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT = resolve(HERE, "..", "..", "..");
const CONFIG_ROOT = join(PROJECT_ROOT, "config", "Config");
const DATA_ROOT = join(CONFIG_ROOT, "Data");
const DEFINES_ROOT = join(CONFIG_ROOT, "Defines");
const LOCKED_ROLE_FIELDS = new Set(["id", "name", "desc"]);
const ROLE_EDITABLE_FIELDS = new Set([
  "camp",
  "education_level",
  "life_route",
  "career_sector",
  "faction_tags",
  "avatar_loc",
  "battle_loc",
  "base_hp",
  "base_atk",
  "base_move",
  "base_defense",
  "crit_rate",
  "crit_damage",
  "attack_interval",
  "skill_ids",
  "recruitable",
  "sort",
]);
const RUN_CONTENT_KEYS = {
  RunChapterRule: "chapter_id",
  RogueReward: "id",
  Equipment: "id",
  CharacterEncounter: "role_id",
  RunEvent: "id",
  TimelineEvent: "id",
} as const;
const RUN_CONTENT_FIELDS: Record<keyof typeof RUN_CONTENT_KEYS, Set<string>> = {
  RunChapterRule: new Set([
    "base_active_slots", "ad_extra_slot_limit", "affinity_needed", "rewarded_reroll_limit",
    "extra_member_enemy_mul", "extra_member_spawn_mul", "extra_member_elite_bonus", "comment",
  ]),
  RogueReward: new Set([
    "kind", "title", "desc", "weight", "power_cost", "min_chapter", "ref_id", "value", "stat",
  ]),
  Equipment: new Set([
    "name", "desc", "attack_form", "damage_mul", "secondary_mul", "max_targets", "radius", "vfx_key",
  ]),
  CharacterEncounter: new Set([
    "min_chapter", "weight", "affinity_per_meet", "join_power_mul",
  ]),
  RunEvent: new Set([
    "title", "desc", "required_tag", "success_desc", "success_effect", "success_value",
    "fail_desc", "fail_effect", "fail_value",
  ]),
  TimelineEvent: new Set([
    "anchor_year", "sequence", "title", "history_summary", "gameplay_summary", "chapter_id",
    "flexible_year", "scope", "boost_tags", "encounter_weight_mul", "event_ids",
    "unlock_scene_id", "opportunity_desc", "difficulty_desc", "source_kind", "source_name",
    "source_url", "verified",
  ]),
};

function text(value: unknown): string {
  if (value === null || value === undefined) return "";
  if (typeof value === "object" && value && "text" in value) {
    return String((value as { text: unknown }).text ?? "");
  }
  return String(value);
}

function result(value: unknown, isError = false) {
  return {
    isError,
    content: [{ type: "text" as const, text: JSON.stringify(value, null, 2) }],
  };
}

function splitRefs(value: CellInput): string[] {
  return text(value)
    .split("|")
    .map((item) => item.trim())
    .filter(Boolean);
}

async function businessWorkbooks(): Promise<string[]> {
  const names = await readdir(DATA_ROOT);
  return names
    .filter((name) => extname(name).toLowerCase() === ".xlsx")
    .filter((name) => !name.startsWith("~$"))
    .sort();
}

async function loadWorkbook(fileName: string): Promise<ExcelJS.Workbook> {
  const path = join(DATA_ROOT, fileName);
  if (!existsSync(path)) throw new Error(`Workbook not found: ${fileName}`);
  const workbook = new ExcelJS.Workbook();
  await workbook.xlsx.readFile(path);
  return workbook;
}

async function findWorkbookBySheet(sheetName: string): Promise<string> {
  for (const fileName of await businessWorkbooks()) {
    const workbook = await loadWorkbook(fileName);
    if (workbook.getWorksheet(sheetName)) return fileName;
  }
  throw new Error(`No workbook contains sheet: ${sheetName}`);
}

function headerMap(sheet: ExcelJS.Worksheet): Map<string, number> {
  const headers = new Map<string, number>();
  const row = sheet.getRow(1);
  for (let column = 1; column <= sheet.columnCount; column += 1) {
    const name = text(row.getCell(column).value).trim();
    if (name && name !== "##var") headers.set(name, column);
  }
  return headers;
}

function parseRows(sheet: ExcelJS.Worksheet): ParsedRow[] {
  const headers = headerMap(sheet);
  const rows: ParsedRow[] = [];
  for (let rowNumber = 4; rowNumber <= sheet.rowCount; rowNumber += 1) {
    const row = sheet.getRow(rowNumber);
    const tag = text(row.getCell(1).value).trim();
    if (tag.startsWith("##")) continue;

    const values: RowRecord = {};
    let hasValue = false;
    for (const [name, column] of headers) {
      const raw = row.getCell(column).value;
      let value: CellInput;
      if (typeof raw === "number" || typeof raw === "boolean") value = raw;
      else value = text(raw);
      if (value !== "") hasValue = true;
      values[name] = value;
    }
    if (hasValue) rows.push({ excelRow: rowNumber, values });
  }
  return rows;
}

async function readSheet(sheetName: string): Promise<{
  workbook: string;
  sheet: string;
  headers: string[];
  rows: ParsedRow[];
}> {
  const workbookName = await findWorkbookBySheet(sheetName);
  const workbook = await loadWorkbook(workbookName);
  const sheet = workbook.getWorksheet(sheetName);
  if (!sheet) throw new Error(`Sheet not found: ${sheetName}`);
  return {
    workbook: workbookName,
    sheet: sheetName,
    headers: [...headerMap(sheet).keys()],
    rows: parseRows(sheet),
  };
}

async function backupWorkbook(fileName: string): Promise<string> {
  const stamp = new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
  const backupDir = join(PROJECT_ROOT, ".config-mcp-backup", stamp);
  await mkdir(backupDir, { recursive: true });
  const source = join(DATA_ROOT, fileName);
  const target = join(backupDir, fileName);
  await copyFile(source, target);
  return target;
}

function normalizeCell(value: CellInput): CellInput {
  if (typeof value === "string") return value.trim();
  return value;
}

async function upsertRow(
  sheetName: string,
  keyColumn: string,
  keyValue: string,
  values: RowRecord,
  options?: { locked?: Set<string>; allowed?: Set<string> },
): Promise<{ workbook: string; sheet: string; row: number; backup: string; created: boolean }> {
  const workbookName = await findWorkbookBySheet(sheetName);
  const workbook = await loadWorkbook(workbookName);
  const sheet = workbook.getWorksheet(sheetName);
  if (!sheet) throw new Error(`Sheet not found: ${sheetName}`);
  const headers = headerMap(sheet);
  const keyIndex = headers.get(keyColumn);
  if (!keyIndex) throw new Error(`Key column ${keyColumn} missing in ${sheetName}`);

  for (const field of Object.keys(values)) {
    if (options?.locked?.has(field)) throw new Error(`Field is locked: ${field}`);
    if (options?.allowed && !options.allowed.has(field)) {
      throw new Error(`Field is not editable through this tool: ${field}`);
    }
    if (!headers.has(field)) throw new Error(`Unknown ${sheetName} field: ${field}`);
  }

  let targetRow: ExcelJS.Row | undefined;
  for (let rowNumber = 4; rowNumber <= sheet.rowCount; rowNumber += 1) {
    const row = sheet.getRow(rowNumber);
    if (text(row.getCell(1).value).trim().startsWith("##")) continue;
    if (text(row.getCell(keyIndex).value).trim() === keyValue) {
      targetRow = row;
      break;
    }
  }

  const created = !targetRow;
  if (!targetRow) {
    targetRow = sheet.getRow(sheet.rowCount + 1);
    targetRow.getCell(keyIndex).value = keyValue;
  }
  for (const [field, value] of Object.entries(values)) {
    targetRow.getCell(headers.get(field)!).value = normalizeCell(value) as ExcelJS.CellValue;
  }
  targetRow.commit();

  const backup = await backupWorkbook(workbookName);
  await workbook.xlsx.writeFile(join(DATA_ROOT, workbookName));
  return { workbook: workbookName, sheet: sheetName, row: targetRow.number, backup, created };
}

export type RoleStatUpdate = {
  id: string;
  base_hp: number;
  base_atk: number;
  base_move: number;
  base_defense: number;
  crit_rate: number;
  crit_damage: number;
  attack_interval: number;
  faction_tags?: string;
  skill_ids?: string;
  avatar_loc?: string;
};

export async function batchUpdateRoleStats(updates: RoleStatUpdate[]) {
  if (updates.length === 0) throw new Error("No role stat updates supplied");
  const workbookName = await findWorkbookBySheet("RoleList");
  const workbook = await loadWorkbook(workbookName);
  const sheet = workbook.getWorksheet("RoleList");
  if (!sheet) throw new Error("RoleList sheet missing");
  const headers = headerMap(sheet);
  const idColumn = headers.get("id");
  if (!idColumn) throw new Error("RoleList id column missing");
  const rows = new Map<string, ExcelJS.Row>();
  for (let rowNumber = 4; rowNumber <= sheet.rowCount; rowNumber += 1) {
    const row = sheet.getRow(rowNumber);
    const id = text(row.getCell(idColumn).value).trim();
    if (id) rows.set(id, row);
  }

  const seen = new Set<string>();
  let changed = 0;
  for (const update of updates) {
    if (!update.id || seen.has(update.id)) throw new Error(`Duplicate or empty role id: ${update.id}`);
    seen.add(update.id);
    const row = rows.get(update.id);
    if (!row) throw new Error(`Role not found: ${update.id}`);
    if (!Number.isFinite(update.base_hp) || update.base_hp < 40 || update.base_hp > 250) {
      throw new Error(`${update.id}: base_hp must be 40..250`);
    }
    if (!Number.isFinite(update.base_atk) || update.base_atk < 4 || update.base_atk > 30) {
      throw new Error(`${update.id}: base_atk must be 4..30`);
    }
    if (!Number.isFinite(update.base_move) || update.base_move < 1.2 || update.base_move > 4) {
      throw new Error(`${update.id}: base_move must be 1.2..4`);
    }
    if (!Number.isFinite(update.base_defense) || update.base_defense < 0 || update.base_defense > 100) {
      throw new Error(`${update.id}: base_defense must be 0..100`);
    }
    if (!Number.isFinite(update.crit_rate) || update.crit_rate < 0 || update.crit_rate > 0.75) {
      throw new Error(`${update.id}: crit_rate must be 0..0.75`);
    }
    if (!Number.isFinite(update.crit_damage) || update.crit_damage < 1 || update.crit_damage > 4) {
      throw new Error(`${update.id}: crit_damage must be 1..4`);
    }
    if (!Number.isFinite(update.attack_interval) || update.attack_interval < 0.15 || update.attack_interval > 2) {
      throw new Error(`${update.id}: attack_interval must be 0.15..2`);
    }

    const values: Record<string, string | number> = {
      base_hp: Math.round(update.base_hp * 10) / 10,
      base_atk: Math.round(update.base_atk * 10) / 10,
      base_move: Math.round(update.base_move * 100) / 100,
      base_defense: Math.round(update.base_defense * 10) / 10,
      crit_rate: Math.round(update.crit_rate * 1000) / 1000,
      crit_damage: Math.round(update.crit_damage * 100) / 100,
      attack_interval: Math.round(update.attack_interval * 1000) / 1000,
    };
    if (update.faction_tags !== undefined) values.faction_tags = update.faction_tags.trim();
    if (update.skill_ids !== undefined) values.skill_ids = update.skill_ids.trim();
    if (update.avatar_loc !== undefined) values.avatar_loc = update.avatar_loc.trim();
    let rowChanged = false;
    for (const [field, value] of Object.entries(values)) {
      const column = headers.get(field);
      if (!column) throw new Error(`RoleList ${field} column missing`);
      if (Number(row.getCell(column).value) !== value) rowChanged = true;
      row.getCell(column).value = value;
    }
    row.commit();
    if (rowChanged) changed += 1;
  }

  const backup = await backupWorkbook(workbookName);
  await workbook.xlsx.writeFile(join(DATA_ROOT, workbookName));
  return { workbook: workbookName, sheet: "RoleList", backup, updated: updates.length, changed };
}

async function characterContext(roleId: string) {
  const roles = await readSheet("RoleList");
  const role = roles.rows.find((row) => text(row.values.id) === roleId);
  if (!role) throw new Error(`Role not found: ${roleId}`);

  const skills = await readSheet("SkillIndex");
  const effects = await readSheet("SkillEffect");
  const fusions = await readSheet("FusionSkill");
  const educationPrograms = await readSheet("EducationProgram");
  const lifeRoutes = await readSheet("LifeRouteGrowth");
  const careers = await readSheet("CareerGrowth");

  const skillIds = new Set(splitRefs(role.values.skill_ids));
  const ownedSkills = skills.rows.filter(
    (row) => skillIds.has(text(row.values.id)) || text(row.values.owner_id) === roleId,
  );
  const effectIds = new Set(
    ownedSkills.flatMap((row) => splitRefs(row.values.effect_ids)),
  );
  const ownedEffects = effects.rows.filter((row) => effectIds.has(text(row.values.id)));
  const roleTags = new Set(splitRefs(role.values.faction_tags));
  const matchingFusions = fusions.rows.filter((row) =>
    splitRefs(row.values.required_tags).some((tag) => roleTags.has(tag)),
  );
  const educationLevel = text(role.values.education_level);
  const lifeRoute = text(role.values.life_route);
  const careerSector = text(role.values.career_sector);
  const educationProgram = educationPrograms.rows.find(
    (row) => text(row.values.level) === educationLevel,
  );
  const lifeRouteGrowth = lifeRoutes.rows.find(
    (row) => text(row.values.route) === lifeRoute,
  );
  const careerGrowth = careers.rows.find(
    (row) => text(row.values.sector) === careerSector,
  );

  return {
    role: role.values,
    skills: ownedSkills.map((row) => row.values),
    effects: ownedEffects.map((row) => row.values),
    related_fusions: matchingFusions.map((row) => row.values),
    education_program: educationProgram?.values ?? null,
    life_route_growth: lifeRouteGrowth?.values ?? null,
    career_growth: careerGrowth?.values ?? null,
    design_rules: {
      locked_fields: ["name", "desc"],
      skill_id: "{owner}_{action}",
      effect_id: "{skill_id}_{effect}",
      composition:
        "Character traits -> faction/common tags -> trigger -> skill effects -> buffs -> fusion rules",
      late_graduation:
        "Master/Phd job skills unlock late and must be stronger after unlock",
    },
  };
}

async function enumNames(enumName: string): Promise<Set<string>> {
  const workbook = await loadWorkbook("__enums__.xlsx");
  const names = new Set<string>();
  for (const sheet of workbook.worksheets) {
    let currentEnum = "";
    for (let row = 4; row <= sheet.rowCount; row += 1) {
      const declaredEnum = text(sheet.getRow(row).getCell(2).value).trim();
      if (declaredEnum) currentEnum = declaredEnum;
      if (currentEnum !== enumName) continue;
      const item = text(sheet.getRow(row).getCell(8).value).trim();
      if (item) names.add(item);
    }
  }
  if (names.size > 0) return names;

  const xml = await readFile(join(DEFINES_ROOT, "game_enums.xml"), "utf8");
  const block = xml.match(
    new RegExp(`<enum\\s+name="${enumName}"[\\s\\S]*?<\\/enum>`, "i"),
  )?.[0];
  if (!block) return new Set();
  return new Set([...block.matchAll(/<var\s+name="([^"]+)"/g)].map((match) => match[1]));
}

export async function validateConfig() {
  const issues: Array<{ level: "error" | "warning"; code: string; message: string }> = [];
  const roles = await readSheet("RoleList");
  const skills = await readSheet("SkillIndex");
  const effects = await readSheet("SkillEffect");
  const fusions = await readSheet("FusionSkill");
  const educationPrograms = await readSheet("EducationProgram");
  const lifeRoutes = await readSheet("LifeRouteGrowth");
  const careers = await readSheet("CareerGrowth");
  const chapterRules = await readSheet("RunChapterRule");
  const rogueRewards = await readSheet("RogueReward");
  const equipment = await readSheet("Equipment");
  const encounters = await readSheet("CharacterEncounter");
  const runEvents = await readSheet("RunEvent");
  const timelineEvents = await readSheet("TimelineEvent");

  function duplicates(rows: ParsedRow[], key: string, label: string) {
    const seen = new Set<string>();
    for (const row of rows) {
      const value = text(row.values[key]);
      if (!value) {
        issues.push({ level: "error", code: "EMPTY_ID", message: `${label} row ${row.excelRow} has empty ${key}` });
      } else if (seen.has(value)) {
        issues.push({ level: "error", code: "DUPLICATE_ID", message: `${label} duplicate ${key}: ${value}` });
      }
      seen.add(value);
    }
    return seen;
  }

  const roleIds = duplicates(roles.rows, "id", "RoleList");
  const skillIds = duplicates(skills.rows, "id", "SkillIndex");
  const effectIds = duplicates(effects.rows, "id", "SkillEffect");
  duplicates(fusions.rows, "id", "FusionSkill");
  const equipmentIds = duplicates(equipment.rows, "id", "Equipment");
  const eventIds = duplicates(runEvents.rows, "id", "RunEvent");
  duplicates(timelineEvents.rows, "id", "TimelineEvent");
  duplicates(rogueRewards.rows, "id", "RogueReward");
  duplicates(encounters.rows, "role_id", "CharacterEncounter");
  duplicates(chapterRules.rows, "chapter_id", "RunChapterRule");

  const educationLevels = await enumNames("EEducationLevel");
  const routeNames = await enumNames("ELifeRoute");
  const careerSectors = await enumNames("ECareerSector");
  const factionTags = await enumNames("EFactionTag");

  for (const row of roles.rows) {
    const id = text(row.values.id);
    const educationLevel = text(row.values.education_level);
    const lifeRoute = text(row.values.life_route);
    const careerSector = text(row.values.career_sector);
    if (!educationLevels.has(educationLevel)) {
      issues.push({ level: "error", code: "UNKNOWN_EDUCATION_LEVEL", message: `${id}: unknown education level ${educationLevel}` });
    }
    if (!routeNames.has(lifeRoute)) {
      issues.push({ level: "error", code: "UNKNOWN_LIFE_ROUTE", message: `${id}: unknown life route ${lifeRoute}` });
    }
    if (!careerSectors.has(careerSector)) {
      issues.push({ level: "error", code: "UNKNOWN_CAREER_SECTOR", message: `${id}: unknown career sector ${careerSector}` });
    }
    for (const tag of splitRefs(row.values.faction_tags)) {
      if (!factionTags.has(tag)) {
        issues.push({ level: "error", code: "UNKNOWN_FACTION_TAG", message: `${id}: unknown faction tag ${tag}` });
      }
    }
    for (const skillId of splitRefs(row.values.skill_ids)) {
      if (!skillIds.has(skillId)) {
        issues.push({ level: "error", code: "MISSING_SKILL", message: `${id}: missing skill ${skillId}` });
      }
    }
    const defense = Number(row.values.base_defense);
    const critRate = Number(row.values.crit_rate);
    const critDamage = Number(row.values.crit_damage);
    const attackInterval = Number(row.values.attack_interval);
    if (!Number.isFinite(defense) || defense < 0 || defense > 100) {
      issues.push({ level: "error", code: "INVALID_DEFENSE", message: `${id}: base_defense must be 0..100` });
    }
    if (!Number.isFinite(critRate) || critRate < 0 || critRate > 0.75) {
      issues.push({ level: "error", code: "INVALID_CRIT_RATE", message: `${id}: crit_rate must be 0..0.75` });
    }
    if (!Number.isFinite(critDamage) || critDamage < 1 || critDamage > 4) {
      issues.push({ level: "error", code: "INVALID_CRIT_DAMAGE", message: `${id}: crit_damage must be 1..4` });
    }
    if (!Number.isFinite(attackInterval) || attackInterval < 0.15 || attackInterval > 2) {
      issues.push({ level: "error", code: "INVALID_ATTACK_INTERVAL", message: `${id}: attack_interval must be 0.15..2` });
    }
  }

  for (const row of skills.rows) {
    const id = text(row.values.id);
    const owner = text(row.values.owner_id);
    if (owner && !["campus", "fusion", "mow", "loot"].includes(owner) && !roleIds.has(owner)) {
      issues.push({ level: "warning", code: "UNKNOWN_OWNER", message: `${id}: unknown owner ${owner}` });
    }
    for (const effectId of splitRefs(row.values.effect_ids)) {
      if (!effectIds.has(effectId)) {
        issues.push({ level: "error", code: "MISSING_EFFECT", message: `${id}: missing effect ${effectId}` });
      }
    }
  }

  for (const row of fusions.rows) {
    const id = text(row.values.id);
    const grant = text(row.values.grant_skill_id);
    if (grant && !skillIds.has(grant)) {
      issues.push({ level: "error", code: "MISSING_FUSION_SKILL", message: `${id}: missing grant skill ${grant}` });
    }
    for (const tag of splitRefs(row.values.required_tags)) {
      if (!factionTags.has(tag)) {
        issues.push({ level: "error", code: "UNKNOWN_FUSION_TAG", message: `${id}: unknown required tag ${tag}` });
      }
    }
  }

  const chapterIds = new Set(["Primary", "Middle", "High", "University", "Society"]);
  for (const row of chapterRules.rows) {
    const chapter = text(row.values.chapter_id);
    if (!chapterIds.has(chapter)) {
      issues.push({ level: "error", code: "UNKNOWN_RUN_CHAPTER", message: `RunChapterRule: unknown chapter ${chapter}` });
    }
  }
  for (const row of encounters.rows) {
    const roleId = text(row.values.role_id);
    const chapter = text(row.values.min_chapter);
    if (!roleIds.has(roleId)) {
      issues.push({ level: "error", code: "UNKNOWN_ENCOUNTER_ROLE", message: `CharacterEncounter: unknown role ${roleId}` });
    }
    if (!chapterIds.has(chapter)) {
      issues.push({ level: "error", code: "UNKNOWN_ENCOUNTER_CHAPTER", message: `${roleId}: unknown min chapter ${chapter}` });
    }
  }
  for (const row of runEvents.rows) {
    const eventId = text(row.values.id);
    const requiredTag = text(row.values.required_tag);
    if (requiredTag && !factionTags.has(requiredTag)) {
      issues.push({ level: "error", code: "UNKNOWN_EVENT_TAG", message: `${eventId}: unknown required tag ${requiredTag}` });
    }
  }
  const sourceKinds = await enumNames("EHistorySourceKind");
  const timelineScopes = await enumNames("ETimelineScope");
  for (const row of timelineEvents.rows) {
    const id = text(row.values.id);
    const chapter = text(row.values.chapter_id);
    const sourceKind = text(row.values.source_kind);
    const scope = text(row.values.scope);
    const verified = text(row.values.verified).toLowerCase() === "true";
    if (!chapterIds.has(chapter)) {
      issues.push({ level: "error", code: "UNKNOWN_TIMELINE_CHAPTER", message: `${id}: unknown chapter ${chapter}` });
    }
    if (!sourceKinds.has(sourceKind)) {
      issues.push({ level: "error", code: "UNKNOWN_HISTORY_SOURCE", message: `${id}: unknown source kind ${sourceKind}` });
    }
    if (!timelineScopes.has(scope)) {
      issues.push({ level: "error", code: "UNKNOWN_TIMELINE_SCOPE", message: `${id}: unknown scope ${scope}` });
    }
    for (const tag of splitRefs(row.values.boost_tags)) {
      if (!factionTags.has(tag)) {
        issues.push({ level: "error", code: "UNKNOWN_TIMELINE_TAG", message: `${id}: unknown boost tag ${tag}` });
      }
    }
    for (const linkedEventId of splitRefs(row.values.event_ids)) {
      if (!eventIds.has(linkedEventId)) {
        issues.push({ level: "error", code: "MISSING_TIMELINE_EVENT", message: `${id}: missing RunEvent ${linkedEventId}` });
      }
    }
    if (verified && ["Oral", "Creative"].includes(sourceKind)) {
      issues.push({ level: "error", code: "UNVERIFIED_SOURCE_MARKED_VERIFIED", message: `${id}: ${sourceKind} cannot be marked verified` });
    }
    if (verified && !text(row.values.source_url)) {
      issues.push({ level: "error", code: "VERIFIED_SOURCE_WITHOUT_URL", message: `${id}: verified history requires source_url` });
    }
  }
  for (const row of rogueRewards.rows) {
    const id = text(row.values.id);
    const kind = text(row.values.kind);
    const refId = text(row.values.ref_id);
    const chapter = text(row.values.min_chapter);
    if (!chapterIds.has(chapter)) {
      issues.push({ level: "error", code: "UNKNOWN_REWARD_CHAPTER", message: `${id}: unknown min chapter ${chapter}` });
    }
    if (kind === "Equipment" && !equipmentIds.has(refId)) {
      issues.push({ level: "error", code: "MISSING_REWARD_EQUIPMENT", message: `${id}: missing equipment ${refId}` });
    }
    if (kind === "LootSkill" && !skillIds.has(refId)) {
      issues.push({ level: "error", code: "MISSING_REWARD_SKILL", message: `${id}: missing loot skill ${refId}` });
    }
    if (kind === "Event" && refId && !eventIds.has(refId)) {
      issues.push({ level: "error", code: "MISSING_REWARD_EVENT", message: `${id}: missing event ${refId}` });
    }
  }

  const configuredLevels = new Set(educationPrograms.rows.map((row) => text(row.values.level)));
  for (const level of educationLevels) {
    if (!configuredLevels.has(level)) {
      issues.push({ level: "warning", code: "MISSING_EDUCATION_PROGRAM", message: `No EducationProgram row for ${level}` });
    }
  }
  const configuredRoutes = new Set(lifeRoutes.rows.map((row) => text(row.values.route)));
  for (const route of routeNames) {
    if (!configuredRoutes.has(route)) {
      issues.push({ level: "warning", code: "MISSING_LIFE_ROUTE_GROWTH", message: `No LifeRouteGrowth row for ${route}` });
    }
  }
  const configuredCareers = new Set(careers.rows.map((row) => text(row.values.sector)));
  for (const sector of careerSectors) {
    if (!configuredCareers.has(sector)) {
      issues.push({ level: "warning", code: "MISSING_CAREER_GROWTH", message: `No CareerGrowth row for ${sector}` });
    }
  }

  return {
    ok: !issues.some((issue) => issue.level === "error"),
    counts: {
      roles: roles.rows.length,
      skills: skills.rows.length,
      effects: effects.rows.length,
      fusions: fusions.rows.length,
      education_programs: educationPrograms.rows.length,
      life_routes: lifeRoutes.rows.length,
      career_growth: careers.rows.length,
      run_chapter_rules: chapterRules.rows.length,
      rogue_rewards: rogueRewards.rows.length,
      equipment: equipment.rows.length,
      character_encounters: encounters.rows.length,
      run_events: runEvents.rows.length,
      timeline_events: timelineEvents.rows.length,
    },
    issues,
  };
}

export function runLuban() {
  const lubanDll = join(PROJECT_ROOT, "config", "Tools", "Luban", "Luban.dll");
  if (!existsSync(lubanDll)) throw new Error(`Luban.dll missing: ${lubanDll}`);
  const run = spawnSync(
    "dotnet",
    [
      lubanDll,
      "-t",
      "client",
      "-c",
      "cs-simple-json",
      "-d",
      "json",
      "--conf",
      join(CONFIG_ROOT, "luban.conf"),
    ],
    { cwd: CONFIG_ROOT, encoding: "utf8", windowsHide: true },
  );
  const output = `${run.stdout ?? ""}${run.stderr ?? ""}`;
  const hasLoggedError = output.includes("|ERROR|");
  return {
    ok: run.status === 0 && !hasLoggedError,
    exitCode: run.status,
    output,
  };
}

const server = new McpServer({
  name: "jojop-config",
  version: "1.0.0",
});

server.registerTool(
  "list_config",
  {
    description:
      "List JojoP Luban workbooks and sheets. Use before reading or changing character, skill, equipment, encounter, event, chapter-run, or growth config.",
    inputSchema: {},
  },
  async () => {
    const books = [];
    for (const fileName of await businessWorkbooks()) {
      const workbook = await loadWorkbook(fileName);
      books.push({ workbook: fileName, sheets: workbook.worksheets.map((sheet) => sheet.name) });
    }
    return result({ dataRoot: DATA_ROOT, workbooks: books });
  },
);

server.registerTool(
  "read_sheet",
  {
    description:
      "Read one Luban sheet as field-keyed rows, excluding ## comment rows. Use for AI analysis before proposing or applying config changes.",
    inputSchema: {
      sheet: z.string().min(1).describe("Sheet name, e.g. RoleList, SkillIndex, Equipment, CharacterEncounter, RunEvent, TimelineEvent, RunChapterRule, RogueReward"),
      limit: z.number().int().positive().max(500).default(200),
    },
  },
  async ({ sheet, limit }) => {
    const data = await readSheet(sheet);
    return result({ ...data, rows: data.rows.slice(0, limit) });
  },
);

server.registerTool(
  "get_character_context",
  {
    description:
      "Get one real person's locked source description plus education, life-route, career growth, skills, effects, and related tag fusions. Always use before designing skills from personality. Never alter name or desc.",
    inputSchema: {
      roleId: z.string().min(1),
    },
  },
  async ({ roleId }) => result(await characterContext(roleId)),
);

server.registerTool(
  "batch_update_role_stats",
  {
    description:
      "Safely batch-write Unity-balanced base stats, faction tags, graduation skill selection, and avatar resource name back to RoleList. Names and descriptions are untouched.",
    inputSchema: {
      updates: z.array(z.object({
        id: z.string().min(1),
        base_hp: z.number().min(40).max(250),
        base_atk: z.number().min(4).max(30),
        base_move: z.number().min(1.2).max(4),
        base_defense: z.number().min(0).max(100),
        crit_rate: z.number().min(0).max(0.75),
        crit_damage: z.number().min(1).max(4),
        attack_interval: z.number().min(0.15).max(2),
        faction_tags: z.string().optional(),
        skill_ids: z.string().optional(),
        avatar_loc: z.string().optional(),
      })).min(1),
      rationale: z.string().min(1),
    },
  },
  async ({ updates, rationale }) => result({
    ...await batchUpdateRoleStats(updates),
    rationale,
    next: "Run validate_config, then run_luban.",
  }),
);

server.registerTool(
  "update_character_design",
  {
    description:
      "Safely update AI-designed character mechanics. name/desc/id are locked. Accepts only camp, education_level, life_route, career_sector, faction tags, stats, resource keys, skill_ids, recruitable, sort. Use tags and shared mechanisms instead of hardcoding names.",
    inputSchema: {
      roleId: z.string().min(1),
      values: z.record(z.string(), z.union([z.string(), z.number(), z.boolean(), z.null()])),
      rationale: z.string().min(1).describe("Short mapping from real personality/description to mechanics"),
    },
  },
  async ({ roleId, values, rationale }) => {
    const existing = await characterContext(roleId);
    const update = await upsertRow(
      "RoleList",
      "id",
      roleId,
      values as RowRecord,
      { locked: LOCKED_ROLE_FIELDS, allowed: ROLE_EDITABLE_FIELDS },
    );
    return result({
      ...update,
      protected: { name: existing.role.name, desc: existing.role.desc },
      rationale,
      next: "Run validate_config, then run_luban.",
    });
  },
);

server.registerTool(
  "upsert_run_content",
  {
    description:
      "Create or update AI-designed run content in a schema-safe allowlist: chapter rules, rewards, equipment, encounters, Longnan events, and sourced timeline milestones.",
    inputSchema: {
      sheet: z.enum(["RunChapterRule", "RogueReward", "Equipment", "CharacterEncounter", "RunEvent", "TimelineEvent"]),
      key: z.string().min(1).describe("Row key: chapter_id, id, or role_id according to sheet"),
      values: z.record(z.string(), z.union([z.string(), z.number(), z.boolean(), z.null()])),
      rationale: z.string().min(1),
    },
  },
  async ({ sheet, key, values, rationale }) => {
    const keyColumn = RUN_CONTENT_KEYS[sheet];
    const update = await upsertRow(
      sheet,
      keyColumn,
      key,
      values as RowRecord,
      { allowed: RUN_CONTENT_FIELDS[sheet] },
    );
    return result({
      ...update,
      rationale,
      next: "Run validate_config, then run_luban.",
    });
  },
);

server.registerTool(
  "promote_skill_draft",
  {
    description:
      "Turn one free-form SkillIndex draft row into a runnable skill while preserving its original ## note. Use when the user writes an owner/name/idea directly into an otherwise incomplete Excel row.",
    inputSchema: {
      excelRow: z.number().int().min(4),
      skill: z.object({
        id: z.string().min(1),
        owner_id: z.string().min(1),
        name: z.string().min(1),
        desc: z.string().min(1),
        show_tags: z.string().min(1),
        effect_ids: z.string().min(1),
        cd: z.number().nonnegative(),
        icon_loc: z.string(),
      }),
      effects: z.array(z.record(z.string(), z.union([z.string(), z.number(), z.boolean(), z.null()]))).min(1),
      rationale: z.string().min(1),
    },
  },
  async ({ excelRow, skill, effects, rationale }) => {
    const effectIds = new Set(splitRefs(skill.effect_ids));
    for (const effect of effects) {
      const effectId = text(effect.id);
      if (!effectId || !effectIds.has(effectId) || !effectId.startsWith(`${skill.id}_`)) {
        throw new Error(`Invalid effect id for ${skill.id}: ${effectId}`);
      }
    }
    for (const effect of effects) {
      const { id, ...values } = effect;
      await upsertRow("SkillEffect", "id", text(id), values as RowRecord);
    }

    const workbookName = await findWorkbookBySheet("SkillIndex");
    const workbook = await loadWorkbook(workbookName);
    const sheet = workbook.getWorksheet("SkillIndex");
    if (!sheet) throw new Error("SkillIndex sheet missing");
    if (excelRow > sheet.rowCount) throw new Error(`SkillIndex row ${excelRow} does not exist`);
    const headers = headerMap(sheet);
    const row = sheet.getRow(excelRow);
    const existingId = text(row.getCell(headers.get("id")!).value).trim();
    const draftOwner = text(row.getCell(headers.get("owner_id")!).value).trim();
    const sourceNote = text(row.getCell(headers.get("##")!).value).trim();
    if (existingId) throw new Error(`SkillIndex row ${excelRow} is already a skill: ${existingId}`);
    if (draftOwner && draftOwner !== skill.owner_id) {
      throw new Error(`Draft owner ${draftOwner} does not match ${skill.owner_id}`);
    }

    for (const [field, value] of Object.entries(skill)) {
      const column = headers.get(field);
      if (!column) throw new Error(`Unknown SkillIndex field: ${field}`);
      row.getCell(column).value = normalizeCell(value) as ExcelJS.CellValue;
    }
    row.commit();
    const backup = await backupWorkbook(workbookName);
    await workbook.xlsx.writeFile(join(DATA_ROOT, workbookName));
    return result({
      workbook: workbookName,
      sheet: "SkillIndex",
      row: excelRow,
      backup,
      sourceNote,
      rationale,
      next: "Attach the skill id with update_character_design, validate_config, then run_luban.",
    });
  },
);

server.registerTool(
  "retarget_character_encounter",
  {
    description:
      "Retarget one CharacterEncounter row after a user intentionally renamed a RoleList id. This changes only the encounter role_id and rejects duplicates.",
    inputSchema: {
      fromRoleId: z.string().min(1),
      toRoleId: z.string().min(1),
      rationale: z.string().min(1),
    },
  },
  async ({ fromRoleId, toRoleId, rationale }) => {
    const roles = await readSheet("RoleList");
    if (!roles.rows.some((row) => text(row.values.id) === toRoleId)) {
      throw new Error(`Target role does not exist: ${toRoleId}`);
    }
    const workbookName = await findWorkbookBySheet("CharacterEncounter");
    const workbook = await loadWorkbook(workbookName);
    const sheet = workbook.getWorksheet("CharacterEncounter");
    if (!sheet) throw new Error("CharacterEncounter sheet missing");
    const keyColumn = headerMap(sheet).get("role_id");
    if (!keyColumn) throw new Error("CharacterEncounter role_id missing");
    let source: ExcelJS.Row | undefined;
    for (let rowNumber = 4; rowNumber <= sheet.rowCount; rowNumber += 1) {
      const value = text(sheet.getRow(rowNumber).getCell(keyColumn).value).trim();
      if (value === toRoleId) throw new Error(`CharacterEncounter already contains ${toRoleId}`);
      if (value === fromRoleId) source = sheet.getRow(rowNumber);
    }
    if (!source) throw new Error(`CharacterEncounter does not contain ${fromRoleId}`);
    source.getCell(keyColumn).value = toRoleId;
    source.commit();
    const backup = await backupWorkbook(workbookName);
    await workbook.xlsx.writeFile(join(DATA_ROOT, workbookName));
    return result({ workbook: workbookName, sheet: "CharacterEncounter", row: source.number, backup, rationale });
  },
);

server.registerTool(
  "upsert_skill_bundle",
  {
    description:
      "Create or update one string-keyed skill and its effects. AI should map personality -> trigger/show_tags -> generic effects/buffs; use {owner}_{action} and {skill}_{effect} ids. Existing unspecified cells are preserved.",
    inputSchema: {
      skill: z.object({
        id: z.string().min(1),
        owner_id: z.string().min(1),
        name: z.string().min(1),
        desc: z.string().min(1),
        show_tags: z.string().min(1),
        effect_ids: z.string().min(1),
        cd: z.number().nonnegative(),
        icon_loc: z.string(),
      }),
      effects: z.array(z.record(z.string(), z.union([z.string(), z.number(), z.boolean(), z.null()]))).min(1),
      rationale: z.string().min(1),
    },
  },
  async ({ skill, effects, rationale }) => {
    const skillId = skill.id;
    const effectIds = new Set(splitRefs(skill.effect_ids));
    for (const effect of effects) {
      const id = text(effect.id);
      if (!id) throw new Error("Every effect requires id");
      if (!effectIds.has(id)) throw new Error(`Effect ${id} is not referenced by skill.effect_ids`);
      if (!id.startsWith(`${skillId}_`)) {
        throw new Error(`Effect id must start with ${skillId}_`);
      }
    }
    const effectUpdates = [];
    for (const effect of effects) {
      const { id, ...values } = effect;
      effectUpdates.push(await upsertRow("SkillEffect", "id", text(id), values as RowRecord));
    }
    const { id, ...skillValues } = skill;
    const skillUpdate = await upsertRow("SkillIndex", "id", id, skillValues as RowRecord);
    return result({
      skill: skillUpdate,
      effects: effectUpdates,
      rationale,
      next: "Attach the skill id with update_character_design, validate_config, then run_luban.",
    });
  },
);

server.registerTool(
  "upsert_fusion",
  {
    description:
      "Create/update a tag-driven fusion/bond. Never hardcode character names as the trigger; use required_tags and grant_skill_id.",
    inputSchema: {
      id: z.string().min(1),
      name: z.string().min(1),
      desc: z.string().min(1),
      required_tags: z.string().min(1).describe("Pipe-separated EFactionTag values"),
      require_job_unlocked: z.boolean(),
      grant_skill_id: z.string().min(1),
      rationale: z.string().min(1),
    },
  },
  async ({ id, rationale, ...values }) => {
    const update = await upsertRow("FusionSkill", "id", id, values as RowRecord);
    return result({ ...update, rationale, next: "Run validate_config, then run_luban." });
  },
);

server.registerTool(
  "validate_config",
  {
    description:
      "Validate IDs and references across characters, skills, education, equipment, encounters, run rewards, Longnan events, and fusion skills without changing files.",
    inputSchema: {},
  },
  async () => result(await validateConfig()),
);

server.registerTool(
  "run_luban",
  {
    description:
      "Run JojoP Luban export after validation. Generates C# and JSON and treats logged |ERROR| as failure.",
    inputSchema: {
      validateFirst: z.boolean().default(true),
    },
  },
  async ({ validateFirst }) => {
    if (validateFirst) {
      const validation = await validateConfig();
      if (!validation.ok) return result({ validation, export: null }, true);
    }
    const exportResult = runLuban();
    return result(exportResult, !exportResult.ok);
  },
);

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error(`[jojop-config] ready: ${PROJECT_ROOT}`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    console.error("[jojop-config] fatal:", error);
    process.exit(1);
  });
}
