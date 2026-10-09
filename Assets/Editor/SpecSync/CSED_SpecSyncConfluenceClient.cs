using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/*
 * ConfluenceのREST APIからページ本文(storage形式のHTML)を取ってくるクラス
 * 認証は各自のAtlassianアカウントのメールアドレスとAPIトークン(Basic認証)
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

public static class CSED_SpecSyncConfluenceClient
{
    // APIの返り値を読むためのクラス。JsonUtilityはJSONのキー名と変数名を合わせる必要があるため、ここだけpublicの小文字
    // 値はJsonUtilityが入れるため、「代入されていない」警告(CS0649)を出さない
#pragma warning disable 0649
    [Serializable] private class ContentResponse { public ContentBody body; }
    [Serializable] private class ContentBody { public ContentStorage storage; }
    [Serializable] private class ContentStorage { public string value; }
#pragma warning restore 0649

    public static void FetchPageHtml(string site, string pageId, string email, string token, Action<string> onSuccess, Action<string> onError)
    {
        string url = $"https://{site}/wiki/rest/api/content/{pageId}?expand=body.storage";
        UnityWebRequest request = UnityWebRequest.Get(url);
        string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(email + ":" + token));
        request.SetRequestHeader("Authorization", "Basic " + auth);
        request.SetRequestHeader("Accept", "application/json");

        // エディタでも非同期で進むので、完了時に結果を返す
        request.SendWebRequest().completed += _ =>
        {
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(GetErrorMessage(request));
                    return;
                }

                ContentResponse response = JsonUtility.FromJson<ContentResponse>(request.downloadHandler.text);
                string html = response?.body?.storage?.value;
                if (string.IsNullOrEmpty(html)) onError?.Invoke("ページの本文が空でした");
                else onSuccess?.Invoke(html);
            }
            catch (Exception e)
            {
                onError?.Invoke("返ってきた内容を読めませんでした: " + e.Message);
            }
            finally
            {
                request.Dispose();
            }
        };
    }

    private static string GetErrorMessage(UnityWebRequest request)
    {
        switch (request.responseCode)
        {
            case 401: return "認証に失敗しました。メールアドレスとAPIトークンを確認してください (401)";
            case 403: return "このページを見る権限がありません (403)";
            case 404: return "ページが見つかりません。対応表の_pageIdを確認してください (404)";
            default: return $"取得に失敗しました ({request.responseCode}): {request.error}";
        }
    }
}
