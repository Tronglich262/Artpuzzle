    using UnityEngine;
    using UnityEngine.UI;
    using System.Collections.Generic;

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

        void ClearAllBlocks()
        {
            foreach (var b in currentBlocks)
                if (b != null) Destroy(b.gameObject);

            currentBlocks.Clear();
        }

        public void GeneratePuzzle()
        {
            ClearAllBlocks();

            float startX = -((cols - 1) * blockSize) / 2f;
            float startY = ((rows - 1) * blockSize) / 2f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Vector2 pos = new Vector2(startX + c * blockSize, startY - r * blockSize);
                    CreateBlock(r, c, pos);
                }
            }
        }

        void CreateBlock(int row, int col, Vector2 pos)
        {
            GameObject obj = Instantiate(blockPrefab, transform);

            Block b = obj.GetComponent<Block>();

            b.gridPos = new Vector2Int(row, col);
            b.correctPos = new Vector2Int(row, col);
            b.targetPosition = pos;
            b.UpdateTransform(blockSize);

            currentBlocks.Add(b);

            //  mỗi block là 1 group riêng
            BlockGroup g = new BlockGroup();
            g.Add(b);

            // cắt ảnh
            int spriteW = sourceImage.texture.width / cols;
            int spriteH = sourceImage.texture.height / rows;

            Rect rect = new Rect(
                col * spriteW,
                sourceImage.texture.height - (row + 1) * spriteH,
                spriteW,
                spriteH
            );

            Sprite s = Sprite.Create(sourceImage.texture, rect, new Vector2(0.5f, 0.5f));
            obj.GetComponent<Image>().sprite = s;
        }

        public Vector2 GridToPosition(Vector2Int gridPos)
        {
            float startX = -((cols - 1) * blockSize) / 2f;
            float startY = ((rows - 1) * blockSize) / 2f;

            return new Vector2(
                startX + gridPos.y * blockSize,
                startY - gridPos.x * blockSize
            );
        }

        // =========================
        // SWAP GROUP
        // =========================
        public void SwapBlocks(Block a, Block b)
{
    if (a == null || b == null) return;

    BlockGroup groupA = a.group;
    BlockGroup groupB = b.group;

    if (groupA == groupB) return;

    if (groupA.blocks.Count != groupB.blocks.Count) return;

    // lấy origin
    Vector2Int originA = groupA.blocks[0].gridPos;
    Vector2Int originB = groupB.blocks[0].gridPos;

    // lưu pos cũ
    Dictionary<Block, Vector2Int> oldPosA = new Dictionary<Block, Vector2Int>();
    Dictionary<Block, Vector2Int> oldPosB = new Dictionary<Block, Vector2Int>();

    foreach (var bl in groupA.blocks)
        oldPosA[bl] = bl.gridPos;

    foreach (var bl in groupB.blocks)
        oldPosB[bl] = bl.gridPos;

    // move group A -> B
    foreach (var bl in groupA.blocks)
    {
        Vector2Int offset = oldPosA[bl] - originA;
        Vector2Int newPos = originB + offset;

        bl.gridPos = newPos;
        bl.targetPosition = GridToPosition(newPos);
        bl.UpdateTransform(blockSize);
    }

    // move group B -> A
    foreach (var bl in groupB.blocks)
    {
        Vector2Int offset = oldPosB[bl] - originB;
        Vector2Int newPos = originA + offset;

        bl.gridPos = newPos;
        bl.targetPosition = GridToPosition(newPos);
        bl.UpdateTransform(blockSize);
    }

    RecheckAllGroups();
}

        // =========================
        //  MERGE GROUP
        // =========================
        void RecheckAllGroups()
        {
            for (int i = 0; i < currentBlocks.Count; i++)
            {
                for (int j = i + 1; j < currentBlocks.Count; j++)
                {
                    Block a = currentBlocks[i];
                    Block b = currentBlocks[j];

                    if (a.group == b.group) continue;

                    if (IsCorrectNeighbor(a, b))
                    {
                        a.group.Merge(b.group);
                    }
                }
            }
        }

        public bool IsCorrectNeighbor(Block a, Block b)
        {
            Vector2Int diff = a.gridPos - b.gridPos;

            bool isNeighbor =
                (Mathf.Abs(diff.x) == 1 && diff.y == 0) ||
                (Mathf.Abs(diff.y) == 1 && diff.x == 0);

            if (!isNeighbor) return false;

            Vector2Int correctDiff = a.correctPos - b.correctPos;

            return diff == correctDiff;
        }

        public void ShuffleBlocks()
        {
            List<Vector2Int> positions = new List<Vector2Int>();

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    positions.Add(new Vector2Int(r, c));

            foreach (var b in currentBlocks)
            {
                int rand = Random.Range(0, positions.Count);

                b.gridPos = positions[rand];
                b.targetPosition = GridToPosition(b.gridPos);
                b.UpdateTransform(blockSize);

                positions.RemoveAt(rand);
            }
        }
    }