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
        // Xóa block cũ
        foreach (Transform child in transform) Destroy(child.gameObject);
        currentBlocks.Clear();

        // Tính toán offset để căn giữa puzzle
        float startX = -((cols - 1) * blockSize) / 2f;
        float startY = ((rows - 1) * blockSize) / 2f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                GameObject obj = Instantiate(blockPrefab, transform);
                Block b = obj.GetComponent<Block>();

                // Thiết lập tọa độ
                b.gridPos = new Vector2Int(r, c);
                b.correctPos = new Vector2Int(r, c);

                // Cắt ảnh
                SetupBlockVisual(obj, r, c);

                currentBlocks.Add(b);

                // Khởi tạo group riêng cho mỗi block
                BlockGroup g = new BlockGroup();
                g.blocks.Add(b);
                b.group = g;
            }
        }
        UpdateAllBlockPositions();
    }

    void SetupBlockVisual(GameObject obj, int row, int col)
    {
        Block b = obj.GetComponent<Block>();

        if (b == null || b.img == null)
        {
            Debug.LogError("Block hoặc Image chưa gán!");
            return;
        }

        int texW = sourceImage.texture.width;
        int texH = sourceImage.texture.height;
        int spriteW = texW / cols;
        int spriteH = texH / rows;

        Rect rect = new Rect(
            col * spriteW,
            texH - (row + 1) * spriteH,
            spriteW,
            spriteH
        );

        b.img.sprite = Sprite.Create(
            sourceImage.texture,
            rect,
            new Vector2(0.5f, 0.5f)
        );
    }

    // --- LOGIC SWAP NHÓM ---
    public void SwapBlocks(Block a, Block b)
    {
        if (a == null || b == null || a.group == b.group) return;

        BlockGroup groupA = a.group;
        BlockGroup groupB = b.group;

        Vector2Int shift = b.gridPos - a.gridPos;

        // --- BỔ SUNG: KIỂM TRA GIỚI HẠN LƯỚI ---
        if (!CanMoveGroup(groupA, shift) || !CanMoveGroup(groupB, -shift))
        {
            ResetGroupPosition(groupA);
            ResetGroupPosition(groupB);
            return;
        }

        foreach (Block blk in groupA.blocks) blk.gridPos += shift;
        foreach (Block blk in groupB.blocks) blk.gridPos -= shift;

        UpdateAllBlockPositions();
        CheckAndMergeGroups();
    }

    public bool CanMoveGroup(BlockGroup g, Vector2Int shift)
    {
        foreach (var b in g.blocks)
        {
            Vector2Int nextPos = b.gridPos + shift;
            if (nextPos.x < 0 || nextPos.x >= rows || nextPos.y < 0 || nextPos.y >= cols)
                return false;
        }
        return true;
    }

    void ResetGroupPosition(BlockGroup g)
    {
        foreach (var b in g.blocks)
        {
            b.GetComponent<RectTransform>().anchoredPosition = b.targetPosition;
        }
    }

    public void CheckAndMergeGroups()
    {
        bool foundMerge = true;
        while (foundMerge) // Lặp lại cho đến khi không còn cặp nào ghép được
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
                        foundMerge = true;
                        break;
                    }
                }
                if (foundMerge) break;
            }
        }
    }

    public bool IsCorrectNeighbor(Block a, Block b)
    {
        Vector2Int gridDiff = a.gridPos - b.gridPos;
        // Chỉ ghép nếu chúng đang nằm cạnh nhau trên lưới (khoảng cách = 1)
        if (gridDiff.sqrMagnitude != 1) return false;

        // Kiểm tra xem vị trí tương đối này có đúng với ảnh gốc không
        Vector2Int correctDiff = a.correctPos - b.correctPos;
        return gridDiff == correctDiff;
    }

    public void UpdateAllBlockPositions()
    {
        foreach (var b in currentBlocks)
        {
            b.targetPosition = GridToPosition(b.gridPos);
            b.UpdateTransform(blockSize);
        }
    }

    public Vector2 GridToPosition(Vector2Int pos)
    {
        float startX = -((cols - 1) * blockSize) / 2f;
        float startY = ((rows - 1) * blockSize) / 2f;
        return new Vector2(startX + pos.y * blockSize, startY - pos.x * blockSize);
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
    public Vector2Int PositionToGrid(Vector2 pos)
    {
        float startX = -((cols - 1) * blockSize) / 2f;
        float startY = ((rows - 1) * blockSize) / 2f;

        int col = Mathf.RoundToInt((pos.x - startX) / blockSize);
        int row = Mathf.RoundToInt((startY - pos.y) / blockSize);

        return new Vector2Int(row, col);
    }
    public List<Block> GetBlocksAtPositions(List<Vector2Int> positions, BlockGroup ignoreGroup)
    {
        List<Block> result = new List<Block>();

        foreach (var b in currentBlocks)
        {
            if (b.group == ignoreGroup) continue;

            if (positions.Contains(b.gridPos))
            {
                result.Add(b);
            }
        }

        return result;
    }
    
    
}