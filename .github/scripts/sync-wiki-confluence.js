// GitHub Wiki と Confluence の「Wiki」フォルダ配下ページを双方向に同期する。
//
// 対応づけ:
//   ページIDで管理する(状態ファイル)。Confluence側でフォルダ分けやページ移動をしても同期は壊れない。
//   状態ファイルは Wiki リポジトリ直下の .wiki-confluence-sync.json (Wikiの表示には出ない)。
//
// 判定 (ページごと):
//   状態ファイルに、最後に同期した時点の「Wikiの正規化Markdownのハッシュ」「Confluenceを
//   Markdown化したハッシュ」「Confluenceのバージョン番号」を持つ。
//     Wikiだけ変更   -> Confluence を更新
//     Confluenceだけ -> Wiki を更新
//     両方変更       -> 衝突。どちらも上書きせず通知(FORCE_DIRECTION で手動解決)
//   バージョン番号だけ上がって内容(Markdown化したもの)が同じ場合は変更なし扱い。
//
// 同期しないもの: ページの削除・タイトル変更(Wikiのファイル名変更は新規ページ扱い)。
//
// 必要な環境変数:
//   CONFLUENCE_BASE_URL / CONFLUENCE_EMAIL / CONFLUENCE_API_TOKEN / CONFLUENCE_SPACE_KEY
//   WIKI_DIR            Wiki リポジトリのチェックアウト先
// 任意:
//   DRY_RUN=true        読み取りと判定のみ。Confluence・Wiki・状態ファイルへは何も書かない
//   FORCE_DIRECTION     wiki-to-confluence | confluence-to-wiki  衝突ページをその向きで上書き
//   CONFLUENCE_WIKI_FOLDER_ID  「Wiki」フォルダのID(未指定なら状態ファイル、なければ新規作成)
//   DISCORD_WEBHOOK_URL 衝突・エラーの通知先
//   GITHUB_REPOSITORY / GITHUB_OUTPUT / GITHUB_STEP_SUMMARY (Actions が自動設定)

const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const {
  markdownToStorage,
  storageToMarkdown,
  normalizeMarkdown,
} = require("./lib/wiki-confluence-convert");

const STATE_FILE_NAME = ".wiki-confluence-sync.json";
const FOLDER_TITLE = "Wiki";
// Confluence側でスペースのホームページ等とタイトルが衝突しやすいものだけ別名にする
const TITLE_OVERRIDES = { Home: "Wiki Home" };
// Wiki のシステムページは対象外
const IGNORED_WIKI_FILES = new Set(["_Sidebar", "_Footer", "_Header"]);

function requireEnv(name) {
  const value = process.env[name];
  if (!value) throw new Error(`環境変数 ${name} が設定されていません`);
  return value;
}

const sha = (text) => crypto.createHash("sha1").update(text, "utf8").digest("hex");

// ---- Confluence API ------------------------------------------------------

