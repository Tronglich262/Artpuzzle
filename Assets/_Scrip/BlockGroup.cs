using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class BlockGroup
{
    public List<Block> blocks = new List<Block>();
    public Transform root;

    /// <summary>
    /// Lấy block đầu tiên hợp lệ trong group làm block mốc.
    /// </summary>
    public Block GetAnchorBlock()
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] != null)
                return blocks[i];
        }

        return null;
    }

    /// <summary>
    /// Sắp local position của toàn bộ block theo root group.
    /// Nếu snapRoot=true thì root nhảy về đúng vị trí anchor.
    /// </summary>
    public void RebuildLocalLayout(bool snapRoot = true)
    {
        if (root == null || blocks == null || blocks.Count == 0)
            return;

        RectTransform rootRect = root as RectTransform;
        if (rootRect == null)
            return;

        Block anchor = GetAnchorBlock();
        if (anchor == null)
            return;

        Vector2 anchorBoardPos = PuzzleManager.Instance.GridToPosition(anchor.gridPos);

        if (snapRoot)
            rootRect.anchoredPosition = anchorBoardPos;

        for (int i = 0; i < blocks.Count; i++)
        {
            Block b = blocks[i];
            if (b == null)
                continue;

            RectTransform blockRect = b.transform as RectTransform;
            if (blockRect == null)
                continue;

            blockRect.DOKill(false);

            Vector2 blockBoardPos = PuzzleManager.Instance.GridToPosition(b.gridPos);
            Vector2 localOffset = blockBoardPos - anchorBoardPos;

            blockRect.anchoredPosition = localOffset;
            b.targetPosition = localOffset;
            blockRect.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// Gộp group khác vào group hiện tại.
    /// </summary>
    public void Merge(BlockGroup other)
    {
        if (other == null || other == this || other.root == null || root == null)
            return;

        root.SetAsLastSibling();

        foreach (var b in other.blocks)
        {
            if (b != null && b.img != null)
                b.img.DOKill(true);
        }

        other.root.DOKill(true);

        foreach (var b in other.blocks)
        {
            if (b == null) continue;
            if (blocks.Contains(b)) continue;

            b.group = this;
            b.transform.SetParent(root, true);
            blocks.Add(b);

            if (b.img != null)
            {
                b.img.DOColor(Color.white * 1.5f, 0.1f).OnComplete(() =>
                {
                    if (b != null && b.img != null)
                        b.img.DOColor(Color.white, 0.2f);
                });
            }
        }

        UpdateGroupVisuals();
        RebuildLocalLayout(true);

        DOTween.Kill(other.root);
        GameObject.Destroy(other.root.gameObject);
        other.blocks.Clear();
        other.root = null;

        foreach (var b in blocks)
        {
            if (b != null)
                b.transform.localScale = Vector3.one;
        }

        root.localScale = Vector3.one;
        root.DOScale(1.1f, 0.2f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (root != null)
                    root.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
            });
    }

    /// <summary>
    /// Tách 1 số block khỏi group hiện tại để tạo group mới.
    /// </summary>
    public void SplitByBlocks(List<Block> blocksToExtract, Transform parent)
    {
        if (blocksToExtract == null || blocksToExtract.Count == 0)
            return;

        BlockGroup newGroup = new BlockGroup();

        GameObject rootObj = new GameObject("SplitGroup", typeof(RectTransform));
        RectTransform rootRect = rootObj.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;
        rootRect.localScale = Vector3.one;
        rootRect.anchoredPosition = Vector2.zero;

        newGroup.root = rootRect;

        for (int i = blocksToExtract.Count - 1; i >= 0; i--)
        {
            Block b = blocksToExtract[i];
            if (b == null) continue;
            if (!blocks.Contains(b)) continue;

            b.group = newGroup;
            b.transform.SetParent(newGroup.root, true);
            newGroup.blocks.Add(b);
            blocks.Remove(b);
        }

        UpdateGroupVisuals();
        newGroup.UpdateGroupVisuals();

        RebuildLocalLayout(true);
        newGroup.RebuildLocalLayout(true);

        if (blocks.Count == 0 && root != null)
        {
            GameObject.Destroy(root.gameObject);
            root = null;
        }
    }

    /// <summary>
    /// Cập nhật outline:
    /// group 1 block thì bật outline, nhiều block thì tắt.
    /// </summary>
    public void UpdateGroupVisuals()
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] != null)
                blocks[i].SetOutline(false);
        }

        if (blocks.Count == 1 && blocks[0] != null)
            blocks[0].SetOutline(true);
    }

    /// <summary>
    /// Tìm các subgroup còn kết nối đúng trong group bằng BFS.
    /// </summary>
    public List<List<Block>> GetConnectedSubgroups()
    {
        List<List<Block>> result = new List<List<Block>>();
        HashSet<Block> visited = new HashSet<Block>();

        foreach (var start in blocks)
        {
            if (start == null || visited.Contains(start))
                continue;

            List<Block> subgroup = new List<Block>();
            Queue<Block> q = new Queue<Block>();
            q.Enqueue(start);
            visited.Add(start);

            while (q.Count > 0)
            {
                Block current = q.Dequeue();
                subgroup.Add(current);

                foreach (var neighbor in PuzzleManager.Instance.GetCorrectNeighborsInSameGroup(current, this))
                {
                    if (neighbor == null || visited.Contains(neighbor))
                        continue;

                    visited.Add(neighbor);
                    q.Enqueue(neighbor);
                }
            }

            result.Add(subgroup);
        }

        return result;
    }

    /// <summary>
    /// Nếu group bị đứt kết nối thì tách thành nhiều group nhỏ.
    /// </summary>
    public void SplitIfDisconnected(Transform parent)
    {
        var subgroups = GetConnectedSubgroups();
        if (subgroups.Count <= 1)
            return;

        if (root != null)
            root.DOKill(true);

        blocks.Clear();

        foreach (var sub in subgroups)
        {
            BlockGroup newGroup = new BlockGroup();

            GameObject rootObj = new GameObject("AutoSplitGroup", typeof(RectTransform));
            RectTransform rootRect = rootObj.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Vector2.zero;
            rootRect.localScale = Vector3.one;
            rootRect.anchoredPosition = Vector2.zero;

            newGroup.root = rootRect;

            foreach (var b in sub)
            {
                if (b == null) continue;

                b.group = newGroup;
                b.transform.SetParent(newGroup.root, true);
                newGroup.blocks.Add(b);
            }

            newGroup.UpdateGroupVisuals();
            newGroup.RebuildLocalLayout(true);
        }

        if (root != null)
            GameObject.Destroy(root.gameObject);
    }
}