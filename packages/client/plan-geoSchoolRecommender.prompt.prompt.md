# Plan: 基于地理位置的学校推荐系统

## TL;DR
构建一个全栈学校推荐系统应用，前端使用 Vite + React + TypeScript + shadcn/ui（Mac风格暖色调主题），后端使用 Node.js + Express，集成高德地图定位和阿里百炼AI平台进行学校数据搜索分析，使用 Recharts 做数据可视化，最终输出学校排名推荐。

## 技术栈
- **前端**: Vite + React 18 + TypeScript + Tailwind CSS + shadcn/ui
- **后端**: Node.js + Express + TypeScript
- **AI服务**: 阿里百炼平台 (DashScope API)
- **地图**: 高德地图 Web JS API + 高德POI搜索API
- **图表**: Recharts
- **包管理**: pnpm (monorepo)

## 项目结构
```
d:\Work\JimAiDotnet\school\
├── package.json                 # monorepo root
├── pnpm-workspace.yaml
├── packages/
│   ├── client/                  # React前端
│   │   ├── src/
│   │   │   ├── main.tsx
│   │   │   ├── App.tsx
│   │   │   ├── index.css         # Tailwind + Mac主题CSS变量
│   │   │   ├── components/
│   │   │   │   ├── ui/           # shadcn/ui组件
│   │   │   │   ├── layout/
│   │   │   │   │   ├── Header.tsx
│   │   │   │   │   └── Footer.tsx
│   │   │   │   ├── home/
│   │   │   │   │   ├── HeroSection.tsx       # 首页主视觉区
│   │   │   │   │   ├── FeaturesSection.tsx   # 功能介绍
│   │   │   │   │   ├── ValueSection.tsx      # 价值说明
│   │   │   │   │   └── StartButton.tsx       # 开始按钮
│   │   │   │   ├── search/
│   │   │   │   │   ├── SchoolTypeDialog.tsx  # 学校类型选择对话框
│   │   │   │   │   ├── LocationStep.tsx      # 定位步骤
│   │   │   │   │   └── SearchingView.tsx     # 搜索中动画
│   │   │   │   ├── results/
│   │   │   │   │   ├── SchoolCard.tsx        # 学校卡片
│   │   │   │   │   ├── SchoolRanking.tsx     # 排名列表
│   │   │   │   │   ├── SchoolDetail.tsx      # 学校详情
│   │   │   │   │   ├── RadarChart.tsx        # 雷达图(多维度)
│   │   │   │   │   ├── BarChart.tsx          # 柱状图对比
│   │   │   │   │   └── RecommendBanner.tsx   # 推荐建议横幅
│   │   │   │   └── shared/
│   │   │   │       ├── LoadingSpinner.tsx
│   │   │   │       └── StepIndicator.tsx     # 步骤指示器
│   │   │   ├── hooks/
│   │   │   │   ├── useGeolocation.ts         # 地理定位Hook
│   │   │   │   └── useSchoolSearch.ts        # 学校搜索Hook
│   │   │   ├── lib/
│   │   │   │   ├── utils.ts                  # shadcn工具
│   │   │   │   └── api.ts                    # API调用封装
│   │   │   ├── types/
│   │   │   │   └── school.ts                 # 类型定义
│   │   │   └── pages/
│   │   │       ├── HomePage.tsx
│   │   │       └── ResultsPage.tsx
│   │   ├── index.html
│   │   ├── vite.config.ts
│   │   ├── tailwind.config.ts
│   │   ├── tsconfig.json
│   │   └── components.json        # shadcn/ui配置
│   └── server/                    # Express后端
│       ├── src/
│       │   ├── index.ts            # 入口
│       │   ├── routes/
│       │   │   ├── schoolRoutes.ts # 学校搜索路由
│       │   │   └── locationRoutes.ts # 位置相关路由
│       │   ├── services/
│       │   │   ├── aiService.ts    # 百炼API调用
│       │   │   ├── amapService.ts  # 高德地图API
│       │   │   └── schoolAnalyzer.ts # 数据分析&排名算法
│       │   ├── types/
│       │   │   └── index.ts
│       │   └── config/
│       │       └── env.ts          # 环境变量配置
│       ├── package.json
│       └── tsconfig.json
```

## Steps

### Phase 1: 项目初始化与基础架构 (步骤 1-4)

