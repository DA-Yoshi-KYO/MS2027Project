// GitHub Wiki(Markdown) と Confluence storage形式(XHTML)の相互変換。
//
// Markdown を正とし、Confluence 側は「Markdown で表せる範囲」のみ往復可能とする。
//   対応: 見出し / 段落 / 強調 / リスト / 表(GFM) / 引用 / コードブロック / リンク / 水平線 / 画像(<img>, ![]())
//   非対応(Confluence→Wiki 時に欠落): 文字色、パネル、目次などの Confluence マクロ
//
// 比較用の正規化(normalizeMarkdown)もここに置く。「見た目だけの差分」で同期が走らないようにするため。

const { marked } = require("marked");
const TurndownService = require("turndown");
const { gfm } = require("turndown-plugin-gfm");

marked.setOptions({ gfm: true, breaks: false });

function escapeXml(text) {
  return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function unescapeXml(text) {
  return text
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&amp;/g, "&");
}

function getAttr(tag, name) {
  const m = tag.match(new RegExp(`\\s${name}\\s*=\\s*("([^"]*)"|'([^']*)')`, "i"));
  return m ? (m[2] !== undefined ? m[2] : m[3]) : null;
}

// ---- Markdown -> storage -------------------------------------------------

function imgTagToStorage(tag) {
  const src = getAttr(tag, "src");
  if (!src) return tag;
  const attrs = [];
  const width = getAttr(tag, "width");
  const height = getAttr(tag, "height");
  const alt = getAttr(tag, "alt");
  if (width) attrs.push(`ac:width="${escapeXml(unescapeXml(width))}"`);
  if (height) attrs.push(`ac:height="${escapeXml(unescapeXml(height))}"`);
  if (alt) attrs.push(`ac:alt="${escapeXml(unescapeXml(alt))}"`);
  return `<ac:image ${attrs.join(" ")}><ri:url ri:value="${escapeXml(unescapeXml(src))}" /></ac:image>`.replace(
    "<ac:image >",
    "<ac:image>"
  );
}

function markdownToStorage(markdown) {
  let html = marked.parse(markdown.replace(/\r\n/g, "\n"));

  // コードブロック -> Confluence の code マクロ
  html = html.replace(/<pre><code(?: class="language-([^"]*)")?>([\s\S]*?)<\/code><\/pre>/g, (_, lang, body) => {
    const code = unescapeXml(body).replace(/\n$/, "").replace(/]]>/g, "]]]]><![CDATA[>");
    const langParam = lang ? `<ac:parameter ac:name="language">${escapeXml(lang)}</ac:parameter>` : "";
    return (
      `<ac:structured-macro ac:name="code" ac:schema-version="1">${langParam}` +
      `<ac:plain-text-body><![CDATA[${code}]]></ac:plain-text-body></ac:structured-macro>`
    );
  });

  // 画像 -> ac:image (外部URL)
  html = html.replace(/<img\b[^>]*>/gi, imgTagToStorage);

  // タスクリストのチェックボックスはstorage形式で無効なためテキスト化
  html = html.replace(/<input\b[^>]*type="checkbox"[^>]*>/gi, (tag) => (/\schecked/i.test(tag) ? "☑ " : "☐ "));

  // storage形式はXHTMLなので空要素は自己終了にする
  html = html.replace(/<(br|hr)\s*>/gi, "<$1 />");

  return html.trim();
}

// ---- storage -> Markdown -------------------------------------------------

