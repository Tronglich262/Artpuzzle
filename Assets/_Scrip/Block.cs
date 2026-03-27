using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

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

    private Image top, bottom, left, right;
    private Image topBlack, bottomBlack, leftBlack, rightBlack;

    [Header("White Border")]
    [SerializeField] private float borderThickness = 3f;
    [SerializeField] private float whiteInset = 1f;
    [SerializeField] private float borderAlpha = 1f;
    [SerializeField] private float borderFadeDuration = 0.06f;
    [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 1f);

    [Header("Black Inner Border")]
    [SerializeField] private bool useBlackOutline = true;
    [SerializeField] private float blackBorderThickness = 2f;
    [SerializeField] private float blackInset = 1f;
    [SerializeField] private float blackBorderAlpha = 1f;
    [SerializeField] private Color blackBorderColor = new Color(0f, 0f, 0f, 1f);

    private Vector2 lastSize;
    private Vector3 lastScale;
    private Quaternion lastRotation;

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
        if (cachedRectTransform == null) return;

        bool changed =
            cachedRectTransform.rect.size != lastSize ||
            cachedTransform.localScale != lastScale ||
            cachedTransform.localRotation != lastRotation;

        if (changed)
            ForceSyncBorders();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!gameObject.activeInHierarchy) return;
        ForceSyncBorders();
    }

    void CreateBorders()
    {
        top = CreateEdge("Top", borderColor, 10);
        bottom = CreateEdge("Bottom", borderColor, 10);
        left = CreateEdge("Left", borderColor, 10);
        right = CreateEdge("Right", borderColor, 10);

        if (useBlackOutline)
        {
            topBlack = CreateEdge("TopBlack", blackBorderColor, 11);
            bottomBlack = CreateEdge("BottomBlack", blackBorderColor, 11);
            leftBlack = CreateEdge("LeftBlack", blackBorderColor, 11);
            rightBlack = CreateEdge("RightBlack", blackBorderColor, 11);
        }
    }

    Image CreateEdge(string name, Color color, int siblingIndex)
    {
        GameObject go = new GameObject(name);
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

    void ForceSyncBorders()
    {
        if (cachedRectTransform == null) return;

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
    }

    void SyncEdgeTransform(Image edge)
    {
        if (edge == null) return;

        RectTransform rt = edge.rectTransform;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition3D = new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, 0f);
    }

    void UpdateBorderPositions()
    {
        UpdateWhiteBorderPositions();

        if (useBlackOutline)
            UpdateBlackBorderPositions();
    }

    void UpdateWhiteBorderPositions()
    {
        float thickness = borderThickness;
        float inset = whiteInset;

        top.rectTransform.anchorMin = new Vector2(0, 1);
        top.rectTransform.anchorMax = new Vector2(1, 1);
        top.rectTransform.pivot = new Vector2(0.5f, 1f);
        top.rectTransform.sizeDelta = new Vector2(-inset * 2f, thickness);
        top.rectTransform.anchoredPosition = new Vector2(0, -inset);

        bottom.rectTransform.anchorMin = new Vector2(0, 0);
        bottom.rectTransform.anchorMax = new Vector2(1, 0);
        bottom.rectTransform.pivot = new Vector2(0.5f, 0f);
        bottom.rectTransform.sizeDelta = new Vector2(-inset * 2f, thickness);
        bottom.rectTransform.anchoredPosition = new Vector2(0, inset);

        left.rectTransform.anchorMin = new Vector2(0, 0);
        left.rectTransform.anchorMax = new Vector2(0, 1);
        left.rectTransform.pivot = new Vector2(0f, 0.5f);
        left.rectTransform.sizeDelta = new Vector2(thickness, -inset * 2f);
        left.rectTransform.anchoredPosition = new Vector2(inset, 0);

        right.rectTransform.anchorMin = new Vector2(1, 0);
        right.rectTransform.anchorMax = new Vector2(1, 1);
        right.rectTransform.pivot = new Vector2(1f, 0.5f);
        right.rectTransform.sizeDelta = new Vector2(thickness, -inset * 2f);
        right.rectTransform.anchoredPosition = new Vector2(-inset, 0);
    }

    void UpdateBlackBorderPositions()
    {
        float thickness = blackBorderThickness;
        float inset = whiteInset + blackInset;

        topBlack.rectTransform.anchorMin = new Vector2(0, 1);
        topBlack.rectTransform.anchorMax = new Vector2(1, 1);
        topBlack.rectTransform.pivot = new Vector2(0.5f, 1f);
        topBlack.rectTransform.sizeDelta = new Vector2(-inset * 2f, thickness);
        topBlack.rectTransform.anchoredPosition = new Vector2(0, -inset);

        bottomBlack.rectTransform.anchorMin = new Vector2(0, 0);
        bottomBlack.rectTransform.anchorMax = new Vector2(1, 0);
        bottomBlack.rectTransform.pivot = new Vector2(0.5f, 0f);
        bottomBlack.rectTransform.sizeDelta = new Vector2(-inset * 2f, thickness);
        bottomBlack.rectTransform.anchoredPosition = new Vector2(0, inset);

        leftBlack.rectTransform.anchorMin = new Vector2(0, 0);
        leftBlack.rectTransform.anchorMax = new Vector2(0, 1);
        leftBlack.rectTransform.pivot = new Vector2(0f, 0.5f);
        leftBlack.rectTransform.sizeDelta = new Vector2(thickness, -inset * 2f);
        leftBlack.rectTransform.anchoredPosition = new Vector2(inset, 0);

        rightBlack.rectTransform.anchorMin = new Vector2(1, 0);
        rightBlack.rectTransform.anchorMax = new Vector2(1, 1);
        rightBlack.rectTransform.pivot = new Vector2(1f, 0.5f);
        rightBlack.rectTransform.sizeDelta = new Vector2(thickness, -inset * 2f);
        rightBlack.rectTransform.anchoredPosition = new Vector2(-inset, 0);
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
    }

    private void SetEdgeVisible(Image edge, bool visible, float alphaValue, bool instant)
    {
        if (edge == null) return;

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

        Vector2 snappedTarget = new Vector2(
            Mathf.Round(targetPosition.x),
            Mathf.Round(targetPosition.y)
        );

        cachedRectTransform
            .DOAnchorPos(snappedTarget, duration)
            .SetEase(Ease.OutCubic)
            .OnUpdate(ForceSyncBorders)
            .OnComplete(() =>
            {
                cachedRectTransform.anchoredPosition = snappedTarget;
                ForceSyncBorders();
            });

        if (resetScale && cachedTransform != null)
        {
            if ((cachedTransform.localScale - Vector3.one).sqrMagnitude > 0.0001f)
            {
                cachedTransform.DOKill(false);
                cachedTransform
                    .DOScale(Vector3.one, duration)
                    .OnUpdate(ForceSyncBorders)
                    .OnComplete(ForceSyncBorders);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;

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

        ForceSyncBorders();
    }
#endif
}