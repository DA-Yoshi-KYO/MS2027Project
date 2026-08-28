// Confluenceページが更新されたら、表の中身などは見ずに
// 「ページが更新されました」という通知だけをDiscordに送る。
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

async function postToDiscord({ webhookUrl, page, pageUrl }) {
  const editor = page.version?.by?.displayName ?? "不明";
  const when = page.version?.when ?? "";

  const payload = {
    embeds: [
      {
        title: `📝 ページが更新されました: ${page.title}`,
        url: pageUrl,
        color: 3447003,
        fields: [
          { name: "スペース", value: page.space?.name ?? page.space?.key ?? "不明", inline: true },
          { name: "更新者", value: editor, inline: true },
          { name: "更新日時", value: when, inline: true },
        ],
      },
    ],
  };

  const res = await fetch(webhookUrl, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
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

  console.log(`Discordに通知します: ${page.title}`);
  await postToDiscord({ webhookUrl, page, pageUrl });
  console.log("通知しました。");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
