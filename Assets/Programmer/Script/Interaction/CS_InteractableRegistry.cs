using System.Collections.Generic;
using UnityEngine;

/*
 * シーン上の操作できるもの(IInteractable)の一覧
 * 操作できるものは有効な間ここに登録し、プレイヤー側(CS_PlayerInteractor)はここから近いものを探す
 *
 * 制作者：　中出峻輔
 */

// シーン上の操作できるものの一覧
public static class CS_InteractableRegistry
{
    private static readonly List<IInteractable> _interactables = new List<IInteractable>();

    public static IReadOnlyList<IInteractable> interactables => _interactables;

    public static void Register(IInteractable interactable)
    {
        if (!_interactables.Contains(interactable)) _interactables.Add(interactable);
    }

    public static void Unregister(IInteractable interactable)
    {
        _interactables.Remove(interactable);
    }

    // positionから操作できる範囲内で、一番近い操作できるもの(interactorが今始められるものだけ)
    public static IInteractable FindNearest(Vector3 position, GameObject interactor)
    {
        IInteractable nearest = null;
        float nearestSqr = float.MaxValue;
        foreach (IInteractable interactable in _interactables)
        {
            Vector3 offset = interactable.interactPosition - position;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr > interactable.interactRange * interactable.interactRange || sqr >= nearestSqr) continue;
            if (!interactable.CanInteract(interactor)) continue;

            nearest = interactable;
            nearestSqr = sqr;
        }
        return nearest;
    }

    // Domain Reloadオフ対策(再生ごとに一覧を空にする)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _interactables.Clear();
    }
}
