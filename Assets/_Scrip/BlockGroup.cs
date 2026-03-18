using System.Collections.Generic;
using UnityEngine;

public class BlockGroup
{
    public List<Block> blocks = new List<Block>();

    public void Merge(BlockGroup other)
    {
        foreach (var b in other.blocks)
        {
            b.group = this;
            blocks.Add(b);
        }
        other.blocks.Clear();
    }
}