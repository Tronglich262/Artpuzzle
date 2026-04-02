using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class Block : MonoBehaviour
{
    public Image img;
    public Vector2Int gridPos;
    public Vector2Int correctPos;

    [Header("Dark Borders")]
    public Image topDark;
    public Image bottomDark;
    public Image leftDark;
    public Image rightDark;

    [Header("Light Borders")]
    public Image topLight;
    public Image bottomLight;
    public Image leftLight;
    public Image rightLight;

    [Header("Fill Inset")]
    [SerializeField] private float borderInset = 3f;

    [HideInInspector] public BlockGroup group;
    [HideInInspector] public Vector2 targetPosition;

    private RectTransform cachedRectTransform;
    private Transform cachedTransform;
    private RectTransform fillRect;

    private void Awake()
    {
        cachedRectTransform = GetComponent<RectTransform>();
        cachedTransform = transform;

        if (img == null)
            img = transform.Find("Fill")?.GetComponent<Image>() ?? transform.Find("fill")?.GetComponent<Image>();

        if (img != null)
            fillRect = img.rectTransform;
    }

    public enum Edge
    {
        Top,
        Bottom,
        Left,
        Right
    }

    public void MarkBorderDirty()
    {
        RefreshBorders(true);
    }

    public void ShowAllBorders(bool instant = false)
    {
        SetBorders(true, true, true, true, instant);
    }

    public void SetBorders(bool showTop, bool showBottom, bool showLeft, bool showRight, bool instant = false)
    {
        SetEdgePair(topDark, topLight, showTop, instant);
        SetEdgePair(bottomDark, bottomLight, showBottom, instant);
        SetEdgePair(leftDark, leftLight, showLeft, instant);
        SetEdgePair(rightDark, rightLight, showRight, instant);

        UpdateFillInset(showTop, showBottom, showLeft, showRight);
    }

    public void SetEdge(Edge edge, bool visible, bool instant = false)
    {
        switch (edge)
        {
            case Edge.Top:
                SetEdgePair(topDark, topLight, visible, instant);
                break;
            case Edge.Bottom:
                SetEdgePair(bottomDark, bottomLight, visible, instant);
                break;
            case Edge.Left:
                SetEdgePair(leftDark, leftLight, visible, instant);
                break;
            case Edge.Right:
                SetEdgePair(rightDark, rightLight, visible, instant);
                break;
        }

        RefreshBorders(true);
    }

    private void SetEdgePair(Image dark, Image light, bool visible, bool instant)
    {
        SetImageVisible(dark, visible, instant);
        SetImageVisible(light, visible, instant);
    }

    private void SetImageVisible(Image image, bool visible, bool instant)
    {
        if (image == null) return;

        float targetAlpha = visible ? 1f : 0f;
        image.DOKill();

        Color c = image.color;

        if (instant)
        {
            c.a = targetAlpha;
            image.color = c;
            return;
        }

        image.DOFade(targetAlpha, 0.08f);
    }

    private void UpdateFillInset(bool showTop, bool showBottom, bool showLeft, bool showRight)
    {
        if (fillRect == null)
            return;

        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0.5f, 0.5f);

        float left = showLeft ? borderInset : 0f;
        float right = showRight ? borderInset : 0f;
        float top = showTop ? borderInset : 0f;
        float bottom = showBottom ? borderInset : 0f;

        fillRect.offsetMin = new Vector2(left, bottom);
        fillRect.offsetMax = new Vector2(-right, -top);
    }

    public void RefreshBorders(bool instant = true)
    {
        PuzzleManager puzzle = PuzzleManager.Instance;
        if (puzzle == null) return;

        Block top = puzzle.GetBlockAt(gridPos + new Vector2Int(-1, 0));
        Block bottom = puzzle.GetBlockAt(gridPos + new Vector2Int(1, 0));
        Block left = puzzle.GetBlockAt(gridPos + new Vector2Int(0, -1));
        Block right = puzzle.GetBlockAt(gridPos + new Vector2Int(0, 1));

        bool showTop = ShouldShowBorderWith(top);
        bool showBottom = ShouldShowBorderWith(bottom);
        bool showLeft = ShouldShowBorderWith(left);
        bool showRight = ShouldShowBorderWith(right);

        SetBorders(showTop, showBottom, showLeft, showRight, instant);
    }

    private bool ShouldShowBorderWith(Block neighbor)
    {
        PuzzleManager puzzle = PuzzleManager.Instance;
        if (puzzle == null) return true;
        if (neighbor == null) return true;
        if (neighbor.group != group) return true;

        return !puzzle.IsCorrectNeighbor(this, neighbor);
    }

    public void UpdateTransform(float duration = 0.05f, bool resetScale = true)
    {
        if (cachedRectTransform == null)
            return;

        cachedRectTransform.DOKill(false);

        Vector2 snappedTarget = new(
            Mathf.Round(targetPosition.x),
            Mathf.Round(targetPosition.y)
        );

        cachedRectTransform
            .DOAnchorPos(snappedTarget, duration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                cachedRectTransform.anchoredPosition = snappedTarget;
            });

        if (!resetScale || cachedTransform == null)
            return;

        if ((cachedTransform.localScale - Vector3.one).sqrMagnitude <= 0.0001f)
            return;

        cachedTransform.DOKill(false);
        cachedTransform
            .DOScale(Vector3.one, duration)
            .SetEase(Ease.OutCubic);
    }
}