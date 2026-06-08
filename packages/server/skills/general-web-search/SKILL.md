---
name: general-web-search
description: 通用联网搜索技能（轻量 HTTP 版）。参数为必填的搜索关键词，先用搜狗 PC 端 https://www.sogou.com/web 抓取通用网页搜索结果；当遇到反爬验证码、网络异常或结果为空时，自动 fallback 到 Bing 国内版 https://cn.bing.com/search 兜底获取数据。返回标题、概要、来源站点与链接，结果以 JSON 格式输出。基于 Node.js + cheerio，无需浏览器依赖。当用户提到"联网搜索"、"网页搜索"、"查一下某个关键词"、"搜索某条新闻/学校/公司/人物的公开信息"时使用此技能。
---

# 通用联网搜索（搜狗 + Bing 双通道，HTTP 版）

## 适用场景

- 用户说"帮我搜一下某个关键词的网页/新闻"
- 需要拿到通用网页搜索结果（非微信公众号文章）
- 学校/公司/人物/事件的公开网页资料检索
- 单一搜索引擎被反爬时仍需稳定获取结果
- 不希望引入 Playwright/Chromium 等重型依赖的环境

## 双通道策略

| 阶段 | 引擎        | 端点                 | 说明                                                              |
| ---- | ----------- | -------------------- | ----------------------------------------------------------------- |
| 主路 | 搜狗 PC 端  | `www.sogou.com/web`  | UA 池随机轮换，附带 Sogou cookie，反爬触发时立即降级              |
| 兜底 | Bing 国内版 | `cn.bing.com/search` | 搜狗触发验证码 / 拿不到结果 / 数量不足时自动启用                  |
| 合并 | —           | —                    | 两路结果按 URL 去重合并，每条带 `engine` 字段（`sogou` / `bing`） |

兜底触发条件（`engine=auto` 时）：

- 搜狗返回 antispider/captcha 页（HTML 命中"验证码""请输入下方校验码""为了您的网络安全"等关键词）
- 搜狗 HTTP 状态非 2xx
- 搜狗有效结果数 < `min(num, 3)`

## 工作流程

### 步骤 1: 确认依赖包

该脚本依赖 NodeJS 包 `cheerio`。skill 目录已经携带了 `package.json`，**首次使用前在 skill 目录下执行一次安装即可**：

```bash
cd {baseDir}
npm install
```

也可以在系统中全局安装（任选其一）：

```bash
npm install -g cheerio
```

### 步骤 2: 确认搜索关键词与数量

### 步骤 3: 执行搜索命令

```bash
cd {baseDir}
node scripts/search_web.js "关键词"
```

## 特殊流程（可选）

1. 限制返回数量（默认 10，最大 30）

```bash
node scripts/search_web.js "关键词" -n 15
```

2. 保存结果到文件

```bash
node scripts/search_web.js "关键词" -n 20 -o result.json
```

3. 强制只用某一路引擎

```bash
node scripts/search_web.js "关键词" --engine sogou   # 仅搜狗
node scripts/search_web.js "关键词" --engine bing    # 仅 Bing
node scripts/search_web.js "关键词" --engine auto    # 默认：搜狗+Bing 兜底
```

4. 解析搜狗 `/link?url=...` 跳转链接为真实地址（仅搜狗结果生效）

```bash
node scripts/search_web.js "关键词" -n 5 -r
```

## 参数说明

- `query`：搜索关键词（必填）
- `-n, --num`：返回数量（默认 10，最大 30）
- `-o, --output`：输出 JSON 文件路径（可选）
- `-r, --resolve-url`：把搜狗 `/link?url=...` 跳转链接解析为真实 URL（每条结果会额外发起请求）
- `--engine`：搜索引擎选择，`auto`（默认） / `sogou` / `bing`

## 输出结构

```json
{
  "query": "关键词",
  "engine_used": "sogou+bing",
  "sogou_status": "ok",
  "bing_status": "skipped",
  "total": 10,
  "results": [
    {
      "title": "...",
      "url": "...",
      "summary": "...",
      "source": "...",
      "engine": "sogou"
    }
  ]
}
```

字段说明：

- `engine_used`：实际使用的引擎，可能值 `sogou` / `bing` / `sogou+bing` / `none`
- `sogou_status` / `bing_status`：每路引擎的状态，`ok` / `captcha` / `empty` / `error` / `skipped`
- `results[].engine`：该条结果来自哪一路引擎
- `results[].url_resolved`：仅 `-r` 模式且为搜狗结果时出现

## 反爬保护设计

| 机制              | 说明                                                     |
| ----------------- | -------------------------------------------------------- |
| UA 池轮换         | 内置 20 个真实浏览器 UA，每次请求随机挑选                |
| Sogou cookie 预热 | 先访问 sogou 首页拿到 SUV/SUID 等 cookie，再带到搜索请求 |
| 自动降级 Bing     | 检测到 antispider/验证码关键词或空结果立即切到 Bing      |
| 翻页节流          | 翻页之间随机 500-1500ms 间隔，模拟人类节奏               |
| URL 去重          | 主路 + 兜底合并时按结果 URL 去重，保留主路优先           |

## 常见问题处理

- **两路都拿不到结果**：稍后重试，或换更精准的关键词
- **搜狗持续返回验证码**：用 `--engine bing` 跳过搜狗
- **解析真实 URL 失败**：搜狗 `/link` 跳转有限频，仅在最终需要时才用 `-r`
- **结果偏少**：增大 `-n` 数值，脚本会自动翻页（每页 10 条）

## 注意事项

- 本工具仅用于学习和研究目的，请勿用于商业用途或大规模爬取
- 使用本工具时请遵守搜狗与 Bing 的使用条款
- 过度使用可能导致 IP 被封禁，请控制请求频率（脚本内置随机延迟，不要在外层并发跑多个实例）
