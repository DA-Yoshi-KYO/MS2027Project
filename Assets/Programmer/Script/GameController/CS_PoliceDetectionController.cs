/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-13 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警察検知UIのController。
/// </summary>
public class CS_PoliceDetectionController : MonoBehaviour
{
    [SerializeField] private CS_UIPoliceDetectionPresenter _presenter;

    private CS_UIPoliceDetectionModel _model;
    private Transform _playerTransform;

    private void Awake()
    {
        _model = new CS_UIPoliceDetectionModel();
    }

    private void Start()
    {
        if (_presenter == null)
        {
            Debug.LogError(
                "[CS_PoliceDetectionController] Presenterが未設定です。",
                this
            );
            return;
        }

        _presenter.BindModel(_model);

        if (_playerTransform != null)
            _presenter.SetPlayerTransform(_playerTransform);
    }

    public void SetPlayerTransform(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        _playerTransform = playerTransform;
        _presenter?.SetPlayerTransform(playerTransform);
    }

    public void RegisterPolice(
        string policeId,
        Transform policeTransform,
        CSE_PlayerDetectionState initialState =
            CSE_PlayerDetectionState.Safe)
    {
        if (string.IsNullOrWhiteSpace(policeId) ||
            policeTransform == null)
        {
            return;
        }

        _model.RegisterPolice(
            new CS_PoliceDetectionData(
                policeId,
                policeTransform,
                initialState
            )
        );
    }

    public void SetPoliceState(
        string policeId,
        CSE_PlayerDetectionState state)
    {
        _model.SetPoliceState(policeId, state);
    }

    public void UnregisterPolice(string policeId)
    {
        _model.UnregisterPolice(policeId);
    }

    private void OnDestroy()
    {
        _model?.Dispose();
    }
}
