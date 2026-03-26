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

    /// <summary>
    /// Cache component để dùng nhanh hơn.
    /// </summary>
    private void Awake()
    {
        cachedOutline = GetComponent<Outline>();
        cachedRectTransform = GetComponent<RectTransform>();
        cachedTransform = transform;

        if (img == null)
            img = GetComponent<Image>();
    }

    /// <summary>
    /// Bật/tắt outline của block.
    /// </summary>
    public void SetOutline(bool active)
    {
        if (cachedOutline != null)
            cachedOutline.enabled = active;
    }

    /// <summary>
    /// Tween block tới targetPosition.
    /// Có thể reset scale về 1 nếu cần.
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