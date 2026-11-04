using UnityEngine;

/*
 * プレイヤーが近くでボタンを押して操作できるもの(ケアパッケージなど)が実装するインターフェース
 * プレイヤー側(CS_PlayerInteractor)が、近くにある操作できるものを探してBegin/Endを呼ぶ
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・操作できるものは、有効な間 CS_InteractableRegistry に登録しておく(OnEnableでRegister / OnDisableでUnregister)
 *   プレイヤー側は登録されたものの中から、範囲内で一番近いものを探す
 * ・CanInteract / IsInteracting は全マシンで呼ばれる(プレイヤーの手元で「今押したら操作が始まるか」「中断されたか」を判断するため)
 *   → 同期済みの状態(NetworkVariable)だけで判断できるようにする
 * ・BeginInteract / EndInteract はサーバー(またはオフライン)で呼ばれる
 *   中断(離れた・ダメージを受けたなど)の判定は、操作される側がサーバーで行う
 */
// ========================================

public interface IInteractable
{
    // 操作できる範囲の中心と半径(水平方向)
    Vector3 interactPosition { get; }
    float interactRange { get; }

    // interactor(プレイヤー)が今、操作を始められるか(全マシンで呼ばれる)
    bool CanInteract(GameObject interactor);

    // interactorが今、操作している最中か(全マシンで呼ばれる。中断されたらfalseになる)
    bool IsInteracting(GameObject interactor);

    // 操作を始める・やめる(サーバー、またはオフライン)
    void BeginInteract(GameObject interactor);
    void EndInteract(GameObject interactor);
}
