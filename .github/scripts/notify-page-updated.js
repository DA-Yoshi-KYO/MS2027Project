// Confluenceページが更新されたら、
// 1) 更新時に入力された「変更の概要」(version.message)をDiscordに通知し、
// 2) 専用の「変更履歴」ページの表に1行自動追記する。
//
// Confluenceの編集画面で「更新」を押すと出てくる公開ダイアログには
// 「変更の概要を追加(任意)」という入力欄があり、そこに入力した内容が
// REST APIの version.message として取得できる。
//
// 対象フォルダ配下かどうかの判定は、Confluence Automation側のCQL条件(検索インデックス
// 経由で遅延が必要になる)ではなく、ここでページIDから直接取得した ancestors で行う。
// 直接GETは検索インデックスを経由しないため、保存直後でも正確に判定でき、
// Automation側の遅延をほぼ無くせる。
// 「変更履歴」ページ自身の編集(この処理自身の書き込みが引き起こす再トリガー)も
// ここで弾くので、Automation側でIDを除外するCQL条件も不要。
//
// 必要な環境変数:
//   CONFLUENCE_BASE_URL          例: https://xxxx.atlassian.net
//   CONFLUENCE_EMAIL             APIトークンを発行したAtlassianアカウントのメールアドレス
//   CONFLUENCE_API_TOKEN         https://id.atlassian.com/manage-profile/security/api-tokens で発行
//   CONFLUENCE_PAGE_ID           通知対象のページID(Confluence Automationから渡される)
//   CONFLUENCE_FOLDER_ID         監視対象フォルダのページID(この配下のページだけ処理する)
//   CONFLUENCE_CHANGELOG_PAGE_ID 変更履歴ページのID(表に行を追記する対象。処理対象からは除外される)
//   DISCORD_WEBHOOK_URL          通知先のDiscord Webhook URL

const REQUIRED = [
  "CONFLUENCE_BASE_URL",
  "CONFLUENCE_EMAIL",
  "CONFLUENCE_API_TOKEN",
  "CONFLUENCE_PAGE_ID",
  "CONFLUENCE_FOLDER_ID",
  "CONFLUENCE_CHANGELOG_PAGE_ID",
  "DISCORD_WEBHOOK_URL",
];

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`環境変数 ${name} が設定されていません`);
  }
  return value;
}

function escapeHtml(text) {
  return String(text)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function stripTags(html) {
  return html
    .replace(/<[^>]*>/g, "")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/\s+/g, " ")
    .trim();
}

// <tr> ごとに { raw, isHeader, cells: [{html, text}] } を返す
function parseTableRows(tableHtml) {
  const rows = [];
  const rowRe = /<tr[^>]*>([\s\S]*?)<\/tr>/gi;
  let rowMatch;
  while ((rowMatch = rowRe.exec(tableHtml)) !== null) {
    const raw = rowMatch[0];
    const inner = rowMatch[1];
    const isHeader = /<th[\s>]/i.test(raw);
    const cellRe = /<t[hd][^>]*>([\s\S]*?)<\/t[hd]>/gi;
    const cells = [];
    let cellMatch;
    while ((cellMatch = cellRe.exec(inner)) !== null) {
      cells.push({ html: cellMatch[1], text: stripTags(cellMatch[1]) });
    }
    rows.push({ raw, isHeader, cells });
  }
  return rows;
}

// Confluenceの version.when (タイムゾーン付きISO文字列) から "8/29(土)" 形式を作る
function formatDateJa(whenIso) {
  const match = whenIso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (!match) return whenIso;
  const [, y, mo, d] = match;
  const weekday = ["日", "月", "火", "水", "木", "金", "土"][
    new Date(Date.UTC(Number(y), Number(mo) - 1, Number(d))).getUTCDay()
  ];
  return `${Number(mo)}/${Number(d)}(${weekday})`;
}

