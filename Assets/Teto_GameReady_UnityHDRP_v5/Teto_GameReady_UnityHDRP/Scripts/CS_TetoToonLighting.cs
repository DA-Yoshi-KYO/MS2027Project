/* ================================================
 * テトのシェーダーに照明と頭の向きを渡す
 * 制作者：吉本竜
 * 2026-09-25 | 初回作成
 * ================================================ */
using UnityEngine;

namespace MS2027.Teto
{
    /// <summary>
    /// 頭の回転と主光源を共有マテリアルを書き換えずに描画へ反映する。
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CS_TetoToonLighting : MonoBehaviour
    {
        [Tooltip("陰影の方向を決めるDirectional Light。")]
        public Light keyLight;
        [Tooltip("顔の陰影を追従させる頭ボーン。")]
        public Transform head;
        [Tooltip("輪郭線以外の本体Renderer。")]
        public Renderer[] targets;
        private MaterialPropertyBlock properties;

        /// <summary>
        /// アニメーション更新後の頭の姿勢を描画直前に反映する。
        /// </summary>
        private void LateUpdate()
        {
            if (targets == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            var direction = keyLight ? -keyLight.transform.forward : new Vector3(-0.5f,0.7f,-0.5f).normalized;
            Transform face = head ? head : transform;
            foreach (var target in targets)
            {
                if (!target) continue;
                target.GetPropertyBlock(properties);
                properties.SetVector("_TetoLightDirection", direction);
                // このFBXの顔の正面はローカル-Z方向。
                properties.SetVector("_TetoHeadForward", -face.forward);
                properties.SetVector("_TetoHeadRight", face.right);
                target.SetPropertyBlock(properties);
            }
        }
    }
}
