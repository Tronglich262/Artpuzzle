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

    [SerializeField] private float inputCooldown = 0.2f;

    private Canvas canvas;
    private PuzzleManager puzzle;
    private Block block;

    private Vector2 pointerStart;
    private Vector2 rootStartPos;
    private float lastInputTime = -1f;
    private bool isDragging;
    private DragMoveType currentDragMoveType = DragMoveType.None;

    /// <summary>
    /// Khởi tạo reference cần thiết như Canvas, PuzzleManager và Block.
    /// </summary>
    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        puzzle = GetComponentInParent<PuzzleManager>();
        block = GetComponent<Block>();
    }

    /// <summary>
    /// Bắt đầu thao tác kéo block, lưu vị trí ban đầu của con trỏ và group,
    /// đồng thời kích hoạt hiệu ứng scale cho group đang được kéo.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (puzzle.isTweening || Time.time - lastInputTime < inputCooldown)
            return;

        lastInputTime = Time.time;
        isDragging = true;
        currentDragMoveType = DragMoveType.None;
        puzzle.SetTweening(true);

        RectTransform rootRect = GetRootRect();
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
    /// Cập nhật vị trí group theo chuyển động kéo của con trỏ
    /// và xác định hướng kéo hiện tại.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 currentPointer
        );

        Vector2 delta = currentPointer - pointerStart;
        currentDragMoveType = GetMoveTypeFromDelta(delta);
        GetRootRect().anchoredPosition = rootStartPos + delta;
    }

    /// <summary>
    /// Kết thúc kéo, tính offset thả, kiểm tra khả năng di chuyển,
    /// sau đó move group hoặc reset về vị trí cũ nếu không hợp lệ.
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

        RectTransform rootRect = GetRootRect();
        rootRect.DOKill();
        rootRect.DOScale(1f, 0.15f)
            .SetEase(Ease.OutQuad)
            .OnKill(() => rootRect.localScale = Vector3.one)
            .OnComplete(() => rootRect.localScale = Vector3.one);

        Vector2Int offset = GetDropOffset(rootRect);

        if (offset == Vector2Int.zero || !puzzle.CanMoveGroup(draggedGroup, offset))
        {
            ResetGroup(draggedGroup);
            return;
        }

        bool success = puzzle.MoveGroupWithPush(draggedGroup, offset, currentDragMoveType);

        if (!success)
        {
            ResetGroup(draggedGroup);
            return;
        }

        rootRect.anchoredPosition = Vector2.zero;
        SnapDraggedGroupVisual(draggedGroup);
        puzzle.SetTweening(false);
    }

    /// <summary>
    /// Lấy RectTransform root của group hiện tại đang chứa block.
    /// </summary>
    private RectTransform GetRootRect()
    {
        return block.group.root.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Tính toán độ lệch grid từ vị trí hiện tại đến vị trí thả,
    /// dựa trên anchoredPosition của block và root group.
    /// </summary>
    private Vector2Int GetDropOffset(RectTransform rootRect)
    {
        RectTransform blockRect = block.GetComponent<RectTransform>();
        Vector2 boardPos = blockRect.anchoredPosition + rootRect.anchoredPosition;
        Vector2Int targetGrid = puzzle.PositionToGrid(boardPos);

        targetGrid.x = Mathf.Clamp(targetGrid.x, 0, puzzle.rows - 1);
        targetGrid.y = Mathf.Clamp(targetGrid.y, 0, puzzle.cols - 1);

        return targetGrid - block.gridPos;
    }

    /// <summary>
    /// Đồng bộ lại vị trí hiển thị của toàn bộ block trong group
    /// theo gridPos mới sau khi di chuyển thành công.
    /// </summary>
    private void SnapDraggedGroupVisual(BlockGroup group)
    {
        for (int i = 0; i < group.blocks.Count; i++)
        {
            Block b = group.blocks[i];
            if (b == null) continue;

            RectTransform rt = b.GetComponent<RectTransform>();
            if (rt == null) continue;

            Vector2 newPos = puzzle.GridToPosition(b.gridPos);
            b.targetPosition = newPos;
            rt.anchoredPosition = newPos;
        }
    }

    /// <summary>
    /// Xác định loại hướng kéo dựa trên độ lệch delta của con trỏ:
    /// ngang, dọc, chéo hoặc không đáng kể.
    /// </summary>
    private DragMoveType GetMoveTypeFromDelta(Vector2 delta)
    {
        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);

        if (absX < 5f && absY < 5f) return DragMoveType.None;
        if (absX > absY * 1.5f) return DragMoveType.Horizontal;
        if (absY > absX * 1.5f) return DragMoveType.Vertical;
        return DragMoveType.Diagonal;
    }

    /// <summary>
    /// Đưa toàn bộ block trong group về targetPosition trước đó
    /// khi thao tác kéo thả không hợp lệ hoặc move thất bại.
    /// </summary>
    private void ResetGroup(BlockGroup group)
    {
        if (group == null) return;

        int total = group.blocks.Count;
        int done = 0;

        for (int i = 0; i < group.blocks.Count; i++)
        {
            Block b = group.blocks[i];
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

        if (group.root == null) return;

        RectTransform rootRt = group.root.GetComponent<RectTransform>();
        if (rootRt == null) return;

        rootRt.DOKill();
        rootRt.localScale = Vector3.one;
        rootRt.anchoredPosition = Vector2.zero;
    }
}