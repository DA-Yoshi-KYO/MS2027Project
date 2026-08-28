// Confluenceページが更新された際、ページ内の「変更履歴」見出し直下にある表を
// 更新前後のバージョンで比較し、新しく追加された行・内容が変更された行をDiscordに通知する。
//
// 必要な環境変数:
//   CONFLUENCE_BASE_URL   例: https://summervacationgamejam.atlassian.net
//   CONFLUENCE_EMAIL      APIトークンを発行したAtlassianアカウントのメールアドレス
//   CONFLUENCE_API_TOKEN  https://id.atlassian.com/manage-profile/security/api-tokens で発行
//   CONFLUENCE_PAGE_ID    通知対象のページID(Confluence Automationから渡される)
//   DISCORD_WEBHOOK_URL   通知先のDiscord Webhook URL
//   CHANGELOG_HEADING     変更履歴表の直前にある見出しの文字列(既定値: "仕様書の変更ログ")
//
// 実装メモ:
// - Confluence REST API v1 の `content` エンドポイントで過去バージョンの本文を取得するには、
//   `version` パラメータと合わせて `status=historical` を指定する必要がある(実機で確認済み。
//   `status=any` では "Only historical content can be fetched by version" エラーになる)。
// - 表の「新規/変更行」判定は、行内容(セルの結合文字列)を多重集合として比較する方式。
//   直前バージョンの行内容と一致しない行(新規追加・既存行の文字変更・途中への挿入を含む)を
//   通知対象とする。行の削除や、内容が全く同じままの並び替えは通知対象にならない。

const REQUIRED = [
  "CONFLUENCE_BASE_URL",
  "CONFLUENCE_EMAIL",
  "CONFLUENCE_API_TOKEN",
  "CONFLUENCE_PAGE_ID",
  "DISCORD_WEBHOOK_URL",
];

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`環境変数 ${name} が設定されていません`);
  }
  return value;
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

// 見出しテキストの直後にある最初の <table>...</table> を取り出す
function extractSectionTable(html, headingText) {
  const headingRe = /<h[1-6][^>]*>(.*?)<\/h[1-6]>/gis;
  let match;
  let sectionStart = -1;
  while ((match = headingRe.exec(html)) !== null) {
    if (stripTags(match[1]) === headingText) {
      sectionStart = match.index + match[0].length;
      break;
    }
  }
  if (sectionStart === -1) {
    return null;
  }

  const tableStart = html.indexOf("<table", sectionStart);
  if (tableStart === -1) {
    return null;
  }
  const tableEndTagIdx = html.indexOf("</table>", tableStart);
  if (tableEndTagIdx === -1) {
    return null;
  }
  return html.slice(tableStart, tableEndTagIdx + "</table>".length);
}

// <tr> ごとに { isHeader, cells } を返す。<th> を含む行はヘッダー行とみなす
function parseTableRows(tableHtml) {
  const rows = [];
  const rowRe = /<tr[^>]*>(.*?)<\/tr>/gis;
  let rowMatch;
  while ((rowMatch = rowRe.exec(tableHtml)) !== null) {
    const rowHtml = rowMatch[1];
    const cellRe = /<t[hd][^>]*>(.*?)<\/t[hd]>/gis;
    const cells = [];
    let isHeader = false;
    let cellMatch;
    while ((cellMatch = cellRe.exec(rowHtml)) !== null) {
      cells.push(stripTags(cellMatch[1]));
    }
    if (/<th[\s>]/i.test(rowHtml)) {
      isHeader = true;
    }
    if (cells.length > 0) {
      rows.push({ isHeader, cells });
    }
  }
  return rows;
}

async function fetchPageVersion({ baseUrl, headers, pageId, version }) {
  const versionParam = version ? `&version=${version}&status=historical` : "";
  const url = `${baseUrl}/wiki/rest/api/content/${pageId}?expand=body.storage,version,space${versionParam}`;
  const res = await fetch(url, { headers });
  if (!res.ok) {
    throw new Error(`${url} -> HTTP ${res.status}: ${await res.text()}`);
  }
  return res.json();
}

function rowKey(cells) {
  return cells.join("");
}

