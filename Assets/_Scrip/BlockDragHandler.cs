using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class BlockDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum DragMoveType
    {
        None,
        Horizontal,
        Vertical,
        Diagonal
    }

    private Canvas canvas;
    private PuzzleManager puzzle;
    private Block block;

    private Vector2 pointerStart;
    private Vector2 rootStartPos;

    private float lastInputTime = -1f;
    [SerializeField] private float inputCooldown = 0.2f;

    private bool isDragging = false;

    // FIX: lưu hướng kéo thật từ chuột
    private DragMoveType currentDragMoveType = DragMoveType.None;
    private Vector2 currentDragDelta = Vector2.zero;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        puzzle = GetComponentInParent<PuzzleManager>();
        block = GetComponent<Block>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (puzzle.isTweening) return;
        if (Time.time - lastInputTime < inputCooldown) return;

        lastInputTime = Time.time;
        isDragging = true;

        // reset trạng thái kéo
        currentDragMoveType = DragMoveType.None;
        currentDragDelta = Vector2.zero;

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

        // FIX: lưu delta + loại kéo từ chuột
        currentDragDelta = delta;
        currentDragMoveType = GetMoveTypeFromDelta(delta);

        RectTransform rootRect = block.group.root.GetComponent<RectTransform>();
        Vector2 target = rootStartPos + delta;

        rootRect.anchoredPosition = target;
    }

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

        Vector2 boardPos = blockRect.anchoredPosition + rootRect.anchoredPosition;
        Vector2Int targetGrid = puzzle.PositionToGrid(boardPos);

        targetGrid.x = Mathf.Clamp(targetGrid.x, 0, puzzle.rows - 1);
        targetGrid.y = Mathf.Clamp(targetGrid.y, 0, puzzle.cols - 1);

        Vector2Int offset = targetGrid - block.gridPos;
        DragMoveType moveType = currentDragMoveType;

        if (offset == Vector2Int.zero)
        {
            ResetGroup(draggedGroup);
            return;
        }

        if (!puzzle.CanMoveGroup(draggedGroup, offset))
        {
            ResetGroup(draggedGroup);
            return;
        }

        bool success = puzzle.MoveGroupWithPush(draggedGroup, offset, moveType);

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

            puzzle.SetTweening(false);
        }
    }

    private DragMoveType GetMoveTypeFromDelta(Vector2 delta)
    {
        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);

        // chống rung chuột
        if (absX < 5f && absY < 5f)
        {
            return DragMoveType.None;
        }

        // ngang rõ rệt
        if (absX > absY * 1.5f)
        {
            return DragMoveType.Horizontal;
        }

        // dọc rõ rệt
        if (absY > absX * 1.5f)
        {
            return DragMoveType.Vertical;
        }

        // còn lại xem là chéo
        return DragMoveType.Diagonal;
    }

    //private Vector2Int GetNormalizedOffsetByMouse(Vector2Int rawOffset, DragMoveType moveType)
    //{
    //    int dx = rawOffset.x; // row: lên/xuống
    //    int dy = rawOffset.y; // col: trái/phải


    //    if (dx == 0 && dy == 0)
    //    {
    //        return Vector2Int.zero;
    //    }

    //    switch (moveType)
    //    {
    //        case DragMoveType.Horizontal:
    //            {
    //                if (dy == 0)
    //                {
    //                    return Vector2Int.zero;
    //                }

    //                Vector2Int result = new Vector2Int(0, dy);
    //                return result;
    //            }

    //        case DragMoveType.Vertical:
    //            {
    //                if (dx == 0)
    //                {
    //                    return Vector2Int.zero;
    //                }

    //                Vector2Int result = new Vector2Int(dx, 0);
    //                return result;
    //            }

    //        case DragMoveType.Diagonal:
    //            {
    //                if (dx == 0 || dy == 0)
    //                {
    //                    return Vector2Int.zero;
    //                }

    //                int stepX = dx > 0 ? 1 : -1;
    //                int stepY = dy > 0 ? 1 : -1;
    //                int step = Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy));

    //                Vector2Int result = new Vector2Int(stepX * step, stepY * step);
    //                return result;
    //            }

    //        default:
    //            {
    //                return Vector2Int.zero;
    //            }
    //    }
    //}

    //private string GetDragDirectionFromDelta(Vector2 delta)
    //{
    //    float absX = Mathf.Abs(delta.x);
    //    float absY = Mathf.Abs(delta.y);

    //    if (absX < 5f && absY < 5f)
    //        return "None";

    //    if (absX > absY * 1.5f)
    //        return delta.x > 0 ? "Right" : "Left";

    //    if (absY > absX * 1.5f)
    //        return delta.y > 0 ? "Up" : "Down";

    //    if (delta.x > 0 && delta.y > 0) return "UpRight";
    //    if (delta.x > 0 && delta.y < 0) return "DownRight";
    //    if (delta.x < 0 && delta.y > 0) return "UpLeft";
    //    return "DownLeft";
    //}

    //private string GetDragDirectionFromOffset(Vector2Int offset)
    //{
    //    if (offset == Vector2Int.zero) return "None";

    //    if (offset.x == 0)
    //        return offset.y > 0 ? "Right" : "Left";

    //    if (offset.y == 0)
    //        return offset.x > 0 ? "Down" : "Up";

    //    if (offset.x < 0 && offset.y > 0) return "UpRight";
    //    if (offset.x < 0 && offset.y < 0) return "UpLeft";
    //    if (offset.x > 0 && offset.y > 0) return "DownRight";
    //    return "DownLeft";
    //}

    void ResetGroup(BlockGroup g)
    {
        if (g == null) return;

        int total = g.blocks.Count;
        int done = 0;

        Debug.Log("[RESET GROUP] Reset group với " + total + " block");

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
                    {
                        Debug.Log("[RESET GROUP] Reset xong toàn bộ block");
                        puzzle.SetTweening(false);
                    }
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