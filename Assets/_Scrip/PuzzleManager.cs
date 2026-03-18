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

                GameObject rootObj = new GameObject("GroupRoot", typeof(RectTransform)); g.root = rootObj.transform;
                RectTransform rt = rootObj.GetComponent<RectTransform>();
                rt.SetParent(this.transform);
                rt.localScale = Vector3.one;
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
                // ✅ trong PuzzleManager thì dùng this
                g.root.SetParent(this.transform);
                g.blocks.Add(b);
                b.group = g;
                b.transform.SetParent(g.root);
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
        return currentBlocks
            .Where(b => b.group != ignoreGroup && positions.Contains(b.gridPos))
            .ToList();
    }

    // =========================================
    // MA TRẬN GRID - TRACK VỊ TRÍ TRỐNG
    // =========================================

    /// <summary>
    /// Cập nhật ma trận grid: đánh dấu vị trí nào đã bị chiếm
    /// </summary>
    public void UpdateGridMatrix()
    {
        // Xóa tất cả marks
        foreach (var b in currentBlocks)
        {
            b.gridMark = -1;
        }
    }

    /// <summary>
    /// Kiểm tra xem vị trí có trống không (không có block nào ở đó)
    /// </summary>
    public bool IsPositionEmpty(Vector2Int pos)
    {
        return !currentBlocks.Any(b => b.gridPos == pos);
    }

    /// <summary>
    /// Lấy tất cả vị trí trống trên lưới
    /// </summary>
    public List<Vector2Int> GetEmptyPositions()
    {
        List<Vector2Int> empty = new List<Vector2Int>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Vector2Int pos = new Vector2Int(r, c);
                if (IsPositionEmpty(pos))
                {
                    empty.Add(pos);
                }
            }
        }
        return empty;
    }

    /// <summary>
    /// Tìm vị trí trống gần nhất với một vị trí cho trước
    /// </summary>
    public Vector2Int FindNearestEmptyPosition(Vector2Int fromPos)
    {
        List<Vector2Int> empty = GetEmptyPositions();
        if (empty.Count == 0) return fromPos; // Không có chỗ trống

        Vector2Int nearest = empty[0];
        float minDist = Vector2Int.Distance(fromPos, nearest);

        foreach (var pos in empty)
        {
            float dist = Vector2Int.Distance(fromPos, pos);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = pos;
            }
        }
        return nearest;
    }

    /// <summary>
    /// Kiểm tra xem có đủ vị trí trống cho N block hay không
    /// </summary>
    public bool HasEnoughEmptyPositions(int count)
    {
        return GetEmptyPositions().Count >= count;
    }

    // =========================================
    // LOGIC SWAP NÂNG CAO - TÌM VỊ TRÍ TRỐNG
    // =========================================

    /// <summary>
    /// Swap nhóm với logic tìm vị trí trống
    /// Trả về true nếu swap thành công
    /// </summary>
    public bool SwapGroupsWithEmptyCheck(BlockGroup groupA, BlockGroup groupB, Vector2Int offset)
    {
        if (groupA == null || groupB == null || groupA == groupB) return false;

        // Bước 1: Thu thập tất cả vị trí mới mà mỗi nhóm sẽ chiếm
        HashSet<Vector2Int> targetPositionsA = new HashSet<Vector2Int>(); // Nhóm A muốn đến
        HashSet<Vector2Int> targetPositionsB = new HashSet<Vector2Int>(); // Nhóm B muốn đến

        foreach (var b in groupA.blocks)
        {
            targetPositionsA.Add(b.gridPos + offset);
        }

        foreach (var b in groupB.blocks)
        {
            targetPositionsB.Add(b.gridPos - offset);
        }

        // Bước 2: Kiểm tra xem có block nào muốn đến cùng 1 vị trí không (trùng lặp)
        // Nếu target của A trùng với target của B -> Cần tìm vị trí trống
        HashSet<Vector2Int> targetAtoB = new HashSet<Vector2Int>(); // A muốn đến chỗ B
        HashSet<Vector2Int> targetBtoA = new HashSet<Vector2Int>(); // B muốn đến chỗ A

        // Tìm vị trí A muốn đến mà B đang chiếm (hoặc B cũng muốn đến)
        foreach (var pos in targetPositionsA)
        {
            bool bOccupies = groupB.blocks.Any(b => b.gridPos == pos);
            bool bAlsoWants = targetPositionsB.Contains(pos);
            if (bOccupies || bAlsoWants)
            {
                targetAtoB.Add(pos);
            }
        }

        // Tìm vị trí B muốn đến mà A đang chiếm (hoặc A cũng muốn đến)
        foreach (var pos in targetPositionsB)
        {
            bool aOccupies = groupA.blocks.Any(b => b.gridPos == pos);
            bool aAlsoWants = targetPositionsA.Contains(pos);
            if (aOccupies || aAlsoWants)
            {
                targetBtoA.Add(pos);
            }
        }

        // Bước 3: Đếm số vị trí bị trùng
        int conflictCount = targetAtoB.Count;
        if (conflictCount == 0)
        {
            // Không có trùng lặp -> Swap bình thường
            ExecuteSwap(groupA, groupB, offset);
            return true;
        }

        // Bước 4: Tìm vị trí trống cho các block bị trùng
        // Các vị trí TRONG grid mà không có block nào
        List<Vector2Int> emptyPositions = GetEmptyPositions();

        if (emptyPositions.Count < conflictCount)
        {
            // Không đủ chỗ trống -> Không thể swap
            return false;
        }

        // Bước 5: Gom các block cần tìm chỗ mới
        List<Block> blocksNeedEmptyA = new List<Block>(); // A đang ở chỗ B muốn đến
        List<Block> blocksNeedEmptyB = new List<Block>(); // B đang ở chỗ A muốn đến

        foreach (var b in groupA.blocks)
        {
            if (targetAtoB.Contains(b.gridPos + offset))
            {
                blocksNeedEmptyA.Add(b);
            }
        }

        foreach (var b in groupB.blocks)
        {
            if (targetBtoA.Contains(b.gridPos - offset))
            {
                blocksNeedEmptyB.Add(b);
            }
        }

        // Bước 6: Thực hiện swap với logic đặc biệt
        return ExecuteSwapWithEmptyPositions(groupA, groupB, offset, emptyPositions,
                                              blocksNeedEmptyA, blocksNeedEmptyB);
    }

    /// <summary>
    /// Thực hiện swap cơ bản (không có trùng lặp vị trí)
    /// </summary>
    void ExecuteSwap(BlockGroup groupA, BlockGroup groupB, Vector2Int offset)
    {
        // Di chuyển nhóm B trước (để giải phóng chỗ cho A)
        foreach (var b in groupB.blocks)
        {
            b.gridPos -= offset;
        }

        // Di chuyển nhóm A
        foreach (var b in groupA.blocks)
        {
            b.gridPos += offset;
        }

        UpdateAllBlockPositions();
        CheckAndMergeGroups();
    }

    /// <summary>
    /// Thực hiện swap với việc tìm vị trí trống cho các block bị trùng
    /// </summary>
    bool ExecuteSwapWithEmptyPositions(BlockGroup groupA, BlockGroup groupB, Vector2Int offset,
                                        List<Vector2Int> emptyPositions, 
                                        List<Block> blocksNeedEmptyA,
                                        List<Block> blocksNeedEmptyB)
    {
        // Danh sách vị trí trống đã được sử dụng
        HashSet<Vector2Int> usedEmpty = new HashSet<Vector2Int>();

        // Dictionary lưu vị trí mới cho mỗi block
        Dictionary<Block, Vector2Int> newPositions = new Dictionary<Block, Vector2Int>();

        // Bước 1: Xử lý block của A cần vị trí trống
        foreach (var block in blocksNeedEmptyA)
        {
            Vector2Int currentTarget = block.gridPos + offset;

            // Tìm vị trí trống gần nhất với vị trí block muốn đến
            Vector2Int bestEmpty = FindNearestEmptyPositionForSet(currentTarget, emptyPositions, usedEmpty);
            usedEmpty.Add(bestEmpty);
            newPositions[block] = bestEmpty;
        }

        // Bước 2: Xử lý block của B cần vị trí trống
        foreach (var block in blocksNeedEmptyB)
        {
            Vector2Int currentTarget = block.gridPos - offset;

            Vector2Int bestEmpty = FindNearestEmptyPositionForSet(currentTarget, emptyPositions, usedEmpty);
            usedEmpty.Add(bestEmpty);
            newPositions[block] = bestEmpty;
        }

        // Bước 3: Các block còn lại (không bị trùng) - swap bình thường
        // A đi đến chỗ B cũ (không bị trùng)
        // B đi đến chỗ A cũ (không bị trùng)

        // Bước 4: Cập nhật gridPos cho TẤT CẢ blocks
        // Đầu tiên, đánh dấu blocks cần vị trí trống
        HashSet<Block> blocksWithEmpty = new HashSet<Block>();
        foreach (var block in blocksNeedEmptyA) blocksWithEmpty.Add(block);
        foreach (var block in blocksNeedEmptyB) blocksWithEmpty.Add(block);

        // Cập nhật blocks có vị trí trống
        foreach (var kvp in newPositions)
        {
            kvp.Key.gridPos = kvp.Value;
        }

        // Cập nhật blocks còn lại của A (đi đến chỗ B cũ)
        foreach (var b in groupA.blocks)
        {
            if (!blocksWithEmpty.Contains(b))
            {
                b.gridPos += offset;
            }
        }

        // Cập nhật blocks còn lại của B (đi đến chỗ A cũ)
        foreach (var b in groupB.blocks)
        {
            if (!blocksWithEmpty.Contains(b))
            {
                b.gridPos -= offset;
            }
        }

        UpdateAllBlockPositions();
        CheckAndMergeGroups();
        return true;
    }

    /// <summary>
    /// Tìm vị trí trống gần nhất (từ tập hợp cho phép)
    /// </summary>
    Vector2Int FindNearestEmptyPositionForSet(Vector2Int fromPos, 
                                               List<Vector2Int> allEmpty, 
                                               HashSet<Vector2Int> usedEmpty)
    {
        Vector2Int best = allEmpty[0];
        float minDist = float.MaxValue;

        foreach (var empty in allEmpty)
        {
            if (usedEmpty.Contains(empty)) continue;

            float dist = Vector2Int.Distance(fromPos, empty);
            if (dist < minDist)
            {
                minDist = dist;
                best = empty;
            }
        }
        return best;
    }

    // =========================================
    // CẬP NHẬT DRAG HANDLER
    // =========================================

    /// <summary>
    /// Di chuyển nhóm với offset và tự động swap với các nhóm khác
    /// </summary>
    public bool MoveGroupWithPush(BlockGroup draggedGroup, Vector2Int offset)
    {
        // 1. Tính vị trí mới của tất cả blocks trong nhóm kéo
        List<Vector2Int> targetPositions = draggedGroup.blocks.Select(b => b.gridPos + offset).ToList();

        // 2. Tìm tất cả blocks bị "chiếm chỗ"
        var hitBlocks = GetBlocksAtPositions(targetPositions, draggedGroup);

        // 3. Nếu không có ai bị chiếm chỗ -> Di chuyển bình thường
        if (hitBlocks.Count == 0)
        {
            foreach (var b in draggedGroup.blocks)
            {
                b.gridPos += offset;
            }
            UpdateAllBlockPositions();
            CheckAndMergeGroups();
            return true;
        }

        // 4. Gom các nhóm bị ảnh hưởng (loại bỏ trùng lặp)
        HashSet<BlockGroup> affectedGroups = new HashSet<BlockGroup>();
        foreach (var hb in hitBlocks)
        {
            if (hb.group != draggedGroup)
                affectedGroups.Add(hb.group);
        }

        if (affectedGroups.Count == 0)
        {
            // Không có nhóm nào bị ảnh hưởng -> Di chuyển bình thường
            foreach (var b in draggedGroup.blocks)
            {
                b.gridPos += offset;
            }
            UpdateAllBlockPositions();
            CheckAndMergeGroups();
            return true;
        }

        // 5. Swap với nhóm bị ảnh hưởng đầu tiên
        var groupB = affectedGroups.First();

        // Kiểm tra giới hạn grid
        if (!CanMoveGroup(draggedGroup, offset) || !CanMoveGroup(groupB, -offset))
        {
            return false;
        }

        // 6. Thực hiện swap
        // Bước 1: Di chuyển blocks của groupB đến vị trí mới
        foreach (var b in groupB.blocks)
        {
            b.gridPos -= offset;
        }

        // Bước 2: Di chuyển blocks của draggedGroup đến vị trí mới
        foreach (var b in draggedGroup.blocks)
        {
            b.gridPos += offset;
        }

        // Bước 3: Cập nhật visual
        UpdateAllBlockPositions();

        // Bước 4: Kiểm tra merge (nếu các block đã đúng vị trí liền kề)
        CheckAndMergeGroups();

        return true;
    }
}