async function postToDiscord({ webhookUrl, pageTitle, pageUrl, headerRow, newRows }) {
  const labels = headerRow ? headerRow.cells : null;

  const blocks = newRows.map((row) => {
    if (labels && labels.length === row.cells.length) {
      return row.cells.map((v, i) => `**${labels[i]}**: ${v || "(空欄)"}`).join("\n");
    }
    return row.cells.join(" / ");
  });

  const content =
    `📝 **${pageTitle}** の変更履歴に新規/変更行があります\n${pageUrl}\n\n` +
    blocks.map((b) => `---\n${b}`).join("\n");

  const res = await fetch(webhookUrl, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ content: content.slice(0, 1900) }),
  });
  if (!res.ok) {
    throw new Error(`Discordへの送信に失敗しました: HTTP ${res.status} ${await res.text()}`);
  }
}

async function main() {
  for (const name of REQUIRED) requireEnv(name);

  const baseUrl = process.env.CONFLUENCE_BASE_URL;
  const email = process.env.CONFLUENCE_EMAIL;
  const apiToken = process.env.CONFLUENCE_API_TOKEN;
  const pageId = process.env.CONFLUENCE_PAGE_ID;
  const webhookUrl = process.env.DISCORD_WEBHOOK_URL;
  const heading = process.env.CHANGELOG_HEADING || "仕様書の変更ログ";

  const auth = Buffer.from(`${email}:${apiToken}`).toString("base64");
  const headers = { Authorization: `Basic ${auth}`, Accept: "application/json" };

  console.log(`ページ ${pageId} の現在バージョンを取得します...`);
  const current = await fetchPageVersion({ baseUrl, headers, pageId });
  const currentVersion = current.version.number;
  console.log(`現在バージョン: ${currentVersion}`);

  if (currentVersion <= 1) {
    console.log("初版のため比較対象の旧バージョンがありません。終了します。");
    return;
  }

  const previous = await fetchPageVersion({
    baseUrl,
    headers,
    pageId,
    version: currentVersion - 1,
  });

  const currentHtml = current.body.storage.value;
  const previousHtml = previous.body.storage.value;

  const currentTable = extractSectionTable(currentHtml, heading);
  const previousTable = extractSectionTable(previousHtml, heading);

  if (!currentTable) {
    console.log(`見出し「${heading}」の表が現在のページに見つかりませんでした。終了します。`);
    return;
  }

  const currentRows = parseTableRows(currentTable);
  const previousRows = previousTable ? parseTableRows(previousTable) : [];

  const currentHeader = currentRows.find((r) => r.isHeader) || null;
  const currentData = currentRows.filter((r) => !r.isHeader);
  const previousData = previousRows.filter((r) => !r.isHeader);

  console.log(`現在の行数: ${currentData.length} / 直前バージョンの行数: ${previousData.length}`);

  // 直前バージョンの行内容を多重集合(出現回数つき)として持ち、現在の各行が
  // 直前バージョンの「未消費の」同一内容行と一致するかどうかを順に判定する。
  // 一致すれば「そのまま残っていた行」として消費し、一致しなければ
  // 「新規に追加された行」または「内容が変更された行」とみなす。
  // これにより、末尾への追加だけでなく、既存行の文字変更や途中への挿入も拾える。
  const previousKeyCounts = new Map();
  for (const r of previousData) {
    const k = rowKey(r.cells);
    previousKeyCounts.set(k, (previousKeyCounts.get(k) || 0) + 1);
  }

  const newRows = [];
  for (const r of currentData) {
    const k = rowKey(r.cells);
    const remaining = previousKeyCounts.get(k) || 0;
    if (remaining > 0) {
      previousKeyCounts.set(k, remaining - 1);
    } else {
      newRows.push(r);
    }
  }

  if (newRows.length === 0) {
    console.log("新規または変更された行はありませんでした。");
    return;
  }

  const pageUrl = `${current._links.base}${current._links.webui}`;

  console.log(`新規行 ${newRows.length} 件をDiscordに通知します。`);
  await postToDiscord({
    webhookUrl,
    pageTitle: current.title,
    pageUrl,
    headerRow: currentHeader,
    newRows,
  });
  console.log("通知しました。");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
