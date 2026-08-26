# jojop-api

JojoP **游戏后端**（Cloudflare Worker），不是热更 CDN。

- `GET /health`
- `GET /config` — 远程开关（KV：`JOJOP_KV`）
- `GET|PUT /save/:deviceId` — 轻量存档（180 天 TTL）

Yoo 热更文件直接上 R2 桶 `jojop-cdn`，客户端从公开 URL 拉：

```text
https://pub-781168dca86c49c3826ace7d12450b5a.r2.dev/{channel}/{platform}/
```

## 本地跑游戏接口

PowerShell 不要直接敲 `npx`，用 `.cmd`：

```bat
npx.cmd wrangler login
npm.cmd run dev
```

`http://127.0.0.1:8787` 只给 `/config` `/save`。Unity 热更不依赖这个进程。

## 上线

```bat
npx.cmd wrangler login
npm.cmd run deploy
```

Worker URL 填进 `JojoPGlobalSettings.workerBaseUrl`（业务接口）。
