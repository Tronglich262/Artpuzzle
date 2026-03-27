using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class BlockGroup
{
    public readonly List<Block> blocks = new();
    public Transform root;

    private readonly Queue<Block> _queueCache = new();
    private readonly HashSet<Block> _visitedCache = new();

    public Block GetAnchorBlock()
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] != null)
                return blocks[i];
        }

        return null;
    }

    public void RebuildLocalLayout(bool snapRoot = true, bool animateChildren = false, float childDuration = 0.07f)
    {
        if (root == null || blocks.Count == 0)
            return;

        RectTransform rootRect = root as RectTransform;
        if (rootRect == null)
            return;

        Block anchor = GetAnchorBlock();
        if (anchor == null)
            return;

        PuzzleManager puzzle = PuzzleManager.Instance;
        if (puzzle == null)
            return;

        Vector2 anchorBoardPos = puzzle.GridToPosition(anchor.gridPos);

        if (snapRoot)
            rootRect.anchoredPosition = anchorBoardPos;

        for (int i = 0; i < blocks.Count; i++)
        {
            Block block = blocks[i];
            if (block == null)
                continue;

            RectTransform blockRect = block.transform as RectTransform;
            if (blockRect == null)
                continue;

            Vector2 blockBoardPos = puzzle.GridToPosition(block.gridPos);
            Vector2 localOffset = blockBoardPos - anchorBoardPos;

            blockRect.DOKill(false);
            block.targetPosition = localOffset;
            block.MarkBorderDirty();

            if (animateChildren)
            {
                blockRect.DOAnchorPos(localOffset, childDuration)
                    .SetEase(Ease.OutCubic)
                    .OnUpdate(block.MarkBorderDirty)
                    .OnComplete(block.MarkBorderDirty);
            }
            else
            {
                blockRect.anchoredPosition = localOffset;
            }

            blockRect.localScale = Vector3.one;
        }
    }

    public void Merge(BlockGroup other)
    {
        if (other == null || other == this || other.root == null || root == null)
            return;

        root.SetAsLastSibling();

        for (int i = 0; i < other.blocks.Count; i++)
        {
            Block b = other.blocks[i];
            if (b != null && b.img != null)
                b.img.DOKill(true);
        }

        other.root.DOKill(true);

        for (int i = 0; i < other.blocks.Count; i++)
        {
            Block b = other.blocks[i];
            if (b == null)
                continue;

            b.group = this;
            b.transform.SetParent(root, true);
            blocks.Add(b);
            b.MarkBorderDirty();

            if (b.img != null)
            {
                b.img.DOColor(Color.white * 1.5f, 0.1f).OnComplete(() =>
                {
                    if (b != null && b.img != null)
                        b.img.DOColor(Color.white, 0.2f);
                });
            }
        }

        RebuildLocalLayout(true);

        DOTween.Kill(other.root);
        GameObject.Destroy(other.root.gameObject);
        other.blocks.Clear();
        other.root = null;

        for (int i = 0; i < blocks.Count; i++)
        {
            Block b = blocks[i];
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

        PuzzleManager.Instance?.RebuildGridFromBlocksStrict();
        PuzzleManager.Instance?.RefreshAllBorders(false);
    }

    public void SplitByBlocks(List<Block> blocksToExtract, Transform parent, bool keepWorldVisual = true)
    {
        if (blocksToExtract == null || blocksToExtract.Count == 0)
            return;

        PuzzleManager puzzle = PuzzleManager.Instance;
        if (puzzle == null)
            return;

        BlockGroup newGroup = new BlockGroup();
        RectTransform rootRect = puzzle.CreateGroupRoot("SplitGroup", parent);
        newGroup.root = rootRect;

        Vector3 firstWorldPos = Vector3.zero;
        bool hasFirstWorldPos = false;

        for (int i = blocksToExtract.Count - 1; i >= 0; i--)
        {
            Block b = blocksToExtract[i];
            if (b == null)
                continue;

            int index = blocks.IndexOf(b);
            if (index < 0)
                continue;

            if (!hasFirstWorldPos)
            {
                firstWorldPos = b.transform.position;
                hasFirstWorldPos = true;
            }

            b.group = newGroup;
            b.transform.SetParent(newGroup.root, true);
            b.MarkBorderDirty();
            newGroup.blocks.Add(b);
            blocks.RemoveAt(index);
        }

        if (newGroup.blocks.Count == 0)
        {
            GameObject.Destroy(rootRect.gameObject);
            return;
        }

        if (keepWorldVisual && hasFirstWorldPos)
        {
            rootRect.position = firstWorldPos;
            newGroup.RebuildLocalLayout(false, false);
        }
        else
        {
            newGroup.RebuildLocalLayout(true, false);
        }

        RebuildLocalLayout(true, false);

        if (blocks.Count == 0 && root != null)
        {
            GameObject.Destroy(root.gameObject);
            root = null;
        }

        puzzle.RebuildGridFromBlocksStrict();
        puzzle.RefreshAllBorders(false);
    }

    public List<List<Block>> GetConnectedSubgroups()
    {
        List<List<Block>> result = new();
        PuzzleManager puzzle = PuzzleManager.Instance;
        if (puzzle == null)
            return result;

        _visitedCache.Clear();
        _queueCache.Clear();

        for (int i = 0; i < blocks.Count; i++)
        {
            Block start = blocks[i];
            if (start == null || _visitedCache.Contains(start))
                continue;

            List<Block> subgroup = new();
            _queueCache.Enqueue(start);
            _visitedCache.Add(start);

            while (_queueCache.Count > 0)
            {
                Block current = _queueCache.Dequeue();
                subgroup.Add(current);

                List<Block> neighbors = puzzle.GetCorrectNeighborsInSameGroup(current, this);
                for (int n = 0; n < neighbors.Count; n++)
                {
                    Block neighbor = neighbors[n];
                    if (neighbor == null || _visitedCache.Contains(neighbor))
                        continue;

                    _visitedCache.Add(neighbor);
                    _queueCache.Enqueue(neighbor);
                }
            }

            result.Add(subgroup);
        }

        return result;
    }

    public void SplitIfDisconnected(Transform parent)
    {
        PuzzleManager puzzle = PuzzleManager.Instance;
        if (puzzle == null)
            return;

        List<List<Block>> subgroups = GetConnectedSubgroups();
        if (subgroups.Count <= 1)
            return;

        if (root != null)
            root.DOKill(true);

        blocks.Clear();

        for (int i = 0; i < subgroups.Count; i++)
        {
            List<Block> sub = subgroups[i];
            BlockGroup newGroup = new BlockGroup();
            RectTransform rootRect = puzzle.CreateGroupRoot("AutoSplitGroup", parent);
            newGroup.root = rootRect;

            for (int j = 0; j < sub.Count; j++)
            {
                Block b = sub[j];
                if (b == null)
                    continue;

                b.group = newGroup;
                b.transform.SetParent(newGroup.root, true);
                b.MarkBorderDirty();
                newGroup.blocks.Add(b);
            }

            newGroup.RebuildLocalLayout(true);
        }

        if (root != null)
            GameObject.Destroy(root.gameObject);

        puzzle.RebuildGridFromBlocksStrict();
        puzzle.RefreshAllBorders(false);
    }
}
