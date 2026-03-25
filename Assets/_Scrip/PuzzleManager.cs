using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine.InputSystem;

public class PuzzleManager : MonoBehaviour
{
    [Header("Level System")]
    public List<PuzzleLevel> levels;

    //level Data
    public int currentLevelIndex = 0;
    [SerializeField] public Sprite sourceImage;
    [SerializeField] public GameObject blockPrefab;
    [SerializeField] public int rows = 3;
    [SerializeField] public int cols = 3;
    [SerializeField] public float blockSize = 100f;

    public bool isTweening { get; private set; }
    public void SetTweening(bool state) => isTweening = state;

    [SerializeField] private RectTransform boardRoot;

    [HideInInspector]
    public List<Block> currentBlocks = new List<Block>();

    public static PuzzleManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        GeneratePuzzle();
        ShuffleBlocks();
    }

    /// <summary>
    /// Xoá block cũ, tạo block mới theo thông số level,
    /// gán sprite và tạo group riêng cho từng block.
    /// </summary>
    public void GeneratePuzzle()
    {
        foreach (Transform child in boardRoot) Destroy(child.gameObject);
        currentBlocks.Clear();
        Debug.Log("Clear image cũ");

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                GameObject obj = Instantiate(blockPrefab, boardRoot);

                Block b = obj.GetComponent<Block>();
                b.gridPos = new Vector2Int(r, c);
                b.correctPos = new Vector2Int(r, c);

                SetupBlockVisual(obj, r, c);
                currentBlocks.Add(b);

                // Tạo group root đúng chuẩn UI
                BlockGroup g = new BlockGroup();
                GameObject rootObj = new GameObject("GroupRoot", typeof(RectTransform));

                RectTransform rootRect = rootObj.GetComponent<RectTransform>();
                rootRect.SetParent(boardRoot, false);
                rootRect.localScale = Vector3.one;
                rootRect.anchoredPosition = Vector2.zero;

                g.root = rootRect;

                b.group = g;
                g.blocks.Add(b);
                b.transform.SetParent(g.root, false);

                b.SetOutline(true);
            }
        }

        Debug.Log("Load Level mới");
        UpdateAllBlockPositions();
    }

    /// <summary>
    /// Cắt sprite từ sourceImage dựa trên vị trí row, col và gán cho block.
    /// </summary>
    void SetupBlockVisual(GameObject obj, int row, int col)
    {
        Block b = obj.GetComponent<Block>();
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

    /// <summary>
    /// Di chuyển group block theo offset.
    /// Nếu có block bị chặn sẽ đẩy chúng sang vị trí trống gần nhất.
    /// </summary>
    public bool MoveGroupWithPush(
    BlockGroup draggedGroup,
    Vector2Int offset,
    BlockDragHandler.DragMoveType moveType,
    bool animate = true)
    {
        var finalPositions = new Dictionary<Block, Vector2Int>(draggedGroup.blocks.Count);
        var occupiedTargets = new HashSet<Vector2Int>();

        // ===== TÍNH TOÁN VỊ TRÍ CUỐI CỦA GROUP ĐANG KÉO =====
        foreach (var b in draggedGroup.blocks)
        {
            Vector2Int target = b.gridPos + offset;

            if (target.x < 0 || target.x >= rows || target.y < 0 || target.y >= cols)
                return false;

            finalPositions[b] = target;
            occupiedTargets.Add(target);
        }

        // ===== TÌM CÁC BLOCK BỊ ĐỤNG =====
        var hitBlocks = new List<Block>();
        var hitSet = new HashSet<Block>();

        foreach (var b in draggedGroup.blocks)
        {
            Vector2Int target = finalPositions[b];

            var hit = currentBlocks.FirstOrDefault(x =>
                x != null &&
                x.group != draggedGroup &&
                x.gridPos == target);

            if (hit != null && !hitSet.Contains(hit))
            {
                hitBlocks.Add(hit);
                hitSet.Add(hit);
            }
        }

        // ===== THỬ SWAP ĐÚNG THEO TARGET THỰC TẾ =====
        bool canSwap = hitBlocks.Count == draggedGroup.blocks.Count;

        if (canSwap)
        {
            var swapPairs = new List<(Block dragged, Block hit)>();
            var usedHits = new HashSet<Block>();

            foreach (var dragged in draggedGroup.blocks)
            {
                Vector2Int target = finalPositions[dragged];

                var hit = currentBlocks.FirstOrDefault(x =>
                    x != null &&
                    x.group != draggedGroup &&
                    x.gridPos == target);

                if (hit == null || usedHits.Contains(hit))
                {
                    canSwap = false;
                    break;
                }

                swapPairs.Add((dragged, hit));
                usedHits.Add(hit);
            }

            if (canSwap)
            {
                foreach (var pair in swapPairs)
                {
                    Vector2Int temp = pair.dragged.gridPos;
                    pair.dragged.gridPos = pair.hit.gridPos;
                    pair.hit.gridPos = temp;
                }
            }
        }

        // ===== NẾU KHÔNG SWAP ĐƯỢC THÌ PUSH =====
        if (!canSwap)
        {
            bool pushSuccess = PushHitBlocks(hitBlocks, draggedGroup, finalPositions, occupiedTargets, moveType);

            if (!pushSuccess)
                return false;

            foreach (var b in draggedGroup.blocks)
            {
                b.gridPos = finalPositions[b];
            }
        }

        // ===== UPDATE UI =====
        UpdateAllBlockPositions(animate);
        ValidateAllGroups();
        Invoke(nameof(CheckAndMergeGroups), 0.4f);

        var allGroups = currentBlocks
            .Where(b => b != null && b.group != null)
            .Select(b => b.group)
            .Distinct()
            .ToList();

        foreach (var g in allGroups)
        {
            if (g != null)
                g.SplitIfDisconnected(transform);
        }

        // ===== SORT LAYER =====
        if (draggedGroup.root != null)
        {
            draggedGroup.root.SetAsLastSibling();

            int topIndex = draggedGroup.root.GetSiblingIndex();
            int currentIndex = topIndex - 1;

            foreach (var hb in hitBlocks)
            {
                if (hb != null && hb.group != null && hb.group.root != null)
                {
                    hb.group.root.SetSiblingIndex(Mathf.Max(0, currentIndex));
                    currentIndex--;
                }
            }
        }

        return true;
    }

    private bool PushHitBlocks(
        List<Block> hitBlocks,
        BlockGroup draggedGroup,
        Dictionary<Block, Vector2Int> finalPositions,
        HashSet<Vector2Int> occupiedTargets,
        BlockDragHandler.DragMoveType moveType)
    {
        var available = new List<Vector2Int>(GetEmptyPositions());

        // cho phép hit block được đẩy vào vị trí cũ của dragged group
        foreach (var b in draggedGroup.blocks)
        {
            if (!available.Contains(b.gridPos))
                available.Add(b.gridPos);
        }

        // loại bỏ các ô mà dragged group sắp chiếm
        available.RemoveAll(pos => occupiedTargets.Contains(pos));

        var used = new HashSet<Vector2Int>();
        var hitMoves = new Dictionary<Block, Vector2Int>();

        foreach (var hb in hitBlocks)
        {
            if (hb == null) continue;

            bool found = TryFindBestPushSpot(
                hb.gridPos,
                available,
                used,
                moveType,
                out Vector2Int bestSpot);

            if (!found)
                return false;

            hitMoves[hb] = bestSpot;
            used.Add(bestSpot);
        }

        foreach (var hb in hitBlocks)
        {
            if (hb == null) continue;

            if (hb.group != null && hb.group.blocks.Count > 1)
            {
                hb.group.SplitByBlocks(new List<Block> { hb }, transform);
            }

            hb.gridPos = hitMoves[hb];
        }

        return true;
    }

    private bool TryFindBestPushSpot(
        Vector2Int origin,
        List<Vector2Int> available,
        HashSet<Vector2Int> used,
        BlockDragHandler.DragMoveType moveType,
        out Vector2Int bestSpot)
    {
        bestSpot = default;

        var candidates = available
            .Where(pos => !used.Contains(pos))
            .ToList();

        if (candidates.Count == 0)
            return false;

        // Ưu tiên theo hướng kéo
        IEnumerable<Vector2Int> prioritized = null;

        switch (moveType)
        {
            case BlockDragHandler.DragMoveType.Horizontal:
                {
                    // cùng hàng trước
                    prioritized = candidates.Where(pos => pos.x == origin.x);
                    break;
                }

            case BlockDragHandler.DragMoveType.Vertical:
                {
                    // cùng cột trước
                    prioritized = candidates.Where(pos => pos.y == origin.y);
                    break;
                }

            case BlockDragHandler.DragMoveType.Diagonal:
                {
                    // cùng đường chéo trước
                    prioritized = candidates.Where(pos =>
                        Mathf.Abs(pos.x - origin.x) == Mathf.Abs(pos.y - origin.y));
                    break;
                }

            default:
                {
                    prioritized = Enumerable.Empty<Vector2Int>();
                    break;
                }
        }

        var prioritizedList = prioritized.ToList();

        if (prioritizedList.Count > 0)
        {
            float bestDist = float.MaxValue;

            foreach (var pos in prioritizedList)
            {
                float dist = (origin - pos).sqrMagnitude;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestSpot = pos;
                }
            }

            return true;
        }

        // fallback: lấy ô gần nhất
        {
            float bestDist = float.MaxValue;
            bool found = false;

            foreach (var pos in candidates)
            {
                float dist = (origin - pos).sqrMagnitude;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestSpot = pos;
                    found = true;
                }
            }

            return found;
        }
    }

    /// <summary>
    /// Kiểm tra và gộp các block đúng vị trí thành group.
    /// </summary>
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
                        foundMerge = true;
                        break;
                    }
                }
                if (foundMerge) break;
            }
        }
    }

    /// <summary>
    /// Kiểm tra hai block có phải là hàng xóm đúng.
    /// </summary>
    public bool IsCorrectNeighbor(Block a, Block b)
    {
        Vector2Int gridDiff = a.gridPos - b.gridPos;
        if (gridDiff.sqrMagnitude != 1) return false;

        return gridDiff == (a.correctPos - b.correctPos);
    }

    /// <summary>
    /// Cập nhật vị trí UI của tất cả block.
    /// </summary>
    public void UpdateAllBlockPositions(bool animate = true)
    {
        int completedCount = 0;
        int totalBlocks = currentBlocks.Count;

        foreach (var b in currentBlocks)
        {
            if (b == null) continue;

            RectTransform rt = b.GetComponent<RectTransform>();
            if (rt == null) continue;

            rt.DOKill();

            b.targetPosition = GridToPosition(b.gridPos);

            Vector2 snappedPos = new Vector2(
                Mathf.Round(b.targetPosition.x),
                Mathf.Round(b.targetPosition.y)
            );

            if (!animate)
            {
                rt.anchoredPosition = snappedPos;
                continue;
            }

            rt.DOAnchorPos(snappedPos, 0.5f)
              .SetEase(Ease.OutCubic)
              .OnComplete(() =>
              {
                  if (this == null) return;

                  completedCount++;
                  if (completedCount >= totalBlocks)
                      isTweening = false;
              });
        }

        isTweening = animate;
    }

    /// <summary>
    /// Chuyển grid sang vị trí UI.
    /// </summary>
    public Vector2 GridToPosition(Vector2Int pos)
    {
        float startX = -((cols - 1) * blockSize) / 2f;
        float startY = ((rows - 1) * blockSize) / 2f;

        return new Vector2(
            startX + pos.y * blockSize,
            startY - pos.x * blockSize
        );
    }

    /// <summary>
    /// Chuyển vị trí UI sang grid.
    /// </summary>
    public Vector2Int PositionToGrid(Vector2 pos)
    {
        float startX = -((cols - 1) * blockSize) / 2f;
        float startY = ((rows - 1) * blockSize) / 2f;

        int col = Mathf.RoundToInt((pos.x - startX) / blockSize);
        int row = Mathf.RoundToInt((startY - pos.y) / blockSize);

        return new Vector2Int(row, col);
    }

    /// <summary>
    /// Lấy danh sách ô trống.
    /// </summary>
    public List<Vector2Int> GetEmptyPositions()
    {
        List<Vector2Int> empty = new List<Vector2Int>();

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (!currentBlocks.Any(b => b.gridPos == new Vector2Int(r, c)))
                    empty.Add(new Vector2Int(r, c));

        return empty;
    }

    /// <summary>
    /// Xáo trộn vị trí block.  
    /// </summary>
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

    /// <summary>
    /// Kiểm tra group có thể di chuyển.
    /// </summary>
    public bool CanMoveGroup(BlockGroup g, Vector2Int shift)
    {
        foreach (var b in g.blocks)
        {
            Vector2Int nextPos = b.gridPos + shift;

            if (nextPos.x < 0 || nextPos.x >= rows ||
                nextPos.y < 0 || nextPos.y >= cols)
                return false;
        }

        return true;
    }
    public void ValidateAllGroups()
    {
        var groups = currentBlocks
            .Select(b => b.group)
            .Distinct()
            .ToList();

        foreach (var g in groups)
        {
            var blocksCopy = new List<Block>(g.blocks);

            foreach (var b in blocksCopy)
            {
                bool hasCorrectNeighbor = blocksCopy.Any(other =>
                    other != b && IsCorrectNeighbor(b, other)
                );

                // nếu không có neighbor đúng → tách ra
                if (!hasCorrectNeighbor && blocksCopy.Count > 1)
                {
                    g.SplitByBlocks(new List<Block> { b }, transform);
                }
            }
        }
    }
}