---
name: sogou-search
description: 通用网页搜索技能（搜狗移动端 + Bing 国内版双通道）。参数为必填的搜索关键词，先用 Playwright 访问搜狗移动端 wap.sogou.com 抓取结果；当遇到反爬验证码、加载失败或结果为空时，自动 fallback 到 Bing 国内版 cn.bing.com 兜底；两路结果按 URL 去重合并，每条带 `engine` 字段标明来源。返回标题、摘要、来源站点与链接，结果以 JSON 格式输出。内置 cookie 持久化与 stealth 指纹隐藏。当用户提到"搜狗搜索"、"网页搜索"、"必应搜索"、"查一下某个关键词的网页"、"搜索某个学校/公司/人物的公开信息"时使用此技能。
---

# 通用网页搜索（搜狗移动端 + Bing 双通道）

## 适用场景

- 用户说"帮我搜一下某个关键词"
- 需要拿到通用网页搜索结果（非微信公众号文章）
- 学校/公司/人物的公开网页资料检索
- 单一搜索引擎被反爬时仍需稳定获取结果

## 双通道策略

| 阶段 | 引擎        | 端点                               | 说明                                                                       |
| ---- | ----------- | ---------------------------------- | -------------------------------------------------------------------------- |
| 主路 | 搜狗移动端  | `wap.sogou.com/web/searchList.jsp` | iPhone UA，反爬强度比 PC 端低                                              |
| 兜底 | Bing 国内版 | `cn.bing.com/search`               | 搜狗触发验证码 / 拿不到结果 / 数量不足时自动启用                           |
| 合并 | —           | —                                  | 两路结果按 URL+title 去重，每条带 `engine` 字段（`sogou-mobile` / `bing`） |

兜底触发条件（`engine=auto` 时）：

- 搜狗移动端返回状态 ≠ `ok`（captcha/timeout/error）
- 或搜狗有效结果数 < `min(num, 3)`

## 前置条件

```bash
cd {baseDir}
pip install -r requirements.txt
playwright install chromium
```

Linux/WSL 还需：

```bash
playwright install-deps chromium
```

## 工作流程

### 基础用法

```bash
cd {baseDir}
python scripts/search_sogou.py "关键词"
```

### 指定数量与输出文件

```bash
python scripts/search_sogou.py "关键词" -n 15 -o result.json
```

### 强制只用某一路

```bash
python scripts/search_sogou.py "关键词" --engine sogou   # 仅搜狗移动端
python scripts/search_sogou.py "关键词" --engine bing    # 仅 Bing
python scripts/search_sogou.py "关键词" --engine auto    # 默认：搜狗+Bing兜底
```

### 触发验证码时手动过一次

```bash
python scripts/search_sogou.py "关键词" --headless=false
```

弹出 Chromium 窗口，按页面提示完成搜狗的"点选文字"验证。验证完成后 cookie 会持久化到 `~/.sogou-search/userdata`，后续无头模式通常不会再触发。

### 解析搜狗 `/link?url=...` 为真实地址

```bash
python scripts/search_sogou.py "关键词" -n 5 -r
```

仅对搜狗结果生效，Bing 结果本身就是真实 URL。

## 参数说明

- `query`：搜索关键词（必填）
- `-n, --num`：返回数量（默认 10，最大 50）
- `-o, --output`：输出 JSON 文件路径（可选）
- `-r, --resolve-url`：把搜狗 `/link?url=...` 跳转链接解析为真实 URL
- `--headless`：是否无头模式，默认 `true`；遇验证码时设为 `false`
- `--user-data-dir`：chromium 持久化目录，默认 `~/.sogou-search/userdata`
- `--engine`：搜索引擎选择，`auto`（默认） / `sogou` / `bing`

## 输出结构

```json
{
  "query": "关键词",
  "engine_used": "sogou-mobile+bing",
  "sogou_status": "ok",
  "bing_status": "ok",
  "total": 10,
  "results": [
    {
      "title": "...",
      "url": "...",
      "summary": "...",
      "source": "...",
      "engine": "sogou-mobile"
    }
  ]
}
```

字段说明：

- `engine_used`：实际使用的引擎，可能是 `sogou-mobile` / `bing` / `sogou-mobile+bing` / `none`
- `sogou_status` / `bing_status`：每路引擎的状态，`ok` / `captcha` / `timeout` / `error` / `skipped`
- `results[].engine`：该条结果来自哪一路引擎
- `results[].url_resolved`：仅 `-r` 模式且为搜狗结果时出现

## 反爬保护设计

| 机制            | 说明                                                                                  |
| --------------- | ------------------------------------------------------------------------------------- |
| 移动端优先      | 先访问搜狗 wap 端，反爬强度低于 PC 端                                                 |
| Bing 自动兜底   | 搜狗失败时无缝切换，业务侧无需感知                                                    |
| 持久化 userdata | `launch_persistent_context` 复用 SUV/SNUID 等 cookie                                  |
| 指纹隐藏        | 注入 init script 隐藏 `navigator.webdriver`，补齐 `plugins`/`languages`/`chrome`      |
| 真实浏览器渲染  | chromium 真渲染，规避 header / TLS 指纹检测                                           |
| 验证码自动检测  | URL 含 `antispider`/`captcha`/`seccode`，或页面文本含"验证码""按顺序依次点击"等关键词 |
| 半自动过验证    | 有头模式下等待用户手动完成；headless 模式直接走 Bing 兜底                             |
| 节流            | 翻页之间随机 1-2.5s 间隔，模拟人类节奏                                                |

## 常见问题

