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
    //chong double click
    private float lastInputTime = -1f;
    [SerializeField] private float inputCooldown = 0.3f;

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

        RectTransform rootRect = block.group.root.GetComponent<RectTransform>();

        // Huy cac animation cu tren root de tranh xung dot
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
    public void OnPointerUp(PointerEventData eventData)
    {
        if (puzzle.isTweening) return;
        BlockGroup draggedGroup = block.group;

        // Kiem tra an toan - dam bao group va root con ton tai
        if (draggedGroup == null || draggedGroup.root == null) return;

        RectTransform rootRect = draggedGroup.root.GetComponent<RectTransform>();
        if (rootRect == null) return;

        // Huy animation cu va animate scale ve 1
        rootRect.DOKill(true);
        rootRect.DOScale(1f, 0.1f);

        // Tinh toan Offset dua tren khoi dang cam
        RectTransform blockRect = block.GetComponent<RectTransform>();
        if (blockRect == null) return;

        Vector2 worldPosOfHeldBlock = blockRect.anchoredPosition + rootRect.anchoredPosition;
        Vector2Int targetGridOfHeldBlock = puzzle.PositionToGrid(worldPosOfHeldBlock);
        Vector2Int offset = targetGridOfHeldBlock - block.gridPos;
        if (offset == Vector2Int.zero)
        {
            ResetGroup(draggedGroup);
            return;
        }
        if (!puzzle.CanMoveGroup(draggedGroup, offset))
        {
            Debug.Log("Ra ngoai bien reset vi tri");
            ResetGroup(draggedGroup);
            return;
        }
        bool success = puzzle.MoveGroupWithPush(draggedGroup, offset);
        if (!success)
        {
            Debug.Log("Di chuyen that bai reset vi tri");
            ResetGroup(draggedGroup);
        }
        else
        {
            Debug.Log("Di chuyen thanh cong");
            // Reset root
            rootRect.anchoredPosition = Vector2.zero;

            // Cap nhat vi tri UI cua tung block theo grid moi
            foreach (var b in draggedGroup.blocks)
            {
                if (b == null || b.GetComponent<RectTransform>() == null) continue;

                Vector2 newPos = puzzle.GridToPosition(b.gridPos);
                b.targetPosition = newPos;
                b.GetComponent<RectTransform>().anchoredPosition = newPos;
            }

            // Cho phep swap tiep ngay lap tuc (khong can cho animation)
            rootRect.DOComplete();
            puzzle.SetTweening(false);
        }
    }

    void ResetGroup(BlockGroup g)
    {
        if (g == null) return;

        puzzle.SetTweening(true);
        int totalBlocks = g.blocks.Count;
        int count = 0;

        foreach (var b in g.blocks)
        {
            if (b == null) continue;

            RectTransform rt = b.GetComponent<RectTransform>();
            if (rt == null) continue;

            // Huy animation cu truoc
            rt.DOKill(true);

            rt.DOAnchorPos(b.targetPosition, 0.05f)
                .OnComplete(() =>
                {
                    // Kiem tra an toan trong callback
                    if (this == null || puzzle == null) return;
                    count++;
                    if (count >= totalBlocks) puzzle.SetTweening(false);
                });
        }

        if (g.root != null)
        {
            RectTransform rootRt = g.root.GetComponent<RectTransform>();
            if (rootRt != null) rootRt.anchoredPosition = Vector2.zero;
        }
    }
}
