using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class PuzzleManager : MonoBehaviour
{
    public Sprite sourceImage;
    public GameObject blockPrefab;
    public int rows = 3;
    public int cols = 3;
    public float blockSize = 100f;

    [HideInInspector]
    public List<Block> currentBlocks = new List<Block>();

    void Start()
    {
        GeneratePuzzle();
        ShuffleBlocks();
    }

    public void GeneratePuzzle()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        currentBlocks.Clear();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                GameObject obj = Instantiate(blockPrefab, transform);
                Block b = obj.GetComponent<Block>();
                b.gridPos = new Vector2Int(r, c);
                b.correctPos = new Vector2Int(r, c);

                SetupBlockVisual(obj, r, c);
                currentBlocks.Add(b);

                // Khởi tạo Group
                BlockGroup g = new BlockGroup();
                GameObject rootObj = new GameObject("GroupRoot", typeof(RectTransform));
                g.root = rootObj.transform;
                g.root.SetParent(this.transform, false);

                b.group = g;
                g.blocks.Add(b);
                b.transform.SetParent(g.root);
            }
        }
        UpdateAllBlockPositions();
    }

    void SetupBlockVisual(GameObject obj, int row, int col)
    {
        Block b = obj.GetComponent<Block>();
        int texW = sourceImage.texture.width;
        int texH = sourceImage.texture.height;
        int spriteW = texW / cols;
        int spriteH = texH / rows;

        Rect rect = new Rect(col * spriteW, texH - (row + 1) * spriteH, spriteW, spriteH);
        b.img.sprite = Sprite.Create(sourceImage.texture, rect, new Vector2(0.5f, 0.5f));
    }

    public bool MoveGroupWithPush(BlockGroup draggedGroup, Vector2Int offset)
    {
        Dictionary<Block, Vector2Int> finalPositions = new Dictionary<Block, Vector2Int>();
        HashSet<Vector2Int> occupiedByMainGroup = new HashSet<Vector2Int>();

        foreach (var b in draggedGroup.blocks)
        {
            Vector2Int target = b.gridPos + offset;
            if (target.x < 0 || target.x >= rows || target.y < 0 || target.y >= cols) return false;
            finalPositions[b] = target;
            occupiedByMainGroup.Add(target);
        }

        List<Block> hitBlocks = currentBlocks.Where(b => b.group != draggedGroup && occupiedByMainGroup.Contains(b.gridPos)).ToList();
        List<Vector2Int> availableSpots = GetEmptyPositions();
        foreach (var b in draggedGroup.blocks) availableSpots.Add(b.gridPos);
        availableSpots.RemoveAll(pos => occupiedByMainGroup.Contains(pos));

        Dictionary<Block, Vector2Int> hitBlockNewMoves = new Dictionary<Block, Vector2Int>();
        HashSet<Vector2Int> usedByHit = new HashSet<Vector2Int>();

        foreach (var hb in hitBlocks)
        {
            Vector2Int bestSpot = availableSpots
                .Where(pos => !usedByHit.Contains(pos))
                .OrderBy(pos => Vector2Int.Distance(hb.gridPos, pos))
                .FirstOrDefault();

            if (bestSpot == null && availableSpots.Count == 0) return false;
            hitBlockNewMoves[hb] = bestSpot;
            usedByHit.Add(bestSpot);
        }

        foreach (var hb in hitBlocks)
        {
            if (hb.group.blocks.Count > 1) hb.group.SplitByBlocks(new List<Block> { hb }, transform);
            hb.gridPos = hitBlockNewMoves[hb];
        }

        foreach (var b in draggedGroup.blocks) b.gridPos = finalPositions[b];

        UpdateAllBlockPositions();
        CheckAndMergeGroups();
        return true;
    }

    public void CheckAndMergeGroups()
    {
        bool foundMerge = true;
        while (foundMerge)
        {
            foundMerge = false;
            for (int i = 0; i < currentBlocks.Count; i++)
            {
                for (int j = i + 1; j < currentBlocks.Count; j++)
                {
                    Block a = currentBlocks[i];
                    Block b = currentBlocks[j];
                    if (a.group != b.group && IsCorrectNeighbor(a, b))
                    {
                        a.group.Merge(b.group);
                        foundMerge = true; break;
                    }
                }
                if (foundMerge) break;
            }
        }
    }

    bool IsCorrectNeighbor(Block a, Block b)
    {
        Vector2Int gridDiff = a.gridPos - b.gridPos;
        if (gridDiff.sqrMagnitude != 1) return false;
        return gridDiff == (a.correctPos - b.correctPos);
    }

    public void UpdateAllBlockPositions()
    {
        foreach (var b in currentBlocks)
        {
            b.targetPosition = GridToPosition(b.gridPos);
            b.UpdateTransform();
        }
    }

    public Vector2 GridToPosition(Vector2Int pos)
    {
        float startX = -((cols - 1) * blockSize) / 2f;
        float startY = ((rows - 1) * blockSize) / 2f;
        return new Vector2(startX + pos.y * blockSize, startY - pos.x * blockSize);
    }

    public Vector2Int PositionToGrid(Vector2 pos)
    {
        float startX = -((cols - 1) * blockSize) / 2f;
        float startY = ((rows - 1) * blockSize) / 2f;
        int col = Mathf.RoundToInt((pos.x - startX) / blockSize);
        int row = Mathf.RoundToInt((startY - pos.y) / blockSize);
        return new Vector2Int(row, col);
    }

    public List<Vector2Int> GetEmptyPositions()
    {
        List<Vector2Int> empty = new List<Vector2Int>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (!currentBlocks.Any(b => b.gridPos == new Vector2Int(r, c)))
                    empty.Add(new Vector2Int(r, c));
        return empty;
    }

    public void ShuffleBlocks()
    {
        var positions = currentBlocks.Select(b => b.gridPos).ToList();
        foreach (var b in currentBlocks)
        {
            int rnd = Random.Range(0, positions.Count);
            b.gridPos = positions[rnd];
            positions.RemoveAt(rnd);
        }
        UpdateAllBlockPositions();
    }

    public bool CanMoveGroup(BlockGroup g, Vector2Int shift)
    {
        foreach (var b in g.blocks)
        {
            Vector2Int nextPos = b.gridPos + shift;
            if (nextPos.x < 0 || nextPos.x >= rows || nextPos.y < 0 || nextPos.y >= cols) return false;
        }
        return true;
    }
}