function createConfluenceClient({ baseUrl, email, apiToken }) {
  const auth = Buffer.from(`${email}:${apiToken}`).toString("base64");

  async function request(method, urlPath, body) {
    const url = urlPath.startsWith("http") ? urlPath : `${baseUrl}${urlPath}`;
    for (let attempt = 1; ; attempt++) {
      const res = await fetch(url, {
        method,
        headers: {
          Authorization: `Basic ${auth}`,
          Accept: "application/json",
          ...(body ? { "Content-Type": "application/json" } : {}),
        },
        body: body ? JSON.stringify(body) : undefined,
      });
      if ((res.status === 429 || res.status >= 500) && attempt < 3) {
        await new Promise((r) => setTimeout(r, 2000 * attempt));
        continue;
      }
      if (res.status === 404) return null;
      if (!res.ok) throw new Error(`${method} ${urlPath} -> HTTP ${res.status}: ${await res.text()}`);
      return res.status === 204 ? {} : res.json();
    }
  }

  return {
    async getSpaceId(spaceKey) {
      const json = await request("GET", `/wiki/api/v2/spaces?keys=${encodeURIComponent(spaceKey)}`);
      const space = json && json.results && json.results[0];
      if (!space) throw new Error(`スペース ${spaceKey} が見つかりません`);
      return space.id;
    },
    async getFolder(id) {
      return request("GET", `/wiki/api/v2/folders/${id}`);
    },
    async createFolder(spaceId, title) {
      return request("POST", "/wiki/api/v2/folders", { spaceId, title });
    },
    async getPage(id) {
      const p = await request("GET", `/wiki/api/v2/pages/${id}?body-format=storage`);
      if (!p) return null;
      return {
        id: p.id,
        title: p.title,
        status: p.status,
        version: p.version.number,
        storage: p.body && p.body.storage ? p.body.storage.value : "",
      };
    },
    async createPage(spaceId, parentId, title, storage) {
      return request("POST", "/wiki/api/v2/pages", {
        spaceId,
        status: "current",
        title,
        parentId,
        body: { representation: "storage", value: storage },
      });
    },
    async updatePage(id, title, storage, currentVersion, message) {
      return request("PUT", `/wiki/api/v2/pages/${id}`, {
        id,
        status: "current",
        title,
        body: { representation: "storage", value: storage },
        version: { number: currentVersion + 1, message },
      });
    },
    // フォルダ配下(サブフォルダ含む)の全ページ。v2の親指定は絞り込みが不正確なためCQLを使う
    async listDescendantPageIds(folderId) {
      const ids = [];
      let url = `/wiki/rest/api/content/search?cql=${encodeURIComponent(`ancestor = ${folderId}`)}&limit=100`;
      while (url) {
        const json = await request("GET", url);
        if (!json) break;
        for (const item of json.results || []) if (item.type === "page") ids.push(item.id);
        url = json._links && json._links.next ? `${baseUrl}${json._links.next}` : null;
      }
      return ids;
    },
  };
}

// ---- 状態ファイル / Wiki ファイル ------------------------------------------

function loadState(wikiDir) {
  const file = path.join(wikiDir, STATE_FILE_NAME);
  if (!fs.existsSync(file)) return { folderId: null, pages: {} };
  const state = JSON.parse(fs.readFileSync(file, "utf8"));
  state.pages = state.pages || {};
  return state;
}

function saveState(wikiDir, state) {
  fs.writeFileSync(path.join(wikiDir, STATE_FILE_NAME), JSON.stringify(state, null, 2) + "\n", "utf8");
}

function listWikiPages(wikiDir) {
  return fs
    .readdirSync(wikiDir)
    .filter((f) => f.endsWith(".md"))
    .map((f) => f.slice(0, -3))
    .filter((n) => !IGNORED_WIKI_FILES.has(n))
    .sort();
}

const wikiFilePath = (wikiDir, name) => path.join(wikiDir, `${name}.md`);
const wikiTitleOf = (name) => TITLE_OVERRIDES[name] || name.replace(/-/g, " ");
const wikiNameOf = (title) => title.replace(/[\\/]/g, "-").replace(/\s+/g, "-");

// ---- 同期本体 ------------------------------------------------------------

