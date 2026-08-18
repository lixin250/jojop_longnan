# Unity MCP（IvanMurzak / AI Game Developer）

Unity 端已装好：`com.ivanmurzak.unity.mcp` 0.88.0。

## Cursor 这边要做的

本仓库工作区是 `JojoP/`，不是 `unity/`。Unity 写的配置在 `unity/.cursor/mcp.json`，Cursor 读的是根目录 `.cursor/mcp.json`。

已把本地服务接到根配置：

```json
"ai-game-developer": {
  "type": "http",
  "url": "http://localhost:21852/p/0cff7463"
}
```

请在 Cursor 里：

1. 打开 **Settings → MCP**
2. 找到 `ai-game-developer`，打开开关（必要时点 Refresh）
3. Unity 保持打开，窗口 **Window → AI Game Developer** 显示已连接
4. 新开一轮对话后再让 Agent 调 Unity 工具（例如 `console-get-logs`）

不要用全局 `~/.cursor/mcp.json` 里的 `unity-mcp`（`relay_win.exe`），那是另一套 Unity 官方 relay，和本项目这套不是同一个。

## 端口

本地 MCP Server 默认 `http://localhost:21852`。若 Unity 窗口里端口变了，同步改根目录 `.cursor/mcp.json` 的 url。
