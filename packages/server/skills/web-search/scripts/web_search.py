#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
free-web-search v7
意图识别 + query改写 + 请求节流 + 结果质量评分 + 单域名排除重试 + 保留CSS"""

import sys
import json
import time
import re
import argparse
import subprocess
from urllib.parse import urlencode, quote, urlparse
from datetime import datetime

# ==================== 强制UTF-8 ====================
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')

# ==================== 配置 ====================
DEFAULT_MAX  = 10
DEFAULT_FULL = 0
TIMEOUT      = 30000
FETCH_TIMEOUT= 15000
DDG_TIMEOUT  = 10000
MAX_RETRIES  = 3
DDG_RETRIES  = 1
WAIT_TIME    = 2000

QUALITY_THRESHOLD = 0.45

MIN_REQUEST_INTERVAL = 3.0
_last_request_time = 0.0

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
    "AppleWebKit/537.36 (KHTML, like Gecko) "
    "Chrome/125.0.0.0 Safari/537.36"
)

BLOCK_DOMAINS = [
    # 知乎搜索结果可以抓全文（可能遇到反爬，但值得试）
]

LOW_QUALITY_DOMAINS = [
    "jingyan.baidu.com",
    "zhidao.baidu.com",
    "tieba.baidu.com",
    "baike.baidu.com",
    "wenku.baidu.com",
    "bbs.16fan.com",
    "zhihu.com",
    "zhuanlan.zhihu.com",
]

# ==================== 学校领域配置（强制学校相关） ====================
# 学校相关关键词：标题+摘要必须命中至少一个，否则视为离题丢弃
SCHOOL_KEYWORDS = [
    # 学校类型
    "学校", "小学", "中学", "初中", "高中", "完中", "九年一贯", "十二年一贯",
    "幼儿园", "幼教", "托幼", "托儿所",
    "大学", "学院", "高校", "高职", "中职", "职校", "技校", "中专",
    "国际学校", "外国语学校", "实验学校", "附属学校", "附小", "附中",
    "民办学校", "公办学校", "私立学校", "公立学校", "双语学校",
    # 教育/招生/学区
    "教育", "教育局", "教育部", "教委",
    "招生", "入学", "报名", "学位", "学籍", "学区", "划片", "对口",
    "升学", "中考", "高考", "小升初", "幼升小",
    "学费", "择校", "录取", "考点", "考试",
    # 学校相关属性
    "校长", "教师", "师资", "校园", "校区", "教学楼",
    "课程", "教材", "教学", "教研", "课改",
    "重点学校", "示范学校", "名校", "学区房",
    # 排行/榜单
    "学校排名", "排行榜", "榜单",
    # 英文
    "school", "university", "college", "campus", "education",
    "kindergarten", "primary", "secondary", "academy",
]

# 学校权威域名：命中加分，可信度高
SCHOOL_AUTHORITY_DOMAINS = [
    ".edu.cn", ".edu.",            # 高校与教育机构域名
    "moe.gov.cn",                   # 教育部
    "sz.gov.cn/szjyj",              # 深圳市教育局
    "edu.sz.gov.cn",
    "gd.gov.cn",                    # 广东省教育厅相关
    "eea.gd.gov.cn",                # 广东省教育考试院
    "szeb.sz.gov.cn",               # 深圳教育
    "jyj.",                         # XX 教育局通用前缀
    "edu.cn",
]

# 学校搜索常见离题/干扰域名：广告/中介/租房/学车/学习类不相干站点
SCHOOL_OFFTOPIC_DOMAINS = [
    "anjuke.com",       # 学区房中介
    "lianjia.com",
    "ke.com",
    "fang.com",
    "58.com",
    "ganji.com",
    "soufun.com",
    "haoche.cn",
    "xueche.com",       # 学车
    "jiakaobaodian.com",
    "yuanfudao.com",    # 在线教育广告
    "zybang.com",
    "zuoyebang.com",
    "xueqiu.com",       # 雪球（股票"学"字误命中）
    "tmall.com",
    "taobao.com",
    "jd.com",
]

AUTHORITY_HINTS = SCHOOL_AUTHORITY_DOMAINS + [
    ".gov.cn", ".gov.",
]

FUZZY_TIME_WORDS = re.compile(r'(今日|今天|最新|最近|当前|目前|当下|现在)')

# 相关性匹配阈值：结果中必须命中至少这么多比例的query token才保留
RELEVANCE_MIN_HIT_RATIO = 0.34
# 相关性得分低于此值的结果会被丢弃（除非过滤后空集）
RELEVANCE_DROP_THRESHOLD = 0.32

# 中文停用词 / 噪声词（不参与相关性命中计算）
STOPWORDS = set([
    "的", "了", "和", "与", "及", "或", "是", "在", "我", "你", "他", "她", "它",
    "什么", "怎么", "怎样", "如何", "为什么", "哪里", "哪个", "哪些", "多少",
    "请问", "请", "帮", "查", "查询", "查一下", "搜索", "搜", "找", "找一下",
    "一下", "一个", "一些", "这个", "那个", "这些", "那些",
    "介绍", "推荐", "评价", "对比", "方法", "教程",
    "the", "a", "an", "of", "to", "in", "on", "and", "or", "is", "are",
])

# ==================== 意图识别 + query 改写 ====================
CITIES = "深圳|广州|北京|上海|杭州|成都|武汉|南京|重庆|西安|长沙|苏州|厦门|青岛|大连|天津|昆明|珠海|东莞|佛山|惠州|中山"

# 意图规则: (匹配正则, 改写函数, 描述)
# 改写函数接收 match 对象和原始 query，返回改写后的完整 query
# 原则：只精简/替换，不加词！Bing CN对简洁query效果最好
INTENT_RULES = [
    # 城市+好玩/去哪 → 精简为"城市 景点"
    (re.compile(rf'({CITIES})\s*(有什么好玩的|哪里好玩|好玩的地方|去哪玩|周末.*去哪|好去处|逛|玩什么)'),
     lambda m, q: f'{m.group(1)} 景点', '城市游玩→景点'),

    # 城市+活动 → 精简
    (re.compile(rf'({CITIES})\s*(活动|展览|演出|市集|音乐会|演唱会)'),
     lambda m, q: f'{m.group(1)} {m.group(2)}', '城市活动→精简'),

    # "xxx是什么" → "xxx 介绍"
    (re.compile(r'(.+?)是什么(?:意思)?$', re.IGNORECASE),
     lambda m, q: f'{m.group(1)} 介绍', '是什么→介绍'),

    # "xxx怎么样" → "xxx 评价"
    (re.compile(r'(.+?)怎么样$', re.IGNORECASE),
     lambda m, q: f'{m.group(1)} 评价', '怎么样→评价'),

    # "怎么xxx" → "xxx 方法"
    (re.compile(r'^怎么(.+)', re.IGNORECASE),
     lambda m, q: f'{m.group(1)} 方法', '怎么→方法'),

    # "xxx和yyy哪个好" → "xxx yyy 对比"
    (re.compile(r'(.+?)和(.+?)(哪个好|哪个更好|选哪个)'),
     lambda m, q: f'{m.group(1)} {m.group(2)} 对比', '哪个好→对比'),
]


def rewrite_query(query: str) -> tuple:
    """意图识别 + query改写。只应用第一个匹配的规则。返回 (改写后query, 意图描述或None)"""
    for pattern, rewrite_fn, desc in INTENT_RULES:
        m = pattern.search(query)
        if m:
            rewritten = rewrite_fn(m, query)
            rewritten = re.sub(r'\s+', ' ', rewritten).strip()
            return rewritten, desc
    return query, None


def ensure_school_context(query: str) -> str:
    """若 query 中完全不含学校相关关键词，自动追加"学校"以约束搜索范围。"""
    q_lower = query.lower()
    for kw in SCHOOL_KEYWORDS:
        if kw.lower() in q_lower:
            return query
    return f"{query} 学校".strip()


# 反检测初始化脚本
STEALTH_JS = """
Object.defineProperty(navigator, 'webdriver', {get: () => undefined});
Object.defineProperty(navigator, 'plugins', {
    get: () => [
        {name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer'},
        {name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpafafjmlifpcpbgpcj'},
        {name: 'Native Client Executable', filename: 'internal-nacl-plugin'}
    ]
});
Object.defineProperty(navigator, 'languages', {get: () => ['zh-CN', 'zh', 'en']});
Object.defineProperty(navigator, 'platform', {get: () => 'Win32'});
Object.defineProperty(navigator, 'hardwareConcurrency', {get: () => 8});
Object.defineProperty(navigator, 'deviceMemory', {get: () => 8});
window.chrome = {runtime: {}};
const originalQuery = window.navigator.permissions.query;
window.navigator.permissions.query = (parameters) => (
    parameters.name === 'notifications' ?
        Promise.resolve({ state: Notification.permission }) :
        originalQuery(parameters)
);
const originalToDataURL = HTMLCanvasElement.prototype.toDataURL;
HTMLCanvasElement.prototype.toDataURL = function(type) {
    if (type === 'image/png' && this.width === 220 && this.height === 30) {
        return 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAANwAAAAeCAYAAABwJ3rwAAAABGdBTUEAALGPC/xhBQAAACBjSFJNAAB6JgAAgIQAAPoAAACA6AAAdTAAAOpgAAA6mAAAF3CculE8AAAABmJLR0QA/wD/AP+gvaeTAAAABmJLR0QA/wD/AP+gvaeTAAAABmJLR0QA/wD/AP+gvaeT';
    }
    return originalToDataURL.apply(this, arguments);
};
const getParameter = WebGLRenderingContext.prototype.getParameter;
WebGLRenderingContext.prototype.getParameter = function(parameter) {
    if (parameter === 37445) return 'Intel Inc.';
    if (parameter === 37446) return 'Intel Iris OpenGL Engine';
    return getParameter.apply(this, arguments);
};
"""

BROWSER_ARGS = [
    '--no-sandbox', '--disable-gpu', '--disable-dev-shm-usage',
    '--disable-blink-features=AutomationControlled', '--disable-infobars',
    '--disable-extensions', '--disable-background-networking', '--disable-sync',
    '--metrics-recording-only', '--disable-default-apps', '--no-first-run',
    '--disable-component-extensions-with-background-pages',
    '--disable-features=IsolateOrigins,site-per-process',
    '--disable-site-isolation-trials', '--disable-web-security',
    '--allow-running-insecure-content',
]

ROUTE_PATTERN = "**/*.{png,jpg,jpeg,gif,svg,woff,woff2,ttf,mp4,ico,webp,js.map}"

_browser = None
_playwright = None
_ddg_browser = None
_ddg_uses_chrome = False


def ensure_playwright():
    try:
        import playwright; return
    except ImportError: pass
    print("[INFO] 安装 playwright...", file=sys.stderr)
    for cmd in [
        [sys.executable, "-m", "pip", "install", "-q", "playwright", "--break-system-packages"],
        [sys.executable, "-m", "pip", "install", "-q", "playwright"],
    ]:
        if subprocess.run(cmd, capture_output=True, text=True).returncode == 0:
            break
    subprocess.run([sys.executable, "-m", "playwright", "install", "chromium"],
                   capture_output=True, text=True)
    import os; os.execv(sys.executable, [sys.executable] + sys.argv)


def parse_args():
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("query", nargs="*")
    parser.add_argument("--max", type=int, default=DEFAULT_MAX, dest="max_results")
    parser.add_argument("--full", type=int, default=DEFAULT_FULL)
    parser.add_argument("--engine", type=str, default="bing", choices=["bing", "duckduckgo", "auto"])
    # 默认关闭低质量域名过滤：与 SKILL.md 文档及上层调用方（C# SearchSchoolInfoAsync）意图一致，
    # 上层会做自己的相关性过滤；这里再硬过滤一遍容易出现空数组。
    parser.add_argument("--filter", action="store_true", default=False, help="过滤低质量域名（默认关闭）")
    parser.add_argument("--no-filter", dest="filter", action="store_false", help="关闭低质量域名过滤")
    parser.add_argument("--no-rewrite", action="store_true", help="禁用query改写")
    args = parser.parse_args()
    query = " ".join(args.query).strip()
    args.max_results = max(1, min(20, args.max_results))
    args.full = max(0, min(5, args.full))
    return query, args.max_results, args.full, args.engine, args.filter, args.no_rewrite


def build_bing_url(query, count):
    return "https://cn.bing.com/search?" + urlencode({
        "q": query, "mkt": "zh-CN", "setlang": "zh-CN", "cc": "CN", "count": str(count + 2)
    })

def build_duckduckgo_url(query):
    return "https://duckduckgo.com/?q=" + quote(query) + "&ia=web"


def init_browser():
    global _browser, _playwright
    if _browser: return
    from playwright.sync_api import sync_playwright
    print("[DEBUG] 启动 Chromium...", file=sys.stderr)
    _playwright = sync_playwright().start()
    _browser = _playwright.chromium.launch(headless=True, args=BROWSER_ARGS)
    print("[DEBUG] Chromium 已就绪", file=sys.stderr)

def close_browser():
    try:
        if _browser: _browser.close()
    except: pass
    try:
        if _ddg_browser: _ddg_browser.close()
    except: pass
    try:
        if _playwright: _playwright.stop()
    except: pass


def init_ddg_browser():
    """DDG 对 bundled Chromium 强反爬（直接 418），改用本机 Chrome 通道。
    若本机无 Chrome，则降级用 chromium 并返回 False（调用方可决定是否放弃）。"""
    global _ddg_browser, _ddg_uses_chrome, _playwright
    if _ddg_browser:
        return _ddg_uses_chrome
    from playwright.sync_api import sync_playwright
    if _playwright is None:
        _playwright = sync_playwright().start()
    try:
        _ddg_browser = _playwright.chromium.launch(
            headless=True, channel="chrome", args=BROWSER_ARGS,
        )
        _ddg_uses_chrome = True
        print("[DEBUG] DDG 浏览器: 本机 Chrome 通道", file=sys.stderr)
    except Exception as e:
        print(f"[WARN] 无法使用本机 Chrome 通道（{e}），降级到 bundled chromium，DDG 大概率被识别为机器人", file=sys.stderr)
        _ddg_browser = _playwright.chromium.launch(headless=True, args=BROWSER_ARGS)
        _ddg_uses_chrome = False
    return _ddg_uses_chrome


def create_ddg_context():
    ctx = _ddg_browser.new_context(
        locale="zh-CN", user_agent=UA,
        viewport={"width": 1920, "height": 1080}, screen={"width": 1920, "height": 1080},
        device_scale_factor=1, timezone_id="Asia/Shanghai",
        has_touch=False, is_mobile=False, java_script_enabled=True,
    )
    ctx.add_init_script(STEALTH_JS)
    return ctx

def create_context():
    ctx = _browser.new_context(
        locale="zh-CN", user_agent=UA,
        viewport={"width":1920,"height":1080}, screen={"width":1920,"height":1080},
        device_scale_factor=1, timezone_id="Asia/Shanghai",
        has_touch=False, is_mobile=False, java_script_enabled=True,
    )
    ctx.add_init_script(STEALTH_JS)
    return ctx

def throttle():
    global _last_request_time
    gap = MIN_REQUEST_INTERVAL - (time.time() - _last_request_time)
    if gap > 0:
        time.sleep(gap)
    _last_request_time = time.time()

def is_blocked_domain(url): return any(d in url for d in BLOCK_DOMAINS)
def is_low_quality_domain(url): return any(d in url for d in LOW_QUALITY_DOMAINS)
def is_offtopic_domain(url): return any(d in url for d in SCHOOL_OFFTOPIC_DOMAINS)
def is_school_authority(url): return any(d in url for d in SCHOOL_AUTHORITY_DOMAINS)


# 强学校信号词：明确指向学校实体（用于校名命中后判定结果可信）
# 不包含 "教育"/"教委"/"教学"/"教研"/"课程"/"教材" 等弱词——这些常出现在网站导航菜单里造成误命中
STRONG_SCHOOL_KEYWORDS = [
    "学校", "小学", "中学", "初中", "高中", "完中",
    "九年一贯", "十二年一贯",
    "幼儿园", "幼教", "托幼", "托儿所",
    "大学", "学院", "高校", "高职", "中职", "职校", "技校", "中专",
    "国际学校", "外国语学校", "实验学校", "附属学校", "附小", "附中",
    "民办学校", "公办学校", "私立学校", "公立学校", "双语学校",
    "招生", "入学", "学位", "学籍", "学区", "划片",
    "小升初", "幼升小", "中考", "高考", "升学", "录取",
    "校长", "师资", "校园", "校区",
    "教育局", "教育部", "教育厅", "教育考试院",
    "示范学校", "重点学校", "名校", "学区房",
    "school", "university", "college", "campus",
    "kindergarten", "primary school", "secondary school", "academy",
]


def _haystack(result):
    return ((result.get("title") or "") + " " + (result.get("snippet") or "")).lower()


def is_school_related(result):
    """硬过滤：标题+摘要必须出现至少一个【强】学校信号词。
    弱词如 "教育" 不再单独作为通过条件——它们在大型门户网站的导航栏里高频出现，会泄漏无关结果。
    域名是 .edu.cn / 教育局官网时直接通过。"""
    url = (result.get("url") or "").lower()
    if is_school_authority(url):
        return True
    haystack = _haystack(result)
    if not haystack.strip():
        return False
    for kw in STRONG_SCHOOL_KEYWORDS:
        if kw.lower() in haystack:
            return True
    return False


def filter_school_only(results):
    """只保留学校相关结果。空集直接返回空，绝不回退到无关结果。
    这是【硬约束】：本 skill 只搜学校信息，离题就让上游重试或兜底。"""
    if not results:
        return results
    filtered = []
    dropped_offtopic = 0
    dropped_unrelated = 0
    for r in results:
        if is_offtopic_domain(r.get("url", "")):
            dropped_offtopic += 1
            continue
        if not is_school_related(r):
            dropped_unrelated += 1
            continue
        filtered.append(r)
    if dropped_offtopic or dropped_unrelated:
        print(f"[INFO] 学校域过滤：丢弃离题域名 {dropped_offtopic} 条 / 非学校内容 {dropped_unrelated} 条", file=sys.stderr)
    if not filtered:
        print("[WARN] 学校域过滤后为空（所有结果均与学校无关），返回空数组", file=sys.stderr)
        return []
    return filtered

def score_result(r):
    s = 0.5
    url, snippet = r.get("url",""), r.get("snippet","")
    if is_low_quality_domain(url): s -= 0.3
    if is_offtopic_domain(url): s -= 0.4
    if is_school_authority(url): s += 0.35
    if re.search(r'\d{2,}', snippet): s += 0.15
    if len(snippet) < 20: s -= 0.1
    for h in AUTHORITY_HINTS:
        if h in url: s += 0.2; break
    return max(0.0, min(1.0, s))

def score_results(results):
    return sum(score_result(r) for r in results) / len(results) if results else 0.0


# ==================== 相关性匹配 ====================
def tokenize_query(query: str):
    """把query拆成tokens用于相关性匹配。
    保留：连续中文片段(>=2字)、英文单词、数字。去停用词。"""
    raw = re.findall(r'[\u4e00-\u9fff]+|[A-Za-z][A-Za-z0-9_]+|\d{2,}', query)
    tokens = []
    for t in raw:
        if t in STOPWORDS:
            continue
        # 中文长串拆成 2-gram + 整串，提升匹配灵活度
        if re.match(r'^[\u4e00-\u9fff]+$', t) and len(t) >= 4:
            tokens.append(t)
            for i in range(len(t) - 1):
                bg = t[i:i+2]
                if bg not in STOPWORDS:
                    tokens.append(bg)
        elif len(t) >= 2:
            tokens.append(t.lower() if t.isascii() else t)
    # 去重保序
    seen, uniq = set(), []
    for t in tokens:
        if t not in seen:
            seen.add(t)
            uniq.append(t)
    return uniq


def relevance_score(result, query_tokens, primary_tokens):
    """计算结果与query的相关性 0~1。
    primary_tokens: 原始查询中的"主要词"（长度>=2的中文整串/英文词），缺失则严重扣分。
    """
    if not query_tokens:
        return 0.5
    title = (result.get("title") or "").lower()
    snippet = (result.get("snippet") or "").lower()
    haystack = title + " " + snippet
    hit, primary_hit = 0, 0
    for t in query_tokens:
        if t.lower() in haystack:
            hit += 1
    for t in primary_tokens:
        if t.lower() in haystack:
            primary_hit += 1
    hit_ratio = hit / len(query_tokens)
    primary_ratio = primary_hit / len(primary_tokens) if primary_tokens else 1.0
    # 主要词缺一半以上 → 几乎肯定不相关
    score = 0.4 * hit_ratio + 0.6 * primary_ratio
    # 标题命中加分
    title_hits = sum(1 for t in primary_tokens if t.lower() in title)
    if primary_tokens and title_hits / len(primary_tokens) >= 0.5:
        score = min(1.0, score + 0.15)
    return score


def filter_and_rank_by_relevance(results, query):
    """按相关性过滤+排序。
    【硬约束】结果的标题或摘要必须命中至少一个查询主要词（如学校名）。
    全军覆没时返回空数组——绝不回退到原始结果，避免泄漏无关页面。"""
    if not results:
        return results
    tokens = tokenize_query(query)
    # 主要词：原 query 中长度>=2 的中文整串 / 英文词 / 数字。
    # 同时把长中文串拆成 2-gram 加入主要词候选，例如 "大江幼儿园" → 也包含 "大江""幼儿园"。
    primary_raw = [t for t in re.findall(r'[\u4e00-\u9fff]{2,}|[A-Za-z][A-Za-z0-9_]+|\d{2,}', query)
                   if t not in STOPWORDS]
    primary = list(primary_raw)
    for t in primary_raw:
        if re.match(r'^[\u4e00-\u9fff]+$', t) and len(t) >= 4:
            for i in range(len(t) - 1):
                bg = t[i:i+2]
                if bg not in STOPWORDS and bg not in primary:
                    primary.append(bg)
    # 去重保序
    seen, uniq = set(), []
    for t in primary:
        if t not in seen:
            seen.add(t); uniq.append(t)
    primary = uniq

    if not tokens:
        return results

    kept = []
    for r in results:
        haystack = _haystack(r)
        # 必须命中至少一个主要词
        primary_hit = any(t.lower() in haystack for t in primary) if primary else True
        if not primary_hit:
            continue
        s = relevance_score(r, tokens, primary)
        if s < RELEVANCE_DROP_THRESHOLD:
            continue
        kept.append((r, s))

    if not kept:
        print(f"[WARN] 全部结果相关性过低（query={query}），返回空数组", file=sys.stderr)
        return []
    # 按相关性降序，原顺序作为次要键
    kept_with_idx = [(r, s, i) for i, (r, s) in enumerate(kept)]
    kept_with_idx.sort(key=lambda x: (-x[1], x[2]))
    ranked = [r for r, _, _ in kept_with_idx]
    dropped = len(results) - len(ranked)
    if dropped > 0:
        print(f"[INFO] 相关性过滤丢弃 {dropped} 条不相关结果", file=sys.stderr)
    return ranked

def get_dominant_domain(results):
    if not results: return (None, 0, 0)
    domains = {}
    for r in results:
        d = urlparse(r["url"]).netloc.replace("www.", "")
        domains[d] = domains.get(d, 0) + 1
    top = max(domains, key=domains.get)
    if domains[top] > len(results) * 0.5:
        return (top, domains[top], len(results))
    return (None, 0, len(results))

def merge_results(primary, secondary, max_results):
    seen, merged = set(), []
    for r in primary + secondary:
        if r["url"] not in seen: seen.add(r["url"]); merged.append(r)
    return merged[:max_results]

def apply_filter(results, do_filter):
    if not results:
        return results
    if do_filter:
        filtered = [r for r in results if not is_blocked_domain(r["url"]) and not is_low_quality_domain(r["url"])]
        if filtered:
            return filtered
        # 过滤后空集：若原结果100%都是低质量域名，宁可返回空让上层走兜底，不回退
        all_low = all(is_low_quality_domain(r["url"]) or is_blocked_domain(r["url"]) for r in results)
        if all_low:
            print("[WARN] 全部结果均为低质量域名，已丢弃（建议改用更具体的query或 --engine=auto）", file=sys.stderr)
            return []
        print("[WARN] --filter 过滤后为空，回退", file=sys.stderr)
    return results


# ==================== Bing 搜索 ====================
def search_bing(query, max_results, do_filter=False, original_query=None):
    start = time.time()
    url = build_bing_url(query, max_results + 5)
    print(f"[DEBUG] Bing: {query} | max={max_results}", file=sys.stderr)
    init_browser()
    results = []
    for attempt in range(MAX_RETRIES):
        throttle()
        ctx, page = create_context(), None
        try:
            page = ctx.new_page()
            page.route(ROUTE_PATTERN, lambda r: r.abort())
            page.goto(url, timeout=TIMEOUT, wait_until="domcontentloaded")
            page.wait_for_timeout(WAIT_TIME)
            raw = page.evaluate("""() => {
                const items = [];
                // 主结果容器：li.b_algo（排除广告/sponsored）
                document.querySelectorAll('li.b_algo').forEach(el => {
                    try {
                        // 跳过广告：b_ad / b_adTop / data-priority="Ads" / 含"广告"标记
                        if (el.classList.contains('b_ad') || el.classList.contains('b_adTop')) return;
                        if (el.querySelector('.b_adProvider, .b_adlabel, [data-priority="Ads"]')) return;
                        if (el.closest('.b_ad, .b_adTop, #b_results > .b_ad')) return;
                        // 标题：只取主h2 a，不要子链接里的a
                        const h2 = el.querySelector('h2');
                        if (!h2) return;
                        const a = h2.querySelector('a');
                        if (!a || !a.href || !a.href.startsWith('http')) return;
                        // 排除指向bing自身/微软广告
                        if (/bing\\.com\\/aclk|bing\\.com\\/ck\\/a|microsoft\\.com\\/.*\\/ad/.test(a.href)) return;
                        const title = (a.innerText || a.textContent || '').trim();
                        if (!title || title.length < 3) return;
                        // 摘要：优先 .b_caption p，回退 .b_richcard / .b_lineclamp*
                        let snippetEl = el.querySelector('.b_caption p')
                                     || el.querySelector('.b_lineclamp4, .b_lineclamp3, .b_lineclamp2')
                                     || el.querySelector('.b_caption');
                        let snippet = snippetEl ? (snippetEl.innerText || snippetEl.textContent || '').trim() : '';
                        // 截断过长摘要
                        if (snippet.length > 400) snippet = snippet.slice(0, 400);
                        items.push({ title, url: a.href.trim(), snippet });
                    } catch (e) {}
                });
                return items;
            }""")
            for r in raw:
                if r["title"] and r["url"] and len(r["title"]) > 3:
                    results.append({"title":r["title"],"url":r["url"],"snippet":r["snippet"],"content":""})
            if len(results) >= max_results: break
            if len(raw) == 0 and attempt < MAX_RETRIES - 1:
                w = 5 * (attempt + 1)
                print(f"[WARN] 0结果，可能限流，等{w}s", file=sys.stderr)
                time.sleep(w)
        except Exception as e:
            print(f"[WARN] Bing尝试{attempt+1}失败: {e}", file=sys.stderr)
        finally:
            try: ctx.close()
            except: pass
    print(f"[info] 结果：{results}", file=sys.stderr)    

    # 【降级检测】Bing 国内版触发风控时常返回一批与 query 完全无关的"垃圾域"结果
    # （典型：搜任何"深圳XX"都返回同一批知乎/百度知道）。检测到此情况时，
    # 自动追加 -site:xxx 排除式重试一次，给 Bing 一个返回真实学校内容的机会。
    if results and not query.lower().count("-site:"):
        junk_count = sum(1 for r in results if is_low_quality_domain(r["url"]))
        if junk_count >= max(3, len(results) * 0.6):
            junk_sites = ["zhihu.com", "zhuanlan.zhihu.com", "baidu.com"]
            exclude = " ".join(f"-site:{s}" for s in junk_sites)
            retry_q = f"{query} {exclude}"
            print(f"[INFO] 检测到 {junk_count}/{len(results)} 条低质量域名（疑似Bing风控降级），排除重试: {exclude}", file=sys.stderr)
            throttle()
            ctx2 = create_context()
            try:
                p2 = ctx2.new_page()
                p2.route(ROUTE_PATTERN, lambda r: r.abort())
                p2.goto(build_bing_url(retry_q, max_results + 5), timeout=TIMEOUT, wait_until="domcontentloaded")
                p2.wait_for_timeout(WAIT_TIME)
                raw2 = p2.evaluate("""() => {
                    const items=[];
                    document.querySelectorAll('li.b_algo').forEach(el=>{
                        try{
                            if(el.classList.contains('b_ad')||el.classList.contains('b_adTop'))return;
                            const h2=el.querySelector('h2');if(!h2)return;
                            const a=h2.querySelector('a');if(!a||!a.href||!a.href.startsWith('http'))return;
                            const title=(a.innerText||'').trim();if(!title||title.length<3)return;
                            let s=el.querySelector('.b_caption p')||el.querySelector('.b_lineclamp4,.b_lineclamp3,.b_lineclamp2')||el.querySelector('.b_caption');
                            let snippet=s?(s.innerText||'').trim():'';if(snippet.length>400)snippet=snippet.slice(0,400);
                            items.push({title,url:a.href.trim(),snippet});
                        }catch(e){}
                    });
                    return items;
                }""")
                retry_results = [{"title":r["title"],"url":r["url"],"snippet":r["snippet"],"content":""}
                                 for r in raw2 if r["title"] and r["url"] and len(r["title"]) > 3]
                if retry_results:
                    results = retry_results
            except Exception as e:
                print(f"[WARN] 排除重试失败: {e}", file=sys.stderr)
            finally:
                try: ctx2.close()
                except: pass

    results = apply_filter(results, do_filter)
    # 学校领域硬过滤：仅保留学校相关结果
    results = filter_school_only(results)
    # 相关性过滤+排序（用原始query匹配，避免改写后丢相关性信号）
    rel_query = original_query or query
    results = filter_and_rank_by_relevance(results, rel_query)[:max_results]
    dom, dc, tot = get_dominant_domain(results)
    if dom: print(f"[WARN] 单域名集中: {dom} ({dc}/{tot})", file=sys.stderr)
    q = score_results(results)
    print(f"[DEBUG] 质量: {q:.2f} | 数量: {len(results)} | 耗时: {time.time()-start:.1f}s", file=sys.stderr)
    return results, dom


# ==================== DuckDuckGo ====================
def search_duckduckgo(query, max_results, do_filter=False, original_query=None):
    """DuckDuckGo SPA 搜索。
    要点：
    1) 必须用本机 Chrome 通道（channel='chrome'）启动，bundled chromium 会被 418。
    2) DDG 新版 DOM 用随机 hash 类名；锚点用 data-testid。
    3) snippet 节点没有稳定 testid，取 article 内最长的非链接文本节点作为 snippet。
    """
    start = time.time()
    url = build_duckduckgo_url(query)
    print(f"[DEBUG] DDG: {query}", file=sys.stderr)
    using_chrome = init_ddg_browser()
    if not using_chrome:
        print("[WARN] 未检测到本机 Chrome；DDG 仍尝试访问，但很可能拿不到结果", file=sys.stderr)
    results = []
    for attempt in range(max(DDG_RETRIES, 1)):
        ctx = create_ddg_context()
        page = None
        try:
            page = ctx.new_page()
            # 不要 abort 任何资源——DDG SPA 反检测会因此变得敏感
            page.goto(url, timeout=max(DDG_TIMEOUT, 20000), wait_until="domcontentloaded")
            try:
                page.wait_for_selector('article[data-testid="result"]', timeout=15000)
            except Exception:
                # 落到 418 反爬页或没渲染
                cur = page.url or ""
                title = page.title() or ""
                if "/static-pages/418" in cur or "Protection. Privacy" in title:
                    print(f"[WARN] DDG 风控页（418）attempt={attempt+1}", file=sys.stderr)
                else:
                    print(f"[WARN] DDG 未渲染结果 attempt={attempt+1} url={cur}", file=sys.stderr)
                continue

            raw = page.evaluate(r"""() => {
                const items = [];
                document.querySelectorAll('article[data-testid="result"]').forEach(el => {
                    try {
                        // 标题与链接
                        const a = el.querySelector('a[data-testid="result-title-a"]')
                               || el.querySelector('h2 a')
                               || el.querySelector('a[href^="http"]');
                        if (!a || !a.href || !a.href.startsWith('http')) return;
                        // DDG 偶尔出广告/赞助：链接 host 是 duckduckgo 自身的跳转
                        if (/duckduckgo\.com\/y\.js/.test(a.href)) return;

                        const titleEl = el.querySelector('a[data-testid="result-title-a"] span')
                                     || el.querySelector('h2');
                        const title = titleEl ? (titleEl.innerText || titleEl.textContent || '').trim() : '';
                        if (!title || title.length < 3) return;

                        // snippet: DDG 新版无稳定 testid。
                        // 策略：在 article 中遍历叶子文本节点，挑选最长的、不属于标题/URL 面包屑的文本块。
                        const url = a.href;
                        const titleLower = title.toLowerCase();
                        let bestSnippet = '';
                        // 直接取 article.innerText，按行分割筛选
                        const fullText = (el.innerText || '').split(/\n+/);
                        for (const line of fullText) {
                            const t = (line || '').trim();
                            if (!t || t.length < 25) continue;
                            const tl = t.toLowerCase();
                            // 过滤：标题本身、URL/面包屑（http 开头）、超大段（多结果合并）
                            if (tl === titleLower) continue;
                            if (tl.startsWith('http')) continue;
                            if (t.length > 600) continue;
                            // 过滤纯英文 ASCII 中包含很多 › 之类的面包屑
                            if (/›|»|\.\.\.\s*›/.test(t) && t.length < 80) continue;
                            if (t.length > bestSnippet.length) bestSnippet = t;
                        }
                        if (bestSnippet.length > 400) bestSnippet = bestSnippet.slice(0, 400);

                        items.push({ title, url: url.trim(), snippet: bestSnippet });
                    } catch (e) {}
                });
                return items;
            }""")
            for r in raw:
                if r["title"] and r["url"] and len(r["title"]) > 3:
                    results.append({"title": r["title"], "url": r["url"], "snippet": r["snippet"], "content": ""})
            if len(results) >= max_results:
                break
        except Exception as e:
            print(f"[WARN] DDG 异常 attempt={attempt+1}: {e}", file=sys.stderr)
        finally:
            try: ctx.close()
            except: pass

    print(f"[DEBUG] DDG 抓取原始 {len(results)} 条", file=sys.stderr)
    results = apply_filter(results, do_filter)
    results = filter_school_only(results)
    rel_query = original_query or query
    results = filter_and_rank_by_relevance(results, rel_query)[:max_results]
    print(f"[DEBUG] DDG: {len(results)}条 | {time.time()-start:.1f}s", file=sys.stderr)
    return results, None



# ==================== 全文抓取 ====================
def fetch_full(url):
    start = time.time()
    print(f"[DEBUG] 抓全文: {url}", file=sys.stderr)
    if is_blocked_domain(url): return "黑名单域名，跳过"
    ctx, page = create_context(), None
    text = ""
    try:
        page = ctx.new_page()
        page.route(ROUTE_PATTERN, lambda r: r.abort())
        page.goto(url, timeout=FETCH_TIMEOUT, wait_until="domcontentloaded")
        page.wait_for_timeout(800)
        try: page.wait_for_load_state("networkidle", timeout=5000)
        except: pass
        text = page.evaluate("""() => {
            document.querySelectorAll('script,style,nav,header,footer,.ad,.ads,[class*="banner"],[id*="banner"],.sidebar,.comment,.popup,.modal,.cookie').forEach(e=>e.remove());
            for (const sel of ['article','main','.content','.post','.article','#content','#main','.entry-content','.post-content','[itemprop="articleBody"]']) {
                const m=document.querySelector(sel); if(m&&m.innerText.length>200) return m.innerText;
            }
            return document.body?document.body.innerText:'';
        }""")
    except Exception as e:
        print(f"[ERROR] 全文失败: {e}", file=sys.stderr)
    finally:
        try: ctx.close()
        except: pass
    result = (text or "").strip()[:8000]
    print(f"[DEBUG] 全文: {len(result)}字 | {time.time()-start:.1f}s", file=sys.stderr)
    return result or "抓取失败"


# ==================== 主函数 ====================
def main():
    ensure_playwright()
    query, max_results, full, engine, do_filter, no_rewrite = parse_args()
    if not query:
        print(json.dumps({"error": "no query"}, ensure_ascii=False)); sys.exit(1)

    # 学校域专用：若 query 不含学校关键词，自动追加"学校"上下文
    augmented = ensure_school_context(query)
    if augmented != query:
        print(f"[INFO] query 缺少学校上下文，自动追加：{query} → {augmented}", file=sys.stderr)
        query = augmented

    # 意图识别 + query改写(仅在搜索质量差时触发,不提前改写)
    original_query = query

    results = []

    if engine == "duckduckgo":
        results, _ = search_duckduckgo(query, max_results, do_filter, original_query=original_query)

    else:
        # 第1步: 用原始 query 搜索
        results, dominant = search_bing(query, max_results, do_filter, original_query=original_query)
        quality = score_results(results)

        # 第2步: 单域名集中/低质量域名 → 排除重试(最多2轮,限流时停止)
        if len(results) > 0:
            excluded = set()
            for _ in range(2):
                target = None
                if dominant and dominant not in excluded:
                    target = dominant
                elif results and is_low_quality_domain(results[0]["url"]):
                    d = urlparse(results[0]["url"]).netloc.replace("www.", "")
                    if d not in excluded: target = d
                if not target: break
                excluded.add(target)
                rq = query + " " + " ".join(f"-site:{d}" for d in excluded)
                print(f"[INFO] 排除({target})重试", file=sys.stderr)
                rr, _ = search_bing(rq, max_results, do_filter, original_query=original_query)
                if not rr: break
                rq_score = score_results(rr)
                if rq_score > quality:
                    results = merge_results(rr, results, max_results)
                    quality = score_results(results)
                    dominant, _, _ = get_dominant_domain(results)
                else: break

        # 第3步: 质量差 OR 空结果 → 尝试改写 query 重试
        if not no_rewrite and (len(results) == 0 or quality < QUALITY_THRESHOLD):
            rewritten, intent = rewrite_query(query)
            if intent and rewritten != query:
                print(f"[INFO] 触发意图改写({intent}): {query} → {rewritten}", file=sys.stderr)
                rr, _ = search_bing(rewritten, max_results, do_filter, original_query=original_query)
                if rr and (len(results) == 0 or score_results(rr) > quality):
                    results = rr
                    quality = score_results(results)

        # 第4步: 质量仍差 OR 空结果 → 简化query(去引号、模糊时间词、过多并列词)
        if len(results) == 0 or quality < QUALITY_THRESHOLD:
            simplified = re.sub(FUZZY_TIME_WORDS, '', query).strip()
            # 去掉中英文引号：Bing CN 对 "..." 精确匹配命中率很低，常导致 0 结果
            simplified = simplified.replace('"', ' ').replace('“', ' ').replace('”', ' ')
            simplified = re.sub(r'\s+', ' ', simplified).strip()
            if simplified != query and len(simplified) >= 2:
                print(f"[INFO] 简化重试(去引号/时间词): {simplified}", file=sys.stderr)
                rr, _ = search_bing(simplified, max_results, do_filter, original_query=original_query)
                if rr and (len(results) == 0 or score_results(rr) > quality):
                    results = rr
                    quality = score_results(results)

        # 第4.5步: 仍无结果 → 进一步精简，只保留前 2 个核心 token（学校名）
        if len(results) == 0:
            core_tokens = re.findall(r'[\u4e00-\u9fff]{2,}|[A-Za-z][A-Za-z0-9_]+', query)
            core_tokens = [t for t in core_tokens if t not in STOPWORDS]
            if len(core_tokens) >= 1:
                core_query = " ".join(core_tokens[:2]) + " 学校"
                if core_query != query:
                    print(f"[INFO] 核心词重试: {core_query}", file=sys.stderr)
                    rr, _ = search_bing(core_query, max_results, do_filter, original_query=original_query)
                    if rr:
                        results = rr

        # 第5步: Bing完全无结果 → DDG兜底（auto/bing 都尝试）
        if not results:
            print("[INFO] Bing无可用结果，DDG兜底...", file=sys.stderr)
            results, _ = search_duckduckgo(query, max_results, do_filter, original_query=original_query)

    # 全文抓取
    if full > 0 and results:
        for i in range(min(full, len(results))):
            results[i]["content"] = fetch_full(results[i]["url"])

    print(json.dumps(results, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try: main()
    finally: close_browser()
