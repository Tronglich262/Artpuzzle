using System.Collections.Generic;
using UnityEngine;

public class BlockGroup
{
    public List<Block> blocks = new List<Block>();
    public Transform root;

    public void Merge(BlockGroup other)
    {
        foreach (var b in other.blocks)
        {
            b.group = this;
            b.transform.SetParent(root, true);
            blocks.Add(b);
        }
        GameObject.Destroy(other.root.gameObject);
        other.blocks.Clear();

        foreach (var b in blocks) b.transform.localScale = Vector3.one;
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
    }
}