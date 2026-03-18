using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BlockDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;

    private PuzzleManager puzzle;
    private Block block;

    private Vector2 pointerStart;
    private Dictionary<Block, Vector2> startPositions = new Dictionary<Block, Vector2>();

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        puzzle = GetComponentInParent<PuzzleManager>();
        block = GetComponent<Block>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        rectTransform.SetAsLastSibling();

        startPositions.Clear();

        // lưu vị trí ban đầu của cả group
        foreach (var b in block.group.blocks)
        {
            startPositions[b] = b.GetComponent<RectTransform>().anchoredPosition;
        }

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

        // move toàn bộ group theo delta chuẩn
        foreach (var b in block.group.blocks)
        {
            RectTransform rt = b.GetComponent<RectTransform>();
            rt.anchoredPosition = startPositions[b] + delta;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Block nearest = null;
        float minDist = float.MaxValue;

        foreach (var b in puzzle.currentBlocks)
        {
            if (b.group == block.group) continue;

            float dist = Vector2.Distance(
                block.GetComponent<RectTransform>().anchoredPosition,
                b.targetPosition
            );

            if (dist < minDist)
            {
                minDist = dist;
                nearest = b;
            }
        }

        float threshold = puzzle.blockSize * 0.6f;

        if (nearest != null && minDist < threshold)
        {
            puzzle.SwapBlocks(block, nearest);
        }
        else
        {
            // trả về đúng vị trí grid
            foreach (var b in block.group.blocks)
            {
                b.GetComponent<RectTransform>().anchoredPosition = b.targetPosition;
            }
        }
    }
}