async function fetchPage({ baseUrl, headers, pageId, expand }) {
  const url = `${baseUrl}/wiki/rest/api/content/${pageId}?expand=${expand}`;
  const res = await fetch(url, { headers });
  if (!res.ok) {
    throw new Error(`${url} -> HTTP ${res.status}: ${await res.text()}`);
  }
  return res.json();
}

function buildDiscordPayload({ page, pageUrl }) {
  const editor = page.version?.by?.displayName ?? "不明";
  const summary = (page.version?.message ?? "").trim();
  const isFirstVersion = (page.version?.number ?? 0) <= 1;
  const verb = isFirstVersion ? "公開" : "更新";
  const fields = [
    { name: "ページ", value: page.title, inline: true },
    { name: "更新者", value: editor, inline: true },
    { name: "更新日時", value: page.version?.when ?? "不明", inline: true },
  ];

  if (summary) {
    fields.push({ name: "変更の概要", value: summary, inline: false });
  } else if (!isFirstVersion) {
    fields.push({ name: "変更の概要", value: "(未入力です)", inline: false });
  }

  return {
    embeds: [
      {
        title: `📝 ページが${verb}されました: ${page.title}`,
        url: pageUrl,
        color: 3447003,
        fields,
      },
    ],
  };
}

async function postToDiscord({ webhookUrl, payload }) {
  const res = await fetch(webhookUrl, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    throw new Error(`Discordへの送信に失敗しました: HTTP ${res.status} ${await res.text()}`);
  }
}

const CHANGELOG_HEADER_ROW = "<tr><th>日付</th><th>担当者</th><th>ページ</th><th>備考</th></tr>";

// tbody(あれば)の直前、無ければtableの直前に文字列を挿入する
function insertBeforeTableClose(tableHtml, insertHtml) {
  const closeTag = tableHtml.includes("</tbody>") ? "</tbody>" : "</table>";
  const idx = tableHtml.lastIndexOf(closeTag);
  return tableHtml.slice(0, idx) + insertHtml + tableHtml.slice(idx);
}

// 変更履歴ページの最初の表に1行追記する。表がまだ無ければヘッダー付きで新規作成する。
// 同じ日付・担当者・ページの行が既にあれば、新しい行は作らずその行の備考欄に追記する。
async function appendChangelogRow({ baseUrl, headers, changelogPageId, row }) {
  const changelogPage = await fetchPage({
    baseUrl,
    headers,
    pageId: changelogPageId,
    expand: "body.storage,version",
  });

  const html = changelogPage.body.storage.value;
  const summaryText = row.summary || "";
  const newRowHtml =
    "<tr>" +
    `<td>${escapeHtml(row.date)}</td>` +
    `<td>${escapeHtml(row.editor)}</td>` +
    `<td><a href="${row.pageUrl}">${escapeHtml(row.pageTitle)}</a></td>` +
    `<td>${escapeHtml(summaryText)}</td>` +
    "</tr>";

  const tableStart = html.indexOf("<table");
  const tableEndTagIdx = tableStart === -1 ? -1 : html.indexOf("</table>", tableStart);

  let newHtml;
  if (tableStart === -1 || tableEndTagIdx === -1) {
    // 表がまだ無いページ: ヘッダー付きの表を新規作成して末尾に追加する
    console.log("変更履歴ページに表が無いため、ヘッダー付きの表を新規作成します。");
    const newTableHtml = `<table><tbody>${CHANGELOG_HEADER_ROW}${newRowHtml}</tbody></table>`;
    newHtml = html + newTableHtml;
  } else {
    const tableEnd = tableEndTagIdx + "</table>".length;
    const tableHtml = html.slice(tableStart, tableEnd);

    const rows = parseTableRows(tableHtml);
    const existing = rows.find(
      (r) =>
        !r.isHeader &&
        r.cells.length >= 4 &&
        r.cells[0].text === row.date &&
        r.cells[1].text === row.editor &&
        r.cells[2].text === row.pageTitle
    );

    let updatedTableHtml;
    if (existing) {
      const mergedRemarksHtml = summaryText
        ? `${existing.cells[3].html}<br/>${escapeHtml(summaryText)}`
        : existing.cells[3].html;
      const mergedRowHtml =
        "<tr>" +
        `<td>${existing.cells[0].html}</td>` +
        `<td>${existing.cells[1].html}</td>` +
        `<td>${existing.cells[2].html}</td>` +
        `<td>${mergedRemarksHtml}</td>` +
        "</tr>";
      updatedTableHtml = tableHtml.replace(existing.raw, mergedRowHtml);
    } else {
      updatedTableHtml = insertBeforeTableClose(tableHtml, newRowHtml);
    }

    newHtml = html.slice(0, tableStart) + updatedTableHtml + html.slice(tableEnd);
  }

  const res = await fetch(`${baseUrl}/wiki/rest/api/content/${changelogPageId}`, {
    method: "PUT",
    headers: { ...headers, "Content-Type": "application/json" },
    body: JSON.stringify({
      version: { number: changelogPage.version.number + 1 },
      type: "page",
      title: changelogPage.title,
      body: { storage: { value: newHtml, representation: "storage" } },
    }),
  });
  if (!res.ok) {
    throw new Error(`変更履歴ページへの追記に失敗しました: HTTP ${res.status} ${await res.text()}`);
  }
}

