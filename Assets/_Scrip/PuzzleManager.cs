using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    public bool isTweening { get; private set; }
    private bool levelCompleted = false;

    public void SetTweening(bool state) => isTweening = state;

    [HideInInspector] public readonly List<Block> currentBlocks = new();

    private Block[,] grid;
    private int sourceTexW;
    private int sourceTexH;
    private int sourceSpriteW;
    private int sourceSpriteH;

    public static PuzzleManager Instance { get; private set; }

    private static readonly Vector2Int[] FourDirs =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1)
    };

    private readonly List<Block> _validBlocksCache = new(16);
    private readonly List<Block> _hitBlocksCache = new(16);
    private readonly HashSet<Block> _draggedSetCache = new();
    private readonly HashSet<Block> _hitSetCache = new();
    private readonly HashSet<Vector2Int> _occupiedTargetsCache = new();
    private readonly HashSet<Vector2Int> _availablePositionsCache = new();
    private readonly HashSet<Vector2Int> _usedPositionsCache = new();
    private readonly Dictionary<Block, Vector2Int> _finalPositionsCache = new();
    private readonly Dictionary<Block, Vector2Int> _hitMovesCache = new();
    private readonly List<(Block dragged, Block hit)> _swapPairsCache = new(16);
    private readonly HashSet<Block> _usedHitBlocksCache = new();
    private readonly List<BlockGroup> _groupsCache = new(16);
    private readonly HashSet<BlockGroup> _uniqueGroupsCache = new();
    private readonly List<Block> _snapshotBlocksCache = new(16);
    private readonly List<Block> _isolatedBlocksCache = new(16);
    private readonly HashSet<BlockGroup> _handledGroupsCache = new();
    private readonly List<(BlockGroup group, RectTransform root, Vector2 targetPos)> _groupMovesCache = new(16);
    private readonly HashSet<BlockGroup> _sortedGroupsCache = new();
    private readonly List<Block> _neighborsCache = new(4);
    private readonly List<Block> _singleBlockSplitCache = new(1);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        Application.targetFrameRate = 60;

        GameSaveData save = SaveManager.Load();

        if (levels == null || levels.Count == 0)
        {
            Debug.LogWarning("PuzzleManager: levels is empty.");
            return;
        }

        int savedLevelIndex = Mathf.Clamp(save.currentLevelIndex, 0, levels.Count - 1);
        currentLevelIndex = savedLevelIndex;

        PuzzleLevel level = levels[currentLevelIndex];
        sourceImage = level.levelImage;
        rows = level.rows;
        cols = level.cols;

        CacheSourceImageData();
        GeneratePuzzle();

        if (save.currentSession != null &&
            save.currentSession.levelIndex == currentLevelIndex &&
            save.currentSession.blocks != null &&
            save.currentSession.blocks.Count > 0)
        {
            ApplySessionData(save.currentSession);
        }
        else
        {
            ShuffleBlocks();
            SaveCurrentState();
        }
    }

    private void CacheSourceImageData()
    {
        if (sourceImage == null || sourceImage.texture == null)
            return;

        sourceTexW = sourceImage.texture.width;
        sourceTexH = sourceImage.texture.height;
        sourceSpriteW = cols > 0 ? sourceTexW / cols : 0;
        sourceSpriteH = rows > 0 ? sourceTexH / rows : 0;
    }

    public void GeneratePuzzle()
    {
        if (boardRoot == null || blockPrefab == null || sourceImage == null)
            return;

        foreach (Transform child in boardRoot)
            Destroy(child.gameObject);

        currentBlocks.Clear();
        grid = new Block[rows, cols];
        CacheSourceImageData();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                GameObject obj = Instantiate(blockPrefab, boardRoot);
                Block block = obj.GetComponent<Block>();
                if (block == null)
                    continue;

                block.gridPos = new Vector2Int(r, c);
                block.correctPos = new Vector2Int(r, c);

                SetupBlockVisual(block, r, c);

                currentBlocks.Add(block);
                grid[r, c] = block;

                BlockGroup group = CreateSingleBlockGroup();
                block.group = group;
                group.blocks.Add(block);

                block.transform.SetParent(group.root, false);
                group.RebuildLocalLayout(true);
            }
        }

        UpdateAllBlockPositions(false);
        RefreshAllBorders(true);
    }

    private BlockGroup CreateSingleBlockGroup()
    {
        var group = new BlockGroup();
        group.root = CreateGroupRoot("GroupRoot", boardRoot);
        return group;
    }

    public RectTransform CreateGroupRoot(string rootName, Transform parent)
    {
        GameObject rootObj = new GameObject(rootName, typeof(RectTransform));
        RectTransform rootRect = rootObj.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;
        rootRect.localScale = Vector3.one;
        rootRect.anchoredPosition = Vector2.zero;
        return rootRect;
    }

    private void SetupBlockVisual(Block block, int row, int col)
    {
        if (block == null || block.img == null || sourceImage == null || sourceImage.texture == null)
            return;

        Rect rect = new Rect(
            col * sourceSpriteW,
            sourceTexH - (row + 1) * sourceSpriteH,
            sourceSpriteW,
            sourceSpriteH
        );

        block.img.sprite = Sprite.Create(sourceImage.texture, rect, new Vector2(0.5f, 0.5f));
    }

    public bool MoveGroupWithPush(
     BlockGroup draggedGroup,
     Vector2Int offset,
     BlockDragHandler.DragMoveType moveType,
     bool animate = true)
    {
        if (draggedGroup == null || draggedGroup.blocks == null || draggedGroup.blocks.Count == 0)
            return false;

        List<Block> draggedBlocks = GetValidBlocks(draggedGroup.blocks);
        if (draggedBlocks.Count == 0)
            return false;

        _draggedSetCache.Clear();
        _occupiedTargetsCache.Clear();
        _finalPositionsCache.Clear();

        for (int i = 0; i < draggedBlocks.Count; i++)
            _draggedSetCache.Add(draggedBlocks[i]);

        for (int i = 0; i < draggedBlocks.Count; i++)
        {
            Block block = draggedBlocks[i];
            Vector2Int target = block.gridPos + offset;

            if (!IsInside(target))
                return false;

            if (!_occupiedTargetsCache.Add(target))
                return false;

            _finalPositionsCache[block] = target;
        }

        List<Block> hitBlocks = CollectHitBlocks(draggedBlocks, _draggedSetCache, _finalPositionsCache);
        bool swapped = TrySwapBlocks(draggedBlocks, _draggedSetCache, _finalPositionsCache);

        if (!swapped)
        {
            if (!PushHitBlocks(hitBlocks, draggedGroup, _occupiedTargetsCache, moveType))
                return false;

            ApplyDraggedPositions(draggedBlocks, _finalPositionsCache);
        }

        RebuildGridFromBlocksStrict();
        SortDraggedAndHitLayers(draggedGroup, hitBlocks);
        UpdateAllBlockPositions(animate);

        if (animate)
        {
            DOVirtual.DelayedCall(0.52f, () =>
            {
                RebuildGridFromBlocksStrict();
                FinalizeBoardState(false);
                RebuildGridFromBlocksStrict();
                SaveCurrentState();
                CheckLevelCompleteAndShow();
            });
        }
        else
        {
            RebuildGridFromBlocksStrict();
            FinalizeBoardState(true);
            RebuildGridFromBlocksStrict();
            SaveCurrentState();
            CheckLevelCompleteAndShow();
        }

        return true;
    }

    private void FinalizeBoardState(bool instantBorders)
    {
        RebuildGridFromBlocksStrict();
        ValidateAllGroups();
        SplitDisconnectedGroups();
        CheckAndMergeGroups();
        RebuildGridFromBlocksStrict();
        UpdateAllBlockPositions(false);
        RefreshAllBorders(instantBorders);
    }

    private List<Block> GetValidBlocks(List<Block> source)
    {
        _validBlocksCache.Clear();

        for (int i = 0; i < source.Count; i++)
        {
            Block block = source[i];
            if (block != null)
                _validBlocksCache.Add(block);
        }

        return _validBlocksCache;
    }

    private List<Block> CollectHitBlocks(
        List<Block> draggedBlocks,
        HashSet<Block> draggedSet,
        Dictionary<Block, Vector2Int> finalPositions)
    {
        _hitBlocksCache.Clear();
        _hitSetCache.Clear();

        for (int i = 0; i < draggedBlocks.Count; i++)
        {
            Block dragged = draggedBlocks[i];
            if (!finalPositions.TryGetValue(dragged, out Vector2Int targetPos))
                continue;

            Block hit = GetBlockAt(targetPos);
            if (hit == null || draggedSet.Contains(hit) || hit.group == dragged.group)
                continue;

            if (_hitSetCache.Add(hit))
                _hitBlocksCache.Add(hit);
        }

        return _hitBlocksCache;
    }

    private bool TrySwapBlocks(
        List<Block> draggedBlocks,
        HashSet<Block> draggedSet,
        Dictionary<Block, Vector2Int> finalPositions)
    {
        if (draggedBlocks.Count == 0)
            return false;

        _swapPairsCache.Clear();
        _usedHitBlocksCache.Clear();

        for (int i = 0; i < draggedBlocks.Count; i++)
        {
            Block dragged = draggedBlocks[i];
            Block hit = GetBlockAt(finalPositions[dragged]);

            if (hit == null || draggedSet.Contains(hit) || hit.group == dragged.group || !_usedHitBlocksCache.Add(hit))
                return false;

            _swapPairsCache.Add((dragged, hit));
        }

        for (int i = 0; i < _swapPairsCache.Count; i++)
        {
            Block hit = _swapPairsCache[i].hit;
            if (hit != null && hit.group != null && hit.group.blocks.Count > 1)
                SplitSingleBlockForSwap(hit);
        }

        for (int i = 0; i < _swapPairsCache.Count; i++)
        {
            Block dragged = _swapPairsCache[i].dragged;
            Block hit = _swapPairsCache[i].hit;

            Vector2Int oldDraggedPos = dragged.gridPos;
            dragged.gridPos = hit.gridPos;
            hit.gridPos = oldDraggedPos;
        }

        return true;
    }

    private void SplitSingleBlockForSwap(Block block)
    {
        if (block == null || block.group == null || block.group.root == null)
            return;

        BlockGroup oldGroup = block.group;
        if (oldGroup.blocks.Count <= 1)
            return;

        RectTransform blockRect = block.transform as RectTransform;
        RectTransform oldRoot = oldGroup.root as RectTransform;
        if (blockRect == null || oldRoot == null)
            return;

        Vector3 worldPos = blockRect.position;

        _singleBlockSplitCache.Clear();
        _singleBlockSplitCache.Add(block);
        oldGroup.SplitByBlocks(_singleBlockSplitCache, transform, true);

        BlockGroup newGroup = block.group;
        RectTransform newRoot = newGroup != null ? newGroup.root as RectTransform : null;
        if (newRoot == null)
            return;

        newRoot.position = worldPos;
        newRoot.localScale = Vector3.one;

        blockRect.SetParent(newRoot, true);
        blockRect.anchoredPosition = Vector2.zero;
        blockRect.localScale = Vector3.one;
        block.MarkBorderDirty();

        oldGroup.RebuildLocalLayout(false);
        newGroup.RebuildLocalLayout(false);
    }

    private void ApplyDraggedPositions(List<Block> draggedBlocks, Dictionary<Block, Vector2Int> finalPositions)
    {
        for (int i = 0; i < draggedBlocks.Count; i++)
        {
            Block block = draggedBlocks[i];
            block.gridPos = finalPositions[block];
        }
    }

    private bool PushHitBlocks(
        List<Block> hitBlocks,
        BlockGroup draggedGroup,
        HashSet<Vector2Int> occupiedTargets,
        BlockDragHandler.DragMoveType moveType)
    {
        _availablePositionsCache.Clear();
        _usedPositionsCache.Clear();
        _hitMovesCache.Clear();

        FillEmptyPositions(_availablePositionsCache);

        for (int i = 0; i < draggedGroup.blocks.Count; i++)
        {
            Block block = draggedGroup.blocks[i];
            if (block != null)
                _availablePositionsCache.Add(block.gridPos);
        }

        foreach (Vector2Int pos in occupiedTargets)
            _availablePositionsCache.Remove(pos);

        for (int i = 0; i < hitBlocks.Count; i++)
        {
            Block hit = hitBlocks[i];
            if (hit == null)
                continue;

            if (!TryFindBestPushSpot(hit.gridPos, _availablePositionsCache, _usedPositionsCache, moveType, out Vector2Int bestSpot))
                return false;

            _hitMovesCache[hit] = bestSpot;
            _usedPositionsCache.Add(bestSpot);
        }

        for (int i = 0; i < hitBlocks.Count; i++)
        {
            Block hit = hitBlocks[i];
            if (hit == null)
                continue;

            if (hit.group != null && hit.group.blocks.Count > 1)
            {
                _singleBlockSplitCache.Clear();
                _singleBlockSplitCache.Add(hit);
                hit.group.SplitByBlocks(_singleBlockSplitCache, transform, true);
            }

            hit.gridPos = _hitMovesCache[hit];
        }

        return true;
    }

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

    public void CheckAndMergeGroups()
    {
        bool foundMerge = true;

        while (foundMerge)
        {
            foundMerge = false;

            for (int i = 0; i < currentBlocks.Count; i++)
            {
                Block a = currentBlocks[i];
                if (a == null || a.group == null)
                    continue;

                for (int d = 0; d < FourDirs.Length; d++)
                {
                    Block b = GetBlockAt(a.gridPos + FourDirs[d]);
                    if (b == null || b.group == null || a.group == b.group)
                        continue;

                    if (!IsCorrectNeighbor(a, b))
                        continue;

                    a.group.Merge(b.group);
                    foundMerge = true;
                    break;
                }

                if (foundMerge)
                    break;
            }
        }
    }

    public bool IsCorrectNeighbor(Block a, Block b)
    {
        Vector2Int gridDiff = a.gridPos - b.gridPos;
        return gridDiff.sqrMagnitude == 1 && gridDiff == (a.correctPos - b.correctPos);
    }

    public List<Block> GetCorrectNeighborsInSameGroup(Block current, BlockGroup group)
    {
        _neighborsCache.Clear();

        if (current == null || group == null)
            return _neighborsCache;

        for (int i = 0; i < FourDirs.Length; i++)
        {
            Block neighbor = GetBlockAt(current.gridPos + FourDirs[i]);
            if (neighbor != null && neighbor.group == group && IsCorrectNeighbor(current, neighbor))
                _neighborsCache.Add(neighbor);
        }

        return _neighborsCache;
    }

    public void UpdateAllBlockPositions(bool animate = true)
    {
        _handledGroupsCache.Clear();
        _groupMovesCache.Clear();

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block == null || block.group == null || block.group.root == null)
                continue;

            BlockGroup group = block.group;
            if (!_handledGroupsCache.Add(group))
                continue;

            RectTransform rootRect = group.root as RectTransform;
            if (rootRect == null)
                continue;

            Block anchor = group.GetAnchorBlock();
            if (anchor == null)
                continue;

            Vector2 targetRootPos = GridToPosition(anchor.gridPos);
            _groupMovesCache.Add((group, rootRect, targetRootPos));
        }

        if (!animate)
        {
            for (int i = 0; i < _groupMovesCache.Count; i++)
            {
                var item = _groupMovesCache[i];
                item.group.RebuildLocalLayout(false, false);
                item.root.anchoredPosition = item.targetPos;
            }

            isTweening = false;
            return;
        }

        if (_groupMovesCache.Count == 0)
        {
            isTweening = false;
            return;
        }

        isTweening = true;
        int completed = 0;
        int total = _groupMovesCache.Count;

        for (int i = 0; i < _groupMovesCache.Count; i++)
        {
            var item = _groupMovesCache[i];

            item.group.RebuildLocalLayout(false, true, 0.07f);

            item.root.DOKill(false);
            item.root.localScale = Vector3.one;
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

    public Vector2 GridToPosition(Vector2Int pos)
    {
        float startX = -((cols - 1) * blockSize) * 0.5f;
        float startY = ((rows - 1) * blockSize) * 0.5f;

        return new Vector2(
            startX + pos.y * blockSize,
            startY - pos.x * blockSize
        );
    }

    public Vector2Int PositionToGrid(Vector2 pos)
    {
        float startX = -((cols - 1) * blockSize) * 0.5f;
        float startY = ((rows - 1) * blockSize) * 0.5f;

        int col = Mathf.RoundToInt((pos.x - startX) / blockSize);
        int row = Mathf.RoundToInt((startY - pos.y) / blockSize);

        return new Vector2Int(row, col);
    }

    public List<Vector2Int> GetEmptyPositions()
    {
        List<Vector2Int> empty = new(rows * cols);
        FillEmptyPositions(empty);
        return empty;
    }

    private void FillEmptyPositions(ICollection<Vector2Int> output)
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (grid[r, c] == null)
                    output.Add(new Vector2Int(r, c));
            }
        }
    }

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

        for (int i = positions.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }

        int count = Mathf.Min(currentBlocks.Count, positions.Count);
        for (int i = 0; i < count; i++)
            currentBlocks[i].gridPos = positions[i];

        RebuildGridFromBlocksStrict();
        UpdateAllBlockPositions();
        RefreshAllBorders(true);
    }

    public bool CanMoveGroup(BlockGroup group, Vector2Int shift)
    {
        if (group == null)
            return false;

        for (int i = 0; i < group.blocks.Count; i++)
        {
            Block block = group.blocks[i];
            if (block == null)
                continue;

            if (!IsInside(block.gridPos + shift))
                return false;
        }

        return true;
    }

    public void ValidateAllGroups()
    {
        CollectUniqueGroups(_groupsCache);

        for (int i = 0; i < _groupsCache.Count; i++)
        {
            BlockGroup group = _groupsCache[i];
            if (group == null || group.blocks == null || group.blocks.Count <= 1)
                continue;

            _snapshotBlocksCache.Clear();
            _snapshotBlocksCache.AddRange(group.blocks);
            _isolatedBlocksCache.Clear();

            for (int j = 0; j < _snapshotBlocksCache.Count; j++)
            {
                Block block = _snapshotBlocksCache[j];
                if (block == null || block.group != group)
                    continue;

                List<Block> neighbors = GetCorrectNeighborsInSameGroup(block, group);
                if (neighbors.Count == 0)
                    _isolatedBlocksCache.Add(block);
            }

            if (_isolatedBlocksCache.Count == 0 || _isolatedBlocksCache.Count >= _snapshotBlocksCache.Count)
                continue;

            group.SplitByBlocks(_isolatedBlocksCache, transform);
        }
    }

    public void SplitDisconnectedGroups()
    {
        CollectUniqueGroups(_groupsCache);

        for (int i = 0; i < _groupsCache.Count; i++)
            _groupsCache[i]?.SplitIfDisconnected(transform);
    }

    private void CollectUniqueGroups(List<BlockGroup> output)
    {
        output.Clear();
        _uniqueGroupsCache.Clear();

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block != null && block.group != null && _uniqueGroupsCache.Add(block.group))
                output.Add(block.group);
        }
    }

    private void SortDraggedAndHitLayers(BlockGroup draggedGroup, List<Block> hitBlocks)
    {
        if (boardRoot == null || draggedGroup == null || draggedGroup.root == null)
            return;

        CollectUniqueGroups(_groupsCache);

        List<BlockGroup> hitGroups = new List<BlockGroup>();
        HashSet<BlockGroup> hitSet = new HashSet<BlockGroup>();

        // Lấy danh sách group bị hit / swap
        for (int i = 0; i < hitBlocks.Count; i++)
        {
            Block hit = hitBlocks[i];
            if (hit == null || hit.group == null || hit.group.root == null)
                continue;

            if (hit.group == draggedGroup)
                continue;

            if (hitSet.Add(hit.group))
                hitGroups.Add(hit.group);
        }

        int siblingIndex = 0;

        // 1. Các group thường nằm dưới cùng
        for (int i = 0; i < _groupsCache.Count; i++)
        {
            BlockGroup group = _groupsCache[i];
            if (group == null || group.root == null)
                continue;

            if (group == draggedGroup)
                continue;

            if (hitSet.Contains(group))
                continue;

            group.root.SetSiblingIndex(siblingIndex);
            siblingIndex++;
        }

        // 2. Các group bị swap / hit nằm trên group thường
        for (int i = 0; i < hitGroups.Count; i++)
        {
            BlockGroup hitGroup = hitGroups[i];
            if (hitGroup == null || hitGroup.root == null)
                continue;

            hitGroup.root.SetSiblingIndex(siblingIndex);
            siblingIndex++;
        }

        // 3. Group đang click / drag luôn nằm trên cùng
        draggedGroup.root.SetAsLastSibling();
    }

    public Block GetBlockAt(Vector2Int pos)
    {
        return IsInside(pos) ? grid[pos.x, pos.y] : null;
    }

    public bool IsInside(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < rows && pos.y >= 0 && pos.y < cols;
    }

    public void RebuildGridFromBlocksStrict()
    {
        if (grid == null || grid.GetLength(0) != rows || grid.GetLength(1) != cols)
            grid = new Block[rows, cols];
        else
            System.Array.Clear(grid, 0, grid.Length);

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block == null || !IsInside(block.gridPos))
                continue;

            if (grid[block.gridPos.x, block.gridPos.y] != null)
                Debug.LogWarning($"Grid overwrite at {block.gridPos} by {block.name}");

            grid[block.gridPos.x, block.gridPos.y] = block;
        }
    }

    public void RefreshAllBorders(bool instant = false)
    {
        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block != null)
                RefreshBlockBorder(block, instant);
        }
    }

    public void RefreshBlockBorder(Block block, bool instant = false)
    {
        if (block == null)
            return;

        Block topNeighbor = GetBlockAt(block.gridPos + FourDirs[0]);
        Block bottomNeighbor = GetBlockAt(block.gridPos + FourDirs[1]);
        Block leftNeighbor = GetBlockAt(block.gridPos + FourDirs[2]);
        Block rightNeighbor = GetBlockAt(block.gridPos + FourDirs[3]);

        bool showTop = !ShouldHideSharedEdge(block, topNeighbor);
        bool showBottom = !ShouldHideSharedEdge(block, bottomNeighbor);
        bool showLeft = !ShouldHideSharedEdge(block, leftNeighbor);
        bool showRight = !ShouldHideSharedEdge(block, rightNeighbor);

        block.SetBorders(showTop, showBottom, showLeft, showRight, instant);
    }

    private bool ShouldHideSharedEdge(Block a, Block b)
    {
        if (a == null || b == null || a.group == null || b.group == null || a.group != b.group)
            return false;

        return IsCorrectNeighbor(a, b);
    }
    //save game
    public GameSaveData BuildSaveData()
    {
        GameSaveData save = new GameSaveData();
        save.currentLevelIndex = currentLevelIndex;

        save.currentSession = new LevelSessionSaveData();
        save.currentSession.levelIndex = currentLevelIndex;
        save.currentSession.blocks = new List<BlockSaveData>();

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block == null)
                continue;

            BlockSaveData blockSave = new BlockSaveData
            {
                correctRow = block.correctPos.x,
                correctCol = block.correctPos.y,
                gridRow = block.gridPos.x,
                gridCol = block.gridPos.y
            };

            save.currentSession.blocks.Add(blockSave);
        }

        return save;
    }

    public void SaveCurrentState()
    {
        GameSaveData save = BuildSaveData();
        SaveManager.Save(save);
    }

    public void ApplySessionData(LevelSessionSaveData session)
    {
        if (session == null || session.blocks == null || session.blocks.Count == 0)
            return;

        for (int i = 0; i < session.blocks.Count; i++)
        {
            BlockSaveData blockSave = session.blocks[i];

            for (int j = 0; j < currentBlocks.Count; j++)
            {
                Block block = currentBlocks[j];
                if (block == null)
                    continue;

                if (block.correctPos.x == blockSave.correctRow &&
                    block.correctPos.y == blockSave.correctCol)
                {
                    block.gridPos = new Vector2Int(blockSave.gridRow, blockSave.gridCol);
                    break;
                }
            }
        }

        RebuildGridFromBlocksStrict();
        SplitDisconnectedGroups();
        CheckAndMergeGroups();
        RebuildGridFromBlocksStrict();
        UpdateAllBlockPositions(false);
        RefreshAllBorders(true);

        CheckLevelCompleteAndShow();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            SaveCurrentState();
    }

    private void OnApplicationQuit()
    {
        SaveCurrentState();
    }
    //complete level
    public bool IsLevelCompleted()
    {
        if (currentBlocks == null || currentBlocks.Count == 0)
            return false;

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block == null)
                continue;

            if (block.gridPos != block.correctPos)
                return false;
        }

        return true;
    }

    public bool IsBusyAfterComplete()
    {
        return isTweening || levelCompleted;
    }

    public void CheckLevelCompleteAndShow()
    {
        Debug.Log("CheckLevelCompleteAndShow CALLED");

        if (levelCompleted)
        {
            Debug.Log("STOP: levelCompleted already true");
            return;
        }

        if (currentBlocks == null || currentBlocks.Count == 0)
        {
            Debug.Log("STOP: currentBlocks empty");
            return;
        }

        for (int i = 0; i < currentBlocks.Count; i++)
        {
            Block block = currentBlocks[i];
            if (block == null)
                continue;

            Debug.Log(block.name + " grid=" + block.gridPos + " correct=" + block.correctPos);

            if (block.gridPos != block.correctPos)
            {
                Debug.Log("STOP: level not complete");
                return;
            }
        }

        Debug.Log("LEVEL COMPLETE -> PLAY UI");

        levelCompleted = true;
        isTweening = true;

        if (PuzzleCompleteUI.Instance != null)
        {
            Debug.Log("PuzzleCompleteUI.Instance OK");
            PuzzleCompleteUI.Instance.PlayComplete(sourceImage);
        }
        else
        {
            Debug.LogError("PuzzleCompleteUI.Instance NULL");
            isTweening = false;
        }
    }

    public void LoadNextLevel()
    {
        if (PuzzleCompleteUI.Instance != null)
            PuzzleCompleteUI.Instance.ResetCompleteUIForNextLevel();

        currentLevelIndex++;

        if (currentLevelIndex >= levels.Count)
            currentLevelIndex = 0;

        levelCompleted = false;
        isTweening = false;

        PuzzleLevel level = levels[currentLevelIndex];
        sourceImage = level.levelImage;
        rows = level.rows;
        cols = level.cols;

        CacheSourceImageData();
        GeneratePuzzle();
        ShuffleBlocks();
        SaveCurrentState();
    }
}
