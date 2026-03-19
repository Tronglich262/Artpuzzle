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
    // khi click vào image
    public void OnPointerDown(PointerEventData eventData)
    {

        if (puzzle.isTweening) return;
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
    //kéo ảnh
    public void OnDrag(PointerEventData eventData)
    {
        if (puzzle.isTweening) return;
        Vector2 currentPointer;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out currentPointer
        );
        Vector2 delta = currentPointer - pointerStart;
        RectTransform rootRect = block.group.root.GetComponent<RectTransform>();
        rootRect.anchoredPosition = rootStartPos + delta;
    }
    //thả ảnh
    public void OnPointerUp(PointerEventData eventData)
    {
        if (puzzle.isTweening) return;
        BlockGroup draggedGroup = block.group;
        RectTransform rootRect = draggedGroup.root.GetComponent<RectTransform>();
        rootRect.DOScale(1f, 0.1f);

        // 1. Tính toán Offset thực tế dựa trên khối đang cầm
        Vector2 worldPosOfHeldBlock = (Vector2)block.GetComponent<RectTransform>().anchoredPosition + rootRect.anchoredPosition;
        Vector2Int targetGridOfHeldBlock = puzzle.PositionToGrid(worldPosOfHeldBlock);
        Vector2Int offset = targetGridOfHeldBlock - block.gridPos;
        if (offset == Vector2Int.zero)
        {
            ResetGroup(draggedGroup);
            return;
        }
        if (!puzzle.CanMoveGroup(draggedGroup, offset))
        {
            Debug.Log("Ra ngoài biên reset vị trí");
            ResetGroup(draggedGroup);
            return;
        }
        bool success = puzzle.MoveGroupWithPush(draggedGroup, offset);
        if (!success)
        {
            Debug.Log("Di chuyển thất bại reset vị trí");
            ResetGroup(draggedGroup);
        }
        else
        {
            Debug.Log("Di chuyển thành công");

            // Reset root
            rootRect.anchoredPosition = Vector2.zero;

            // Cập nhật lại vị trí UI của từng block theo grid mới
            foreach (var b in draggedGroup.blocks)
            {
                Vector2 newPos = puzzle.GridToPosition(b.gridPos);
                b.targetPosition = newPos;
                b.GetComponent<RectTransform>().anchoredPosition = newPos;
            }
        }
    }

    void ResetGroup(BlockGroup g)
    {
        puzzle.SetTweening(true); // Khóa lại
        int count = 0;
        foreach (var b in g.blocks)
        {
            b.GetComponent<RectTransform>().DOAnchorPos(b.targetPosition, 0.2f)
                .OnComplete(() =>
                {
                    count++;
                    if (count >= g.blocks.Count) puzzle.SetTweening(false); // Mở khóa
                });
        }
        g.root.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
    }
}