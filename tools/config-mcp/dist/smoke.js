import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
const here = dirname(fileURLToPath(import.meta.url));
const transport = new StdioClientTransport({
    command: process.execPath,
    args: [join(here, "index.js")],
    stderr: "pipe",
});
const client = new Client({ name: "jojop-config-smoke", version: "1.0.0" });
try {
    await client.connect(transport);
    const tools = await client.listTools();
    const validation = await client.callTool({
        name: "validate_config",
        arguments: {},
    });
    console.log(JSON.stringify({
        toolNames: tools.tools.map((tool) => tool.name),
        validation: validation.content,
    }, null, 2));
}
finally {
    await client.close();
}
//# sourceMappingURL=smoke.js.map