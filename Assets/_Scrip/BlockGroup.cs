using System.Collections.Generic;
using UnityEngine;

public class BlockGroup
{
    public List<Block> blocks = new List<Block>();
    public Transform root;

    public void Merge(BlockGroup other)
    {
        Vector3 originalScale = root.localScale; // Lưu scale gốc của root

        foreach (var b in other.blocks)
        {
            // Lưu world position trước khi đổi parent
            Vector3 worldPos = b.transform.position;
            Quaternion worldRot = b.transform.rotation;
            Vector3 worldScale = b.transform.lossyScale;

            b.group = this;

            // Di chuyển sang root mới VỚI worldPositionStays = true để giữ nguyên world transform
            b.transform.SetParent(root, true);

            blocks.Add(b);
        }

        // Xóa root cũ
        GameObject.Destroy(other.root.gameObject);

        other.blocks.Clear();

        // Reset scale của root mới về 1 (nếu cần)
        root.localScale = originalScale;

        // Reset scale của tất cả blocks trong nhóm về 1
        foreach (var b in blocks)
        {
            b.transform.localScale = Vector3.one;
        }
    }
}