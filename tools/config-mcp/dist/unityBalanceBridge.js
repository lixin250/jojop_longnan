import { readFile } from "node:fs/promises";
import { batchUpdateRoleStats, runLuban, validateConfig } from "./index.js";
async function main() {
    if (process.argv.includes("--validate-only")) {
        const validation = await validateConfig();
        console.log(JSON.stringify({ ok: validation.ok, validation }, null, 2));
        process.exitCode = validation.ok ? 0 : 1;
        return;
    }
    const payloadPath = process.argv[2];
    if (!payloadPath)
        throw new Error("Usage: unityBalanceBridge <payload.json> [--export]");
    const payload = JSON.parse(await readFile(payloadPath, "utf8"));
    const validationBefore = await validateConfig();
    if (!validationBefore.ok) {
        console.log(JSON.stringify({ ok: false, stage: "preflight", validation: validationBefore }, null, 2));
        process.exitCode = 1;
        return;
    }
    const update = await batchUpdateRoleStats(payload.updates);
    const validation = await validateConfig();
    if (!validation.ok) {
        console.log(JSON.stringify({ ok: false, stage: "validation", update, validation }, null, 2));
        process.exitCode = 1;
        return;
    }
    const shouldExport = payload.exportLuban || process.argv.includes("--export");
    const exportResult = shouldExport ? runLuban() : null;
    const ok = !exportResult || exportResult.ok;
    console.log(JSON.stringify({ ok, update, validation, export: exportResult }, null, 2));
    process.exitCode = ok ? 0 : 1;
}
main().catch((error) => {
    console.error(JSON.stringify({
        ok: false,
        stage: "exception",
        message: error instanceof Error ? error.message : String(error),
    }, null, 2));
    process.exit(1);
});
//# sourceMappingURL=unityBalanceBridge.js.map