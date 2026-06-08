#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
通用网页搜索（Playwright 版，搜狗移动端 + Bing 国内版双通道）

策略：
1. 优先访问搜狗移动端 (wap.sogou.com)：反爬强度比 PC 端低
2. 检测到反爬验证码、抓不到结果或加载失败时，自动 fallback 到 Bing 国内版
   (cn.bing.com)
3. 两路结果按 url 去重后合并，每条带 `engine` 字段标明来源
4. 持久化 chromium userdata 复用 cookie，注入 stealth 脚本隐藏 webdriver 特征
5. headless 模式遇到搜狗验证码会自动跳过到 Bing 兜底；
   --headless=false 时支持人工过验证后继续

CLI:
  python search_sogou.py "<关键词>" [-n N] [-o file] [-r] [--headless=false]
                                   [--engine sogou|bing|auto]
"""

from __future__ import annotations

import argparse
import json
import os
import random
import re
import sys
import time
from pathlib import Path
from urllib.parse import quote, urlparse

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

try:
    from playwright.sync_api import (
        sync_playwright,
        TimeoutError as PlaywrightTimeoutError,
    )
except ImportError:
    print(
        "请先安装 playwright: pip install playwright && playwright install chromium",
        file=sys.stderr,
    )
    raise


USER_DATA_DIR = os.environ.get(
    "SOGOU_USERDATA_DIR",
    os.path.expanduser("~/.sogou-search/userdata"),
)

MOBILE_UA = (
    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) "
    "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1"
)

CAPTCHA_URL_PATTERNS = ("antispider", "/captcha", "seccode", "wsh.cn/anti")
CAPTCHA_TEXT_PATTERNS = re.compile(
    r"验证码|按顺序依次点击|请输入验证码|seccodeInput|访问异常|系统检测到", re.I
)

STEALTH_INIT_SCRIPT = """
Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
Object.defineProperty(navigator, 'languages', { get: () => ['zh-CN', 'zh', 'en'] });
Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
window.chrome = window.chrome || { runtime: {} };
const origQuery = window.navigator.permissions && window.navigator.permissions.query;
if (origQuery) {
    window.navigator.permissions.query = (p) =>
        p && p.name === 'notifications'
            ? Promise.resolve({ state: Notification.permission })
            : origQuery(p);
}
"""

SOGOU_MOBILE_EXTRACT = r"""
() => {
    const norm = (s) => (s || '').replace(/\s+/g, ' ').trim();
    const containerSels = [
        'div.vrResult',
        'div[data-md5]',
        'li.resultItem',
        '.resultItem',
        '.vrwrap',
        '.rb',
        'section.result',
    ];
    const containers = new Set();
    for (const sel of containerSels) {
        document.querySelectorAll(sel).forEach((el) => containers.add(el));
    }
    // 兜底：若未匹配到容器，直接拿带 href 的 a.resultLink/a.item-text 的最近父级
    if (containers.size === 0) {
        document
            .querySelectorAll('a.resultLink[href], a.item-text[href]')
            .forEach((a) => {
                const p = a.closest('div, li, section') || a.parentElement;
                if (p) containers.add(p);
            });
    }

    const out = [];
    const seenUrls = new Set();
    containers.forEach((el) => {
        const link =
            el.querySelector('a.resultLink[href]') ||
            el.querySelector('a.item-text[href]') ||
            el.querySelector('h3 a[href], h2 a[href]') ||
            el.querySelector('a[href]');
        if (!link) return;
        const url = link.href || link.getAttribute('href') || '';
        if (!url || /^javascript:/i.test(url)) return;
        if (seenUrls.has(url)) return;
        seenUrls.add(url);

        let title = norm(link.innerText || link.textContent);
        // 优先标题节点
        const titleNode =
            el.querySelector('.vr-title, .vrTitle, .resultTitle, .result-title, h3, h2');
        if (titleNode) {
            const t = norm(titleNode.innerText || titleNode.textContent);
            if (t) title = t;
        }
        if (!title) return;

        let summary = '';
        const summarySels = [
            '.vr-content', '.vrContent', '.result-summary',
            '.str-text-info', '.summary', '.fz-mid',
            '.text-layout', '.space-txt', '.abstract',
            'p',
        ];
        for (const ss of summarySels) {
            const n = el.querySelector(ss);
            if (n && n !== link) {
                const t = norm(n.innerText || n.textContent);
                if (t && t !== title) {
                    summary = t.length > 240 ? t.slice(0, 240) + '...' : t;
                    break;
                }
            }
        }

        let source = '';
        const sourceSels = [
            '.citeurl', 'cite', '.cite-line',
            '.fb-source', '.source', '.vr-source',
            '.result-source', '.from',
        ];
        for (const ss of sourceSels) {
            const n = el.querySelector(ss);
            if (n) {
                const t = norm(n.innerText || n.textContent);
                if (t) {
                    source = t;
                    break;
                }
            }
        }
        if (!source) {
            try {
                const host = new URL(url, location.href).hostname;
                if (host) source = host;
            } catch (e) {}
        }

        out.push({ title, url, summary, source });
    });
    return out;
}
"""

BING_EXTRACT = r"""
() => {
    const norm = (s) => (s || '').replace(/\s+/g, ' ').trim();
    const out = [];
    document.querySelectorAll('li.b_algo, .b_algo').forEach((el) => {
        const link = el.querySelector('h2 a, h3 a, a.tilk');
        if (!link) return;
        const title = norm(link.innerText || link.textContent);
        const url = link.href || link.getAttribute('href') || '';
        if (!title || !url) return;
        let summary = '';
        for (const ss of ['.b_caption p', '.b_lineclamp1', '.b_lineclamp2', '.b_lineclamp3', '.b_lineclamp4', '.b_paractl', 'p']) {
            const n = el.querySelector(ss);
            if (n) { const t = norm(n.innerText); if (t) { summary = t; break; } }
        }
        let source = '';
        const cite = el.querySelector('.b_attribution cite, cite');
        if (cite) source = norm(cite.innerText);
        out.push({ title, url, summary, source });
    });
    return out;
}
"""


def _is_captcha_url(url):
    if not url:
        return False
    u = url.lower()
    return any(p in u for p in CAPTCHA_URL_PATTERNS)


def _is_captcha_page(page):
    try:
        if _is_captcha_url(page.url):
            return True
        title = (page.title() or "").lower()
        if any(p in title for p in ("验证", "captcha", "异常")):
            return True
        body_text = page.evaluate(
            "() => (document.body && document.body.innerText || '').slice(0, 4000)"
        )
        if body_text and CAPTCHA_TEXT_PATTERNS.search(body_text):
            return True
    except Exception:
        return False
    return False


def _wait_for_captcha_solved(page, timeout_sec=180):
    print(
        f"[反爬] 检测到验证码: {page.url}\n"
        f"  请在浏览器窗口中按提示完成验证（最多等待 {timeout_sec}s）...",
        file=sys.stderr,
    )
    deadline = time.time() + timeout_sec
    while time.time() < deadline:
        time.sleep(2)
        try:
            if not _is_captcha_page(page):
                print("[反爬] 验证完成，继续", file=sys.stderr)
                time.sleep(1)
                return True
        except Exception:
            pass
    return False


def _fetch_page_items(page, url, extract_script, *, headless):
    """返回 (items, status)，status ∈ {ok, captcha, timeout, error}"""
    try:
        page.goto(url, wait_until="domcontentloaded", timeout=30000)
    except PlaywrightTimeoutError:
        return [], "timeout"
    except Exception as e:
        print(f"[fetch] 加载失败 {url}: {e}", file=sys.stderr)
        return [], "error"

    time.sleep(random.uniform(0.8, 1.6))

    if _is_captcha_page(page):
        if not headless:
            if _wait_for_captcha_solved(page):
                try:
                    page.goto(url, wait_until="domcontentloaded", timeout=30000)
                    time.sleep(random.uniform(0.8, 1.5))
                except PlaywrightTimeoutError:
                    return [], "timeout"
            else:
                return [], "captcha"
        else:
            return [], "captcha"

    try:
        items = page.evaluate(extract_script) or []
    except Exception as e:
        print(f"[fetch] 解析失败 {url}: {e}", file=sys.stderr)
        return [], "error"
    return items, "ok"


# 与 search_wechat.js 中 getRealUrl 使用的 base cookie 保持一致，伪装老用户
SOGOU_BASE_COOKIES = [
    {"name": "ABTEST", "value": "7|1716888919|v1"},
    {"name": "IPLOC", "value": "CN5101"},
    {"name": "ariaDefaultTheme", "value": "default"},
    {"name": "ariaFixed", "value": "true"},
    {"name": "ariaReadtype", "value": "1"},
    {"name": "ariaStatus", "value": "false"},
]


def _seed_sogou_base_cookies(ctx):
    """注入伪装基础 cookie（与 search_wechat.js 同源）"""
    cookies = []
    for c in SOGOU_BASE_COOKIES:
        for domain in (".sogou.com", ".weixin.sogou.com"):
            cookies.append({
                "name": c["name"],
                "value": c["value"],
                "domain": domain,
                "path": "/",
            })
    try:
        ctx.add_cookies(cookies)
    except Exception as e:
        print(f"[预热] 基础 cookie 注入失败: {e}", file=sys.stderr)


def _warm_sogou_cookies(ctx):
    """
    参考 search_wechat.js 的 getSogouCookie：先访问搜狗视频搜索页拿 SUV/SNUID，
    把会话 cookie 写入 persistent context，再去走真正的 wap 搜索就不容易撞反爬。
    返回是否拿到了 SNUID。
    """
    warmup_url = "https://v.sogou.com/v?ie=utf8&query=&p=40030600"
    warm_page = None
    try:
        warm_page = ctx.new_page()
        warm_page.set_default_timeout(15000)
        try:
            warm_page.goto(warmup_url, wait_until="domcontentloaded", timeout=15000)
        except PlaywrightTimeoutError:
            print("[预热] v.sogou.com 加载超时，跳过", file=sys.stderr)
            return False
        time.sleep(random.uniform(0.6, 1.2))
        try:
            cookies = ctx.cookies("https://v.sogou.com/")
        except Exception:
            cookies = []
        names = {c.get("name"): c.get("value", "") for c in cookies}
        snuid = names.get("SNUID", "")
        suv = names.get("SUV", "")
        if snuid or suv:
            print(
                f"[预热] 已获取搜狗 cookie: SNUID={'有' if snuid else '无'}, "
                f"SUV={'有' if suv else '无'}",
                file=sys.stderr,
            )
            return bool(snuid)
        print("[预热] 未拿到 SNUID/SUV，继续尝试搜索", file=sys.stderr)
        return False
    except Exception as e:
        print(f"[预热] 失败: {e}", file=sys.stderr)
        return False
    finally:
        if warm_page is not None:
            try:
                warm_page.close()
            except Exception:
                pass


def _search_sogou_mobile(page, query, max_results, headless):
    results = []
    seen = set()
    encoded = quote(query)
    last_status = "ok"

    for page_no in range(1, max(1, (max_results + 9) // 10) + 2):
        url = (
            f"https://wap.sogou.com/web/searchList.jsp?"
            f"keyword={encoded}&p={page_no}&t=0&s_from=result_up"
        )
        items, status = _fetch_page_items(
            page, url, SOGOU_MOBILE_EXTRACT, headless=headless,
        )
        last_status = status
        if status != "ok":
            break

        new_count = 0
        for it in items:
            u = (it.get("url") or "").strip()
            if not u or u in seen:
                continue
            if "wap.sogou.com" in u and "/web/" not in u:
                continue
            seen.add(u)
            it["engine"] = "sogou-mobile"
            results.append(it)
            new_count += 1
            if len(results) >= max_results:
                break

        if len(results) >= max_results or new_count == 0:
            break
        time.sleep(random.uniform(1.2, 2.4))

    return results[:max_results], last_status


def _search_bing(page, query, max_results, headless):
    results = []
    seen = set()
    encoded = quote(query)
    last_status = "ok"

    for page_no in range(0, max(1, (max_results + 9) // 10) + 1):
        first = page_no * 10 + 1
        url = (
            f"https://cn.bing.com/search?q={encoded}&first={first}"
            f"&FORM=PERE&ensearch=0&mkt=zh-CN"
        )
        items, status = _fetch_page_items(
            page, url, BING_EXTRACT, headless=headless,
        )
        last_status = status
        if status != "ok":
            break

        new_count = 0
        for it in items:
            u = (it.get("url") or "").strip()
            if not u or u in seen:
                continue
            host = urlparse(u).hostname or ""
            if host.endswith("bing.com"):
                continue
            seen.add(u)
            it["engine"] = "bing"
            results.append(it)
            new_count += 1
            if len(results) >= max_results:
                break

        if len(results) >= max_results or new_count == 0:
            break
        time.sleep(random.uniform(1.0, 2.0))

    return results[:max_results], last_status


def _resolve_real_urls(page, results, headless):
    """把 sogou /link?url=... 跳转链接解析为真实地址"""
    print(f"[resolve] 解析 {len(results)} 条真实 URL", file=sys.stderr)
    ok = 0
    fail = 0
    for i, it in enumerate(results, 1):
        u = it.get("url") or ""
        if "sogou.com/link" not in u:
            it["url_resolved"] = True
            continue
        try:
            page.goto(u, wait_until="domcontentloaded", timeout=15000)
            time.sleep(random.uniform(0.6, 1.2))
            if _is_captcha_page(page):
                it["url_resolved"] = False
                fail += 1
                continue
            real = page.url
            if real and "sogou.com/link" not in real:
                it["url"] = real
                it["url_resolved"] = True
                ok += 1
            else:
                it["url_resolved"] = False
                fail += 1
        except Exception as e:
            print(f"  [{i}/{len(results)}] 解析失败: {e}", file=sys.stderr)
            it["url_resolved"] = False
            fail += 1
        time.sleep(random.uniform(0.3, 0.8))
    print(f"[resolve] 成功 {ok}, 失败 {fail}", file=sys.stderr)


def search_web(query, num=10, *, resolve_real_url=False, headless=True,
               user_data_dir=USER_DATA_DIR, engine="auto"):
    num = max(1, min(num, 50))
    Path(user_data_dir).mkdir(parents=True, exist_ok=True)

    with sync_playwright() as p:
        ctx = p.chromium.launch_persistent_context(
            user_data_dir,
            headless=headless,
            viewport={"width": 414, "height": 896},
            user_agent=MOBILE_UA,
            locale="zh-CN",
            device_scale_factor=2,
            is_mobile=True,
            has_touch=True,
            args=[
                "--disable-blink-features=AutomationControlled",
                "--disable-features=IsolateOrigins,site-per-process",
                "--no-sandbox",
            ],
        )
        ctx.add_init_script(STEALTH_INIT_SCRIPT)

        page = ctx.pages[0] if ctx.pages else ctx.new_page()
        page.set_default_timeout(30000)

        try:
            sogou_results = []
            bing_results = []
            engines_used = []
            sogou_status = "skipped"
            bing_status = "skipped"

            if engine in ("auto", "sogou"):
                _seed_sogou_base_cookies(ctx)
                _warm_sogou_cookies(ctx)
                print(f"[搜索] 搜狗移动端: {query!r}", file=sys.stderr)
                sogou_results, sogou_status = _search_sogou_mobile(
                    page, query, num, headless,
                )
                if sogou_results:
                    engines_used.append("sogou-mobile")
                print(
                    f"[搜索] 搜狗移动端: {len(sogou_results)} 条 (status={sogou_status})",
                    file=sys.stderr,
                )

            need_bing = (
                engine == "bing"
                or (engine == "auto" and (
                    sogou_status != "ok" or len(sogou_results) < min(num, 3)
                ))
            )
            if need_bing:
                try:
                    page.set_viewport_size({"width": 1366, "height": 900})
                except Exception:
                    pass
                print(f"[搜索] Bing 国内版: {query!r}", file=sys.stderr)
                bing_results, bing_status = _search_bing(
                    page, query, num, headless,
                )
                if bing_results:
                    engines_used.append("bing")
                print(
                    f"[搜索] Bing: {len(bing_results)} 条 (status={bing_status})",
                    file=sys.stderr,
                )

            merged = []
            seen = set()
            for it in sogou_results + bing_results:
                key = (it.get("url") or "") + "|" + (it.get("title") or "")
                if key in seen:
                    continue
                seen.add(key)
                merged.append(it)
                if len(merged) >= num:
                    break

            if resolve_real_url and merged:
                _resolve_real_urls(page, merged, headless)

            engine_used = "+".join(engines_used) if engines_used else "none"
            return {
                "query": query,
                "engine_used": engine_used,
                "sogou_status": sogou_status,
                "bing_status": bing_status,
                "total": len(merged),
                "results": merged,
            }
        finally:
            try:
                ctx.close()
            except Exception:
                pass


def search_sogou(*args, **kwargs):
    """兼容旧函数名"""
    payload = search_web(*args, **kwargs)
    return payload.get("results", [])


def _parse_bool(v):
    if isinstance(v, bool):
        return v
    return str(v).lower() not in ("false", "0", "no", "off")


def main():
    parser = argparse.ArgumentParser(
        description="网页搜索（搜狗移动端 + Bing 国内版双通道，自动应对反爬）",
    )
    parser.add_argument("query", help="搜索关键词")
    parser.add_argument("-n", "--num", type=int, default=10, help="返回数量（默认 10，最大 50）")
    parser.add_argument("-o", "--output", default="", help="输出 JSON 文件路径")
    parser.add_argument(
        "-r", "--resolve-url", action="store_true",
        help="把搜狗 /link?url=... 跳转链接解析为真实 URL（仅对 sogou 结果有效）",
    )
    parser.add_argument(
        "--headless", default="true",
        help="是否无头模式，默认 true；触发验证码时建议 --headless=false",
    )
    parser.add_argument(
        "--user-data-dir", default=USER_DATA_DIR,
        help=f"chromium 持久化目录（默认 {USER_DATA_DIR}）",
    )
    parser.add_argument(
        "--engine", choices=("auto", "sogou", "bing"), default="auto",
        help="搜索引擎：auto=搜狗移动端优先+Bing兜底（默认）/ sogou=仅搜狗 / bing=仅Bing",
    )
    args = parser.parse_args()

    headless = _parse_bool(args.headless)

    print(
        f"[搜索] query={args.query!r}, num={args.num}, "
        f"engine={args.engine}, headless={headless}",
        file=sys.stderr,
    )
    try:
        payload = search_web(
            query=args.query,
            num=args.num,
            resolve_real_url=args.resolve_url,
            headless=headless,
            user_data_dir=args.user_data_dir,
            engine=args.engine,
        )
    except Exception as e:
        print(f"[搜索] 失败: {e}", file=sys.stderr)
        return 1

    text = json.dumps(payload, ensure_ascii=False, indent=2)

    if args.output:
        Path(args.output).write_text(text, encoding="utf-8")
        print(f"[搜索] 结果已保存: {args.output}", file=sys.stderr)
    print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())

CAPTCHA_URL_PATTERNS = ("antispider", "/captcha", "seccode", "wsh.cn/anti")
CAPTCHA_TEXT_PATTERNS = re.compile(
    r"验证码|按顺序依次点击|请输入验证码|seccodeInput|访问异常|系统检测到", re.I
)