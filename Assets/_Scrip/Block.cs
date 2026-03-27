using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class Block : MonoBehaviour
{
    public enum Edge
    {
        Top,
        Bottom,
        Left,
        Right
    }

    public Image img;
    public Vector2Int gridPos;
    public Vector2Int correctPos;

    [HideInInspector] public BlockGroup group;
    [HideInInspector] public Vector2 targetPosition;

    private RectTransform cachedRectTransform;
    private Transform cachedTransform;

    private Image top;
    private Image bottom;
    private Image left;
    private Image right;
    private Image topBlack;
    private Image bottomBlack;
    private Image leftBlack;
    private Image rightBlack;

    [Header("White Border")]
    [SerializeField] private float borderThickness = 3f;
    [SerializeField] private float whiteInset = 1f;
    [SerializeField] private float borderAlpha = 1f;
    [SerializeField] private float borderFadeDuration = 0.06f;
    [SerializeField] private Color borderColor = new(1f, 1f, 1f, 1f);

    [Header("Black Inner Border")]
    [SerializeField] private bool useBlackOutline = true;
    [SerializeField] private float blackBorderThickness = 2f;
    [SerializeField] private float blackInset = 1f;
    [SerializeField] private float blackBorderAlpha = 1f;
    [SerializeField] private Color blackBorderColor = new(0f, 0f, 0f, 1f);

    private Vector2 lastSize;
    private Vector3 lastScale;
    private Quaternion lastRotation;
    private bool borderDirty = true;

    private void Awake()
    {
        cachedRectTransform = GetComponent<RectTransform>();
        cachedTransform = transform;

        if (img == null)
            img = GetComponent<Image>();

        CreateBorders();
        ForceSyncBorders();
    }

    private void LateUpdate()
    {
        if (cachedRectTransform == null)
            return;

        bool changed =
            borderDirty ||
            cachedRectTransform.rect.size != lastSize ||
            cachedTransform.localScale != lastScale ||
            cachedTransform.localRotation != lastRotation;

        if (!changed)
            return;

        ForceSyncBorders();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!gameObject.activeInHierarchy)
            return;

        borderDirty = true;
    }

    public void MarkBorderDirty()
    {
        borderDirty = true;
    }

    private void CreateBorders()
    {
        top = CreateEdge("Top", borderColor, 10);
        bottom = CreateEdge("Bottom", borderColor, 10);
        left = CreateEdge("Left", borderColor, 10);
        right = CreateEdge("Right", borderColor, 10);

        if (!useBlackOutline)
            return;

        topBlack = CreateEdge("TopBlack", blackBorderColor, 11);
        bottomBlack = CreateEdge("BottomBlack", blackBorderColor, 11);
        leftBlack = CreateEdge("LeftBlack", blackBorderColor, 11);
        rightBlack = CreateEdge("RightBlack", blackBorderColor, 11);
    }

    private Image CreateEdge(string edgeName, Color color, int siblingIndex)
    {
        GameObject go = new(edgeName);
        go.transform.SetParent(transform, false);

        Image edge = go.AddComponent<Image>();
        edge.color = color;
        edge.raycastTarget = false;
        edge.enabled = true;

        RectTransform rt = edge.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition3D = Vector3.zero;

        go.transform.SetSiblingIndex(siblingIndex);
        return edge;
    }

    private void ForceSyncBorders()
    {
        if (cachedRectTransform == null)
            return;

        UpdateBorderPositions();

        SyncEdgeTransform(top);
        SyncEdgeTransform(bottom);
        SyncEdgeTransform(left);
        SyncEdgeTransform(right);

        if (useBlackOutline)
        {
            SyncEdgeTransform(topBlack);
            SyncEdgeTransform(bottomBlack);
            SyncEdgeTransform(leftBlack);
            SyncEdgeTransform(rightBlack);
        }

        lastSize = cachedRectTransform.rect.size;
        lastScale = cachedTransform.localScale;
        lastRotation = cachedTransform.localRotation;
        borderDirty = false;
    }

    private static void SyncEdgeTransform(Image edge)
    {
        if (edge == null)
            return;

        RectTransform rt = edge.rectTransform;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition3D = new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, 0f);
    }

    private void UpdateBorderPositions()
    {
        UpdateWhiteBorderPositions();

        if (useBlackOutline)
            UpdateBlackBorderPositions();
    }

    private void UpdateWhiteBorderPositions()
    {
        float thickness = borderThickness;
        float inset = whiteInset;

        RectTransform topRt = top.rectTransform;
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.sizeDelta = new Vector2(-inset * 2f, thickness);
        topRt.anchoredPosition = new Vector2(0f, -inset);

        RectTransform bottomRt = bottom.rectTransform;
        bottomRt.anchorMin = new Vector2(0f, 0f);
        bottomRt.anchorMax = new Vector2(1f, 0f);
        bottomRt.pivot = new Vector2(0.5f, 0f);
        bottomRt.sizeDelta = new Vector2(-inset * 2f, thickness);
        bottomRt.anchoredPosition = new Vector2(0f, inset);

        RectTransform leftRt = left.rectTransform;
        leftRt.anchorMin = new Vector2(0f, 0f);
        leftRt.anchorMax = new Vector2(0f, 1f);
        leftRt.pivot = new Vector2(0f, 0.5f);
        leftRt.sizeDelta = new Vector2(thickness, -inset * 2f);
        leftRt.anchoredPosition = new Vector2(inset, 0f);

        RectTransform rightRt = right.rectTransform;
        rightRt.anchorMin = new Vector2(1f, 0f);
        rightRt.anchorMax = new Vector2(1f, 1f);
        rightRt.pivot = new Vector2(1f, 0.5f);
        rightRt.sizeDelta = new Vector2(thickness, -inset * 2f);
        rightRt.anchoredPosition = new Vector2(-inset, 0f);
    }

    private void UpdateBlackBorderPositions()
    {
        float thickness = blackBorderThickness;
        float inset = whiteInset + blackInset;

        RectTransform topRt = topBlack.rectTransform;
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.sizeDelta = new Vector2(-inset * 2f, thickness);
        topRt.anchoredPosition = new Vector2(0f, -inset);

        RectTransform bottomRt = bottomBlack.rectTransform;
        bottomRt.anchorMin = new Vector2(0f, 0f);
        bottomRt.anchorMax = new Vector2(1f, 0f);
        bottomRt.pivot = new Vector2(0.5f, 0f);
        bottomRt.sizeDelta = new Vector2(-inset * 2f, thickness);
        bottomRt.anchoredPosition = new Vector2(0f, inset);

        RectTransform leftRt = leftBlack.rectTransform;
        leftRt.anchorMin = new Vector2(0f, 0f);
        leftRt.anchorMax = new Vector2(0f, 1f);
        leftRt.pivot = new Vector2(0f, 0.5f);
        leftRt.sizeDelta = new Vector2(thickness, -inset * 2f);
        leftRt.anchoredPosition = new Vector2(inset, 0f);

        RectTransform rightRt = rightBlack.rectTransform;
        rightRt.anchorMin = new Vector2(1f, 0f);
        rightRt.anchorMax = new Vector2(1f, 1f);
        rightRt.pivot = new Vector2(1f, 0.5f);
        rightRt.sizeDelta = new Vector2(thickness, -inset * 2f);
        rightRt.anchoredPosition = new Vector2(-inset, 0f);
    }

    public void SetBorders(bool showTop, bool showBottom, bool showLeft, bool showRight, bool instant = false)
    {
        SetEdgeVisible(top, showTop, borderAlpha, instant);
        SetEdgeVisible(bottom, showBottom, borderAlpha, instant);
        SetEdgeVisible(left, showLeft, borderAlpha, instant);
        SetEdgeVisible(right, showRight, borderAlpha, instant);

        if (useBlackOutline)
        {
            SetEdgeVisible(topBlack, showTop, blackBorderAlpha, instant);
            SetEdgeVisible(bottomBlack, showBottom, blackBorderAlpha, instant);
            SetEdgeVisible(leftBlack, showLeft, blackBorderAlpha, instant);
            SetEdgeVisible(rightBlack, showRight, blackBorderAlpha, instant);
        }

        borderDirty = true;
    }

    public void SetEdge(Edge edge, bool visible, bool instant = false)
    {
        switch (edge)
        {
            case Edge.Top:
                SetEdgeVisible(top, visible, borderAlpha, instant);
                if (useBlackOutline) SetEdgeVisible(topBlack, visible, blackBorderAlpha, instant);
                break;
            case Edge.Bottom:
                SetEdgeVisible(bottom, visible, borderAlpha, instant);
                if (useBlackOutline) SetEdgeVisible(bottomBlack, visible, blackBorderAlpha, instant);
                break;
            case Edge.Left:
                SetEdgeVisible(left, visible, borderAlpha, instant);
                if (useBlackOutline) SetEdgeVisible(leftBlack, visible, blackBorderAlpha, instant);
                break;
            case Edge.Right:
                SetEdgeVisible(right, visible, borderAlpha, instant);
                if (useBlackOutline) SetEdgeVisible(rightBlack, visible, blackBorderAlpha, instant);
                break;
        }

        borderDirty = true;
    }

    private void SetEdgeVisible(Image edge, bool visible, float alphaValue, bool instant)
    {
        if (edge == null)
            return;

        edge.DOKill(false);

        float targetAlpha = visible ? alphaValue : 0f;

        if (instant)
        {
            Color c = edge.color;
            c.a = targetAlpha;
            edge.color = c;
            edge.enabled = visible || targetAlpha > 0f;
            return;
        }

        if (!edge.enabled)
            edge.enabled = true;

        edge.DOFade(targetAlpha, borderFadeDuration)
            .OnComplete(() =>
            {
                if (edge != null && targetAlpha <= 0.001f)
                    edge.enabled = false;
            });
    }

    public void ShowAllBorders(bool instant = false)
    {
        SetBorders(true, true, true, true, instant);
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
            .OnUpdate(() => borderDirty = true)
            .OnComplete(() =>
            {
                cachedRectTransform.anchoredPosition = snappedTarget;
                borderDirty = true;
                ForceSyncBorders();
            });

        if (!resetScale || cachedTransform == null)
            return;

        if ((cachedTransform.localScale - Vector3.one).sqrMagnitude <= 0.0001f)
            return;

        cachedTransform.DOKill(false);
        cachedTransform
            .DOScale(Vector3.one, duration)
            .OnUpdate(() => borderDirty = true)
            .OnComplete(() =>
            {
                borderDirty = true;
                ForceSyncBorders();
            });
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        if (top != null && bottom != null && left != null && right != null)
        {
            top.color = borderColor;
            bottom.color = borderColor;
            left.color = borderColor;
            right.color = borderColor;
        }

        if (useBlackOutline && topBlack != null && bottomBlack != null && leftBlack != null && rightBlack != null)
        {
            topBlack.color = blackBorderColor;
            bottomBlack.color = blackBorderColor;
            leftBlack.color = blackBorderColor;
            rightBlack.color = blackBorderColor;
        }

        borderDirty = true;
        ForceSyncBorders();
    }
#endif
}
