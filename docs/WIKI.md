# 📚 School Recommender 项目 Wiki

> **学校推荐系统** — 基于 AI 的智能学校分析与推荐平台

---

## 目录

1. [项目概述](#1-项目概述)
2. [技术栈](#2-技术栈)
3. [项目结构](#3-项目结构)
4. [前端架构](#4-前端架构)
5. [后端架构](#5-后端架构)
6. [API 接口文档](#6-api-接口文档)
7. [数据模型](#7-数据模型)
8. [AI 分析引擎](#8-ai-分析引擎)
9. [技能系统 (Skills)](#9-技能系统-skills)
10. [第三方集成](#10-第三方集成)
11. [认证系统](#11-认证系统)
12. [实时搜索流程 (SSE)](#12-实时搜索流程-sse)
13. [评分算法](#13-评分算法)
14. [环境变量配置](#14-环境变量配置)
15. [开发指南](#15-开发指南)
16. [测试](#16-测试)
17. [部署指南](#17-部署指南)
18. [项目约定与最佳实践](#18-项目约定与最佳实践)

---

## 1. 项目概述

School Recommender 是一个 **全栈 AI 驱动的学校推荐系统**，帮助用户根据地理位置搜索附近学校，并通过 AI 分析微信公众号文章、网络公开信息等多渠道数据，生成多维度评分和排名推荐。

### 核心功能

- **🏫 学校搜索与推荐**：基于用户位置（GPS / IP / 手动输入），搜索附近幼儿园、小学、初中、高中，并通过 AI 进行多维度分析和排名
- **🎓 大学分析与排名**：支持大学搜索与 AI 分析
- **📊 多维度评分体系**：包括师资力量、声誉口碑、活动活跃度、内容质量、学生活动、获奖情况、距离等 7 个维度
- **💬 留言互动**：用户可针对学校发布留言和回复
- **🔐 用户认证**：邮箱注册/登录，支持验证码
- **📍 地理位置服务**：地址转坐标、坐标转地址、IP 定位
- **📈 站点访问统计**：记录页面访问量
- **🔗 URL 分享**：搜索参数可序列化到 URL，支持分享搜索结果

### 学校类型支持

| 类型   | 标识符         | 高德 POI 类别码 |
| ------ | -------------- | --------------- |
| 幼儿园 | `kindergarten` | 141204          |
| 小学   | `primary`      | 141203          |
| 初中   | `junior_high`  | 141202          |
| 高中   | `senior_high`  | 141202          |

---

## 2. 技术栈

### 前端 (`packages/client`)

| 类别     | 技术                                           | 版本           |
| -------- | ---------------------------------------------- | -------------- |
| 框架     | React                                          | 18.x           |
| 构建工具 | Vite                                           | 6.x            |
| 语言     | TypeScript                                     | 5.6.x          |
| 样式     | Tailwind CSS                                   | 4.x            |
| UI 组件  | Radix UI                                       | 各组件独立版本 |
| 动画     | Framer Motion                                  | 11.x           |
| 图标     | Lucide React                                   | 0.468.x        |
| 图表     | Recharts                                       | 2.x            |
| 路由     | React Router DOM                               | 7.x            |
| 工具库   | clsx, tailwind-merge, class-variance-authority | -              |
| 测试     | Vitest + Testing Library + jsdom               | -              |

### 后端 (`packages/server`)

| 类别      | 技术                                          | 版本/说明          |
| --------- | --------------------------------------------- | ------------------ |
| 框架      | ASP.NET Core Minimal API                      | .NET 10            |
| ORM       | FreeSql                                       | SQLite             |
| AI SDK    | Microsoft.Agents.AI + Microsoft.Extensions.AI | Agent 框架         |
| AI 提供商 | DashScope (阿里云百炼)                        | OpenAI 兼容接口    |
| 默认模型  | qwen3.6-plus                                  | 可配置             |
| 日志      | Serilog                                       | Console + File     |
| API 文档  | Scalar + OpenAPI                              | /scalar            |
| 限流      | ASP.NET Rate Limiting                         | 固定窗口 30次/分钟 |
| 缓存      | IMemoryCache                                  | 内存缓存           |
| 邮件      | System.Net.Mail (SMTP)                        | 注册验证码         |
| 密码      | Microsoft.AspNetCore.Identity PasswordHasher  | PBKDF2             |

### Monorepo 管理

| 工具         | 用途                     |
| ------------ | ------------------------ |
| pnpm         | 包管理器 + workspace     |
| concurrently | 并行运行前后端开发服务器 |

---

## 3. 项目结构

```
school/
├── AGENTS.md                          # 项目约定与指南
├── package.json                       # 根 monorepo 配置
├── pnpm-workspace.yaml                # pnpm workspace 定义
├── school.slnx                        # .NET 解决方案文件
├── deploy/
│   ├── build.ps1                      # Windows 生产构建脚本
│   ├── nginx.conf                     # Nginx 反向代理配置
│   ├── school-recommender.service     # systemd 服务文件
│   └── setup-server.sh               # Ubuntu 服务器初始化脚本
├── packages/
│   ├── client/                        # 前端 React 应用
│   │   ├── index.html                 # 入口 HTML
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   ├── vite.config.ts             # Vite 配置（含 API 代理）
│   │   └── src/
│   │       ├── App.tsx                # 应用入口（路由定义）
│   │       ├── main.tsx               # React 挂载点
│   │       ├── index.css              # 全局样式
│   │       ├── components/
│   │       │   ├── auth/              # 认证相关组件
│   │       │   ├── home/              # 首页区块组件
│   │       │   ├── layout/            # Header、Footer
│   │       │   ├── messages/          # 留言板组件
│   │       │   ├── results/           # 搜索结果展示（排名、图表）
│   │       │   ├── search/            # 搜索流程组件
│   │       │   ├── shared/            # 通用共享组件
│   │       │   └── ui/                # 基础 UI 组件（Button 等）
│   │       ├── hooks/                 # 自定义 React Hooks
│   │       │   ├── useAuth.ts         # 用户认证状态
│   │       │   ├── useGeolocation.ts  # 浏览器地理位置
│   │       │   ├── useLocalStorage.ts # localStorage 封装
│   │       │   ├── useSchoolSearch.ts # 学校搜索 + SSE 流
│   │       │   ├── useSearchHistory.ts# 搜索历史管理
│   │       │   ├── useTheme.ts        # 主题切换
│   │       │   ├── useUniversitySearch.ts # 大学搜索
│   │       │   └── useWeightConfig.ts # 评分权重配置
│   │       ├── lib/
│   │       │   ├── api.ts             # API 调用封装
│   │       │   ├── scoreCalculator.ts # 前端评分计算
│   │       │   └── utils.ts           # 工具函数
│   │       ├── pages/
│   │       │   └── HomePage.tsx       # 唯一页面（SPA）
│   │       └── types/
│   │           └── school.ts          # TypeScript 类型定义
│   └── server/                        # 后端 .NET API
│       ├── Program.cs                 # Minimal API 入口 + 路由定义
│       ├── appsettings.json           # 应用配置
│       ├── SchoolRecommender.Api.csproj
│       ├── Data/
│       │   └── Entities.cs            # FreeSql 实体定义
│       ├── Models/
│       │   └── Contracts.cs           # API 请求/响应 DTO
│       ├── Options/
│       │   └── SchoolServiceOptions.cs# 服务配置选项
│       ├── Services/
│       │   ├── AmapService.cs         # 高德地图 API 封装
│       │   ├── AnalyticsService.cs    # 站点访问统计
│       │   ├── AuthService.cs         # 用户认证
│       │   ├── DashScopeResponseFixerHandler.cs    # DashScope 兼容层
│       │   ├── FunctionCallArgumentNormalization.cs # 函数调用参数修复
│       │   ├── FunctionCallArgumentNormalizerChatClient.cs
│       │   ├── LocalSkillScriptRunner.cs            # 技能脚本运行器
│       │   ├── MessageService.cs      # 留言服务
│       │   ├── SchoolAnalysisAgent.cs # 学校分析 AI Agent
│       │   ├── SchoolAnalysisOrchestrator.cs        # 学校分析编排器
│       │   ├── SchoolRankingService.cs              # 学校排名服务
│       │   ├── UniversityAnalysisAgent.cs           # 大学分析 AI Agent
│       │   ├── UniversityAnalysisOrchestrator.cs    # 大学分析编排器
│       │   └── UniversityRankingService.cs          # 大学排名服务
│       ├── skills/                    # AI Agent 工具技能
│       │   ├── general-web-search/
│       │   ├── sogou-search/
│       │   ├── web-search/
│       │   ├── wechat-article-search/
│       │   └── xiaohongshu-search/
│       ├── Utils/
│       │   └── EnvFileLoader.cs       # .env 文件加载器
│       ├── Properties/
│       │   └── launchSettings.json
│       ├── openapi/
│       │   └── SchoolRecommender.Api.json  # OpenAPI 文档
│       └── tests/
│           └── SchoolRecommender.Api.Tests/
```

---

## 4. 前端架构

### 4.1 路由结构

当前为单页面应用（SPA），仅一个路由：

```
/ → HomePage
```

`HomePage` 是整个应用的核心页面，包含搜索流程的全部状态管理。

### 4.2 组件层次

```
App.tsx
└── BrowserRouter
    └── ErrorBoundary
        └── Routes
            └── HomePage
                ├── Header（导航 + 认证入口）
                ├── AuthDialog（登录/注册弹窗）
                ├── HeroSection（首页英雄区）
                ├── FeaturesSection（功能特性介绍）
                ├── ValueSection（价值主张）
                ├── SearchHistoryCard（搜索历史记录）
                ├── SchoolTypeDialog（学校类型选择弹窗）
                ├── ScoreInputDialog（分数输入弹窗）
                ├── LocationStep（位置获取步骤）
                ├── SearchingView（搜索进行中视图）
                ├── UniversitySearchDialog（大学搜索弹窗）
                ├── SchoolRankingList（学校排名列表）
                ├── UniversityRankingList（大学排名列表）
                ├── ComparisonBarChart（对比柱状图）
                ├── StepIndicator（步骤指示器）
                ├── ShareButton（分享按钮）
                ├── MessageBoard（留言板）
                └── Footer（页脚）
```

### 4.3 搜索流程

**学校搜索（4 步流程）**：

1. **选择类型** — 选择学校类型（幼儿园/小学/初中/高中）
2. **获取位置** — 通过浏览器 GPS、IP 定位或手动输入地址获取坐标
3. **搜索分析** — SSE 流式传输，实时展示各学校的 AI 分析进度
4. **查看结果** — 排名列表、多维评分、柱状图对比

**大学搜索（3 步流程）**：

1. **填写信息** — 输入大学名称和考生分数
2. **AI 分析** — AI 分析大学信息
3. **查看排名** — 排名结果展示

### 4.4 核心 Hooks

| Hook                  | 职责                                                 |
| --------------------- | ---------------------------------------------------- |
| `useAuth`             | 管理认证状态（token、用户信息），localStorage 持久化 |
| `useGeolocation`      | 获取浏览器地理位置（GPS / IP fallback）              |
| `useLocalStorage`     | 通用 localStorage 读写封装                           |
| `useSchoolSearch`     | 学校搜索状态管理，SSE 流式数据处理                   |
| `useSearchHistory`    | 搜索历史记录（localStorage 存储）                    |
| `useTheme`            | 明暗主题切换                                         |
| `useUniversitySearch` | 大学搜索状态管理                                     |
| `useWeightConfig`     | 评分权重配置管理                                     |

### 4.5 URL 参数序列化

搜索状态可序列化为 URL 查询参数，支持结果分享：

```
/?lat=29.563456&lng=106.552891&type=junior_high&r=5000&score=650
```

| 参数    | 说明                       |
| ------- | -------------------------- |
| `lat`   | 纬度                       |
| `lng`   | 经度                       |
| `type`  | 学校类型                   |
| `r`     | 搜索半径（米），默认 5000  |
| `score` | 学生分数（高中搜索时使用） |

### 4.6 Vite 配置

- **路径别名**：`@` → `packages/client/src`
- **开发端口**：6173
- **API 代理**：`/api` → `http://127.0.0.1:3001`（可通过 `VITE_API_TARGET` 环境变量覆盖）

---

## 5. 后端架构

### 5.1 服务注册（DI）

| 生命周期   | 服务                             |
| ---------- | -------------------------------- |
| Singleton  | `SchoolServiceOptions`           |
| Singleton  | `IFreeSql`（FreeSql + SQLite）   |
| Singleton  | `LocalSkillScriptRunner`         |
| Singleton  | `SchoolAnalysisAgent`            |
| Singleton  | `SchoolAnalysisOrchestrator`     |
| Singleton  | `SchoolRankingService`           |
| Singleton  | `UniversityAnalysisAgent`        |
| Singleton  | `UniversityAnalysisOrchestrator` |
| Singleton  | `UniversityRankingService`       |
| Singleton  | `AuthService`                    |
| Singleton  | `MessageService`                 |
| Singleton  | `AnalyticsService`               |
| HttpClient | `AmapService`                    |

### 5.2 中间件管线

```
1. UseForwardedHeaders    — 反向代理头处理
2. UseExceptionHandler    — 全局异常处理
3. UseCors                — 跨域（AllowAny）
4. UseRateLimiter         — 限流（30次/分钟）
5. UseSerilogRequestLogging — 请求日志
```

### 5.3 数据库

- **引擎**：SQLite
- **ORM**：FreeSql（CodeFirst 自动同步结构）
- **数据库文件路径**：通过 `DATABASE_PATH` 环境变量配置，默认为 `data/school-recommender.db`

**实体表**：

| 表名                       | 实体类                  | 用途                            |
| -------------------------- | ----------------------- | ------------------------------- |
| `app_user`                 | `AppUser`               | 用户账户                        |
| `email_verification_codes` | `EmailVerificationCode` | 邮箱验证码                      |
| `messages`                 | `Message`               | 留言                            |
| `message_replies`          | `MessageReply`          | 留言回复                        |
| `school_analysis_logs`     | `SchoolAnalysisLog`     | 学校分析日志（含完整分析 JSON） |
| `site_visit_stats`         | `SiteVisitStat`         | 站点访问统计                    |

### 5.4 日志配置

使用 Serilog，分三层输出：

| 输出     | 级别                    | 文件路径                |
| -------- | ----------------------- | ----------------------- |
| 控制台   | Verbose                 | —                       |
| 信息文件 | Info 以上（排除 Error） | `logs/info-{date}.log`  |
| 错误文件 | Error 以上              | `logs/error-{date}.log` |

日志保留 30 天，按日滚动。

---

## 6. API 接口文档

> 完整 OpenAPI 文档可在运行时访问 `http://localhost:3001/openapi/v1.json`  
> 交互式文档：`http://localhost:3001/scalar`

### 6.1 认证 (Auth)

| 方法   | 路径                      | 说明                 | 认证      |
| ------ | ------------------------- | -------------------- | --------- |
| `POST` | `/api/auth/register/code` | 发送邮箱注册验证码   | ❌        |
| `POST` | `/api/auth/register`      | 邮箱注册             | ❌        |
| `POST` | `/api/auth/login`         | 邮箱登录             | ❌        |
| `GET`  | `/api/auth/me`            | 获取当前登录用户信息 | ✅ Bearer |

### 6.2 留言 (Messages)

| 方法   | 路径                                | 说明                         | 认证      |
| ------ | ----------------------------------- | ---------------------------- | --------- |
| `GET`  | `/api/messages?schoolId=xxx`        | 获取留言列表（可按学校筛选） | ❌        |
| `POST` | `/api/messages`                     | 发布学校留言                 | ✅ Bearer |
| `POST` | `/api/messages/{messageId}/replies` | 回复留言                     | ✅ Bearer |

### 6.3 地理位置 (Location)

| 方法   | 路径                    | 说明       | 认证 |
| ------ | ----------------------- | ---------- | ---- |
| `POST` | `/api/location/geocode` | 地址转坐标 | ❌   |
| `POST` | `/api/location/reverse` | 坐标转地址 | ❌   |
| `GET`  | `/api/location/ip`      | IP 定位    | ❌   |

### 6.4 学校搜索 (Schools)

| 方法   | 路径                             | 说明               | 认证 | 响应类型                |
| ------ | -------------------------------- | ------------------ | ---- | ----------------------- |
| `POST` | `/api/schools/search`            | 学校搜索与 AI 分析 | ❌   | SSE (text/event-stream) |
| `POST` | `/api/schools/university-search` | 大学搜索与 AI 分析 | ❌   | SSE (text/event-stream) |

### 6.5 系统 (System)

| 方法   | 路径                          | 说明               | 认证 |
| ------ | ----------------------------- | ------------------ | ---- |
| `GET`  | `/api/health`                 | 健康检查与系统信息 | ❌   |
| `POST` | `/api/analytics/visit?path=/` | 记录站点访问       | ❌   |

### 6.6 SSE 事件类型

学校搜索 (`/api/schools/search`) 返回 Server-Sent Events，事件类型如下：

| 事件       | 数据                                                        | 说明                               |
| ---------- | ----------------------------------------------------------- | ---------------------------------- |
| `meta`     | `{ location, schools[], searchRadius, totalFound }`         | 搜索元数据，包含学校列表           |
| `analysis` | `{ schoolId, analysis, overallScore, admissionMatchScore }` | 单个学校的 AI 分析结果（逐条推送） |
| `done`     | `{ rankings[] }`                                            | 搜索完成，最终排名                 |
| `error`    | `{ message }`                                               | 错误信息                           |

### 6.7 限流策略

- **策略**：固定窗口限流
- **限制**：30 次/分钟
- **限流范围**：`/api/auth/*`、`/api/messages/*`、`/api/location/*`、`/api/schools/*`
- **超出限流**：返回 `429 Too Many Requests`

---

## 7. 数据模型

### 7.1 后端实体 (FreeSql)

#### AppUser

```csharp
[Table("app_user")]
- Id (long, PK, 自增)
- Email (string, 唯一)
- DisplayName (string)
- PasswordHash (string)
- CreatedAt (DateTime)
```

#### EmailVerificationCode

```csharp
[Table("email_verification_codes")]
- Id (long, PK, 自增)
- Email (string)
- Code (string)
- CreatedAt (DateTime)
- ExpiresAt (DateTime)
- UsedAt (DateTime?)
```

#### Message / MessageReply

```csharp
[Table("messages")]
- Id (long, PK)
- SchoolId, SchoolName, UserId, AuthorName, Content
- CreatedAt

[Table("message_replies")]
- Id (long, PK)
- MessageId (FK), UserId, AuthorName, Content
- CreatedAt
```

#### SchoolAnalysisLog

```csharp
[Table("school_analysis_logs")]
- Id, SearchSessionId, SchoolId, SchoolName, SchoolType
- SearchLatitude, SearchLongitude, SearchRadius, SearchAddress
- SchoolDistance, OverallScore, Rank, IsTopPick
- ActivityScore, ReputationScore, ContentQualityScore
- TeachingStaffScore, AwardsCount, StudentActivityScore
- SourceCount, AnalysisResultJson
- ClientIp, UserAgent, CreatedAt
```

#### SiteVisitStat

```csharp
[Table("site_visit_stats")]
- Id (long, PK)
- Path (string, 唯一索引)
- VisitCount (long)
- LastVisitedAt, CreatedAt
```

### 7.2 前端类型 (TypeScript)

```typescript
// 学校类型
type SchoolType = "kindergarten" | "primary" | "junior_high" | "senior_high";

// 学校基本信息
interface School {
  id: string;
  name: string;
  type: SchoolType;
  address: string;
  latitude: number;
  longitude: number;
  distance: number;
  phone?: string;
  website?: string;
}

// 多维度分析数据
interface SchoolAnalysis {
  activityScore: number; // 发帖活跃度 (0-100)
  lastPostDate: string;
  reputationScore: number; // 声誉口碑 (0-100)
  reputationSummary: string;
  contentQualityScore: number; // 内容质量 (0-100)
  teachingStaffScore: number; // 师资力量 (0-100)
  teachingStaffDesc: string;
  awardsCount: number; // 获奖数量
  awardsDetail: string[];
  studentActivityScore: number; // 学生活动 (0-100)
  studentActivityDesc: string;
  sources: InfoSource[];
  admissionCutoffScores?: string; // 高中录取分数线 JSON
}

// 排名结果
interface SchoolRanking {
  school: School;
  analysis: SchoolAnalysis;
  rank: number;
  overallScore: number;
  recommendation: string;
  isTopPick: boolean;
  admissionMatchScore?: number;
  analyzing?: boolean; // 前端流式状态
}
```

---

## 8. AI 分析引擎

### 8.1 架构概览

```
┌───────────────────────────────────────────────────┐
│                  前端 (React)                       │
│         useSchoolSearch → SSE 流式消费              │
└───────────────┬───────────────────────────────────┘
                │ POST /api/schools/search (SSE)
                ▼
┌───────────────────────────────────────────────────┐
│              Program.cs (Minimal API)               │
│  ┌─────────────────────────────────────────────┐  │
│  │   SchoolAnalysisOrchestrator                 │  │
│  │   - 并发分析（最大 ANALYSIS_MAX_CONCURRENCY） │  │
│  │   - 逐条产出 → IAsyncEnumerable              │  │
│  └───────────────┬─────────────────────────────┘  │
│                  ▼                                  │
│  ┌─────────────────────────────────────────────┐  │
│  │   SchoolAnalysisAgent (AI Agent)             │  │
│  │   - ChatClientAgent (Microsoft.Agents.AI)    │  │
│  │   - 工具：search_wechat_articles             │  │
│  │          search_school_info                   │  │
│  │   - 输出：结构化评分 JSON                     │  │
│  └───────────────┬─────────────────────────────┘  │
│                  ▼                                  │
│  ┌─────────────────────────────────────────────┐  │
│  │   SchoolRankingService                       │  │
│  │   - 加权综合评分                             │  │
│  │   - 录取匹配分（高中）                       │  │
│  │   - 排名 + 推荐理由生成                      │  │
│  └─────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────┘
```

### 8.2 SchoolAnalysisAgent

**职责**：调用 AI 模型分析单个学校的公开信息，输出结构化评分。

**AI 模型**：默认使用 DashScope 的 `qwen3.6-plus`，通过 OpenAI 兼容接口调用。

**工具函数（Tools）**：

| 工具名                   | 参数                                     | 说明                 |
| ------------------------ | ---------------------------------------- | -------------------- |
| `search_wechat_articles` | `query` (必填), `num`, `region`          | 搜索微信公众号文章   |
| `search_school_info`     | `school_name` (必填), `aspect`, `region` | 学校信息专用联网搜索 |

**输出格式**：Agent 被要求输出包含以下维度的结构化 JSON：

- `activityScore` (0-100) — 官方账号发帖活跃度
- `reputationScore` (0-100) — 网络用户评价分
- `contentQualityScore` (0-100) — 文章质量分
- `teachingStaffScore` (0-100) — 师资力量评分
- `awardsCount` (int) — 学校获奖数量
- `studentActivityScore` (0-100) — 学生活动评分
- `admissionCutoffScores` — 高中历年录取分数线 JSON

### 8.3 DashScope 兼容层

由于 DashScope 的 OpenAI 兼容接口存在格式差异，项目包含两个兼容层：

1. **`DashScopeResponseFixerHandler`**（HttpMessageHandler 层）
   - 修复 DashScope 将 `tool_call.arguments` 序列化为数组而非对象的问题
   - 在 OpenAI SDK 反序列化前拦截并修正

2. **`FunctionCallArgumentNormalizerChatClient`**（IChatClient 层）
   - 在 Microsoft.Extensions.AI 层修复函数参数格式
   - 确保模型传入的参数符合 `AIFunctionArguments` 预期的形状

### 8.4 SchoolAnalysisOrchestrator

- 接收一批学校，**并发**调用 `SchoolAnalysisAgent` 分析每个学校
- 并发度由 `ANALYSIS_MAX_CONCURRENCY` 配置，默认 8
- 通过 `IAsyncEnumerable` 逐条产出分析结果，实现流式传输
- 单个学校分析超时由 `SKILL_TIMEOUT_SECONDS` 控制，默认 90 秒

### 8.5 大学分析

大学分析遵循相同架构模式：

- `UniversityAnalysisAgent` — AI 分析大学信息
- `UniversityAnalysisOrchestrator` — 并发编排
- `UniversityRankingService` — 评分与排名

---

## 9. 技能系统 (Skills)

技能是 AI Agent 调用的外部脚本工具，位于 `packages/server/skills/` 目录。

### 9.1 技能列表

| 技能目录                 | 说明                                         |
| ------------------------ | -------------------------------------------- |
| `wechat-article-search/` | 微信公众号文章搜索（基于 Node.js + Cheerio） |
| `web-search/`            | 通用网页搜索                                 |
| `general-web-search/`    | 通用网络搜索                                 |
| `sogou-search/`          | 搜狗搜索引擎                                 |
| `xiaohongshu-search/`    | 小红书搜索                                   |

### 9.2 技能结构

每个技能目录包含：

```
skill-name/
├── SKILL.md           # 技能说明文档
├── _meta.json         # 元数据（可选）
├── scripts/           # 执行脚本
│   ├── search_xxx.js  # Node.js 脚本
│   └── search_xxx.py  # Python 脚本（可选）
└── requirements.txt   # Python 依赖（可选）
```

### 9.3 LocalSkillScriptRunner

**职责**：运行技能脚本并返回结果。

- 支持 Node.js 和 Python 脚本
- 命令路径可配置（`NODE_COMMAND`、`PYTHON_COMMAND`）
- 执行超时可配置（`SKILL_TIMEOUT_SECONDS`）
- 技能根目录可配置（`SKILL_ROOT_PATH`）

---

## 10. 第三方集成

### 10.1 高德地图 (Amap)

`AmapService` 封装了以下高德 Web API：

| 接口       | 路径               | 用途               |
| ---------- | ------------------ | ------------------ |
| 地理编码   | `v3/geocode/geo`   | 地址 → 经纬度      |
| 逆地理编码 | `v3/geocode/regeo` | 经纬度 → 地址      |
| IP 定位    | `v3/ip`            | IP → 粗略位置      |
| 周边搜索   | `v3/place/around`  | 搜索附近的学校 POI |

**配置项**：`AMAP_API_KEY`、`SK`（安全密钥）

**安全签名**：所有请求均附带 HMAC 签名（`sig` 参数），防止 Key 被盗用。

**缓存**：使用 `IMemoryCache` 缓存高频查询结果。

**分页**：每页最多返回 25 条（`AMAP_PAGE_SIZE`）。

### 10.2 DashScope (阿里云百炼)

- **用途**：AI 模型推理（学校/大学分析）
- **接口**：OpenAI 兼容模式
- **默认模型**：`qwen3.6-plus`
- **支持 Responses API**：可通过 `DASHSCOPE_API_STYLE=responses` 启用

### 10.3 SMTP 邮件

- **用途**：发送注册验证码
- **配置**：`SMTP_HOST`、`SMTP_PORT`、`SMTP_USERNAME`、`SMTP_PASSWORD` 等

---

## 11. 认证系统

### 11.1 流程

```
注册流程：
1. 用户输入邮箱 → POST /api/auth/register/code → 发送 6 位验证码到邮箱
2. 用户输入验证码 + 密码 → POST /api/auth/register → 创建账户 → 返回 Token
3. 前端将 Token + 用户信息存储到 localStorage

登录流程：
1. 用户输入邮箱 + 密码 → POST /api/auth/login → 验证 → 返回 Token
2. 前端将 Token + 用户信息存储到 localStorage

认证请求：
- 前端在请求头中添加 Authorization: Bearer {token}
- 后端通过 AuthService.GetUserFromRequest 解析
```

### 11.2 安全措施

- 密码使用 ASP.NET Identity 的 `PasswordHasher`（PBKDF2）哈希存储
- 验证码 10 分钟过期
- 验证码发送间隔限制：60 秒
- Token 存储在前端 localStorage（`school-recommender-auth`）

---

## 12. 实时搜索流程 (SSE)

### 12.1 完整流程

```
前端 useSchoolSearch                后端 /api/schools/search
    │                                        │
    ├── POST 请求 ─────────────────────────→ │
    │                                        ├── 1. 验证经纬度 & 学校类型
    │                                        ├── 2. 高德逆地理编码
    │                                        ├── 3. 高德周边搜索（学校 POI）
    │                                        │
    │ ←── event: meta ────────────────────── ├── 4. 返回学校列表 + 位置信息
    │    (location, schools, totalFound)      │
    │                                        ├── 5. 并发启动 AI 分析
    │ ←── event: analysis ────────────────── ├──    逐个学校产出分析结果
    │    (schoolId, analysis, score)          │
    │    ... (重复 N 个学校)                   │
    │                                        │
    │ ←── event: done ────────────────────── ├── 6. 返回最终排名
    │    (rankings[])                         │
    │                                        ├── 7. 记录分析日志到数据库
```

### 12.2 SSE 事件格式

```
event: meta
data: {"location":{"address":"...","latitude":29.56,"longitude":106.55},"schools":[...],"totalFound":5}

event: analysis
data: {"schoolId":"B0FFH5D2GF","analysis":{...},"overallScore":85}

event: done
data: {"rankings":[...]}
```

---

## 13. 评分算法

### 13.1 综合评分

采用加权平均算法：

```
overallScore =
    teachingStaffScore × 0.25 +
    reputationScore    × 0.20 +
    awardsScore        × 0.15 +
    activityScore      × 0.15 +
    contentQualityScore× 0.10 +
    studentActivityScore× 0.10 +
    distanceScore      × 0.05
```

其中：

- `awardsScore = min(100, awardsCount × 5)`
- `distanceScore = max(0, (1 - distance / 5000) × 100)`

### 13.2 权重配置（默认）

| 维度     | 权重 | 说明                        |
| -------- | ---- | --------------------------- |
| 师资力量 | 25%  | `teachingStaffScore`        |
| 声誉口碑 | 20%  | `reputationScore`           |
| 获奖情况 | 15%  | `awardsCount × 5`，上限 100 |
| 活跃度   | 15%  | `activityScore`             |
| 内容质量 | 10%  | `contentQualityScore`       |
| 学生活动 | 10%  | `studentActivityScore`      |
| 距离     | 5%   | 5km 内线性衰减              |

### 13.3 录取匹配分（高中专用）

当搜索高中并提供学生分数时，额外计算录取匹配分：

1. 从 AI 分析结果中获取历年录取分数线
2. 以最近一年分数线为基准
3. 学生分数 ≥ 基准 → 100 分
4. 学生分数 ≤ 最早一年分数线 → 0 分
5. 中间线性插值

**最终评分（高中 + 有分数时）**：

```
finalScore = overallScore × 0.8 + admissionMatchScore × 0.2
```

---

## 14. 环境变量配置

所有配置通过 `.env` 文件或环境变量注入，由 `SchoolServiceOptions.FromConfiguration` 统一解析。

### 14.1 完整配置表

| 环境变量                                     | 默认值                                              | 说明                                |
| -------------------------------------------- | --------------------------------------------------- | ----------------------------------- |
| `PORT`                                       | `3001`                                              | 后端监听端口                        |
| `AMAP_API_KEY`                               | —                                                   | 高德地图 API Key                    |
| `SK`                                         | —                                                   | 高德地图安全密钥                    |
| `DASHSCOPE_API_KEY` / `OPENAI_API_KEY`       | —                                                   | AI 模型 API Key                     |
| `DASHSCOPE_BASE_URL` / `OPENAI_BASE_URL`     | `https://dashscope.aliyuncs.com/compatible-mode/v1` | AI 模型 Base URL                    |
| `DASHSCOPE_MODEL` / `OPENAI_CHAT_MODEL_NAME` | `qwen3.6-plus`                                      | AI 模型名称                         |
| `DASHSCOPE_API_STYLE` / `OPENAI_API_STYLE`   | —                                                   | 设为 `responses` 使用 Responses API |
| `NODE_COMMAND`                               | `node`                                              | Node.js 命令路径                    |
| `PYTHON_COMMAND`                             | `python`                                            | Python 命令路径                     |
| `SKILL_TIMEOUT_SECONDS`                      | `90`                                                | 技能脚本超时（秒），最小 15         |
| `SKILL_ROOT_PATH`                            | `skills` (相对于程序目录)                           | 技能目录路径                        |
| `DATABASE_PATH`                              | `data/school-recommender.db`                        | SQLite 数据库路径                   |
| `ANALYSIS_MAX_CONCURRENCY`                   | `8`                                                 | AI 分析并发数 (1-32)                |
| `AMAP_PAGE_SIZE`                             | `25`                                                | 高德搜索每页数量 (1-25)             |
| `SMTP_HOST`                                  | —                                                   | SMTP 服务器地址                     |
| `SMTP_PORT`                                  | `587`                                               | SMTP 端口                           |
| `SMTP_ENABLE_SSL`                            | `true`                                              | 启用 SMTP SSL                       |
| `SMTP_USERNAME`                              | —                                                   | SMTP 用户名                         |
| `SMTP_PASSWORD`                              | —                                                   | SMTP 密码                           |
| `SMTP_FROM_EMAIL`                            | 同 `SMTP_USERNAME`                                  | 发件人邮箱                          |
| `SMTP_FROM_NAME`                             | `学校推荐系统`                                      | 发件人名称                          |

### 14.2 `.env` 文件示例

```env
# 服务端口
PORT=3001

# 高德地图
AMAP_API_KEY=your_amap_api_key
SK=your_amap_security_key

# AI 模型（DashScope / OpenAI 兼容）
DASHSCOPE_API_KEY=your_dashscope_api_key
DASHSCOPE_BASE_URL=https://dashscope.aliyuncs.com/compatible-mode/v1
DASHSCOPE_MODEL=qwen3.6-plus

# SMTP 邮件
SMTP_HOST=smtp.example.com
SMTP_PORT=587
SMTP_USERNAME=noreply@example.com
SMTP_PASSWORD=your_smtp_password
```

---

## 15. 开发指南

### 15.1 快速开始

```bash
# 1. 克隆仓库
git clone <repo-url>
cd school

# 2. 安装依赖
pnpm install

# 3. 配置环境变量
cp packages/server/.env.example packages/server/.env
# 编辑 .env 填入 API Keys

# 4. 启动开发服务器
pnpm dev
```

### 15.2 常用命令

| 命令              | 说明                          |
| ----------------- | ----------------------------- |
| `pnpm dev`        | 同时启动前后端开发服务器      |
| `pnpm dev:client` | 仅启动前端（Vite，端口 6173） |
| `pnpm dev:server` | 仅启动后端（.NET，端口 3001） |
| `pnpm build`      | 构建前端和后端                |
| `dotnet build`    | 仅编译 .NET 项目              |

### 15.3 开发 URL

| 服务              | URL                                     |
| ----------------- | --------------------------------------- |
| 前端              | `http://localhost:6173`                 |
| 后端 API          | `http://localhost:3001`                 |
| API 文档 (Scalar) | `http://localhost:3001/scalar`          |
| OpenAPI JSON      | `http://localhost:3001/openapi/v1.json` |
| 健康检查          | `http://localhost:3001/api/health`      |

### 15.4 新增 API 端点

保持 Minimal API 风格：

```csharp
app.MapGet("/api/example/{id}", (int id) =>
    TypedResults.Ok(ApiResponse<object>.Ok(new { id })))
    .WithTags("Example")
    .WithName("GetExample")
    .WithSummary("示例接口");
```

### 15.5 新增前端组件

1. 在 `src/components/` 对应目录创建组件
2. 复用现有 hooks 和 UI 组件
3. 使用 `@` 路径别名导入
4. 遵循 Tailwind CSS 样式约定

### 15.6 API 调用约定

所有 API 响应遵循统一格式：

```typescript
interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
  fallbackToBrowser?: boolean; // IP 定位失败时提示前端改用浏览器定位
}
```

---

## 16. 测试

### 16.1 前端测试

- **框架**：Vitest + Testing Library + jsdom
- **配置**：`packages/client/vite.config.ts` 中的 `test` 字段
- **Setup 文件**：`src/test/setup.ts`

```bash
# 运行测试
cd packages/client
pnpm test

# 监听模式
pnpm test:watch
```

**测试文件位置**：`src/hooks/__tests__/`、`src/lib/__tests__/`

### 16.2 后端测试

- **框架**：xUnit（推测）
- **测试项目**：`packages/server/tests/SchoolRecommender.Api.Tests/`

```bash
cd packages/server
dotnet test
```

---

## 17. 部署指南

### 17.1 生产构建

```powershell
# Windows 执行构建脚本
.\deploy\build.ps1
```

构建流程：

1. 安装 pnpm 依赖
2. 构建前端 → `dist-deploy/client/`
3. 构建后端 → `dist-deploy/server/`
4. 安装后端生产依赖（npm packages for skills）
5. 复制部署配置文件

### 17.2 服务器部署（Ubuntu）

```bash
# 1. 上传 dist-deploy 目录到服务器
scp -r dist-deploy/ user@server:/opt/school-recommender/

# 2. SSH 登录服务器执行初始化
ssh user@server
chmod +x /opt/school-recommender/setup-server.sh
sudo /opt/school-recommender/setup-server.sh
```

### 17.3 Nginx 配置

`deploy/nginx.conf` 提供反向代理配置：

- 前端静态文件：从 `client/` 目录服务
- API 请求：代理到 `http://127.0.0.1:3001`
- SSE 支持：禁用缓冲 (`proxy_buffering off`)

### 17.4 systemd 服务

`deploy/school-recommender.service` 配置了 .NET 应用作为系统服务自动运行。

---

## 18. 项目约定与最佳实践

### 18.1 代码风格

- **前端**：TypeScript 严格模式，使用 ESLint
- **后端**：C# 12+ 特性（records、pattern matching）
- **样式**：Tailwind CSS 工具类优先，避免自定义 CSS

### 18.2 变更纪律

- API 契约变更时，同步更新前端类型和调用代码
- 新 API 端点保持 Minimal API 风格
- 新配置项通过 `SchoolServiceOptions.FromConfiguration` 接入
- 技能目录命名为复数形式 `skills/`

### 18.3 注意事项

- 后端启动时从工作目录加载 `.env`，不同环境可能表现不同
- `/api/schools/search` 是 SSE 端点，修改时保持流式语义
- AI 工具参数使用 `AIFunctionArguments` 形式，避免模型输出原始 JSON
- 高德地图 API 需要安全签名（`sig`），不可省略
- FreeSql 使用 CodeFirst 自动同步结构，生产环境谨慎使用

### 18.4 已知依赖

| 包              | 用途                          |
| --------------- | ----------------------------- |
| `cheerio` (npm) | 微信文章搜索脚本的 HTML 解析  |
| `node`          | 技能脚本运行时                |
| `python`        | Python 技能脚本运行时（可选） |

---

## 附录

### A. 组件清单

**Auth 组件**：`AuthDialog`

**Home 组件**：`HeroSection`、`FeaturesSection`、`ValueSection`、`SearchHistoryCard`

**Layout 组件**：`Header`、`Footer`

**Messages 组件**：`MessageBoard`

**Results 组件**：`SchoolRanking`、`UniversityRanking`、`BarChart`

**Search 组件**：`SchoolTypeDialog`、`ScoreInputDialog`、`LocationStep`、`SearchingView`、`UniversitySearchDialog`

**Shared 组件**：`ErrorBoundary`、`StepIndicator`、`ShareButton`

**UI 组件**：`Button` 等基础 UI 原子组件（基于 Radix UI + CVA）

### B. 数据流图

```
┌──────────┐     ┌──────────┐     ┌────────────────────┐
│  浏览器   │────→│  Vite    │────→│  .NET API (:3001)  │
│ (:6173)  │     │  Proxy   │     │                    │
└──────────┘     └──────────┘     │  ┌─── AmapService  │
                                  │  │    (高德地图)     │
                                  │  ├─── AI Agent      │
                                  │  │    (DashScope)   │
                                  │  ├─── Skills        │
                                  │  │    (Node/Python) │
                                  │  ├─── SQLite DB     │
                                  │  └─── SMTP          │
                                  └────────────────────┘
```

### C. 技术决策记录

1. **为什么选择 Minimal API 而非 Controller？** — 项目规模适中，Minimal API 更简洁
2. **为什么使用 SQLite？** — 轻量级，无需额外数据库服务，适合单服务器部署
3. **为什么 SSE 而非 WebSocket？** — 搜索场景是单向服务端推送，SSE 更简单高效
4. **为什么用 FreeSql 而非 EF Core？** — 更轻量，CodeFirst 自动建表更便捷
5. **为什么 AI 工具函数显式注册？** — 自动生成的工具不带参数 Schema，部分模型无法可靠传参
