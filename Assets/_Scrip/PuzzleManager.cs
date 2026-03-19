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
    private int currentLevelIndex = 0;
    [SerializeField]  public Sprite sourceImage;
    [SerializeField] public GameObject blockPrefab;
    [SerializeField] public int rows = 3;
    [SerializeField] public int cols = 3;
    [SerializeField] public float blockSize = 100f;

    //btn help
    [SerializeField] public int maxHints = 100;
    private int currentHintsUsed = 0;

    [HideInInspector]
    public List<Block> currentBlocks = new List<Block>();

    void Start()
    {
        GeneratePuzzle();
        ShuffleBlocks();
    }
    void Update()
    {
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            NextLevel();
        }

    }

    public void NextLevel()
    {
        int nextIndex = currentLevelIndex + 1;
        if (nextIndex >= levels.Count)
        {
            nextIndex = 0;
        }
        LoadLevel(nextIndex);
    }
    private void LoadLevel(int index)
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.Log("Level null or == 0");
            return;
        } 

        currentLevelIndex = index;
        PuzzleLevel data = levels[index];
        // Cập nhật thông số từ ScriptableObject
        this.sourceImage = data.levelImage;
        this.rows = data.rows;
        this.cols = data.cols;
        this.maxHints = data.hintLimit;
        Debug.Log("SourceImage: " + (sourceImage == null ? "NULL" : "true" + "Level: " + currentLevelIndex));
        this.currentHintsUsed = 0;
        GeneratePuzzle();
        ShuffleBlocks();
    }
    //xoá block cũ , tạo block mới theo thông số level, gán sprite, tạo group mới cho từng block
    public void GeneratePuzzle()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        currentBlocks.Clear();
        Debug.Log("Clear image cũ");

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
                b.SetOutline(true);
            }
        }
        Debug.Log("Load Level mới");
        UpdateAllBlockPositions();
    }
    // Cắt sprite từ sourceImage dựa trên vị trí row, col và gán cho block
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
    // Di chuyển nhóm block được kéo, nếu có block nào bị chặn sẽ tìm vị trí trống gần nhất để đẩy chúng đi
    public bool MoveGroupWithPush(BlockGroup draggedGroup, Vector2Int offset)
    {
        var finalPositions = new Dictionary<Block, Vector2Int>(draggedGroup.blocks.Count);
        var occupied = new HashSet<Vector2Int>();

        // 1. Tính vị trí mới + check bounds
        foreach (var b in draggedGroup.blocks)
        {
            var target = b.gridPos + offset;

            if (target.x < 0 || target.x >= rows || target.y < 0 || target.y >= cols)
                return false;

            finalPositions[b] = target;
            occupied.Add(target);
        }

        // 2. Tìm block bị đụng (O(n))
        var hitBlocks = new List<Block>();
        foreach (var b in currentBlocks)
        {
            if (b.group != draggedGroup && occupied.Contains(b.gridPos))
                hitBlocks.Add(b);
        }

        // 3. Lấy vị trí trống
        var available = new List<Vector2Int>(GetEmptyPositions());

        // add vị trí cũ của group
        foreach (var b in draggedGroup.blocks)
            available.Add(b.gridPos);

        // remove vị trí sẽ bị chiếm
        available.RemoveAll(pos => occupied.Contains(pos));

        // tối ưu lookup
        var used = new HashSet<Vector2Int>();
        var hitMoves = new Dictionary<Block, Vector2Int>(hitBlocks.Count);

        // 4. Assign vị trí gần nhất (KHÔNG SORT)
        foreach (var hb in hitBlocks)
        {
            float bestDist = float.MaxValue;
            Vector2Int bestSpot = default;
            bool found = false;

            for (int i = 0; i < available.Count; i++)
            {
                var pos = available[i];
                if (used.Contains(pos)) continue;

                float dist = (hb.gridPos - pos).sqrMagnitude; // nhanh hơn Distance

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

        // 5. Apply push
        foreach (var hb in hitBlocks)
        {
            if (hb.group.blocks.Count > 1)
                hb.group.SplitByBlocks(new List<Block> { hb }, transform);

            hb.gridPos = hitMoves[hb];
        }

        // 6. Move main group
        foreach (var b in draggedGroup.blocks)
            b.gridPos = finalPositions[b];

        // 7. Update
        UpdateAllBlockPositions();
        CheckAndMergeGroups();

        return true;
    }
    // Kiểm tra tất cả block, nếu có block nào đang ở cạnh nhau và đúng vị trí tương đối thì gộp chúng vào cùng 1 group
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
    // Hai block là "hàng xóm đúng" nếu chúng ở cạnh nhau trên lưới (gridPos chênh lệch 1 ô) và vị trí tương đối của chúng so với vị trí đúng cũng phải giống nhau
    bool IsCorrectNeighbor(Block a, Block b)
    {
        Vector2Int gridDiff = a.gridPos - b.gridPos;
        if (gridDiff.sqrMagnitude != 1) return false;
        return gridDiff == (a.correctPos - b.correctPos);
    }
    // Cập nhật vị trí thực tế của tất cả block dựa trên gridPos của chúng
    public void UpdateAllBlockPositions()
    {
        foreach (var b in currentBlocks)
        {
            b.targetPosition = GridToPosition(b.gridPos);
            b.UpdateTransform();
        }
    }
    // Chuyển từ tọa độ lưới (row, col) sang vị trí thực tế trên
    public Vector2 GridToPosition(Vector2Int pos)
    {
        float startX = -((cols - 1) * blockSize) / 2f;
        float startY = ((rows - 1) * blockSize) / 2f;
        return new Vector2(startX + pos.y * blockSize, startY - pos.x * blockSize);
    }
    // Chuyển từ vị trí thực tế sang tọa độ lưới (row, col)
    public Vector2Int PositionToGrid(Vector2 pos)
    {
        float startX = -((cols - 1) * blockSize) / 2f;
        float startY = ((rows - 1) * blockSize) / 2f;
        int col = Mathf.RoundToInt((pos.x - startX) / blockSize);
        int row = Mathf.RoundToInt((startY - pos.y) / blockSize);
        return new Vector2Int(row, col);
    }
    //  Lấy danh sách tất cả vị trí lưới hiện đang trống (không có block nào chiếm)
    public List<Vector2Int> GetEmptyPositions()
    {
        List<Vector2Int> empty = new List<Vector2Int>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (!currentBlocks.Any(b => b.gridPos == new Vector2Int(r, c)))
                    empty.Add(new Vector2Int(r, c));
        return empty;
    }
    // Xáo trộn vị trí của tất cả block để tạo thành một câu đố mới
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
    // Kiểm tra xem nếu di chuyển nhóm g với một khoảng shift nhất định thì có block nào trong nhóm sẽ nhảy ra ngoài biên không
    public bool CanMoveGroup(BlockGroup g, Vector2Int shift)
    {
        foreach (var b in g.blocks)
        {
            Vector2Int nextPos = b.gridPos + shift;
            if (nextPos.x < 0 || nextPos.x >= rows || nextPos.y < 0 || nextPos.y >= cols) return false;
        }
        return true;
    }
    //btn help
    public void OnHintButtonClicked()
    {
        if (currentHintsUsed >= maxHints)
        {
            return;
        }
        if (AutoSolveOneStep())
        {
            currentHintsUsed++;
        }
    }
    // Tự động giải một bước: chọn một block sai vị trí, nếu nó đang bị chặn bởi block khác thì đẩy block đó sang vị trí trống, sau đó đưa block về đúng vị trí
    public bool AutoSolveOneStep()
    {
        // 1. Chỉ tìm những block chưa nằm đúng vị trí (gridPos != correctPos)
        var wrongBlocks = currentBlocks.Where(b => b.gridPos != b.correctPos).ToList();

        if (wrongBlocks.Count == 0)
        {
            Debug.Log("Win");
            NextLevel();
            return false;
        }
        // 2. Chọn 1 mảnh ngẫu nhiên để "cứu"
        Block targetBlock = wrongBlocks[Random.Range(0, wrongBlocks.Count)];
        // 3. TÁCH MẢNH: Nếu nó đang nằm trong một Group (mà Group đó đang sai), phải tách nó ra
        if (targetBlock.group.blocks.Count > 1)
        {
            targetBlock.group.SplitByBlocks(new List<Block> { targetBlock }, transform);
        }
        // 4. KIỂM TRA VỊ TRÍ ĐÍCH: Xem có mảnh nào đang chiếm chỗ đúng của targetBlock không
        Vector2Int destination = targetBlock.correctPos;
        Block blocker = currentBlocks.FirstOrDefault(b => b.gridPos == destination && b != targetBlock);
        if (blocker != null)
        {
            // Nếu kẻ chiếm chỗ cũng đang nằm trong Group, tách nó ra để dễ "đá" đi chỗ khác
            if (blocker.group.blocks.Count > 1)
            {
                blocker.group.SplitByBlocks(new List<Block> { blocker }, transform);
            }
            // Tìm 1 ô trống bất kỳ để đẩy kẻ chiếm chỗ sang đó
            Vector2Int emptySpot = GetEmptyPositions().FirstOrDefault();
            blocker.gridPos = emptySpot;
        }
        // 5. ĐƯA VỀ VỊ TRÍ ĐÚNG
        targetBlock.gridPos = destination;
        UpdateAllBlockPositions();
        CheckAndMergeGroups();
        targetBlock.img.DOColor(Color.green, 0.3f).OnComplete(() =>
        {
            targetBlock.img.DOColor(Color.white, 0.5f);
        });
        return true;
    }
}