- **两路都拿不到结果**：稍等几分钟换关键词重试，或 `--headless=false` 观察实际页面
- **反复触发搜狗验证码**：清空 `~/.sogou-search/userdata` 后用 `--headless=false` 重新过一次
- **只想用 Bing**：`--engine bing` 跳过搜狗（适合搜狗长期被风控的场景）
- **解析真实 URL 失败**：搜狗 `/link` 跳转有限频，仅在最终需要时才用 `-r`

## 注意事项

- 本工具仅用于学习与研究，请勿用于商业用途或大规模爬取
- 使用时请遵守搜狗与 Bing 的使用条款
- 过度使用可能导致 IP 被封禁，请控制请求频率（脚本内置随机延迟，不要在外层并发跑多个实例）

---

name: sogou-search
description: 搜狗网页搜索技能。参数为必填的搜索关键词，通过 Playwright 浏览器自动化访问 https://www.sogou.com/web 获取通用网页搜索结果，覆盖资讯、百科、博客、论坛、企业站点等中文网页内容；返回标题、概要、来源站点与链接，结果以 JSON 格式输出。内置反爬验证码检测与 cookie 持久化，触发"按顺序点选文字"验证时支持手动通过一次后长期免验证。当用户提到"搜狗搜索"、"网页搜索"、"查一下某个关键词的网页"、"搜索某个学校/公司/人物的公开信息"时使用此技能。

---

# 搜狗网页搜索（Playwright 版）

## 适用场景

- 用户说"帮我用搜狗搜一下某个关键词"
- 用户需要拿到通用网页搜索结果（非微信公众号文章）
- 需要快速获取：标题、摘要、来源域名、可访问链接
- 学校/公司/人物的公开网页资料检索

## 前置条件

在 `{baseDir}` 目录下安装依赖（仅首次）：

```bash
cd {baseDir}
pip install -r requirements.txt
playwright install chromium
```

Linux/WSL 环境还需：

```bash
playwright install-deps chromium
```

## 工作流程

### 步骤 1: 确认搜索关键词与数量

### 步骤 2: 执行搜索命令

```bash
cd {baseDir}
python scripts/search_sogou.py "关键词"
```

### 步骤 3: 触发验证码时的处理

如果终端日志出现 `触发搜狗反爬验证（点选文字验证码）`，按提示在有头模式下手动过一次验证：

```bash
python scripts/search_sogou.py "关键词" --headless=false
```

会弹出 Chromium 窗口，按页面提示**依次点击图中文字**完成验证。验证完成后脚本会自动恢复执行，并把 cookie 持久化到 `~/.sogou-search/userdata`。**之后再用默认无头模式跑通常不会再触发验证**。

## 特殊流程（可选）

1. 限制返回数量

   ```bash
   python scripts/search_sogou.py "关键词" -n 15
   ```

2. 保存结果到 JSON 文件

   ```bash
   python scripts/search_sogou.py "关键词" -n 20 -o result.json
   ```

3. 把搜狗 `/link?url=...` 跳转解析为真实目标 URL

   ```bash
   python scripts/search_sogou.py "关键词" -n 5 -r
   ```

4. 自定义持久化目录（多账号或并发场景）

   ```bash
   python scripts/search_sogou.py "关键词" --user-data-dir ./.sogou-userdata
   ```

## 参数说明

- `query`：搜索关键词（必填）
- `-n, --num`：返回数量（默认 10，最大 50）
- `-o, --output`：输出 JSON 文件路径（可选）
- `-r, --resolve-url`：把搜狗 `/link?url=...` 跳转链接解析为真实 URL（会额外导航每条）
- `--headless`：是否无头模式，默认 `true`；触发验证码时建议 `--headless=false`
- `--user-data-dir`：chromium 持久化目录，默认 `~/.sogou-search/userdata`

## 输出字段（结果对象）

- `title`：网页标题
- `url`：网页链接（默认是搜狗 `/link?url=...` 跳转，加 `-r` 后尝试解析为真实地址）
- `summary`：网页摘要片段
- `source`：来源站点名称或域名
- `url_resolved`：仅在 `-r` 模式下出现，表示是否成功解析出真实地址

完整输出结构：

```json
{
  "query": "关键词",
  "total": 10,
  "results": [
    {
      "title": "...",
      "url": "...",
      "summary": "...",
      "source": "..."
    }
  ]
}
```

## 反爬保护设计

| 机制            | 说明                                                                              |
| --------------- | --------------------------------------------------------------------------------- |
| 持久化 userdata | 使用 `launch_persistent_context`，SUV/SNUID 等 cookie 跨次复用                    |
| 指纹隐藏        | 注入 init script 隐藏 `navigator.webdriver`，补齐 `plugins`/`languages`/`chrome`  |
| 真实浏览器渲染  | 走 chromium 而非 https + cheerio，规避 header / TLS 指纹检测                      |
| 验证码自动检测  | URL 含 `antispider`/`captcha`，或页面文本含"验证码""按顺序依次点击"等关键词时触发 |
| 半自动过验证    | 有头模式下等待用户手动点选完成；headless 模式直接抛错并提示切换                   |
| 节流            | 翻页之间随机 1.5-3s 间隔，模拟人类节奏                                            |

## 常见问题处理

- **结果为空**：稍等几分钟、更换关键词，或加 `--headless=false` 查看是否被风控
- **反复触发验证码**：清空 `~/.sogou-search/userdata` 后用 `--headless=false` 重新过一次验证
- **解析真实 URL 失败**：搜狗对 `/link` 跳转有限频，仅在最终需要时才用 `-r`，或让用户在浏览器中手动打开 `url`

## 注意事项

- 本工具仅用于学习和研究目的，请勿用于商业用途或大规模爬取
- 使用本工具时请遵守搜狗网站的使用条款
- 过度使用可能导致 IP 被封禁，请控制请求频率（脚本内置随机延迟，不要在外层并发跑多个实例）
