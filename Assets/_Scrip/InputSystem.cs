using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class InputSystem : MonoBehaviour
{
    public void OnHintButtonClicked()
    {
        if (LevelSystem.Instance.currentHintsUsed >= LevelSystem.Instance.maxHints)
        {
            return;
        }
        if (AutoSolveOneStep())
        {
            LevelSystem.Instance.currentHintsUsed++;
        }
    }

    // Tự động giải một bước: chọn một block sai vị trí, nếu nó đang bị chặn bởi block khác thì đẩy block đó sang vị trí trống, sau đó đưa block về đúng vị trí
    public bool AutoSolveOneStep()
    {
        // 1. Chỉ tìm những block chưa nằm đúng vị trí (gridPos != correctPos)
        var wrongBlocks = PuzzleManager.Instance.currentBlocks.Where(b => b.gridPos != b.correctPos).ToList();

        if (wrongBlocks.Count == 0)
        {
            Debug.Log("Win");
            LevelSystem.Instance.NextLevel();
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
        Block blocker = PuzzleManager.Instance.currentBlocks.FirstOrDefault(b => b.gridPos == destination && b != targetBlock);
        if (blocker != null)
        {
            // Nếu kẻ chiếm chỗ cũng đang nằm trong Group, tách nó ra để dễ "đá" đi chỗ khác
            if (blocker.group.blocks.Count > 1)
            {
                blocker.group.SplitByBlocks(new List<Block> { blocker }, transform);
            }
            // Tìm 1 ô trống bất kỳ để đẩy kẻ chiếm chỗ sang đó
            Vector2Int emptySpot = PuzzleManager.Instance.GetEmptyPositions().FirstOrDefault();
            Vector2Int tempPos = blocker.gridPos;
            blocker.gridPos = targetBlock.gridPos;
            targetBlock.gridPos = tempPos;
        }
        // 5. ĐƯA VỀ VỊ TRÍ ĐÚNG
        targetBlock.gridPos = destination;
        PuzzleManager.Instance.RebuildGridFromBlocksStrict();
        PuzzleManager.Instance.UpdateAllBlockPositions();
        PuzzleManager.Instance.CheckAndMergeGroups();
        PuzzleManager.Instance.RebuildGridFromBlocksStrict();
        PuzzleManager.Instance.RefreshAllBorders(true);
        PuzzleManager.Instance.SaveCurrentState();

        targetBlock.img.DOColor(Color.green, 0.3f).OnComplete(() =>
        {
            targetBlock.img.DOColor(Color.white, 0.5f);
        });

        return true;
    }
}
