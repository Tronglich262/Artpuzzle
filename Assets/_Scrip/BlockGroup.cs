using System.Collections.Generic;
using UnityEngine;

public class BlockGroup
{
    public List<Block> blocks = new List<Block>();

    public void Add(Block b)
    {
        if (!blocks.Contains(b))
        {
            blocks.Add(b);
            b.group = this;
        }
    }

    public void Merge(BlockGroup other)
{
    if (other == this) return;

    foreach (var b in other.blocks)
    {
        Add(b);
    }

    other.blocks.Clear(); 
}
}