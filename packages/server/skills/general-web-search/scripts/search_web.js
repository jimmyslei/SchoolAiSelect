#!/usr/bin/env node

/**
 * 通用联网搜索工具（搜狗 + Bing 双通道，HTTP 版）
 * 主路：搜狗 PC 端 https://www.sogou.com/web
 * 兜底：Bing 国内版 https://cn.bing.com/search
 */

const https = require("https");
const cheerio = require("cheerio");
const zlib = require("zlib");

// 真实浏览器 UA 池，每次请求随机选一个
const USER_AGENTS = [
  "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
  "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_2_1) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15",
  "Mozilla/5.0 (Macintosh; Intel Mac OS X 13_6_4) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
  "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_3) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edg/123.0.0.0 Chrome/123.0.0.0 Safari/537.36",
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edg/122.0.0.0 Chrome/122.0.0.0 Safari/537.36",
  "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
  "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
  "Mozilla/5.0 (X11; Linux x86_64; rv:123.0) Gecko/20100101 Firefox/123.0",
  "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:123.0) Gecko/20100101 Firefox/123.0",
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:123.0) Gecko/20100101 Firefox/123.0",
  "Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1",
  "Mozilla/5.0 (iPhone; CPU iPhone OS 16_7 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Mobile/15E148 Safari/604.1",
  "Mozilla/5.0 (iPad; CPU OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1",
  "Mozilla/5.0 (Linux; Android 14; Pixel 8 Pro) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Mobile Safari/537.36",
  "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Mobile Safari/537.36",
  "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Mobile Safari/537.36",
  "Mozilla/5.0 (Linux; Android 13; Mi 11) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Mobile Safari/537.36",
];