async function main() {
  for (const name of REQUIRED) requireEnv(name);

  const baseUrl = process.env.CONFLUENCE_BASE_URL;
  const email = process.env.CONFLUENCE_EMAIL;
  const apiToken = process.env.CONFLUENCE_API_TOKEN;
  const pageId = process.env.CONFLUENCE_PAGE_ID;
  const folderId = process.env.CONFLUENCE_FOLDER_ID;
  const changelogPageId = process.env.CONFLUENCE_CHANGELOG_PAGE_ID;
  const webhookUrl = process.env.DISCORD_WEBHOOK_URL;

  const auth = Buffer.from(`${email}:${apiToken}`).toString("base64");
  const headers = { Authorization: `Basic ${auth}`, Accept: "application/json" };

  if (pageId === changelogPageId) {
    console.log("変更履歴ページ自身の編集のため、処理をスキップします(無限ループ防止)。");
    return;
  }

  console.log(`ページ ${pageId} の情報を取得します...`);
  const page = await fetchPage({
    baseUrl,
    headers,
    pageId,
    expand: "version,history,space,ancestors",
  });
  const pageUrl = `${page._links.base}${page._links.webui}`;

  const ancestorIds = (page.ancestors ?? []).map((a) => String(a.id));
  if (!ancestorIds.includes(String(folderId))) {
    console.log(`ページ ${pageId} は監視対象フォルダ(${folderId})の配下ではないため、処理をスキップします。`);
    return;
  }

  const payload = buildDiscordPayload({ page, pageUrl });
  console.log(`Discordに通知します: ${page.title}`);
  await postToDiscord({ webhookUrl, payload });
  console.log("Discordへ通知しました。");

  const summary = (page.version?.message ?? "").trim();
  const isFirstVersion = (page.version?.number ?? 0) <= 1;
  // 公開(初版)時は概要を入力する欄が無いため、備考の既定値として「ページ作成」を使う
  const changelogSummary = summary || (isFirstVersion ? "ページ作成" : "");

  try {
    await appendChangelogRow({
      baseUrl,
      headers,
      changelogPageId,
      row: {
        date: formatDateJa(page.version.when),
        editor: page.version?.by?.displayName ?? "不明",
        pageUrl,
        pageTitle: page.title,
        summary: changelogSummary,
      },
    });
    console.log("変更履歴ページに追記しました。");
  } catch (err) {
    // Discord通知は既に成功しているので、ここで失敗してもジョブ全体は失敗として
    // 検知できるようにログを残しつつ、後続処理があれば止めない。
    console.error("変更履歴ページへの追記でエラーが発生しました:", err);
    process.exitCode = 1;
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
