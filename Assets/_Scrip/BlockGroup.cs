using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System.Linq;
public class BlockGroup
{
    public List<Block> blocks = new List<Block>();
    public Transform root;

    public void Merge(BlockGroup other)
    {
        root.SetAsLastSibling();
        foreach (var b in other.blocks)
        {
            b.group = this;
            b.transform.SetParent(root, true);
            blocks.Add(b);

            b.img.DOColor(Color.white * 1.5f, 0.1f).OnComplete(() =>
            {
                b.img.DOColor(Color.white, 0.2f);
            });
        }
        // Cập nhật viền cho cả nhóm sau khi merge
        UpdateGroupVisuals();
        DOTween.Kill(other.root);
        GameObject.Destroy(other.root.gameObject);
        other.blocks.Clear();
        foreach (var b in blocks) b.transform.localScale = Vector3.one;
        root.localScale = Vector3.one;
        root.DOScale(1.25f, 0.15f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                root.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
            });
    }

    public void SplitByBlocks(List<Block> blocksToExtract, Transform parent)
    {
        BlockGroup newGroup = new BlockGroup();
        GameObject rootObj = new GameObject("SplitGroup", typeof(RectTransform));
        rootObj.transform.SetParent(parent, false);
        newGroup.root = rootObj.transform;

        foreach (var b in blocksToExtract)
        {
            b.group = newGroup;
            b.transform.SetParent(newGroup.root, true);
            newGroup.blocks.Add(b);
            blocks.Remove(b);
        }

        // Cập nhật viền cho cả nhóm cũ và nhóm mới
        UpdateGroupVisuals();
        newGroup.UpdateGroupVisuals();
    }

    public void UpdateGroupVisuals()
    {
        if (blocks.Count == 0) return;
        if (blocks.Count == 1)
        {
            blocks[0].SetOutline(true);
            return;
        }
        foreach (var b in blocks)
        {
            b.SetOutline(false);
        }
    }

    bool IsBlockOnEdge(Block b)
    {
        Vector2Int[] dirs = {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

        foreach (var d in dirs)
        {
            Vector2Int neighborPos = b.gridPos + d;

            // Nếu KHÔNG có block cùng group ở hướng này → là edge
            bool hasNeighbor = blocks.Any(other => other.gridPos == neighborPos);

            if (!hasNeighbor)
                return true;
        }

        return false;
    }
    public List<List<Block>> GetConnectedSubgroups()
    {
        List<List<Block>> result = new List<List<Block>>();
        HashSet<Block> visited = new HashSet<Block>();

        foreach (var start in blocks)
        {
            if (visited.Contains(start)) continue;

            List<Block> group = new List<Block>();
            Queue<Block> q = new Queue<Block>();
            q.Enqueue(start);
            visited.Add(start);

            while (q.Count > 0)
            {
                var current = q.Dequeue();
                group.Add(current);

                Vector2Int[] dirs = {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

                foreach (var d in dirs)
                {
                    Vector2Int neighborPos = current.gridPos + d;

                    var neighbor = blocks.FirstOrDefault(b => b.gridPos == neighborPos);
                    if (neighbor != null && !visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        q.Enqueue(neighbor);
                    }
                }
            }

            result.Add(group);
        }

        return result;
    }
    public void SplitIfDisconnected(Transform parent)
    {
        var subgroups = GetConnectedSubgroups();

        if (subgroups.Count <= 1) return;

        // Xóa group cũ
        var oldBlocks = new List<Block>(blocks);
        blocks.Clear();

        foreach (var sub in subgroups)
        {
            BlockGroup newGroup = new BlockGroup();
            GameObject rootObj = new GameObject("AutoSplitGroup", typeof(RectTransform));
            rootObj.transform.SetParent(parent, false);
            newGroup.root = rootObj.transform;

            foreach (var b in sub)
            {
                b.group = newGroup;
                b.transform.SetParent(newGroup.root, true);
                newGroup.blocks.Add(b);
            }

            newGroup.UpdateGroupVisuals();
        }

        GameObject.Destroy(root.gameObject);
    }
}