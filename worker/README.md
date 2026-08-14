# jojop-api

JojoP 轻量 Cloudflare Worker：

- `GET /health`
- `GET /config` — 远程开关（KV：`config`）
- `GET|PUT /save/:deviceId` — 轻量存档（KV：`save:<id>`，180 天 TTL）

## 本地

```bash
npm install
npm run dev
```

Unity `BackendConfig.baseUrl` → `http://127.0.0.1:8787`

## 上线

```bash
npx wrangler login
npm run kv:create
# 把真实 KV id 填进 wrangler.jsonc（替换占位）
npm run deploy
```
