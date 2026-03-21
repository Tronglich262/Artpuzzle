using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class BlockDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private Canvas canvas;
    private PuzzleManager puzzle;
    private Block block;

    private Vector2 pointerStart;
    private Vector2 rootStartPos;

    private float lastInputTime = -1f;
    [SerializeField] private float inputCooldown = 0.2f;

    private bool isDragging = false;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        puzzle = GetComponentInParent<PuzzleManager>();
        block = GetComponent<Block>();
    }

    /// <summary>
    /// Khi bắt đầu nhấn vào block:
    /// - Kiểm tra cooldown và trạng thái tween
    /// - Bắt đầu drag
    /// - Scale nhẹ group và đưa lên top UI
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (puzzle.isTweening) return;
        if (Time.time - lastInputTime < inputCooldown) return;

        lastInputTime = Time.time;
        isDragging = true;

        puzzle.SetTweening(true);

        RectTransform rootRect = block.group.root.GetComponent<RectTransform>();
        rootRect.DOKill();
        rootRect.localScale = Vector3.one;

        rootRect.SetAsLastSibling();
        rootRect.DOScale(1.1f, 0.1f).SetEase(Ease.OutQuad);

        rootStartPos = rootRect.anchoredPosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out pointerStart
        );
    }
    /// <summary>
    /// Khi đang kéo:
    /// - Tính delta chuột
    /// - Di chuyển group theo kiểu mượt (lerp)
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        Vector2 currentPointer;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out currentPointer
        );

        Vector2 delta = currentPointer - pointerStart;

        RectTransform rootRect = block.group.root.GetComponent<RectTransform>();

        Vector2 target = rootStartPos + delta;

        rootRect.anchoredPosition = Vector2.Lerp(
            rootRect.anchoredPosition,
            target,
            2f
        );
    }

    /// <summary>
    /// Khi thả chuột:
    /// - Tính vị trí grid mới
    /// - Thực hiện move + push
    /// - Nếu fail → reset về vị trí cũ
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;

        BlockGroup draggedGroup = block.group;

        if (draggedGroup == null || draggedGroup.root == null)
        {
            puzzle.SetTweening(false);
            return;
        }

        RectTransform rootRect = draggedGroup.root.GetComponent<RectTransform>();
        rootRect.DOKill();
        rootRect.DOScale(1f, 0.15f)
            .SetEase(Ease.OutQuad)
            .OnKill(() => rootRect.localScale = Vector3.one)
            .OnComplete(() => rootRect.localScale = Vector3.one);

        RectTransform blockRect = block.GetComponent<RectTransform>();

        Vector2 worldPos = blockRect.anchoredPosition + rootRect.anchoredPosition;
        Vector2Int targetGrid = puzzle.PositionToGrid(worldPos);
        Vector2Int offset = targetGrid - block.gridPos;

        if (offset == Vector2Int.zero || !puzzle.CanMoveGroup(draggedGroup, offset))
        {
            ResetGroup(draggedGroup);
            return;
        }
        bool success = puzzle.MoveGroupWithPush(draggedGroup, offset);

        if (!success)
        {
            ResetGroup(draggedGroup);
        }
        else
        {
            rootRect.anchoredPosition = Vector2.zero;

            foreach (var b in draggedGroup.blocks)
            {
                if (b == null) continue;

                RectTransform rt = b.GetComponent<RectTransform>();
                if (rt == null) continue;

                Vector2 newPos = puzzle.GridToPosition(b.gridPos);
                b.targetPosition = newPos;
                rt.anchoredPosition = newPos;
            }
        }
    }
    /// <summary>
    /// Reset group về vị trí cũ nếu move thất bại:
    /// - Animate từng block về targetPosition
    /// - Mở lại input sau khi hoàn thành
    /// </summary>
    void ResetGroup(BlockGroup g)
    {
        if (g == null) return;

        int total = g.blocks.Count;
        int done = 0;

        foreach (var b in g.blocks)
        {
            if (b == null) continue;

            RectTransform rt = b.GetComponent<RectTransform>();
            if (rt == null) continue;

            rt.DOKill(true);

            rt.DOAnchorPos(b.targetPosition, 0.2f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    done++;
                    if (done >= total)
                        puzzle.SetTweening(false);
                });
        }
        if (g.root != null)
        {
            RectTransform rootRt = g.root.GetComponent<RectTransform>();
            if (rootRt != null)
            {
                rootRt.DOKill();
                rootRt.localScale = Vector3.one;
                rootRt.anchoredPosition = Vector2.zero;
            }
        }
    }
}