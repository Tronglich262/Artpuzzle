using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PuzzleManager : MonoBehaviour
{
    [Header("Level System")]
    public List<PuzzleLevel> levels;
    public int currentLevelIndex = 0;

    [SerializeField] public Sprite sourceImage;
    [SerializeField] public GameObject blockPrefab;
    [SerializeField] public int rows = 3;
    [SerializeField] public int cols = 3;
    [SerializeField] public float blockSize = 100f;
    [SerializeField] private RectTransform boardRoot;

    // Đánh dấu hệ thống đang tween để chặn input liên tiếp
    public bool isTweening { get; private set; }
    public void SetTweening(bool state) => isTweening = state;
    // Danh sách toàn bộ block hiện có trên board
    [HideInInspector] public List<Block> currentBlocks = new();
    // Ma trận lưu trạng thái board theo grid
    private Block[,] grid;
    public static PuzzleManager Instance { get; private set; }

    // 4 hướng cơ bản để check neighbor
    private static readonly Vector2Int[] FourDirs =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    /// <summary>
    /// Khởi tạo singleton.
    /// Nếu đã có instance khác thì destroy object hiện tại.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Tạo puzzle ban đầu và shuffle vị trí block.
    /// </summary>
    private void Start()
    {
        GeneratePuzzle();
        ShuffleBlocks();
    }

    /// <summary>
    /// Tạo toàn bộ block theo rows/cols từ source image.
    /// Mỗi block ban đầu có correctPos và gridPos trùng nhau.
    /// Đồng thời tạo mỗi block nằm trong 1 group riêng.
    /// </summary>
    public void GeneratePuzzle()
    {
        foreach (Transform child in boardRoot)
            Destroy(child.gameObject);

        currentBlocks.Clear();
        grid = new Block[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                GameObject obj = Instantiate(blockPrefab, boardRoot);
                Block block = obj.GetComponent<Block>();

                block.gridPos = new Vector2Int(r, c);
                block.correctPos = new Vector2Int(r, c);

                SetupBlockVisual(block, r, c);

                currentBlocks.Add(block);
                grid[r, c] = block;

                BlockGroup group = CreateSingleBlockGroup();
                block.group = group;
                group.blocks.Add(block);
                block.transform.SetParent(group.root, false);
                block.SetOutline(true);
            }
        }

        UpdateAllBlockPositions(false);
    }

    /// <summary>
    /// Tạo 1 group mới chỉ chứa 1 block.
    /// Mỗi group có root riêng để drag/move theo nhóm.
    /// </summary>
    private BlockGroup CreateSingleBlockGroup()
    {
        var group = new BlockGroup();

        GameObject rootObj = new("GroupRoot", typeof(RectTransform));
        RectTransform rootRect = rootObj.GetComponent<RectTransform>();
        rootRect.SetParent(boardRoot, false);
        rootRect.localScale = Vector3.one;
        rootRect.anchoredPosition = Vector2.zero;

        group.root = rootRect;
        return group;
    }

    /// <summary>
    /// Cắt sprite con từ ảnh gốc theo vị trí row/col
    /// rồi gán cho block tương ứng.
    /// </summary>
    private void SetupBlockVisual(Block block, int row, int col)
    {
        int texW = sourceImage.texture.width;
        int texH = sourceImage.texture.height;
        int spriteW = texW / cols;
        int spriteH = texH / rows;

        Rect rect = new(
            col * spriteW,
            texH - (row + 1) * spriteH,
            spriteW,
            spriteH
        );

        block.img.sprite = Sprite.Create(sourceImage.texture, rect, new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Xử lý move chính của 1 group:
    /// 1. Tính vị trí đích
    /// 2. Tìm block va chạm
    /// 3. Thử swap
    /// 4. Nếu không swap được thì push
    /// 5. Cập nhật grid, position, group merge/split
    /// </summary>
    public bool MoveGroupWithPush(
    BlockGroup draggedGroup,
    Vector2Int offset,
    BlockDragHandler.DragMoveType moveType,
    bool animate = true)
    {
        if (draggedGroup == null || draggedGroup.blocks.Count == 0)
            return false;

        List<Block> draggedBlocks = GetValidBlocks(draggedGroup.blocks);
        if (draggedBlocks.Count == 0)
            return false;

        HashSet<Block> draggedSet = new(draggedBlocks);
        Dictionary<Block, Vector2Int> finalPositions = new(draggedBlocks.Count);
        HashSet<Vector2Int> occupiedTargets = new();

        for (int i = 0; i < draggedBlocks.Count; i++)
        {
            Block block = draggedBlocks[i];
            Vector2Int target = block.gridPos + offset;

            if (!IsInside(target))
                return false;

            if (!occupiedTargets.Add(target))
            {
                Debug.LogError($"Duplicate target detected while moving group: {target}");
                return false;
            }

            finalPositions[block] = target;
        }

        List<Block> hitBlocks = CollectHitBlocks(draggedBlocks, draggedSet, finalPositions);
        bool swapped = TrySwapBlocks(draggedBlocks, draggedSet, finalPositions);

        if (!swapped)
        {
            if (!PushHitBlocks(hitBlocks, draggedGroup, occupiedTargets, moveType))
                return false;

            ApplyDraggedPositions(draggedBlocks, finalPositions);
        }
        RebuildGridFromBlocksStrict();
        ValidateAllGroups();
        SplitDisconnectedGroups();
        Invoke(nameof(CheckAndMergeGroups), 0.4f);
        RebuildGridFromBlocksStrict();
        SortDraggedAndHitLayers(draggedGroup, hitBlocks);
        UpdateAllBlockPositions(animate);

        return true;
    }

    /// <summary>
    /// Lọc ra các block hợp lệ khác null từ 1 list.
    /// </summary>
    private List<Block> GetValidBlocks(List<Block> source)
    {
        List<Block> result = new(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                result.Add(source[i]);
        }
        return result;
    }

    /// <summary>
    /// Lấy danh sách các block bị group đang drag đụng vào ở vị trí target.
    /// Không lấy block nằm trong cùng group đang drag.
    /// </summary>
    private List<Block> CollectHitBlocks(
        List<Block> draggedBlocks,
        HashSet<Block> draggedSet,
        Dictionary<Block, Vector2Int> finalPositions)
    {
        List<Block> hitBlocks = new();
        HashSet<Block> hitSet = new();

        for (int i = 0; i < draggedBlocks.Count; i++)
        {
            Block dragged = draggedBlocks[i];
            Block hit = GetBlockAt(finalPositions[dragged]);

            if (hit == null || draggedSet.Contains(hit) || hit.group == dragged.group)
                continue;

            if (hitSet.Add(hit))
                hitBlocks.Add(hit);
        }

        return hitBlocks;
    }

    /// <summary>
    /// Thử swap block-by-block giữa group kéo và block ở vị trí đích.
    /// Chỉ thành công khi mọi target đều có block khác hợp lệ để swap.
    /// </summary>
    private bool TrySwapBlocks(
        List<Block> draggedBlocks,
        HashSet<Block> draggedSet,
        Dictionary<Block, Vector2Int> finalPositions)
    {
        if (draggedBlocks.Count == 0)
            return false;

        List<(Block dragged, Block hit)> pairs = new(draggedBlocks.Count);
        HashSet<Block> usedHits = new();

        // Kiểm tra điều kiện swap hợp lệ
        for (int i = 0; i < draggedBlocks.Count; i++)
        {
            Block dragged = draggedBlocks[i];
            Block hit = GetBlockAt(finalPositions[dragged]);

            if (hit == null || draggedSet.Contains(hit) || hit.group == dragged.group || !usedHits.Add(hit))
                return false;

            pairs.Add((dragged, hit));
        }

        // Đổi chỗ gridPos giữa từng cặp block
        for (int i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            Vector2Int oldDraggedPos = pair.dragged.gridPos;
            pair.dragged.gridPos = pair.hit.gridPos;
            pair.hit.gridPos = oldDraggedPos;
        }

        return true;
    }

    /// <summary>
    /// Gán gridPos mới cho toàn bộ block đang drag
    /// sau khi push thành công hoặc move thường.
    /// </summary>
    private void ApplyDraggedPositions(List<Block> draggedBlocks, Dictionary<Block, Vector2Int> finalPositions)
    {
        for (int i = 0; i < draggedBlocks.Count; i++)
        {
            Block block = draggedBlocks[i];
            block.gridPos = finalPositions[block];
        }
    }

    /// <summary>
    /// Đẩy các block bị va chạm sang ô trống phù hợp.
    /// Ưu tiên theo hướng kéo (ngang, dọc, chéo).
    /// Nếu block hit đang nằm trong group > 1 block thì tách ra trước khi đẩy.
    /// </summary>
    private bool PushHitBlocks(
        List<Block> hitBlocks,
        BlockGroup draggedGroup,
        HashSet<Vector2Int> occupiedTargets,
        BlockDragHandler.DragMoveType moveType)
    {
        HashSet<Vector2Int> available = new(GetEmptyPositions());

        // Cho phép dùng vị trí cũ của group kéo làm chỗ chứa block bị push
        for (int i = 0; i < draggedGroup.blocks.Count; i++)
        {
            Block block = draggedGroup.blocks[i];
            if (block != null)
                available.Add(block.gridPos);
        }

        // Loại bỏ các ô mà group kéo sẽ chiếm sau cùng
        foreach (Vector2Int pos in occupiedTargets)
            available.Remove(pos);

        HashSet<Vector2Int> used = new();
        Dictionary<Block, Vector2Int> hitMoves = new();

        // Tìm vị trí push tốt nhất cho từng block bị đụng
        for (int i = 0; i < hitBlocks.Count; i++)
        {
            Block hit = hitBlocks[i];
            if (hit == null) continue;

            if (!TryFindBestPushSpot(hit.gridPos, available, used, moveType, out Vector2Int bestSpot))
                return false;

            hitMoves[hit] = bestSpot;
            used.Add(bestSpot);
        }

        // Apply vị trí push
        for (int i = 0; i < hitBlocks.Count; i++)
        {
            Block hit = hitBlocks[i];
            if (hit == null) continue;

            if (hit.group != null && hit.group.blocks.Count > 1)
                hit.group.SplitByBlocks(new List<Block> { hit }, transform);

            hit.gridPos = hitMoves[hit];
        }

        return true;
    }

    /// <summary>
    /// Tìm ô push tốt nhất cho 1 block.
    /// Ưu tiên ô đúng cùng hướng drag, nếu không có thì lấy ô gần nhất còn lại.
    /// </summary>
    private bool TryFindBestPushSpot(
        Vector2Int origin,
        HashSet<Vector2Int> available,
        HashSet<Vector2Int> used,
        BlockDragHandler.DragMoveType moveType,
        out Vector2Int bestSpot)
    {
        bestSpot = default;

        bool foundPriority = false;
        bool foundFallback = false;
        float bestPriorityDist = float.MaxValue;
        float bestFallbackDist = float.MaxValue;

        foreach (Vector2Int pos in available)
        {
            if (used.Contains(pos))
                continue;

            float dist = (origin - pos).sqrMagnitude;

            if (MatchMoveType(origin, pos, moveType))
            {
                if (dist < bestPriorityDist)
                {
                    bestPriorityDist = dist;
                    bestSpot = pos;
                    foundPriority = true;
                }
            }
            else if (!foundPriority && dist < bestFallbackDist)
            {
                bestFallbackDist = dist;
                bestSpot = pos;
                foundFallback = true;
            }
        }

        return foundPriority || foundFallback;
    }

    /// <summary>
    /// Kiểm tra 2 ô có cùng trục ưu tiên theo kiểu kéo hay không.
    /// Horizontal: cùng hàng
    /// Vertical: cùng cột
    /// Diagonal: cùng đường chéo
    /// </summary>
    private bool MatchMoveType(Vector2Int origin, Vector2Int target, BlockDragHandler.DragMoveType moveType)
    {
        return moveType switch
        {
            BlockDragHandler.DragMoveType.Horizontal => target.x == origin.x,
            BlockDragHandler.DragMoveType.Vertical => target.y == origin.y,
            BlockDragHandler.DragMoveType.Diagonal => Mathf.Abs(target.x - origin.x) == Mathf.Abs(target.y - origin.y),
            _ => false
        };
    }

    /// <summary>
    /// Quét toàn bộ block để merge các group nếu có 2 block
    /// đang đứng cạnh nhau đúng theo correctPos gốc.
    /// </summary>
    public void CheckAndMergeGroups()
    {
        bool foundMerge = true;

        while (foundMerge)
        {
            foundMerge = false;

            for (int i = 0; i < currentBlocks.Count; i++)
            {
                Block a = currentBlocks[i];
                if (a == null || a.group == null) continue;

                for (int d = 0; d < FourDirs.Length; d++)
                {
                    Block b = GetBlockAt(a.gridPos + FourDirs[d]);
                    if (b == null || b.group == null || a.group == b.group) continue;

                    if (!IsCorrectNeighbor(a, b)) continue;

                    a.group.Merge(b.group);
                    foundMerge = true;
                    break;
                }

                if (foundMerge)
                    break;
            }
        }
    }

    /// <summary>
    /// Kiểm tra 2 block có phải hàng xóm đúng theo ảnh gốc hay không.
    /// Điều kiện:
    /// - đang đứng cạnh nhau trên grid
    /// - độ lệch hiện tại giống độ lệch correctPos
    /// </summary>
    public bool IsCorrectNeighbor(Block a, Block b)
    {
        Vector2Int gridDiff = a.gridPos - b.gridPos;
        return gridDiff.sqrMagnitude == 1 && gridDiff == (a.correctPos - b.correctPos);
    }

    /// <summary>
    /// Lấy các block hàng xóm đúng của current trong cùng 1 group.
    /// Dùng để validate hoặc split group.
    /// </summary>
    public List<Block> GetCorrectNeighborsInSameGroup(Block current, BlockGroup group)
    {
        List<Block> result = new(4);

        if (current == null || group == null)
            return result;

        for (int i = 0; i < FourDirs.Length; i++)
        {
            Block neighbor = GetBlockAt(current.gridPos + FourDirs[i]);
            if (neighbor != null && neighbor.group == group && IsCorrectNeighbor(current, neighbor))
                result.Add(neighbor);
        }

        return result;
    }

    /// <summary>
    /// Cập nhật vị trí UI của tất cả block theo gridPos.
    /// Nếu animate = true thì tween về đúng ô.
    /// Nếu animate = false thì set luôn.
    /// </summary>
    public void UpdateAllBlockPositions(bool animate = true)
    {
        List<(Block block, RectTransform rt, Vector2 snappedPos)> validTargets = new();

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block == null) continue;

            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt == null) continue;

            rt.DOKill();

            block.targetPosition = GridToPosition(block.gridPos);
            Vector2 snappedPos = new(
                Mathf.Round(block.targetPosition.x),
                Mathf.Round(block.targetPosition.y)
            );

            validTargets.Add((block, rt, snappedPos));
        }

        if (!animate)
        {
            for (int i = 0; i < validTargets.Count; i++)
            {
                validTargets[i].rt.anchoredPosition = validTargets[i].snappedPos;
            }

            isTweening = false;
            return;
        }

        if (validTargets.Count == 0)
        {
            isTweening = false;
            return;
        }

        isTweening = true;
        int completed = 0;
        int total = validTargets.Count;

        for (int i = 0; i < validTargets.Count; i++)
        {
            var item = validTargets[i];

            item.rt.DOAnchorPos(item.snappedPos, 0.5f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    completed++;
                    if (completed >= total)
                        isTweening = false;
                })
                .OnKill(() =>
                {
                    completed++;
                    if (completed >= total)
                        isTweening = false;
                });
        }
    }

    /// <summary>
    /// Chuyển tọa độ grid sang anchoredPosition trên UI board.
    /// </summary>
    public Vector2 GridToPosition(Vector2Int pos)
    {
        float startX = -((cols - 1) * blockSize) * 0.5f;
        float startY = ((rows - 1) * blockSize) * 0.5f;

        return new Vector2(
            startX + pos.y * blockSize,
            startY - pos.x * blockSize
        );
    }

    /// <summary>
    /// Chuyển vị trí UI về tọa độ grid gần nhất.
    /// Dùng khi thả drag để xác định offset move.
    /// </summary>
    public Vector2Int PositionToGrid(Vector2 pos)
    {
        float startX = -((cols - 1) * blockSize) * 0.5f;
        float startY = ((rows - 1) * blockSize) * 0.5f;

        int col = Mathf.RoundToInt((pos.x - startX) / blockSize);
        int row = Mathf.RoundToInt((startY - pos.y) / blockSize);

        return new Vector2Int(row, col);
    }

    /// <summary>
    /// Trả về danh sách các ô trống hiện tại trên board.
    /// </summary>
    public List<Vector2Int> GetEmptyPositions()
    {
        List<Vector2Int> empty = new();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (grid[r, c] == null)
                    empty.Add(new Vector2Int(r, c));
            }
        }

        return empty;
    }

    /// <summary>
    /// Xáo trộn vị trí tất cả block trên board.
    /// Sau đó rebuild grid và update lại UI.
    /// </summary>
    public void ShuffleBlocks()
    {
        List<Vector2Int> positions = new(rows * cols);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                positions.Add(new Vector2Int(r, c));
            }
        }

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            int rnd = Random.Range(0, positions.Count);
            currentBlocks[i].gridPos = positions[rnd];
            positions.RemoveAt(rnd);
        }

        RebuildGridFromBlocksStrict();
        UpdateAllBlockPositions();
    }

    /// <summary>
    /// Kiểm tra cả group có thể move theo shift mà không ra khỏi board hay không.
    /// Chỉ check biên, chưa check collision.
    /// </summary>
    public bool CanMoveGroup(BlockGroup group, Vector2Int shift)
    {
        if (group == null) return false;

        for (int i = 0; i < group.blocks.Count; i++)
        {
            Block block = group.blocks[i];
            if (block == null) continue;

            if (!IsInside(block.gridPos + shift))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Validate toàn bộ group:
    /// nếu block nào không còn correct neighbor trong group
    /// thì tách block đó ra khỏi group hiện tại.
    /// </summary>
    public void ValidateAllGroups()
    {
        List<BlockGroup> groups = new();
        HashSet<BlockGroup> uniqueGroups = new();

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block != null && block.group != null && uniqueGroups.Add(block.group))
                groups.Add(block.group);
        }
        for (int i = 0; i < groups.Count; i++)
        {
            BlockGroup group = groups[i];
            if (group == null || group.blocks == null || group.blocks.Count <= 1)
                continue;

            List<Block> snapshot = new(group.blocks);
            List<Block> isolatedBlocks = new();

            for (int j = 0; j < snapshot.Count; j++)
            {
                Block block = snapshot[j];
                if (block == null)
                    continue;
                if (block.group != group)
                    continue;
                List<Block> neighbors = GetCorrectNeighborsInSameGroup(block, group);
                if (neighbors.Count == 0)
                    isolatedBlocks.Add(block);
            }
            if (isolatedBlocks.Count == 0)
                continue;
            if (isolatedBlocks.Count >= snapshot.Count)
                continue;

            group.SplitByBlocks(isolatedBlocks, transform);
        }
    }

    /// <summary>
    /// Tách các group bị đứt kết nối thành nhiều subgroup độc lập.
    /// </summary>
    public void SplitDisconnectedGroups()
    {
        List<BlockGroup> groups = new();
        HashSet<BlockGroup> uniqueGroups = new();

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block != null && block.group != null && uniqueGroups.Add(block.group))
                groups.Add(block.group);
        }

        for (int i = 0; i < groups.Count; i++)
        {
            groups[i]?.SplitIfDisconnected(transform);
        }
    }

    /// <summary>
    /// Sắp xếp layer hiển thị:
    /// group đang drag nằm trên cùng,
    /// các group bị hit nằm ngay phía dưới để render đẹp hơn.
    /// </summary>
    private void SortDraggedAndHitLayers(BlockGroup draggedGroup, List<Block> hitBlocks)
    {
        if (draggedGroup == null || draggedGroup.root == null)
            return;

        draggedGroup.root.SetAsLastSibling();

        int currentIndex = draggedGroup.root.GetSiblingIndex() - 1;
        HashSet<BlockGroup> sortedGroups = new();

        for (int i = 0; i < hitBlocks.Count; i++)
        {
            Block hit = hitBlocks[i];
            if (hit == null || hit.group == null || hit.group.root == null)
                continue;

            if (!sortedGroups.Add(hit.group))
                continue;

            hit.group.root.SetSiblingIndex(Mathf.Max(0, currentIndex));
            currentIndex--;
        }
    }

    /// <summary>
    /// Lấy block tại 1 ô grid.
    /// Nếu ngoài biên thì trả null.
    /// </summary>
    public Block GetBlockAt(Vector2Int pos)
    {
        return IsInside(pos) ? grid[pos.x, pos.y] : null;
    }

    /// <summary>
    /// Kiểm tra 1 tọa độ grid có nằm trong board không.
    /// </summary>
    public bool IsInside(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < rows && pos.y >= 0 && pos.y < cols;
    }

    /// <summary>
    /// Đồng bộ lại ma trận grid từ danh sách currentBlocks.
    /// Dùng sau khi move/swap/push/shuffle để bảo đảm board state đúng.
    /// </summary>
    public void RebuildGridFromBlocksStrict()
    {
        if (grid == null || grid.GetLength(0) != rows || grid.GetLength(1) != cols)
            grid = new Block[rows, cols];
        else
            System.Array.Clear(grid, 0, grid.Length);

        HashSet<Block> visited = new();

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block == null) continue;

            if (!visited.Add(block))
            {
                Debug.LogError($"Duplicate block reference in currentBlocks: {block.name}");
                continue;
            }

            if (!IsInside(block.gridPos))
            {
                Debug.LogError($"Block out of range: {block.name} => {block.gridPos}");
                continue;
            }

            Block existing = grid[block.gridPos.x, block.gridPos.y];
            if (existing != null && existing != block)
            {
                Debug.LogError(
                    $"Duplicate gridPos detected at {block.gridPos}. Existing={existing.name}, New={block.name}"
                );
                continue;
            }

            grid[block.gridPos.x, block.gridPos.y] = block;
        }
    }
}