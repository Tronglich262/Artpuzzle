using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class BlockGroup
{
    public List<Block> blocks = new List<Block>();
    public Transform root;

    /// <summary>
    /// Gộp toàn bộ block từ group khác vào group hiện tại,
    /// cập nhật parent, visual và animation sau khi merge.
    /// </summary>
    public void Merge(BlockGroup other)
    {
        if (other == null || other == this || other.root == null) return;

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

        DOTween.Kill(other.root);
        GameObject.Destroy(other.root.gameObject);
        other.blocks.Clear();

        foreach (var b in blocks)
        {
            if (b != null) b.transform.localScale = Vector3.one;
        }

        if (root != null)
        {
            root.localScale = Vector3.one;
            root.DOScale(1.1f, 0.2f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (root != null)
                        root.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
                });
        }
    }

    /// <summary>
    /// Tách một danh sách block ra khỏi group hiện tại
    /// để tạo thành một group mới độc lập.
    /// </summary>
    public void SplitByBlocks(List<Block> blocksToExtract, Transform parent)
    {
        if (blocksToExtract == null || blocksToExtract.Count == 0) return;

        BlockGroup newGroup = new BlockGroup();
        GameObject rootObj = new GameObject("SplitGroup", typeof(RectTransform));
        rootObj.transform.SetParent(parent, false);
        newGroup.root = rootObj.transform;

        foreach (var b in blocksToExtract)
        {
            if (b == null) continue;
            if (!blocks.Contains(b)) continue;

            b.group = newGroup;
            b.transform.SetParent(newGroup.root, true);
            newGroup.blocks.Add(b);
            blocks.Remove(b);
        }

        UpdateGroupVisuals();
        newGroup.UpdateGroupVisuals();
    }

    /// <summary>
    /// Cập nhật hiển thị của group dựa trên số lượng block,
    /// ví dụ bật/tắt outline khi group có 1 hoặc nhiều block.
    /// </summary>
    public void UpdateGroupVisuals()
    {
        if (blocks.Count == 0) return;

        if (blocks.Count == 1)
        {
            if (blocks[0] != null) blocks[0].SetOutline(true);
            return;
        }

        foreach (var b in blocks)
        {
            if (b != null) b.SetOutline(false);
        }
    }

    /// <summary>
    /// Tìm các cụm block còn liên thông trong group hiện tại
    /// bằng cách duyệt BFS qua các block neighbor hợp lệ.
    /// </summary>
    public List<List<Block>> GetConnectedSubgroups()
    {
        List<List<Block>> result = new List<List<Block>>();
        HashSet<Block> visited = new HashSet<Block>();

        foreach (var start in blocks)
        {
            if (start == null || visited.Contains(start)) continue;

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
                    if (neighbor == null || visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);
                    q.Enqueue(neighbor);
                }
            }

            result.Add(subgroup);
        }

        return result;
    }

    /// <summary>
    /// Kiểm tra group hiện tại có bị tách rời thành nhiều cụm hay không.
    /// Nếu có, tạo các group mới tương ứng cho từng cụm liên thông.
    /// </summary>
    public void SplitIfDisconnected(Transform parent)
    {
        var subgroups = GetConnectedSubgroups();
        if (subgroups.Count <= 1) return;

        if (root != null)
            root.DOKill(true);

        blocks.Clear();

        foreach (var sub in subgroups)
        {
            BlockGroup newGroup = new BlockGroup();
            GameObject rootObj = new GameObject("AutoSplitGroup", typeof(RectTransform));
            rootObj.transform.SetParent(parent, false);
            newGroup.root = rootObj.transform;

            foreach (var b in sub)
            {
                if (b == null) continue;
                b.group = newGroup;
                b.transform.SetParent(newGroup.root, true);
                newGroup.blocks.Add(b);
            }

            newGroup.UpdateGroupVisuals();
        }

        if (root != null)
            GameObject.Destroy(root.gameObject);
    }
}