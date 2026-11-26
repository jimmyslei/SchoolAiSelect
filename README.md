# SchoolAiSelect

SchoolAiSelect 是面向学生和家长的 AI 择校辅助工具。它结合高德地图、公开资料和大模型，帮助用户查找附近学校、比较意向大学，以及按考试成绩筛选高中。分析过程会实时展示进度，登录后可以回看历史结果。

> AI 分析仅供参考。招生政策、报名资格和录取分数线以当地教育主管部门、考试院及学校发布的信息为准。

[快速开始](#快速开始) · [环境配置](#环境配置) · [接口速览](#接口速览) · [构建与测试](#构建与测试) · [常见问题](#常见问题)

## 功能

| 场景 | 输入 | 输出 |
| --- | --- | --- |
| 附近学校搜索 | 学校类型（幼儿园、小学、初中、高中）、当前位置或手动输入的地址、搜索半径 | 学校列表、多维度分析和可调权重的排名，也可查看学校周边设施 |
| 大学报考分析 | 高考或摸底成绩，以及意向大学或专业方向、个人偏好 | 指定院校对比，或按分数和偏好推荐院校；展示近年录取分数线 |
| 高中择校推荐 | 考试分数、所在省份，可选城市 | 根据近年录取分数线，提供「冲、稳、保」三档推荐 |

应用还提供邮箱验证码注册、登录、学校留言和站点留言。三种分析都需要登录；完成后的分析可在 `/history` 页面回看或删除。

## 快速开始

### 1. 安装环境与依赖

- **Node.js 24 LTS 和 pnpm 10**：用于安装工作区依赖、运行前端及 Node.js 搜索技能。
- **.NET 10 SDK**：用于构建和运行后端。
- **Redis**：完整体验需要可访问的 Redis，用于登录会话。默认地址为 `localhost:6379`。
- **Python 3**：仅在执行 Python 技能时需要；部分技能还需要 Playwright 和 Chromium，见[搜索技能](#搜索技能)。

```bash
git clone https://github.com/jimmyslei/SchoolAiSelect.git
cd SchoolAiSelect
pnpm install
```

如果已有 Redis，可以直接使用。没有 Redis 且已安装 Docker 时，可启动一个仅监听本机的开发实例：

```bash
docker run --rm -d --name school-ai-redis -p 127.0.0.1:6379:6379 redis:7
```

### 2. 创建后端配置

在仓库根目录执行以下任一命令。如果已有 `.env`，请直接编辑，避免覆盖本地配置。

```powershell
# Windows PowerShell
Copy-Item packages/server/.env.example packages/server/.env
```

```bash
# macOS / Linux
cp packages/server/.env.example packages/server/.env
```

替换模板中的占位值：附近学校搜索和地图功能需要高德 Web 服务密钥及签名密钥；AI 分析需要大模型密钥；注册新账号还需要可用的 SMTP。若 Redis 不在默认地址，在 `.env` 中补上 `REDIS_CONNECTION_STRING`。仅检查站点和后端是否启动时，可以暂不配置外部服务。

### 3. 启动应用

在仓库根目录启动前后端：

```bash
pnpm dev
```

也可以在两个终端分别运行 `pnpm dev:client` 和 `pnpm dev:server`。

| 地址 | 用途 |
| --- | --- |
| [http://localhost:5173](http://localhost:5173) | 前端首页；若端口被占用，以 Vite 输出为准 |
| [http://localhost:3001/api/health](http://localhost:3001/api/health) | 后端健康检查，无需登录；正常返回 `status: "ok"` |
| [http://localhost:3001/scalar](http://localhost:3001/scalar) | Scalar 交互式接口文档 |
| [http://localhost:3001/openapi/v1.json](http://localhost:3001/openapi/v1.json) | OpenAPI JSON |

健康接口只确认后端可响应，不检查 Redis、SMTP、地图或模型是否可用。体验完整流程时，在首页注册并登录，然后选择一种分析模式。

## 环境配置

后端从**进程当前工作目录**读取 `.env`，已设置且非空的进程环境变量优先。根目录的 `pnpm dev` 和 `pnpm dev:server` 会在后端包目录运行对应脚本；直接运行后端时，请先进入 `packages/server`，或通过进程环境变量传入配置。不要提交 `.env`、真实密钥或带凭证的 Redis 连接字符串。

完整示例见 [`packages/server/.env.example`](packages/server/.env.example)，实际取值和默认值见 [`SchoolServiceOptions.cs`](packages/server/Options/SchoolServiceOptions.cs)。

| 变量 | 用途 / 默认值 |
| --- | --- |
| `PORT` | 后端 Development 环境的 HTTP 端口，默认 `3001`。如修改，还需更新前端代理目标 |
| `AMAP_API_KEY`、`SK` | 高德地图 Web 服务 API Key 与服务端签名密钥，用于位置和学校周边查询 |
| `DASHSCOPE_API_KEY` | 阿里云百炼模型调用密钥；也支持 `OPENAI_API_KEY` |
| `DASHSCOPE_BASE_URL`、`DASHSCOPE_MODEL` | 默认 `https://dashscope.aliyuncs.com/compatible-mode/v1` 和 `qwen3.6-plus`；兼容变量分别为 `OPENAI_BASE_URL`、`OPENAI_CHAT_MODEL_NAME` |
| `DASHSCOPE_API_STYLE` | 默认使用 Chat Completions；设为 `responses` 可切换，Base URL 以 `/responses` 结尾也可自动切换。支持 `OPENAI_API_STYLE` |
| `REDIS_CONNECTION_STRING` | 登录会话依赖 Redis，默认 `localhost:6379`；如需自定义，请手动补入模板 |
| `SMTP_HOST`、`SMTP_FROM_EMAIL` | 发送注册验证码所需的 SMTP 主机与发件地址。未设置 `SMTP_FROM_EMAIL` 时使用 `SMTP_USERNAME` |
| `SMTP_PORT`、`SMTP_ENABLE_SSL`、`SMTP_USERNAME`、`SMTP_PASSWORD`、`SMTP_FROM_NAME` | 邮件连接与发件人设置。默认端口 `587`，SSL 默认开启；账号与密码按邮件服务商要求配置 |
| `DATABASE_PATH` | SQLite 文件路径；默认是后端输出目录下的 `data/school-recommender.db`。如果使用相对路径，也以程序所在目录为基准 |
| `SKILL_ROOT_PATH`、`NODE_COMMAND`、`PYTHON_COMMAND` | 技能根目录默认是程序所在目录的 `skills/`；脚本执行命令默认为 `node` 和 `python` |
| `SKILL_TIMEOUT_SECONDS`、`ANALYSIS_MAX_CONCURRENCY` | 单次技能超时与分析并发数，代码默认分别为 `60` 秒和 `5`；`.env.example` 将技能超时设为 `90` 秒 |
| `AMAP_PAGE_SIZE`、`DASHSCOPE_NETWORK_TIMEOUT_SECONDS` | 高德搜索单页条数和模型网络超时，默认分别为 `25` 和 `180` 秒。模型限流重试等进阶选项见配置源码 |

使用其他 OpenAI 兼容服务时，可以直接修改模板中的 `DASHSCOPE_*` 项。如果改用 `OPENAI_*` 兼容变量，请删除对应的 `DASHSCOPE_*` 项，包括模板中的占位值，否则它们会优先取用。

前端始终请求相对路径 `/api`。开发时，Vite 默认将 `/api` 代理到 `http://127.0.0.1:3001`；如后端端口或地址有变，在启动 Vite 前设置进程环境变量 `VITE_API_TARGET`。它只影响开发代理，不改变生产构建的 API 路径。

## 项目结构

项目是一个 pnpm monorepo。前端使用 React 18、TypeScript、Vite 和 Tailwind CSS；后端使用 .NET 10 Minimal API、Microsoft Agents AI、FreeSql / SQLite 和 Redis。

```text
SchoolAiSelect/
├── packages/
│   ├── client/                  # 前端：页面、组件、Hooks、API 封装
│   └── server/                  # 后端：HTTP API、分析 Agent 与数据访问
│       ├── Endpoints/           # 按功能分组的 API 路由
│       ├── Services/            # 地图、认证、AI 分析、排名与历史服务
│       ├── Models/              # 请求、响应和分析结果契约
│       ├── Options/             # 后端配置读取与默认值
│       ├── skills/              # 文件式搜索技能
│       └── tests/               # 后端测试项目
├── docs/WIKI.md                 # 架构与实现细节
├── school.slnx                  # .NET 解决方案
└── pnpm-workspace.yaml           # pnpm 工作区定义
```

开发入口：[前端路由](packages/client/src/App.tsx)、[首页](packages/client/src/pages/HomePage.tsx)、[前端 API 封装](packages/client/src/lib/api.ts)、[后端启动入口](packages/server/Program.cs)和[接口契约](packages/server/Models/Contracts.cs)。

### 搜索技能

[`packages/server/skills`](packages/server/skills) 下的技能以 `SKILL.md` 描述能力，并将执行脚本放在 `scripts/` 中；部分技能还包含 `_meta.json` 或 `requirements.txt`。项目构建和发布时会把技能文件复制到输出目录。

[公众号文章搜索](packages/server/skills/wechat-article-search/SKILL.md)和[通用网络搜索](packages/server/skills/general-web-search/SKILL.md)使用 Node.js；[网页搜索](packages/server/skills/web-search/SKILL.md)、[搜狗搜索](packages/server/skills/sogou-search/SKILL.md)和[小红书搜索](packages/server/skills/xiaohongshu-search/SKILL.md)使用 Python。请按对应说明安装依赖，有 Playwright 要求时还需要安装 Chromium；如果 Python 命令不是 `python`，请设置 `PYTHON_COMMAND`。[AnySearch](packages/server/skills/anysearch-skill/SKILL.md) 有单独的凭证和运行时配置说明。

## 接口速览

需要登录的接口使用 `Authorization: Bearer <token>`。请求与响应字段以 [API 契约](packages/server/Models/Contracts.cs)和启动后的 [Scalar 文档](http://localhost:3001/scalar)为准。

| 方法与路径 | 用途 | 权限 |
| --- | --- | --- |
| `GET /api/health` | 查看后端状态 | 公开 |
| `POST /api/auth/register/code`、`POST /api/auth/register`、`POST /api/auth/login` | 发送注册验证码、注册、登录 | 公开 |
| `GET /api/auth/me` | 获取当前登录用户 | 登录 |
| `POST /api/schools/search` | 搜索附近学校、流式分析和排名 | 登录 |
| `POST /api/universities/search` | 流式分析指定院校或推荐大学 | 登录 |
| `POST /api/highschools/recommend` | 根据分数和地区流式推荐高中 | 登录 |
| `POST /api/location/geocode`、`POST /api/location/reverse`、`GET /api/location/ip` | 地址与坐标互转、IP 定位 | 公开 |
| `GET /api/schools/nearby` | 查询地铁、医院等周边设施 | 公开 |
| `GET /api/schools/{id}` | 学校详情预留接口，当前返回 `501` | 公开 |
| `GET /api/history`、`GET /api/history/{id}` | 查询个人历史列表、详情 | 登录 |
| `DELETE /api/history/{id}`、`DELETE /api/history` | 删除单条记录、清空个人记录 | 登录 |
| `GET /api/messages`、`GET /api/site-messages` | 查看学校和站点留言 | 公开 |
| `POST /api/messages`、`POST /api/messages/{messageId}/replies`、`POST /api/site-messages` | 发布学校留言、回复学校留言、发布站点留言 | 登录 |
| `POST /api/site-messages/{messageId}/replies` | 回复站点留言 | 管理员 |
| `POST /api/analytics/visit` | 记录站点访问 | 公开 |

三种分析接口都接收 JSON `POST` 请求，并返回 `text/event-stream`。客户端使用 `fetch` 读取响应流：`meta` 描述当前查询，`agent_step` 推送进度，附近学校和大学接口可通过 `analysis` 推送单项结果；最终结果由 `done` 发送，流内错误使用 `error`。鉴权或参数校验失败时，接口会在开启 SSE 前返回 JSON 错误。

## 构建与测试

以下命令均从仓库根目录执行：

| 命令 | 用途 |
| --- | --- |
| `pnpm build` | 构建前后端，前端静态产物位于 `packages/client/dist/` |
| `pnpm --filter client build` | 只构建前端 |
| `pnpm --filter client test` | 运行前端 Vitest 测试 |
| `pnpm --filter client test:watch` | 监听前端文件变化并重跑测试 |
| `dotnet build school.slnx` | 构建后端解决方案 |
| `dotnet test school.slnx` | 运行后端测试 |
| `dotnet publish packages/server/SchoolRecommender.Api.csproj -c Release -o artifacts/server` | 生成后端发布产物 |

### 部署要点

- 用静态站点或反向代理托管 `packages/client/dist/`，将 `/api` 转发给后端；前端路由需要回退到 `index.html`，使 `/history` 等地址可以直接打开。
- 反向代理应关闭 SSE 响应缓冲，并为流式请求留出足够的读取超时时间。公开部署时使用 HTTPS，只让受信任的代理访问后端。
- 将后端进程的工作目录设为发布目录，执行 `dotnet SchoolRecommender.Api.dll`。在该目录放置 `.env`，或由宿主注入环境变量。当前 `Program.cs` 在 `Production` 环境中监听 HTTP `80`，与开发端口 `3001` 不同。为 SQLite 配置持久、可写并可备份的目录，并确保后端能访问 Redis 及已配置的外部服务。
- 根 `package.json` 的 `deploy:build` 依赖未随仓库提供的 `deploy/build.ps1`。没有该脚本时，使用上面的标准构建与发布命令。

## 常见问题

| 现象 | 检查方式 |
| --- | --- |
| 前端请求 `/api` 返回 502 或连接失败 | 先访问后端 `/api/health`；确认后端已启动、环境为 `Development`，且 Vite 的代理目标与实际端口一致 |
| 无法注册或登录 | 检查 Redis 是否可达；注册收不到验证码时，还需核对 SMTP 主机、发件人和邮件服务商要求的授权信息 |
| 定位或附近学校搜索失败 | 检查浏览器定位权限，可尝试手动输入地址；若地图接口仍失败，核对高德 Web 服务 API Key 和签名密钥 |
| 模型分析失败或缺少部分搜索资料 | 核对模型密钥、端点与模型名，检查模型服务配额；再根据后端日志确认相关技能的运行时、依赖和授权是否齐全 |
| 反向代理后分析进度一次性显示 | 检查代理是否缓冲 `text/event-stream`，以及流式请求是否过早超时 |

## 贡献与许可证

提交问题时，请附上复现步骤、运行环境和已脱敏的错误信息。提交改动前，建议运行对应包的构建和测试；修改 API 契约时，同时更新前端类型及调用方。更多实现细节见 [项目 Wiki](docs/WIKI.md)，仓库开发约定见 [AGENTS.md](AGENTS.md)。

根项目采用 [MIT 许可证](LICENSE)。第三方技能附带的许可证与声明以其目录中的文件为准。
