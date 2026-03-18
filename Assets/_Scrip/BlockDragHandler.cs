using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DG.Tweening;
public class BlockDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;

    private PuzzleManager puzzle;
    private Block block;

    private Vector2 pointerStart;
    private Dictionary<Block, Vector2> startPositions = new Dictionary<Block, Vector2>();
    private Dictionary<Block, Vector2Int> previewPositions = new Dictionary<Block, Vector2Int>();
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
        foreach (var b in block.group.blocks)
        {
            b.transform.DOScale(1.2f, 0.1f).SetEase(Ease.OutQuad);
        }
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

        // 🔥 lock trục trước
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            delta.y = 0;
        else
            delta.x = 0;

        // 🔥 rồi mới move
        foreach (var b in block.group.blocks)
        {
            RectTransform rt = b.GetComponent<RectTransform>();
            rt.anchoredPosition = startPositions[b] + delta;
        }

    }

    public void OnPointerUp(PointerEventData eventData)
    {
        foreach (var b in block.group.blocks)
        {
            b.transform.DOScale(1f, 0.15f).SetEase(Ease.OutBack);
        }

        BlockGroup draggedGroup = block.group;

        previewPositions.Clear();

        // 🔥 1. Tính vị trí grid cho TOÀN BỘ group
        foreach (var b in draggedGroup.blocks)
        {
            Vector2 pos = b.GetComponent<RectTransform>().anchoredPosition;
            Vector2Int grid = puzzle.PositionToGrid(pos);
            previewPositions[b] = grid;
        }

        // 🔥 2. Check out of bounds
        foreach (var kvp in previewPositions)
        {
            Vector2Int g = kvp.Value;

            if (g.x < 0 || g.x >= puzzle.rows || g.y < 0 || g.y >= puzzle.cols)
            {
                ResetGroup(draggedGroup);
                return;
            }
        }

        // 🔥 3. Lấy tất cả vị trí target
        List<Vector2Int> targetPositions = new List<Vector2Int>(previewPositions.Values);

        // 🔥 4. Lấy block bị đè
        var hitBlocks = puzzle.GetBlocksAtPositions(targetPositions, draggedGroup);

        // 🔥 5. Gom group bị ảnh hưởng
        HashSet<BlockGroup> affectedGroups = new HashSet<BlockGroup>();
        foreach (var b in hitBlocks)
        {
            affectedGroups.Add(b.group);
        }

        // 🔥 6. Check xem có push được không
        foreach (var g in affectedGroups)
        {
            foreach (var b in g.blocks)
            {
                Vector2Int newPos = b.gridPos;

                // tính direction push
                Vector2Int dir = previewPositions[draggedGroup.blocks[0]] - draggedGroup.blocks[0].gridPos;

                newPos -= dir;

                if (newPos.x < 0 || newPos.x >= puzzle.rows || newPos.y < 0 || newPos.y >= puzzle.cols)
                {
                    ResetGroup(draggedGroup);
                    return;
                }
            }
        }

        // 🔥 7. Push các group bị đè
        Vector2Int pushDir = previewPositions[draggedGroup.blocks[0]] - draggedGroup.blocks[0].gridPos;

        foreach (var g in affectedGroups)
        {
            foreach (var b in g.blocks)
            {
                b.gridPos -= pushDir;
            }
        }

        // 🔥 8. Apply vị trí mới cho group chính
        foreach (var kvp in previewPositions)
        {
            kvp.Key.gridPos = kvp.Value;
        }

        // 🔥 9. Update + merge
        puzzle.UpdateAllBlockPositions();
        puzzle.CheckAndMergeGroups();
    }

    // Hàm tính trung tâm của một nhóm block
    private Vector2 GetGroupCenter(BlockGroup group)
    {
        Vector2 sum = Vector2.zero;
        foreach (var b in group.blocks)
        {
            sum += b.GetComponent<RectTransform>().anchoredPosition;
        }
        return sum / group.blocks.Count;
    }
    void ResetGroup(BlockGroup g)
    {
        foreach (var b in g.blocks)
        {
            b.GetComponent<RectTransform>().anchoredPosition = b.targetPosition;
        }
    }

}