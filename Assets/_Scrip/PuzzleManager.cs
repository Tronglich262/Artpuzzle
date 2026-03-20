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
    public bool MoveGroupWithPush(BlockGroup draggedGroup, Vector2Int offset, bool animate = true)
    {
        var finalPositions = new Dictionary<Block, Vector2Int>(draggedGroup.blocks.Count);
        var occupied = new HashSet<Vector2Int>();

        foreach (var b in draggedGroup.blocks)
        {
            var target = b.gridPos + offset;

            if (target.x < 0 || target.x >= rows || target.y < 0 || target.y >= cols)
                return false;

            finalPositions[b] = target;
            occupied.Add(target);
        }

        var hitBlocks = new List<Block>();
        foreach (var b in currentBlocks)
        {
            if (b.group != draggedGroup && occupied.Contains(b.gridPos))
                hitBlocks.Add(b);
        }

        var available = new List<Vector2Int>(GetEmptyPositions());

        foreach (var b in draggedGroup.blocks)
            available.Add(b.gridPos);

        available.RemoveAll(pos => occupied.Contains(pos));

        var used = new HashSet<Vector2Int>();
        var hitMoves = new Dictionary<Block, Vector2Int>(hitBlocks.Count);

        foreach (var hb in hitBlocks)
        {
            float bestDist = float.MaxValue;
            Vector2Int bestSpot = default;
            bool found = false;

            for (int i = 0; i < available.Count; i++)
            {
                var pos = available[i];
                if (used.Contains(pos)) continue;

                float dist = (hb.gridPos - pos).sqrMagnitude;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestSpot = pos;
                    found = true;
                }
            }

            if (!found) return false;

            hitMoves[hb] = bestSpot;
            used.Add(bestSpot);
        }

        foreach (var hb in hitBlocks)
        {
            if (hb.group.blocks.Count > 1)
                hb.group.SplitByBlocks(new List<Block> { hb }, transform);

            hb.gridPos = hitMoves[hb];
        }

        foreach (var b in draggedGroup.blocks)
            b.gridPos = finalPositions[b];

        UpdateAllBlockPositions(animate);
        CheckAndMergeGroups();

        var allGroups = currentBlocks
            .Select(b => b.group)
            .Distinct()
            .ToList();

        foreach (var g in allGroups)
        {
            g.SplitIfDisconnected(transform);
        }

        draggedGroup.root.SetAsLastSibling();

        int topIndex = draggedGroup.root.GetSiblingIndex();
        int currentIndex = topIndex - 1;

        foreach (var hb in hitBlocks)
        {
            hb.group.root.SetSiblingIndex(currentIndex);
            currentIndex--;
        }

        return true;
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
    bool IsCorrectNeighbor(Block a, Block b)
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
}