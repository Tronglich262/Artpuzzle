using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class Block : MonoBehaviour
{
    public Image img;
    public Vector2Int gridPos;
    public Vector2Int correctPos;

    [HideInInspector] public BlockGroup group;
    [HideInInspector] public Vector2 targetPosition;

    // Hàm bật/tắt viền của riêng mảnh này
    public void SetOutline(bool active)
    {
        var outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = active;
    }

    public void UpdateTransform()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) return;

        float duration = 0.05f;
        rt.DOKill(true);
        transform.DOKill(true);

        rt.DOMove(targetPosition, duration)
            .SetEase(Ease.OutCubic);

        transform.DOScale(Vector3.one, duration);
    }
}