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

    private Outline cachedOutline;
    private RectTransform cachedRectTransform;
    private Transform cachedTransform;

    private void Awake()
    {
        cachedOutline = GetComponent<Outline>();
        cachedRectTransform = GetComponent<RectTransform>();
        cachedTransform = transform;

        if (img == null)
            img = GetComponent<Image>();
    }

    public void SetOutline(bool active)
    {
        if (cachedOutline != null)
            cachedOutline.enabled = active;
    }

    /// <summary>
    /// Cập nhật vị trí của block theo targetPosition.
    /// Chỉ reset scale nếu đang lệch đáng kể để tránh đạp vào visual state khác.
    /// </summary>
    public void UpdateTransform(float duration = 0.05f, bool resetScale = true)
    {
        if (cachedRectTransform == null)
            return;

        cachedRectTransform.DOKill(false);

        cachedRectTransform
            .DOAnchorPos(targetPosition, duration)
            .SetEase(Ease.OutCubic);

        if (resetScale && cachedTransform != null)
        {
            if ((cachedTransform.localScale - Vector3.one).sqrMagnitude > 0.0001f)
            {
                cachedTransform.DOKill(false);
                cachedTransform.DOScale(Vector3.one, duration);
            }
        }
    }
}