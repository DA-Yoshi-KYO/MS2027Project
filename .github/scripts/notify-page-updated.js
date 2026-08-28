// Confluenceページが更新されたら、更新時に入力された「変更の概要」(version.message)を
// Discordに通知する。
//
// Confluenceの編集画面で「更新」を押すと出てくる公開ダイアログには
// 「変更の概要を追加(任意)」という入力欄があり、そこに入力した内容が
// REST APIの version.message として取得できる。本文の自動差分計算はせず、
// 編集者が書いた概要をそのまま転記するだけのシンプルな仕組み。
//
// 必要な環境変数:
//   CONFLUENCE_BASE_URL   例: https://xxxx.atlassian.net
//   CONFLUENCE_EMAIL      APIトークンを発行したAtlassianアカウントのメールアドレス
//   CONFLUENCE_API_TOKEN  https://id.atlassian.com/manage-profile/security/api-tokens で発行
//   CONFLUENCE_PAGE_ID    通知対象のページID(Confluence Automationから渡される)
//   DISCORD_WEBHOOK_URL   通知先のDiscord Webhook URL

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

async function fetchPage({ baseUrl, headers, pageId }) {
  const url = `${baseUrl}/wiki/rest/api/content/${pageId}?expand=version,history,space`;
  const res = await fetch(url, { headers });
  if (!res.ok) {
    throw new Error(`${url} -> HTTP ${res.status}: ${await res.text()}`);
  }
  return res.json();
}

function buildMessage({ page, pageUrl }) {
  const editor = page.version?.by?.displayName ?? "不明";
  const summary = (page.version?.message ?? "").trim();
  const isFirstVersion = (page.version?.number ?? 0) <= 1;
  const verb = isFirstVersion ? "公開" : "更新";

  if (summary) {
    return `📝 **${page.title}** が${verb}されました(${editor})\n> ${summary}\n${pageUrl}`;
  }
  return `📝 **${page.title}** が${verb}されました(${editor})\n(変更の概要は未入力です)\n${pageUrl}`;
}

async function postToDiscord({ webhookUrl, content }) {
  const res = await fetch(webhookUrl, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ content }),
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

  const auth = Buffer.from(`${email}:${apiToken}`).toString("base64");
  const headers = { Authorization: `Basic ${auth}`, Accept: "application/json" };

  console.log(`ページ ${pageId} の情報を取得します...`);
  const page = await fetchPage({ baseUrl, headers, pageId });
  const pageUrl = `${page._links.base}${page._links.webui}`;

  const content = buildMessage({ page, pageUrl });
  console.log(`Discordに通知します: ${page.title}`);
  await postToDiscord({ webhookUrl, content });
  console.log("通知しました。");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