function createTurndown() {
  const td = new TurndownService({
    headingStyle: "atx",
    codeBlockStyle: "fenced",
    bulletListMarker: "-",
    emDelimiter: "*",
    strongDelimiter: "**",
    hr: "---",
  });
  td.use(gfm);

  // gfmプラグイン既定の ~text~ ではなく、GitHub Wiki で一般的な ~~text~~ に揃える
  td.addRule("strikethrough", {
    filter: ["del", "s", "strike"],
    replacement: (content) => `~~${content}~~`,
  });

  // 変換前に <img data-md-img> へ置き換えた画像。Wiki側の既存記法に合わせ、
  // サイズ指定があれば <img ... /> 、なければ ![alt](src) で出力する
  td.addRule("confluenceImage", {
    filter: (node) => node.nodeName === "IMG" && node.hasAttribute("data-md-img"),
    replacement: (_content, node) => {
      const src = node.getAttribute("src");
      const width = node.getAttribute("width");
      const height = node.getAttribute("height");
      const alt = node.getAttribute("alt") || "image";
      if (!width && !height) return `![${alt}](${src})`;
      const attrs = [];
      if (width) attrs.push(`width="${width}"`);
      if (height) attrs.push(`height="${height}"`);
      attrs.push(`alt="${alt}"`, `src="${src}"`);
      return `<img ${attrs.join(" ")} />`;
    },
  });

  // 変換前に <pre data-lang> へ置き換えたコードマクロ
  td.addRule("confluenceCode", {
    filter: (node) => node.nodeName === "PRE" && node.hasAttribute("data-confluence-code"),
    replacement: (_content, node) => {
      const lang = node.getAttribute("data-lang") || "";
      const code = node.textContent.replace(/\n$/, "");
      return `\n\n\`\`\`${lang}\n${code}\n\`\`\`\n\n`;
    },
  });

  // 空になるマクロ要素(目次など)は出力しない
  td.addRule("dropUnknownMacros", {
    filter: (node) => /^ac:(structured-macro|task-list|placeholder)$/i.test(node.nodeName),
    replacement: (content) => content,
  });

  return td;
}

const turndown = createTurndown();

// blank判定で捨てられないよう、外部URL画像(ac:image + ri:url)を先に <img> へ置き換える
function prepareImages(storage) {
  return storage.replace(
    /<ac:image\b([^>]*)>\s*<ri:url\b([^>]*?)\/?>\s*(?:<\/ri:url>)?\s*<\/ac:image>/g,
    (_, imageAttrs, urlAttrs) => {
      const src = getAttr(" " + urlAttrs, "ri:value");
      if (!src) return "";
      const attrs = ["data-md-img=\"1\"", `src="${src}"`];
      for (const name of ["width", "height", "alt"]) {
        const v = getAttr(" " + imageAttrs, `ac:${name}`);
        if (v) attrs.push(`${name}="${v}"`);
      }
      return `<img ${attrs.join(" ")} />`;
    }
  );
}

function storageToMarkdown(storage) {
  // CDATA は HTML パーサではコメント扱いになり中身が消えるため、先にコードマクロを <pre> 化する
  const prepared = storage.replace(
    /<ac:structured-macro\b[^>]*ac:name="code"[^>]*>([\s\S]*?)<\/ac:structured-macro>/g,
    (_, inner) => {
      const langMatch = inner.match(/<ac:parameter\b[^>]*ac:name="language"[^>]*>([\s\S]*?)<\/ac:parameter>/);
      const bodyMatch = inner.match(/<ac:plain-text-body>\s*<!\[CDATA\[([\s\S]*?)\]\]>\s*<\/ac:plain-text-body>/);
      const lang = langMatch ? unescapeXml(langMatch[1]).trim() : "";
      const code = bodyMatch ? bodyMatch[1] : "";
      return `<pre data-confluence-code="1" data-lang="${escapeXml(lang)}">${escapeXml(code)}</pre>`;
    }
  );
  return turndown.turndown(prepareImages(prepared));
}

// ---- 比較用の正規化 ------------------------------------------------------

// 行末の2スペース(改行)は意味を持つため残し、改行コード・末尾の空白行・連続空行だけ揃える
function normalizeMarkdown(markdown) {
  return (
    markdown
      .replace(/\r\n/g, "\n")
      .replace(/\n{3,}/g, "\n\n")
      .replace(/[ \t　]+$/gm, (m) => (m.length >= 2 ? "  " : ""))
      .trim() + "\n"
  );
}

module.exports = { markdownToStorage, storageToMarkdown, normalizeMarkdown };
