using UnityEngine;
using UnityEngine.UI;

/*
 * UIのSliderを、音量の設定(CS_SoundVolumeController)につなぐクラス
 * 設定画面の音量スライダーに付けるだけで動く
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・表示した時に、今の設定をスライダーに入れる(Sliderの範囲は0～1にしておく)
 * ・スライダーを動かすと、すぐ音量に反映する(保存はしない)。離した時ではなく、画面を閉じた時(OnDisable)にまとめて保存する
 * ・別の場所で設定が変わったら(初期値に戻すなど)、スライダーの表示も合わせる
 * ・Sliderが未設定なら、同じオブジェクトのSliderを使う
 */
// ========================================

public class CS_SoundVolumeSlider : MonoBehaviour
{
    [SerializeField] private CSE_SoundVolumeType _type = CSE_SoundVolumeType.Bgm;  // どの音量を変えるか
    [SerializeField] private Slider _slider;

    private CS_SoundVolumeController _controller;
    private bool _isChanged;    // 動かしたか(閉じる時に保存する)

    private void Awake()
    {
        if (_slider == null) _slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        _controller = CS_SoundVolumeController.instance;
        if (_slider == null || _controller == null) return;

        _isChanged = false;
        Refresh();
        _slider.onValueChanged.AddListener(HandleValueChanged);
        _controller.onVolumeChanged += Refresh;
    }

    private void OnDisable()
    {
        if (_slider != null) _slider.onValueChanged.RemoveListener(HandleValueChanged);
        if (_controller == null) return;

        _controller.onVolumeChanged -= Refresh;
        if (_isChanged) _controller.Save();
        _controller = null;
    }

    private void HandleValueChanged(float value)
    {
        _isChanged = true;
        _controller.SetVolume(_type, value, false);
    }

    // 今の設定をスライダーに入れる(onValueChangedは呼ばない)
    private void Refresh()
    {
        _slider.SetValueWithoutNotify(_controller.GetVolume(_type));
    }
}
