/* ================================================
 * 実装したUIが動くか確認用のCS
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
///  実装したUIが動くか確認用のCS
/// </summary>
public class CS_TestUI : MonoBehaviour
{
    [Header("Initializer(ここからPresenterを取る)")][SerializeField] private CS_UIHpMVPInit _initializer;
    [Header("キー設定")][SerializeField] private KeyCode _testKey;

    void Update()
    {
        if(Input.GetKeyDown(_testKey))
        {
            _initializer.Presenter.Heal(10);
        }
    }
}