async function main() {
  const baseUrl = requireEnv("CONFLUENCE_BASE_URL").replace(/\/$/, "");
  const spaceKey = requireEnv("CONFLUENCE_SPACE_KEY");
  const wikiDir = path.resolve(requireEnv("WIKI_DIR"));
  const dryRun = /^true$/i.test(process.env.DRY_RUN || "");
  const forceDirection = process.env.FORCE_DIRECTION || "";
  if (forceDirection && !["wiki-to-confluence", "confluence-to-wiki"].includes(forceDirection)) {
    throw new Error(`FORCE_DIRECTION が不正です: ${forceDirection}`);
  }

  const client = createConfluenceClient({
    baseUrl,
    email: requireEnv("CONFLUENCE_EMAIL"),
    apiToken: requireEnv("CONFLUENCE_API_TOKEN"),
  });

  const state = loadState(wikiDir);
  const report = { created: [], toWiki: [], toConfluence: [], unchanged: [], conflicts: [], warnings: [], errors: [] };
  const confluenceUrl = (id) => `${baseUrl}/wiki/spaces/${spaceKey}/pages/${id}`;
  const log = (msg) => console.log(`${dryRun ? "[dry-run] " : ""}${msg}`);

  // 書き込み後にサーバー側の実際の内容を取り直して状態に記録する
  // (Confluenceが保存時に storage を整形しても、次回以降の比較がずれないようにするため)
  async function recordConfluenceState(name, id, wikiHash) {
    const page = await client.getPage(id);
    state.pages[name] = {
      id,
      wikiHash,
      confHash: sha(normalizeMarkdown(storageToMarkdown(page.storage))),
      version: page.version,
    };
  }

  const spaceId = await client.getSpaceId(spaceKey);

  // 「Wiki」フォルダ
  let folderId = process.env.CONFLUENCE_WIKI_FOLDER_ID || state.folderId;
  if (folderId) {
    if (!(await client.getFolder(folderId))) {
      throw new Error(`Wikiフォルダ(id=${folderId})が見つかりません。削除された場合は .wiki-confluence-sync.json の folderId を消してください`);
    }
  } else if (dryRun) {
    log(`「${FOLDER_TITLE}」フォルダを新規作成します(スペース ${spaceKey})`);
  } else {
    const folder = await client.createFolder(spaceId, FOLDER_TITLE);
    folderId = folder.id;
    log(`「${FOLDER_TITLE}」フォルダを作成しました (id=${folderId})`);
  }
  state.folderId = folderId || null;

  // ---- Wiki 側のページを起点に同期 ----
  for (const name of listWikiPages(wikiDir)) {
    try {
      const file = wikiFilePath(wikiDir, name);
      const wikiMd = fs.readFileSync(file, "utf8");
      const wikiHash = sha(normalizeMarkdown(wikiMd));
      const entry = state.pages[name];

      // 新規 (Wikiにだけ存在) -> Confluence に作成
      if (!entry) {
        const title = wikiTitleOf(name);
        log(`新規: ${name} -> Confluence「${title}」`);
        if (!dryRun) {
          const created = await client.createPage(spaceId, folderId, title, markdownToStorage(wikiMd));
          await recordConfluenceState(name, created.id, wikiHash);
        }
        report.created.push(name);
        continue;
      }

      const page = await client.getPage(entry.id);
      if (!page || page.status !== "current") {
        report.warnings.push(`${name}: 対応するConfluenceページ(id=${entry.id})が見つからない/削除済みのためスキップ`);
        continue;
      }

      const confMd = normalizeMarkdown(storageToMarkdown(page.storage));
      const confHash = sha(confMd);
      const wikiChanged = wikiHash !== entry.wikiHash;
      const confChanged = page.version !== entry.version && confHash !== entry.confHash;

      // Confluenceのバージョンだけ進んだ(内容は同じ)場合は状態を追従するのみ
      if (page.version !== entry.version && !confChanged && !dryRun) entry.version = page.version;

      const pushToConfluence = async () => {
        log(`Wiki -> Confluence: ${name}`);
        if (!dryRun) {
          await client.updatePage(entry.id, page.title, markdownToStorage(wikiMd), page.version, "GitHub Wikiから同期");
          await recordConfluenceState(name, entry.id, wikiHash);
        }
        report.toConfluence.push(name);
      };
      const pullToWiki = () => {
        log(`Confluence -> Wiki: ${name}`);
        if (!dryRun) {
          fs.writeFileSync(file, confMd, "utf8");
          state.pages[name] = { id: entry.id, wikiHash: sha(normalizeMarkdown(confMd)), confHash, version: page.version };
        }
        report.toWiki.push(name);
      };

      if (wikiChanged && confChanged) {
        if (wikiHash === confHash) {
          if (!dryRun) state.pages[name] = { id: entry.id, wikiHash, confHash, version: page.version };
          report.unchanged.push(name);
        } else if (forceDirection === "wiki-to-confluence") {
          await pushToConfluence();
        } else if (forceDirection === "confluence-to-wiki") {
          pullToWiki();
        } else {
          log(`衝突: ${name}`);
          report.conflicts.push({ name, confluenceUrl: confluenceUrl(entry.id) });
        }
      } else if (wikiChanged) {
        await pushToConfluence();
      } else if (confChanged) {
        pullToWiki();
      } else {
        report.unchanged.push(name);
      }
    } catch (err) {
      report.errors.push(`${name}: ${err.message}`);
    }
  }

  // ---- Confluence 側にだけある新規ページ -> Wiki に作成 ----
  if (folderId) {
    try {
      const knownIds = new Set(Object.values(state.pages).map((p) => p.id));
      for (const id of await client.listDescendantPageIds(folderId)) {
        if (knownIds.has(id)) continue;
        const page = await client.getPage(id);
        if (!page || page.status !== "current") continue;
        const name = wikiNameOf(page.title);
        if (state.pages[name] || fs.existsSync(wikiFilePath(wikiDir, name))) {
          report.warnings.push(`Confluence「${page.title}」(id=${id}): Wikiに同名ページ ${name} があるため取り込みをスキップ`);
          continue;
        }
        const md = normalizeMarkdown(storageToMarkdown(page.storage));
        log(`新規: Confluence「${page.title}」-> Wiki ${name}`);
        if (!dryRun) {
          fs.writeFileSync(wikiFilePath(wikiDir, name), md, "utf8");
          state.pages[name] = { id, wikiHash: sha(md), confHash: sha(md), version: page.version };
        }
        report.created.push(`${name} (Confluence起点)`);
      }
    } catch (err) {
      report.errors.push(`Confluence新規ページの取り込み: ${err.message}`);
    }
  }

  // Wikiから消えたページは Confluence 側を触らず、状態だけ警告する
  const existingWiki = new Set(listWikiPages(wikiDir));
  for (const name of Object.keys(state.pages)) {
    if (!existingWiki.has(name)) {
      report.warnings.push(`${name}: Wikiにファイルがありません(削除/改名)。Confluence側は変更していません`);
    }
  }

  if (!dryRun) saveState(wikiDir, state);
  await finish(report, { dryRun, confluenceUrl });
}

