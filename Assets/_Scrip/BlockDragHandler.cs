using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

public class BlockDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private Canvas canvas;
    private PuzzleManager puzzle;
    private Block block;

    private Vector2 pointerStart;
    private Vector2 rootStartPos;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        puzzle = GetComponentInParent<PuzzleManager>();
        block = GetComponent<Block>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransform rootRect = block.group.root.GetComponent<RectTransform>();

        rootRect.SetAsLastSibling();
        rootRect.DOScale(1.2f, 0.1f).SetEase(Ease.OutQuad);

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
        Vector2 currentPointer;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out currentPointer
        );

        Vector2 delta = currentPointer - pointerStart;

        // BỎ LOCK TRỤC - cho phép kéo tự do mọi hướng (kể cả chéo)
        // delta.x và delta.y giữ nguyên không ép về 0

        RectTransform rootRect = block.group.root.GetComponent<RectTransform>();
        rootRect.anchoredPosition = rootStartPos + delta;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        BlockGroup draggedGroup = block.group;
        RectTransform rootRect = draggedGroup.root.GetComponent<RectTransform>();
        rootRect.DOScale(1f, 0.1f);

        // 1. Tính toán Offset thực tế dựa trên khối đang cầm
        Vector2 worldPosOfHeldBlock = (Vector2)block.GetComponent<RectTransform>().anchoredPosition + rootRect.anchoredPosition;
        Vector2Int targetGridOfHeldBlock = puzzle.PositionToGrid(worldPosOfHeldBlock);
        Vector2Int offset = targetGridOfHeldBlock - block.gridPos;

        // Nếu không di chuyển, reset về vị trí cũ
        if (offset == Vector2Int.zero)
        {
            ResetGroup(draggedGroup);
            return;
        }

        // 2. Kiểm tra xem Group chính có nhảy ra ngoài biên không
        if (!puzzle.CanMoveGroup(draggedGroup, offset))
        {
            ResetGroup(draggedGroup);
            return;
        }

        // 3. Di chuyển nhóm với logic push - tự động xử lý swap với vị trí trống
        bool success = puzzle.MoveGroupWithPush(draggedGroup, offset);

        if (!success)
        {
            // Di chuyển thất bại -> Reset về vị trí cũ
            ResetGroup(draggedGroup);
        }
        else
        {
            // Thành công -> Reset root về tâm
            rootRect.anchoredPosition = Vector2.zero;
        }
    }

    void ResetGroup(BlockGroup g)
    {
        foreach (var b in g.blocks)
        {
            b.GetComponent<RectTransform>().DOAnchorPos(b.targetPosition, 0.2f);
        }

        g.root.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
    }
}