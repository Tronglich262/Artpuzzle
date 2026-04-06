using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class BlockDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum DragMoveType
    {
        None,
        Horizontal,
        Vertical,
        Diagonal
    }

    [SerializeField] private float inputCooldown = 1.5f;

    private Canvas canvas;
    private RectTransform canvasRect;
    private PuzzleManager puzzle;
    private Block block;

    private Vector2 pointerStart;
    private Vector2 rootStartPos;
    private float lastInputTime = -1f;
    private bool isDragging;
    private DragMoveType currentDragMoveType = DragMoveType.None;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        puzzle = GetComponentInParent<PuzzleManager>();
        block = GetComponent<Block>();
        canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (puzzle == null || canvasRect == null || block == null)
            return;

        if (puzzle.IsBusyAfterComplete())
            return;

        if (Time.time - lastInputTime < inputCooldown)
            return;

        RectTransform rootRect = GetRootRectSafe();
        if (rootRect == null)
            return;

        lastInputTime = Time.time;
        isDragging = true;
        currentDragMoveType = DragMoveType.None;
        puzzle.SetTweening(true);

        rootRect.DOKill();
        rootRect.localScale = Vector3.one;
        rootRect.SetAsLastSibling();
        rootRect.DOScale(1.05f, 0.08f).SetEase(Ease.OutQuad);
        rootStartPos = rootRect.anchoredPosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out pointerStart
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        RectTransform rootRect = GetRootRectSafe();
        if (rootRect == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 currentPointer
        );

        Vector2 delta = currentPointer - pointerStart;
        currentDragMoveType = GetMoveTypeFromDelta(delta);
        rootRect.anchoredPosition = rootStartPos + delta;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;

        BlockGroup draggedGroup = block != null ? block.group : null;
        if (draggedGroup == null || draggedGroup.root == null)
        {
            puzzle?.SetTweening(false);
            return;
        }

        RectTransform rootRect = GetRootRectSafe();
        if (rootRect == null)
        {
            puzzle?.SetTweening(false);
            return;
        }

        rootRect.DOKill();
        rootRect.DOScale(1f, 0.10f)
            .SetEase(Ease.OutQuad)
            .OnKill(() => rootRect.localScale = Vector3.one)
            .OnComplete(() => rootRect.localScale = Vector3.one);

        Vector2Int offset = GetDropOffset(rootRect);

        if (offset == Vector2Int.zero || !puzzle.CanMoveGroup(draggedGroup, offset))
        {
            ResetGroup(draggedGroup);
            return;
        }

        bool success = puzzle.MoveGroupWithPush(draggedGroup, offset, currentDragMoveType, true);
        if (!success)
            ResetGroup(draggedGroup);
    }

    private Vector2Int GetDropOffset(RectTransform rootRect)
    {
        BlockGroup draggedGroup = block != null ? block.group : null;
        if (draggedGroup == null)
            return Vector2Int.zero;

        Block anchor = draggedGroup.GetAnchorBlock();
        if (anchor == null)
            return Vector2Int.zero;

        Vector2 delta = rootRect.anchoredPosition - rootStartPos;

        if (delta.sqrMagnitude < puzzle.blockSize * puzzle.blockSize * 0.0625f)
            return Vector2Int.zero;

        Vector2Int targetGrid = puzzle.PositionToGrid(rootRect.anchoredPosition);
        targetGrid.x = Mathf.Clamp(targetGrid.x, 0, puzzle.rows - 1);
        targetGrid.y = Mathf.Clamp(targetGrid.y, 0, puzzle.cols - 1);

        Vector2Int offset = targetGrid - anchor.gridPos;
        return offset;
    }

    private DragMoveType GetMoveTypeFromDelta(Vector2 delta)
    {
        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);

        if (absX < 5f && absY < 5f)
            return DragMoveType.None;
        if (absX > absY * 1.5f)
            return DragMoveType.Horizontal;
        if (absY > absX * 1.5f)
            return DragMoveType.Vertical;
        return DragMoveType.Diagonal;
    }

    private void ResetGroup(BlockGroup group)
    {
        if (group == null)
        {
            puzzle?.SetTweening(false);
            return;
        }

        RectTransform rootRt = group.root as RectTransform;
        if (rootRt == null)
        {
            puzzle?.SetTweening(false);
            return;
        }

        Block anchor = group.GetAnchorBlock();
        if (anchor == null)
        {
            puzzle?.SetTweening(false);
            return;
        }

        group.RebuildLocalLayout(false);
        Vector2 targetRootPos = puzzle.GridToPosition(anchor.gridPos);

        rootRt.DOKill(false);
        rootRt.DOAnchorPos(targetRootPos, 0.16f)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                rootRt.localScale = Vector3.one;
                puzzle.SetTweening(false);
            })
            .OnKill(() =>
            {
                rootRt.localScale = Vector3.one;
                puzzle.SetTweening(false);
            });
    }

    private RectTransform GetRootRectSafe()
    {
        if (block == null || block.group == null || block.group.root == null)
            return null;

        return block.group.root as RectTransform;
    }
}