// ---- 結果出力 ------------------------------------------------------------

async function finish(report, { dryRun }) {
  const lines = [
    `## Wiki ⇄ Confluence 同期結果${dryRun ? " (dry-run)" : ""}`,
    `- 新規作成: ${report.created.length ? report.created.join(", ") : "なし"}`,
    `- Wiki → Confluence: ${report.toConfluence.length ? report.toConfluence.join(", ") : "なし"}`,
    `- Confluence → Wiki: ${report.toWiki.length ? report.toWiki.join(", ") : "なし"}`,
    `- 変更なし: ${report.unchanged.length}件`,
    `- 衝突: ${report.conflicts.length ? report.conflicts.map((c) => c.name).join(", ") : "なし"}`,
  ];
  if (report.warnings.length) lines.push("", "### 警告", ...report.warnings.map((w) => `- ${w}`));
  if (report.errors.length) lines.push("", "### エラー", ...report.errors.map((e) => `- ${e}`));
  const summary = lines.join("\n");
  console.log("\n" + summary);

  if (process.env.GITHUB_STEP_SUMMARY) fs.appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary + "\n");
  if (process.env.GITHUB_OUTPUT) {
    fs.appendFileSync(
      process.env.GITHUB_OUTPUT,
      `conflicts=${report.conflicts.length}\nerrors=${report.errors.length}\nchanged=${
        report.created.length + report.toWiki.length + report.toConfluence.length
      }\n`
    );
  }

  const webhook = process.env.DISCORD_WEBHOOK_URL;
  if (!dryRun && webhook && (report.conflicts.length || report.errors.length)) {
    const repo = process.env.GITHUB_REPOSITORY || "";
    const parts = [];
    if (report.conflicts.length) {
      parts.push(
        "⚠️ **WikiとConfluenceで同じページが両方編集されたため、同期を保留しています**",
        ...report.conflicts.map(
          (c) => `- ${c.name}\n  Wiki: https://github.com/${repo}/wiki/${encodeURIComponent(c.name)}\n  Confluence: ${c.confluenceUrl}`
        ),
        "どちらかを正としてActionsの「Sync Wiki and Confluence」を手動実行し、force_direction を指定してください。"
      );
    }
    if (report.errors.length) parts.push("❌ **同期エラー**", ...report.errors.map((e) => `- ${e}`));
    try {
      await fetch(webhook, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ content: parts.join("\n").slice(0, 1900) }),
      });
    } catch (err) {
      console.error(`Discord通知に失敗: ${err.message}`);
    }
  }

  // エラーは(他のページの同期結果を保存したうえで)ジョブを失敗させて気付けるようにする
  if (report.errors.length) process.exitCode = 1;
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