**步骤1**: 初始化Monorepo
- 在 `d:\Work\JimAiDotnet\school\` 创建 `package.json` 和 `pnpm-workspace.yaml`
- 配置 packages/client 和 packages/server 两个工作区

**步骤2**: 初始化前端项目 (*parallel with 步骤3*)
- 使用 Vite 创建 React + TypeScript 项目到 `packages/client`
- 安装依赖: `tailwindcss`, `@tailwindcss/vite`, `react-router-dom`, `recharts`, `lucide-react`, `framer-motion`
- 配置 Tailwind CSS + Mac风格暖色调主题CSS变量
- 安装并配置 shadcn/ui（需要 `class-variance-authority`, `clsx`, `tailwind-merge`）

**步骤3**: 初始化后端项目 (*parallel with 步骤2*)
- 创建 `packages/server` Express + TypeScript 项目
- 安装依赖: `express`, `cors`, `dotenv`, `axios`, `typescript`, `tsx`
- 配置 TypeScript 和开发脚本
- 创建 `.env` 模板（高德Key、百炼API Key）

**步骤4**: 定义共享类型
- 创建 `School` 接口: 学校基本信息（名称、类型、地址、经纬度、距离）
- 创建 `SchoolAnalysis` 接口: 分析维度（官方账号数、发帖频率、用户评价分、文章质量分、师资评分、获奖数、学生活动分）
- 创建 `SchoolRanking` 接口: 排名、综合得分、推荐理由
- 创建 `SearchRequest` / `SearchResponse` 接口

### Phase 2: 前端页面开发 (步骤 5-9)

**步骤5**: 搭建布局和路由
- 创建 `Header` 组件：Mac风格毛玻璃效果导航栏，左侧Logo+标题，右侧简洁导航
- 创建 `Footer` 组件
- 配置 React Router：`/` 首页、`/results` 结果页
- 实现全局Mac圆角窗口容器样式

**步骤6**: 开发首页 (*depends on 步骤5*)
- `HeroSection`: 大标题 + 副标题 + 温馨渐变背景 + 装饰性插图/图标动画
- `FeaturesSection`: 三列卡片展示核心功能（智能定位/多维分析/精准推荐），带动画图标
- `ValueSection`: 为什么选择我们 - 数据驱动的学校建议
- `StartButton`: 居中醒目CTA按钮，hover渐变动画，点击触发学校类型选择

**步骤7**: 开发搜索流程交互 (*depends on 步骤6*)
- `SchoolTypeDialog`: shadcn Dialog组件，三个可选卡片（幼儿园🎒/小学📚/中学🎓），含图标和简要说明
- `LocationStep`: 选择学校类型后自动请求地理定位，展示定位动画和当前位置信息，失败时提供手动输入地址选项
- `SearchingView`: 搜索中状态 - 加载动画 + 搜索进度说明（"正在分析附近学校..."）
- `StepIndicator`: 顶部步骤条（选择类型 → 获取位置 → 搜索分析 → 查看结果）

**步骤8**: 开发结果展示页面 (*depends on 步骤7*)
- `SchoolRanking`: 排名列表页，顶部展示推荐学校横幅
- `SchoolCard`: 每个学校卡片 - 排名徽章、学校名、距离、综合评分、关键数据标签
- `RecommendBanner`: 顶部金色推荐卡 - "最适合您孩子的学校" + 推荐理由

**步骤9**: 开发数据可视化 (*depends on 步骤8*)
- `RadarChart`: 使用Recharts雷达图展示单个学校的多维度评分（师资/口碑/活跃度/获奖/活动/文章质量）
- `BarChart`: 多学校横向对比柱状图
- `SchoolDetail`: 点击学校卡片展开详情 - 完整分析数据 + 图表 + 信息来源列表

### Phase 3: 后端API开发 (步骤 10-13)

**步骤10**: 高德地图集成
- `amapService.ts`: 封装高德地图Web服务API
  - 逆地理编码：经纬度 → 地址描述
  - POI搜索：按类型(学校)和半径搜索
  - 返回学校名称、地址、距离等基础信息

**步骤11**: 百炼AI平台集成 (*parallel with 步骤10*)
- `aiService.ts`: 封装DashScope API调用
  - 构建学校信息搜索Prompt：根据学校名称和地区，要求AI搜索该学校在微信公众号、小红书、教育网站上的信息
  - 解析AI返回的结构化数据（JSON格式）
  - 包含: 官方账号发帖数、最近发帖日期、用户评价摘要、文章质量评分、师资信息、获奖记录、学生活动情况
  - 设置合理的超时和重试机制

**步骤12**: 数据分析与排名算法 (*depends on 步骤10, 11*)
- `schoolAnalyzer.ts`:
  - 对每个维度进行归一化评分（0-100）
  - 设置权重：师资(0.25) + 用户评价(0.20) + 获奖(0.15) + 活跃度(0.15) + 文章质量(0.10) + 学生活动(0.10) + 距离(0.05)
  - 计算加权综合分
  - 生成排名和推荐理由文本

**步骤13**: API路由 (*depends on 步骤12*)
- `POST /api/schools/search` - 接收位置和学校类型，返回排名结果
  - 参数验证：经纬度范围、学校类型枚举
  - 流程：高德POI搜索 → 百炼AI分析每所学校 → 综合排名 → 返回结果
- `GET /api/schools/:id` - 获取单个学校详情
- 添加错误处理和请求限流中间件

### Phase 4: 前后端联调 (步骤 14-15)

**步骤14**: API对接
- 在 `api.ts` 中实现真实API调用，替换Mock数据
- `useSchoolSearch` Hook: 管理搜索状态（idle → loading → success/error）
- 处理API错误和超时，展示友好提示

**步骤15**: 地理定位集成
- `useGeolocation` Hook: 封装浏览器 Geolocation API
- 处理用户拒绝定位权限的场景 → 显示手动输入框（接入高德地理编码做地址→坐标转换）
- 在高德地图组件中显示用户位置和附近学校标记

### Phase 5: 响应式与体验优化 (步骤 16-17)

**步骤16**: 响应式适配
- 移动端: 卡片单列、图表全宽、对话框全屏、底部固定CTA
- 桌面端: 双列/三列布局、侧边详情面板
- 关键断点: 640px(sm) / 768px(md) / 1024px(lg)

**步骤17**: 动画与微交互
- 页面过渡使用 framer-motion
- 卡片hover放大 + 阴影增强
- 搜索过程中的骨架屏加载
- 排名数字计数动画
- 步骤切换过渡效果

## Relevant Files

- `packages/client/src/index.css` — Mac风格暖色调主题变量定义（`--warm-50`到`--warm-900`、毛玻璃效果、圆角系统）
- `packages/client/tailwind.config.ts` — 扩展Tailwind主题，映射CSS变量为Tailwind类
- `packages/client/src/App.tsx` — 路由配置和全局状态管理
- `packages/client/src/components/home/HeroSection.tsx` — 首页主视觉，渐变背景 + CTA按钮
- `packages/client/src/components/search/SchoolTypeDialog.tsx` — shadcn Dialog + 三选一卡片
- `packages/client/src/components/results/SchoolRanking.tsx` — 核心排名列表组件
- `packages/client/src/components/results/RadarChart.tsx` — Recharts雷达图封装
- `packages/client/src/hooks/useGeolocation.ts` — 封装浏览器定位API + 高德逆地理编码
- `packages/client/src/hooks/useSchoolSearch.ts` — 管理搜索请求状态
- `packages/client/src/lib/api.ts` — Axios API调用封装
- `packages/client/src/types/school.ts` — 所有TypeScript类型定义
- `packages/server/src/services/aiService.ts` — 百炼DashScope API调用与Prompt构建
- `packages/server/src/services/amapService.ts` — 高德地图POI搜索和地理编码
- `packages/server/src/services/schoolAnalyzer.ts` — 多维度加权排名算法
- `packages/server/src/routes/schoolRoutes.ts` — Express学校搜索路由

## Verification

1. **项目启动**: 根目录执行 `pnpm install && pnpm dev`，前端(5173端口)和后端(3001端口)均正常启动
2. **首页渲染**: 访问 `http://localhost:5173`，确认首页Hero、功能介绍、开始按钮正常显示
3. **响应式测试**: 使用Chrome DevTools切换到iPhone/iPad视图，确认布局自适应
4. **搜索流程**: 点击"开始" → 弹出学校类型选择 → 选择后请求定位 → 搜索 → 展示结果
5. **定位拒绝**: 拒绝定位权限后，确认手动输入地址功能可用
6. **API联调**: 使用Postman测试 `POST /api/schools/search` 接口，确认返回正确JSON结构
7. **图表渲染**: 确认雷达图和柱状图正确展示，数据与排名匹配
8. **Mac风格**: 确认毛玻璃效果、圆角、暖色调、阴影等视觉效果到位
9. **TypeScript**: `tsc --noEmit` 无类型错误

