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

    // Đánh dấu đang tween để chặn input liên tiếp
    public bool isTweening { get; private set; }
    public void SetTweening(bool state) => isTweening = state;

    // Toàn bộ block hiện có trên board
    [HideInInspector] public List<Block> currentBlocks = new();

    // Grid logic của board
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
    /// Tạo puzzle và shuffle lúc bắt đầu game.
    /// </summary>
    private void Start()
    {
        GeneratePuzzle();
        ShuffleBlocks();
    }

    /// <summary>
    /// Tạo toàn bộ block theo rows/cols.
    /// Mỗi block ban đầu có 1 group riêng.
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
                group.RebuildLocalLayout(true);
            }
        }

        UpdateAllBlockPositions(false);
    }

    /// <summary>
    /// Tạo 1 group mới có root riêng.
    /// Root này dùng để kéo cả cụm block.
    /// </summary>
    private BlockGroup CreateSingleBlockGroup()
    {
        var group = new BlockGroup();

        GameObject rootObj = new GameObject("GroupRoot", typeof(RectTransform));
        RectTransform rootRect = rootObj.GetComponent<RectTransform>();

        rootRect.SetParent(boardRoot, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;
        rootRect.localScale = Vector3.one;
        rootRect.anchoredPosition = Vector2.zero;

        group.root = rootRect;
        return group;
    }

    /// <summary>
    /// Cắt sprite con từ ảnh gốc theo row/col rồi gán cho block.
    /// </summary>
    private void SetupBlockVisual(Block block, int row, int col)
    {
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

        block.img.sprite = Sprite.Create(sourceImage.texture, rect, new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Move chính của group:
    /// - tính target
    /// - thử swap
    /// - nếu không swap thì push
    /// - rebuild grid, split/merge, rồi update UI
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

        HashSet<Block> draggedSet = new HashSet<Block>(draggedBlocks);
        Dictionary<Block, Vector2Int> finalPositions = new Dictionary<Block, Vector2Int>(draggedBlocks.Count);
        HashSet<Vector2Int> occupiedTargets = new HashSet<Vector2Int>();

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
    /// Lọc block khác null từ list đầu vào.
    /// </summary>
    private List<Block> GetValidBlocks(List<Block> source)
    {
        List<Block> result = new List<Block>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                result.Add(source[i]);
        }

        return result;
    }

    /// <summary>
    /// Lấy các block bị group đang kéo đụng vào ở vị trí đích.
    /// Không lấy block nằm trong chính group đang kéo.
    /// </summary>
    private List<Block> CollectHitBlocks(
        List<Block> draggedBlocks,
        HashSet<Block> draggedSet,
        Dictionary<Block, Vector2Int> finalPositions)
    {
        List<Block> hitBlocks = new List<Block>();
        HashSet<Block> hitSet = new HashSet<Block>();

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
    /// Thử swap block-by-block.
    /// Chỉ thành công khi mọi target đều có block hợp lệ để đổi chỗ.
    /// </summary>
    private bool TrySwapBlocks(
        List<Block> draggedBlocks,
        HashSet<Block> draggedSet,
        Dictionary<Block, Vector2Int> finalPositions)
    {
        if (draggedBlocks.Count == 0)
            return false;

        List<(Block dragged, Block hit)> pairs = new List<(Block dragged, Block hit)>(draggedBlocks.Count);
        HashSet<Block> usedHits = new HashSet<Block>();

        for (int i = 0; i < draggedBlocks.Count; i++)
        {
            Block dragged = draggedBlocks[i];
            Block hit = GetBlockAt(finalPositions[dragged]);

            if (hit == null || draggedSet.Contains(hit) || hit.group == dragged.group || !usedHits.Add(hit))
                return false;

            pairs.Add((dragged, hit));
        }

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
    /// Gán gridPos mới cho toàn bộ block đang kéo.
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
    /// Đẩy block bị va chạm sang ô trống phù hợp.
    /// Nếu block đang thuộc group lớn thì tách ra trước khi đẩy.
    /// </summary>
    private bool PushHitBlocks(
        List<Block> hitBlocks,
        BlockGroup draggedGroup,
        HashSet<Vector2Int> occupiedTargets,
        BlockDragHandler.DragMoveType moveType)
    {
        HashSet<Vector2Int> available = new HashSet<Vector2Int>(GetEmptyPositions());

        // Cho phép dùng vị trí cũ của group kéo làm chỗ chứa
        for (int i = 0; i < draggedGroup.blocks.Count; i++)
        {
            Block block = draggedGroup.blocks[i];
            if (block != null)
                available.Add(block.gridPos);
        }

        // Loại bỏ các ô group kéo sẽ chiếm
        foreach (Vector2Int pos in occupiedTargets)
            available.Remove(pos);

        HashSet<Vector2Int> used = new HashSet<Vector2Int>();
        Dictionary<Block, Vector2Int> hitMoves = new Dictionary<Block, Vector2Int>();

        for (int i = 0; i < hitBlocks.Count; i++)
        {
            Block hit = hitBlocks[i];
            if (hit == null) continue;

            if (!TryFindBestPushSpot(hit.gridPos, available, used, moveType, out Vector2Int bestSpot))
                return false;

            hitMoves[hit] = bestSpot;
            used.Add(bestSpot);
        }

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
    /// Tìm ô trống tốt nhất để push.
    /// Ưu tiên ô cùng hướng kéo, nếu không có thì lấy ô gần nhất.
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
    /// Check target có đúng hướng kéo hay không.
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
    /// Quét toàn bộ board để merge group nếu 2 block đang đứng cạnh nhau đúng.
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
    /// Check 2 block có là hàng xóm đúng theo ảnh gốc không.
    /// </summary>
    public bool IsCorrectNeighbor(Block a, Block b)
    {
        Vector2Int gridDiff = a.gridPos - b.gridPos;
        return gridDiff.sqrMagnitude == 1 && gridDiff == (a.correctPos - b.correctPos);
    }

    /// <summary>
    /// Lấy các block hàng xóm đúng của current trong cùng group.
    /// </summary>
    public List<Block> GetCorrectNeighborsInSameGroup(Block current, BlockGroup group)
    {
        List<Block> result = new List<Block>(4);

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
    /// Cập nhật vị trí UI của tất cả group theo gridPos.
    /// Animate thì tween root, không animate thì set thẳng.
    /// </summary>
    public void UpdateAllBlockPositions(bool animate = true)
    {
        HashSet<BlockGroup> handledGroups = new HashSet<BlockGroup>();
        List<(RectTransform root, Vector2 targetPos)> groupMoves = new List<(RectTransform, Vector2)>();

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block == null || block.group == null || block.group.root == null)
                continue;

            BlockGroup group = block.group;
            if (!handledGroups.Add(group))
                continue;

            RectTransform rootRect = group.root as RectTransform;
            if (rootRect == null)
                continue;

            Block anchor = group.GetAnchorBlock();
            if (anchor == null)
                continue;

            Vector2 targetRootPos = GridToPosition(anchor.gridPos);

            group.RebuildLocalLayout(false);

            rootRect.DOKill(false);
            groupMoves.Add((rootRect, targetRootPos));
        }

        if (!animate)
        {
            for (int i = 0; i < groupMoves.Count; i++)
                groupMoves[i].root.anchoredPosition = groupMoves[i].targetPos;

            isTweening = false;
            return;
        }

        if (groupMoves.Count == 0)
        {
            isTweening = false;
            return;
        }

        isTweening = true;
        int completed = 0;
        int total = groupMoves.Count;

        for (int i = 0; i < groupMoves.Count; i++)
        {
            var item = groupMoves[i];

            item.root.DOAnchorPos(item.targetPos, 0.5f)
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
    /// Đổi tọa độ grid sang vị trí UI trên board.
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
    /// Đổi vị trí UI về ô grid gần nhất.
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
    /// Lấy toàn bộ ô trống hiện tại trên board.
    /// </summary>
    public List<Vector2Int> GetEmptyPositions()
    {
        List<Vector2Int> empty = new List<Vector2Int>();

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
    /// Xáo trộn vị trí tất cả block rồi rebuild grid và update UI.
    /// </summary>
    public void ShuffleBlocks()
    {
        List<Vector2Int> positions = new List<Vector2Int>(rows * cols);

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
    /// Check cả group có đi theo shift mà không ra ngoài board không.
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
    /// Validate group:
    /// block nào không còn neighbor đúng trong group thì tách ra.
    /// </summary>
    public void ValidateAllGroups()
    {
        List<BlockGroup> groups = new List<BlockGroup>();
        HashSet<BlockGroup> uniqueGroups = new HashSet<BlockGroup>();

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

            List<Block> snapshot = new List<Block>(group.blocks);
            List<Block> isolatedBlocks = new List<Block>();

            for (int j = 0; j < snapshot.Count; j++)
            {
                Block block = snapshot[j];
                if (block == null) continue;
                if (block.group != group) continue;

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
    /// Tách các group bị đứt kết nối thành nhiều subgroup.
    /// </summary>
    public void SplitDisconnectedGroups()
    {
        List<BlockGroup> groups = new List<BlockGroup>();
        HashSet<BlockGroup> uniqueGroups = new HashSet<BlockGroup>();

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
    /// group đang kéo trên cùng, các group bị hit nằm dưới nó.
    /// </summary>
    private void SortDraggedAndHitLayers(BlockGroup draggedGroup, List<Block> hitBlocks)
    {
        if (draggedGroup == null || draggedGroup.root == null)
            return;

        draggedGroup.root.SetAsLastSibling();

        int currentIndex = draggedGroup.root.GetSiblingIndex() - 1;
        HashSet<BlockGroup> sortedGroups = new HashSet<BlockGroup>();

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
    /// Check tọa độ có nằm trong board không.
    /// </summary>
    public bool IsInside(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < rows && pos.y >= 0 && pos.y < cols;
    }

    /// <summary>
    /// Dựng lại ma trận grid từ currentBlocks để sync logic.
    /// </summary>
    public void RebuildGridFromBlocksStrict()
    {
        grid = new Block[rows, cols];

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block == null) continue;

            if (!IsInside(block.gridPos))
            {
                continue;
            }

            if (grid[block.gridPos.x, block.gridPos.y] != null)
            {
                Debug.LogWarning($"Grid overwrite at {block.gridPos} by {block.name}");
            }

            grid[block.gridPos.x, block.gridPos.y] = block;
        }
    }
}