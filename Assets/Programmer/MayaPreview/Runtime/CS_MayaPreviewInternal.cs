using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewInternal
 Summary: Marks objects the preview creates for itself (grid, selection lines, material ball).
          They are never listed in the Hierarchy, picked in the view or exported.
 History: 2026/10/11 New
=====================================+
*/
[DisallowMultipleComponent]
public class CS_MayaPreviewInternal : MonoBehaviour
{
    public static bool Is(Component component) =>
        component != null && component.GetComponentInParent<CS_MayaPreviewInternal>(true) != null;
}
