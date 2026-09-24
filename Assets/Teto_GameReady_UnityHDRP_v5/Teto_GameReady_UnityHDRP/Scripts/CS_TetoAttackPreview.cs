/* ================================================
 * テトの待機と攻撃をプレビューする
 * 制作者：吉本竜
 * 2026-09-25 | 初回作成
 * ================================================ */
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MS2027.Teto
{
    /// <summary>
    /// 専用のGenericボーンアニメーションを再生し、攻撃後は構えに戻す。
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animation))]
    public sealed class CS_TetoAttackPreview : MonoBehaviour
    {
        [Tooltip("有効にすると、再生開始後に攻撃を一定間隔で繰り返す。")]
        public bool automatic = true;
        [Min(1.5f), Tooltip("自動攻撃の間隔（秒）。")]
        public float interval = 3.2f;
        private Animation player;
        private float nextAttack;
        private float returnAt = -1f;

        /// <summary>
        /// 待機姿勢を開始し、少し間を置いて最初の攻撃を行う。
        /// </summary>
        private void OnEnable()
        {
            player = GetComponent<Animation>();
            if (player["Teto_Idle"] != null) player.Play("Teto_Idle");
            nextAttack = Time.time + 0.8f;
            returnAt = -1f;
        }

        /// <summary>
        /// Spaceキーまたは自動再生で攻撃し、終了後に待機へ滑らかに戻す。
        /// </summary>
        private void Update()
        {
            if (returnAt >= 0f && Time.time >= returnAt)
            {
                player.CrossFade("Teto_Idle", 0.15f);
                returnAt = -1f;
            }
            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            pressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pressed = Input.GetKeyDown(KeyCode.Space);
#endif
            if (pressed || (automatic && Time.time >= nextAttack)) Attack();
        }

        /// <summary>
        /// 右の踏み込みパンチを一度再生する。攻撃中の連打は無視する。
        /// </summary>
        [ContextMenu("攻撃を再生")]
        public void Attack()
        {
            if (!Application.isPlaying || !player || returnAt >= 0f || player["Teto_Attack"] == null) return;
            player.CrossFade("Teto_Attack", 0.08f);
            returnAt = Time.time + player["Teto_Attack"].length - 0.1f;
            nextAttack = Time.time + interval;
        }
    }
}