## Decisions

- **Monorepo结构**: 使用pnpm workspace管理前后端，便于类型共享和统一开发
- **Mac风格实现**: 通过CSS变量定义暖色调主题 + backdrop-filter毛玻璃 + 大圆角 + 柔和阴影 + SF Pro风格字体
- **AI搜索策略**: 使用百炼平台的大模型API，通过精心设计的Prompt让AI搜索并返回结构化学校信息，而非直接爬取各平台
- **排名算法**: 加权多维度评分，权重可配置，默认侧重师资和口碑
- **地理定位**: 优先使用浏览器Geolocation API，失败时降级为手动输入地址 + 高德地理编码
- **排除范围**: 不实现用户登录/注册、历史记录保存、学校收藏功能（可后续迭代）

## Further Considerations

1. **百炼 API 调用延迟** — 搜索多所学校时可能较慢（每所学校一次 AI 调用）。建议：并行调用 + 流式返回结果，前端逐步展示已分析完的学校。
2. **高德地图在页面中是否需要可视化展示** — 当前方案仅用 POI 搜索 API，是否需要在结果页嵌入地图组件展示学校位置标记？推荐：加入地图展示，直观性更好。
3. **Mock 数据阶段** — 建议 Phase 2 开发前端时先用 Mock 数据跑通流程，Phase 4 再接真实后端，开发效率更高。
