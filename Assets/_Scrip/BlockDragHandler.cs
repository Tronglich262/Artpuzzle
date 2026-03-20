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

    public void OnPointerDown(PointerEventData eventData)
    {
        if (puzzle.isTweening) return;
        if (Time.time - lastInputTime < inputCooldown) return;

        lastInputTime = Time.time;
        isDragging = true;

        // KHÓA NGAY
        puzzle.SetTweening(true);

        RectTransform rootRect = block.group.root.GetComponent<RectTransform>();

        rootRect.DOKill(true);
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

        RectTransform rootRect = block.group.root.GetComponent<RectTransform>();

        //  DRAG CÓ ĐỘ NẶNG
        Vector2 target = rootStartPos + delta;
        rootRect.anchoredPosition = Vector2.Lerp(
            rootRect.anchoredPosition,
            target,
            0.25f
        );
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

        rootRect.DOKill(true);
        rootRect.DOScale(1f, 0.15f);

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
                        puzzle.SetTweening(false); // mở đúng lúc
                });
        }

        if (g.root != null)
        {
            RectTransform rootRt = g.root.GetComponent<RectTransform>();
            if (rootRt != null)
                rootRt.anchoredPosition = Vector2.zero;
        }
    }
}