function getRandomUserAgent() {
  return USER_AGENTS[Math.floor(Math.random() * USER_AGENTS.length)];
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function decompressBody(buffer, contentEncoding) {
  if (!contentEncoding) return buffer;
  const encoding = String(contentEncoding).toLowerCase();
  try {
    if (encoding.includes("gzip")) return zlib.gunzipSync(buffer);
    if (encoding.includes("deflate")) return zlib.inflateSync(buffer);
    if (encoding.includes("br")) return zlib.brotliDecompressSync(buffer);
  } catch {
    // 解压失败时返回原始数据
  }
  return buffer;
}

/**
 * 统一 https 请求工具，带超时与重试，自动 gzip/deflate/br 解压
 * @param {{url: string, method?: string, headers?: Object, timeoutMs?: number, retries?: number}} options
 * @returns {Promise<{statusCode: number, headers: Object, body: Buffer}>}
 */
async function request(options) {
  const {
    url,
    method = "GET",
    headers = {},
    timeoutMs = 15000,
    retries = 0,
  } = options;
  const lastErrorPrefix = `Request failed: ${method} ${url}`;

  for (let attempt = 0; attempt <= retries; attempt++) {
    try {
      const result = await new Promise((resolve, reject) => {
        const urlObj = new URL(url);
        const reqOptions = {
          hostname: urlObj.hostname,
          path: urlObj.pathname + urlObj.search,
          method,
          headers,
        };

        const req = https.request(reqOptions, (res) => {
          const chunks = [];
          res.on("data", (chunk) => chunks.push(chunk));
          res.on("end", () => {
            const raw = Buffer.concat(chunks);
            const body = decompressBody(raw, res.headers["content-encoding"]);
            resolve({
              statusCode: res.statusCode || 0,
              headers: res.headers,
              body,
            });
          });
        });

        req.on("error", reject);
        req.setTimeout(timeoutMs, () => {
          req.destroy();
          reject(new Error("Request timeout"));
        });
        req.end();
      });

      return result;
    } catch (e) {
      if (attempt >= retries) {
        throw new Error(`${lastErrorPrefix}: ${e.message}`);
      }
      await sleep(300 + attempt * 300);
    }
  }

  throw new Error(`${lastErrorPrefix}: unexpected`);
}

async function requestText(options) {
  const resp = await request(options);
  return { ...resp, text: resp.body.toString("utf-8") };
}

function extractCookies(headers) {
  const cookies = [];
  const setCookieHeader = headers["set-cookie"];
  if (setCookieHeader) {
    setCookieHeader.forEach((cookie) => {
      const cookieValue = cookie.split(";")[0];
      if (cookieValue) cookies.push(cookieValue);
    });
  }
  return cookies.join("; ");
}

/**
 * 预热搜狗 cookie：访问搜狗视频页拿到 SNUID/SUV 等
 * 参考 wechat-article-search 的 getSogouCookie 实现
 * @returns {Promise<{cookieStr: string, cookieObj: Object}>}
 */
async function getSogouCookie() {
  try {
    const resp = await request({
      url: "https://v.sogou.com/v?ie=utf8&query=&p=40030600",
      headers: {
        Accept:
          "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
        "Accept-Encoding": "identity",
        "Accept-Language": "zh-CN,zh;q=0.9,en;q=0.8",
        "User-Agent": getRandomUserAgent(),
      },
      timeoutMs: 10000,
      retries: 1,
    });

    const cookies = extractCookies(resp.headers);
    const cookieObj = {};
    if (cookies) {
      cookies.split("; ").forEach((cookie) => {
        const [key, value] = cookie.split("=");
        if (key && value) {
          cookieObj[key.trim()] = value.trim();
        }
      });
    }

    return { cookieStr: cookies || "", cookieObj };
  } catch {
    return { cookieStr: "", cookieObj: {} };
  }
}

/**
 * 检测搜狗反爬 / 验证码页 / 刪减干预响应
 */
function isSogouBlocked(html, finalUrl = "") {
  if (!html) return true;
  // 过短的 HTML 不可能是正常搜索页（正常页 ~700KB）
  if (html.length < 2000) return true;
  if (/antispider|captcha|seccode/i.test(finalUrl)) return true;
  const sample = html.slice(0, 8000);
  if (
    /请输入验证码|为了您的网络安全|按顺序依次点击|请完成下方验证|访问异常/.test(
      sample,
    )
  )
    return true;
  return false;
}

// ============================ 搜狗解析 ============================

/**
 * 从搜狗 PC 搜索结果 HTML 中解析条目
 * 兼容 .vrwrap / .rb / .results .result 多种结构，并提供 h3 a 顶层兜底
 */
function parseSogouResults(html, maxResults) {
  const results = [];
  const $ = cheerio.load(html);
  const seenUrls = new Set();

  function pushItem($el) {
    if (results.length >= maxResults) return;
    const $title = $el
      .find("h3 a, h3.vr-title a, h3.vrTitle a, a.title-color")
      .first();
    if ($title.length === 0) return;
    const title = $title.text().trim();
    if (!title) return;
    let url = $title.attr("href") || "";
    if (!url) return;
    if (url.startsWith("/")) url = `https://www.sogou.com${url}`;
    if (seenUrls.has(url)) return;

    let summary = "";
    const summaryCandidates = [
      ".fz-mid",
      ".text-layout",
      ".str_info",
      ".str-info",
      ".ft",
      ".space-txt",
    ];
    for (const sel of summaryCandidates) {
      const text = $el.find(sel).first().text().trim();
      if (text) {
        summary = text;
        break;
      }
    }
    if (!summary) {
      summary = $el.find("p").first().text().trim();
    }

    let source = "";
    const $cite = $el.find("cite, .citeurl, .cite-url").first();
    if ($cite.length > 0) {
      source = $cite.text().trim();
    }

    seenUrls.add(url);
    results.push({ title, url, summary, source, engine: "sogou" });
  }

  // 主选择器
  $(".vrwrap, .results .rb, .results > div, div.result").each((_, el) => {
    if (results.length >= maxResults) return false;
    pushItem($(el));
  });

  // 兜底：直接遍历 .results 区块内带 h3 的元素
  if (results.length === 0) {
    $(".results h3 a, #main h3 a").each((_, a) => {
      if (results.length >= maxResults) return false;
      const $a = $(a);
      const $container = $a.closest("div");
      pushItem($container.length ? $container : $a.parent());
    });
  }

  return results;
}

/**
 * 调用搜狗 PC 搜索
 * @returns {Promise<{status: 'ok'|'captcha'|'empty'|'error', results: Array, error?: string}>}
 */
async function searchSogou(query, maxResults) {
  let cookieStr = (await getSogouCookie()).cookieStr;
  const all = [];
  const pagesNeeded = Math.ceil(maxResults / 10);
  let lastCaptcha = false;

  for (let page = 1; page <= pagesNeeded; page++) {
    const url = `https://www.sogou.com/web?query=${encodeURIComponent(query)}&page=${page}&ie=utf8`;

    // 同一页最多重试 3 次（换 UA + 重新拉 cookie）应对反爬
    let pageResults = [];
    let captchaThisPage = false;
    for (let attempt = 0; attempt < 3; attempt++) {
      const headers = {
        Accept:
          "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
        "Accept-Encoding": "gzip, deflate, br",
        "Accept-Language": "zh-CN,zh;q=0.9,en;q=0.8",
        Referer: "https://www.sogou.com/",
        "User-Agent": getRandomUserAgent(),
      };
      if (cookieStr) headers.Cookie = cookieStr;

      let resp;
      try {
        resp = await requestText({
          url,
          headers,
          timeoutMs: 15000,
          retries: 1,
        });
      } catch (e) {
        return { status: "error", results: all, error: e.message };
      }
      if (resp.statusCode >= 400) {
        return {
          status: "error",
          results: all,
          error: `HTTP ${resp.statusCode}`,
        };
      }
      if (isSogouBlocked(resp.text, url)) {
        captchaThisPage = true;
        console.error(
          `[sogou] page=${page} 检测到反爬 attempt=${attempt + 1} (htmlLen=${resp.text.length})`,
        );
        if (attempt < 2) {
          await sleep(1000 + Math.random() * 1500);
          // 重新拉 cookie
          try {
            cookieStr = (await getSogouCookie()).cookieStr;
          } catch {
            // 忽略 cookie 刷新失败
          }
          continue;
        }
        break;
      }
      const remaining = maxResults - all.length;
      pageResults = parseSogouResults(resp.text, remaining);
      captchaThisPage = false;
      if (pageResults.length > 0) break;
      console.error(
        `[sogou] page=${page} 解析到 0 条 attempt=${attempt + 1} (htmlLen=${resp.text.length})`,
      );
      if (attempt < 2) await sleep(800 + Math.random() * 1000);
    }

    if (captchaThisPage && pageResults.length === 0) {
      lastCaptcha = true;
      break;
    }
    if (pageResults.length === 0) break;
    all.push(...pageResults);
    if (all.length >= maxResults) break;
    if (page < pagesNeeded) await sleep(500 + Math.random() * 1000);
  }

  if (all.length === 0) {
    return { status: lastCaptcha ? "captcha" : "empty", results: [] };
  }
  return { status: "ok", results: all };
}

/**
 * 解析搜狗 /link?url=... 为真实链接
 */
async function resolveSogouLink(originalUrl) {
  if (!originalUrl.includes("sogou.com")) return originalUrl;
  try {
    const resp = await request({
      url: originalUrl,
      headers: {
        Accept:
          "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Encoding": "gzip, deflate, br",
        "Accept-Language": "zh-CN,zh;q=0.9,en;q=0.8",
        Referer: "https://www.sogou.com/",
        "User-Agent": getRandomUserAgent(),
      },
      timeoutMs: 8000,
      retries: 0,
    });

    if (
      resp.statusCode >= 300 &&
      resp.statusCode < 400 &&
      resp.headers.location
    ) {
      return resp.headers.location;
    }

    const html = resp.body.toString("utf-8");
    const metaMatch = html.match(
      /<meta[^>]*http-equiv=["']refresh["'][^>]*content=["']\d+;\s*url=([^"']+)["'][^>]*>/i,
    );
    if (metaMatch) return metaMatch[1];

    const jsMatch =
      html.match(/location\.href\s*=\s*["']([^"']+)["']/i) ||
      html.match(
        /window\.location(?:\.replace)?\s*\(\s*["']([^"']+)["']\s*\)/i,
      ) ||
      html.match(/location\s*=\s*["']([^"']+)["']/i);
    if (jsMatch) return jsMatch[1];

    const urlParts = [];
    for (const m of html.matchAll(/url\s*\+=\s*'([^']*)'/g))
      urlParts.push(m[1]);
    for (const m of html.matchAll(/url\s*\+=\s*"([^"]*)"/g))
      urlParts.push(m[1]);
    if (urlParts.length > 0) {
      const joined = urlParts.join("");
      if (/^https?:\/\//.test(joined)) return joined;
    }
  } catch {
    // 忽略解析失败
  }
  return originalUrl;
}

async function resolveSogouUrls(results) {
  const out = [];
  for (let i = 0; i < results.length; i++) {
    const r = results[i];
    if (r.engine !== "sogou") {
      out.push(r);
      continue;
    }
    const realUrl = await resolveSogouLink(r.url);
    const resolved = realUrl !== r.url && !/sogou\.com/.test(realUrl);
    out.push({ ...r, url: resolved ? realUrl : r.url, url_resolved: resolved });
    if (i < results.length - 1) {
      await sleep(300 + Math.random() * 500);
    }
  }
  return out;
}

// ============================ Bing 解析 ============================

/**
 * 从 Bing 搜索结果 HTML 中解析条目
 */
function parseBingResults(html, maxResults) {
  const results = [];
  const $ = cheerio.load(html);

  $("li.b_algo").each((_, el) => {
    if (results.length >= maxResults) return false;
    const $el = $(el);

    const $titleLink = $el.find("h2 a").first();
    if ($titleLink.length === 0) return;
    const title = $titleLink.text().trim();
    let url = $titleLink.attr("href") || "";
    if (!title || !url) return;

    let summary = "";
    const $caption = $el.find(".b_caption p").first();
    if ($caption.length > 0) {
      summary = $caption.text().trim();
    } else {
      summary = $el.find("p").first().text().trim();
    }

    let source = "";
    const $cite = $el.find("cite").first();
    if ($cite.length > 0) {
      source = $cite.text().trim();
    } else {
      try {
        source = new URL(url).hostname;
      } catch {
        source = "";
      }
    }

    results.push({
      title,
      url,
      summary,
      source,
      engine: "bing",
    });
  });

  return results;
}

/**
 * 调用 Bing 国内版搜索。如果 query 是中文复合实体词（无空格），先用引号精确匹配，无结果再退回宽松搜索。
 */
async function searchBing(query, maxResults) {
  const trimmed = (query || "").trim();
  // 判断是否需要加引号：含中文且不含空格 / 引号 / 运算符
  const isCnEntity =
    /[\u4e00-\u9fa5]/.test(trimmed) &&
    !/\s|["'“”‘’]|site:|filetype:/.test(trimmed);

  // 第一轮：精确匹配（如适用）
  if (isCnEntity) {
    const exact = await searchBingOnce(`"${trimmed}"`, maxResults);
    if (exact.status === "ok" && exact.results.length > 0) {
      return exact;
    }
    console.error(`[bing] 精确匹配无结果，退回宽松搜索`);
  }
  return await searchBingOnce(trimmed, maxResults);
}

/**
 * 单次 Bing 请求（不做引号重试）
 */
async function searchBingOnce(query, maxResults) {
  const all = [];
  const pagesNeeded = Math.ceil(maxResults / 10);

  for (let page = 0; page < pagesNeeded; page++) {
    const first = page * 10 + 1;
    const url = `https://cn.bing.com/search?q=${encodeURIComponent(query)}&first=${first}&FORM=PERE`;

    // 同一页最多重试 3 次（换 UA），应对 Bing 反爬伪装的"无结果"页
    let pageResults = [];
    let lastDiag = null;
    for (let attempt = 0; attempt < 3; attempt++) {
      const headers = {
        Accept:
          "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
        "Accept-Encoding": "gzip, deflate, br",
        "Accept-Language": "zh-CN,zh;q=0.9,en;q=0.8",
        Referer: "https://cn.bing.com/",
        "User-Agent": getRandomUserAgent(),
      };
      let resp;
      try {
        resp = await requestText({
          url,
          headers,
          timeoutMs: 15000,
          retries: 1,
        });
      } catch (e) {
        return { status: "error", results: all, error: e.message };
      }
      if (resp.statusCode >= 400) {
        return {
          status: "error",
          results: all,
          error: `HTTP ${resp.statusCode}`,
        };
      }
      const remaining = maxResults - all.length;
      pageResults = parseBingResults(resp.text, remaining);
      if (pageResults.length > 0) break;
      lastDiag = `htmlLen=${resp.text.length}, hasAlgo=${resp.text.includes("b_algo")}, hasNoResults=${resp.text.includes("b_no")}`;
      if (attempt < 2) {
        await sleep(800 + Math.random() * 1200);
      }
    }
    if (pageResults.length === 0) {
      console.error(
        `[bing] page=${page + 1} 解析到 0 条 (3 次重试后) (${lastDiag})`,
      );
      break;
    }
    all.push(...pageResults);

    if (all.length >= maxResults) break;
    if (page < pagesNeeded - 1) {
      await sleep(500 + Math.random() * 1000);
    }
  }

  if (all.length === 0) return { status: "empty", results: [] };
  return { status: "ok", results: all };
}

// ============================ 360 搜索 (so.com) ============================

function parseSo360Results(html, maxResults) {
  const $ = cheerio.load(html);
  const out = [];
  const seen = new Set();
  $("li.res-list, li.res-rich, li.result, .res-list, .res-rich").each(
    (_, el) => {
      if (out.length >= maxResults) return false;
      const $el = $(el);
      const $a = $el.find("h3 a").first();
      let title = $a.text().trim().replace(/\s+/g, " ");
      const href = ($a.attr("href") || "").trim();
      if (!title || !href) return;
      if (seen.has(href)) return;
      seen.add(href);
      const summary = $el
        .find(".res-desc, .res-comm-con, .res-rich-desc, .res-summary, p")
        .first()
        .text()
        .trim()
        .replace(/\s+/g, " ")
        .slice(0, 240);
      const source = ($el.find(".res-linkinfo cite, cite").first().text() || "")
        .trim()
        .slice(0, 120);
      out.push({ title, url: href, summary, source, engine: "so360" });
    },
  );
  return out;
}

async function searchSo360(query, maxResults) {
  const all = [];
  const pagesNeeded = Math.ceil(maxResults / 10);

  for (let page = 1; page <= pagesNeeded; page++) {
    const url = `https://www.so.com/s?q=${encodeURIComponent(query)}&pn=${page}`;
    let pageResults = [];
    let lastDiag = null;
    for (let attempt = 0; attempt < 3; attempt++) {
      const headers = {
        Accept:
          "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
        "Accept-Encoding": "gzip, deflate, br",
        "Accept-Language": "zh-CN,zh;q=0.9,en;q=0.8",
        Referer: "https://www.so.com/",
        "User-Agent": getRandomUserAgent(),
      };
      let resp;
      try {
        resp = await requestText({
          url,
          headers,
          timeoutMs: 15000,
          retries: 1,
        });
      } catch (e) {
        return { status: "error", results: all, error: e.message };
      }
      if (resp.statusCode >= 400) {
        return {
          status: "error",
          results: all,
          error: `HTTP ${resp.statusCode}`,
        };
      }
      const remaining = maxResults - all.length;
      pageResults = parseSo360Results(resp.text, remaining);
      if (pageResults.length > 0) break;
      lastDiag = `htmlLen=${resp.text.length}, hasResList=${resp.text.includes("res-list")}`;
      if (attempt < 2) await sleep(800 + Math.random() * 1200);
    }
    if (pageResults.length === 0) {
      console.error(
        `[so360] page=${page} 解析到 0 条 (3 次重试后) (${lastDiag})`,
      );
      break;
    }
    all.push(...pageResults);
    if (all.length >= maxResults) break;
    if (page < pagesNeeded) await sleep(500 + Math.random() * 1000);
  }

  if (all.length === 0) return { status: "empty", results: [] };
  return { status: "ok", results: all };
}

// ============================ 主流程 ============================

/**
 * 从查询提取相关性匹配 token：
 *  - ASCII 单词（长度 ≥ 2，小写）
 *  - 每段中文连续字符的 2-gram；单字段长度=1 时取该字
 */
function buildQueryTokens(query) {
  const q = String(query || "")
    .toLowerCase()
    .trim();
  const tokens = new Set();
  const asciiMatches = q.match(/[a-z0-9][a-z0-9\-+_.]+/g) || [];
  for (const t of asciiMatches) {
    if (t.length >= 2) tokens.add(t);
  }
  const cjkRuns = q.match(/[\u4e00-\u9fa5]+/g) || [];
  for (const run of cjkRuns) {
    if (run.length === 1) {
      tokens.add(run);
    } else {
      for (let i = 0; i < run.length - 1; i++) {
        tokens.add(run.slice(i, i + 2));
      }
    }
  }
  return Array.from(tokens);
}

/**
 * 计算结果对查询的匹配分（0~1）。空 token 列表则视为相关。
 */
function relevanceScore(result, tokens) {
  if (tokens.length === 0) return 1;
  const text = (
    String(result.title || "") +
    " " +
    String(result.summary || "")
  ).toLowerCase();
  let matched = 0;
  for (const t of tokens) {
    if (text.includes(t)) matched++;
  }
  return matched / tokens.length;
}

/**
 * 计算 query 与 text 的最长公共子串长度，仅统计中文+ASCII 字母数字。
 */
function longestCommonSubstring(a, b) {
  if (!a || !b) return 0;
  const m = a.length;
  const n = b.length;
  let prev = new Array(n + 1).fill(0);
  let max = 0;
  for (let i = 1; i <= m; i++) {
    const cur = new Array(n + 1).fill(0);
    for (let j = 1; j <= n; j++) {
      if (a[i - 1] === b[j - 1]) {
        cur[j] = prev[j - 1] + 1;
        if (cur[j] > max) max = cur[j];
      }
    }
    prev = cur;
  }
  return max;
}

/**
 * 综合相关性分数：取 token 覆盖率与最长公共子串占比的最大值。
 * 注意：LCS 长度必须 ≥ 3 才视为有效（避免 2 字短词碰撞，如"大江"撞"大江大河"）。
 */
function combinedRelevance(result, tokens, normalizedQuery) {
  const text = (
    String(result.title || "") +
    " " +
    String(result.summary || "")
  ).toLowerCase();
  const tokenCov = tokens.length
    ? tokens.filter((t) => text.includes(t)).length / tokens.length
    : 1;
  let lcsScore = 0;
  if (normalizedQuery) {
    const lcsLen = longestCommonSubstring(normalizedQuery, text);
    if (lcsLen >= 3) {
      lcsScore = lcsLen / normalizedQuery.length;
    }
  }
  return Math.max(tokenCov, lcsScore);
}

/**
 * 按相关性过滤搜索结果；不达阈值则丢弃，保证返回都是相关数据。
 * 阈值 0.4：兼顾精度与召回（剔除"大江幼儿园→大江大河"，保留"云水苑社区幼儿园"）。
 */
function filterByRelevance(results, query, threshold = 0.4) {
  const tokens = buildQueryTokens(query);
  if (tokens.length === 0 || results.length === 0) return results;
  const normalizedQuery = String(query || "")
    .toLowerCase()
    .replace(/\s+/g, "");
  const scored = results.map((r) => ({
    r,
    s: combinedRelevance(r, tokens, normalizedQuery),
  }));
  return scored
    .filter((x) => x.s >= threshold)
    .sort((a, b) => b.s - a.s)
    .map((x) => x.r);
}

function mergeResults(primary, fallback, maxResults) {
  const seenUrls = new Set();
  const seenTitles = new Set();
  const out = [];
  for (const list of [primary, fallback]) {
    for (const r of list) {
      const urlKey = (r.url || "").trim().toLowerCase();
      const titleKey = (r.title || "").trim().toLowerCase();
      if (!urlKey) continue;
      if (seenUrls.has(urlKey)) continue;
      // 标题完全相同也视为重复（不同 sogou /link 可能指向同一站点）
      if (titleKey && seenTitles.has(titleKey)) continue;
      seenUrls.add(urlKey);
      if (titleKey) seenTitles.add(titleKey);
      out.push(r);
      if (out.length >= maxResults) return out;
    }
  }
  return out;
}

function parseCliArgs(args) {
  let query = "";
  let num = 10;
  let output = "";
  let resolveRealUrl = false;
  let engine = "auto";

  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a === "-n" || a === "--num") {
      num = parseInt(args[i + 1]) || 10;
      i++;
    } else if (a === "-o" || a === "--output") {
      output = args[i + 1] || "";
      i++;
    } else if (a === "-r" || a === "--resolve-url") {
      resolveRealUrl = true;
    } else if (a === "--engine") {
      engine = (args[i + 1] || "auto").toLowerCase();
      i++;
    } else if (a.startsWith("--engine=")) {
      engine = a.slice("--engine=".length).toLowerCase();
    } else if (!a.startsWith("-")) {
      query = a;
    }
  }

  return { query, num, output, resolveRealUrl, engine };
}

/**
 * 通用搜索：搜狗 → Bing → 360 三级兜底
 */
async function searchWeb(
  query,
  maxResults = 10,
  engine = "auto",
  resolveRealUrl = false,
) {
  maxResults = Math.min(Math.max(maxResults, 1), 30);

  let sogouStatus = "skipped";
  let bingStatus = "skipped";
  let so360Status = "skipped";
  let sogouResults = [];
  let bingResults = [];
  let so360Results = [];

  // 主路：搜狗
  if (engine === "auto" || engine === "sogou") {
    try {
      console.error(`[sogou] 搜索 "${query}"...`);
      const r = await searchSogou(query, maxResults);
      sogouStatus = r.status;
      sogouResults = r.results || [];
      console.error(
        `[sogou] status=${sogouStatus}, count=${sogouResults.length}`,
      );
    } catch (e) {
      sogouStatus = "error";
      console.error(`[sogou] error: ${e.message}`);
    }
  }

  // 解析搜狗真实 URL（仅当需要时）
  if (resolveRealUrl && sogouResults.length > 0) {
    console.error("[sogou] 正在解析搜狗 /link 为真实 URL...");
    sogouResults = await resolveSogouUrls(sogouResults);
  }

  // 相关性过滤一遍 sogou，决定是否还要 Bing
  const sogouBefore = sogouResults.length;
  sogouResults = filterByRelevance(sogouResults, query);
  if (sogouBefore !== sogouResults.length) {
    console.error(
      `[filter] sogou ${sogouBefore} -> ${sogouResults.length} (剔除不相关)`,
    );
  }

  const minOk = Math.min(maxResults, 3);
  const needBing =
    engine === "bing" || (engine === "auto" && sogouResults.length < minOk);

  if (needBing && engine !== "sogou" && engine !== "so360") {
    try {
      console.error(`[bing] 搜索 "${query}"...`);
      const r = await searchBing(query, maxResults);
      bingStatus = r.status;
      bingResults = r.results || [];
      console.error(`[bing] status=${bingStatus}, count=${bingResults.length}`);
    } catch (e) {
      bingStatus = "error";
      console.error(`[bing] error: ${e.message}`);
    }
  }

  const bingBefore = bingResults.length;
  bingResults = filterByRelevance(bingResults, query);
  if (bingBefore !== bingResults.length) {
    console.error(
      `[filter] bing ${bingBefore} -> ${bingResults.length} (剔除不相关)`,
    );
  }

  // 第三兜底：360。仅当 sogou+bing 过滤后总数仍不足 minOk 才走
  const combinedSoFar = sogouResults.length + bingResults.length;
  const needSo360 =
    engine === "so360" || (engine === "auto" && combinedSoFar < minOk);

  if (needSo360 && engine !== "sogou" && engine !== "bing") {
    try {
      console.error(`[so360] 搜索 "${query}"...`);
      const r = await searchSo360(query, maxResults);
      so360Status = r.status;
      so360Results = r.results || [];
      console.error(
        `[so360] status=${so360Status}, count=${so360Results.length}`,
      );
    } catch (e) {
      so360Status = "error";
      console.error(`[so360] error: ${e.message}`);
    }
  }

  const so360Before = so360Results.length;
  so360Results = filterByRelevance(so360Results, query);
  if (so360Before !== so360Results.length) {
    console.error(
      `[filter] so360 ${so360Before} -> ${so360Results.length} (剔除不相关)`,
    );
  }

  let merged;
  if (engine === "sogou") {
    merged = sogouResults.slice(0, maxResults);
  } else if (engine === "bing") {
    merged = bingResults.slice(0, maxResults);
  } else if (engine === "so360") {
    merged = so360Results.slice(0, maxResults);
  } else {
    merged = mergeResults(sogouResults, bingResults, maxResults);
    if (merged.length < maxResults) {
      merged = mergeResults(merged, so360Results, maxResults);
    }
  }

  const usedEngines = new Set(merged.map((r) => r.engine));
  let engineUsed = "none";
  if (usedEngines.size > 0) {
    engineUsed = ["sogou", "bing", "so360"]
      .filter((e) => usedEngines.has(e))
      .join("+");
  }

  return {
    query,
    engine_used: engineUsed,
    sogou_status: sogouStatus,
    bing_status: bingStatus,
    so360_status: so360Status,
    total: merged.length,
    results: merged,
  };
}

async function main() {
  const args = process.argv.slice(2);
  const { query, num, output, resolveRealUrl, engine } = parseCliArgs(args);

  if (!query) {
    console.log(`
通用联网搜索工具（搜狗 + Bing 双通道，HTTP 版）

用法:
  node search_web.js <关键词> [选项]

选项:
  -n, --num <数量>       返回结果数量（默认 10，最大 30）
  -o, --output <文件>    输出 JSON 文件路径
  -r, --resolve-url      解析搜狗 /link?url=... 为真实地址
  --engine <auto|sogou|bing|so360>  搜索引擎选择，默认 auto（搜狗→Bing→360 三级兑底）

示例:
  node search_web.js "2026年最新 AI 政策" -n 15
  node search_web.js "深圳实验学校 招生" -o result.json
  node search_web.js "ChatGPT" --engine bing
  node search_web.js "清华大学 自主招生" -r
`);
    process.exit(0);
  }

  if (!["auto", "sogou", "bing", "so360"].includes(engine)) {
    console.error(`未知 engine: ${engine}（应为 auto/sogou/bing/so360）`);
    process.exit(1);
  }

  try {
    const result = await searchWeb(query, num, engine, resolveRealUrl);
    const jsonOutput = JSON.stringify(result, null, 2);

    if (output) {
      const fs = require("fs");
      fs.writeFileSync(output, jsonOutput, "utf-8");
      console.error(`结果已保存到: ${output}`);
    }

    console.log(jsonOutput);
  } catch (error) {
    console.error("搜索失败:", error.message);
    process.exit(1);
  }
}

module.exports = { searchWeb, searchSogou, searchBing, searchSo360 };

if (require.main === module) {
  main();
}
