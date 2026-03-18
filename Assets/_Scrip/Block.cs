using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
public class Block : MonoBehaviour
{
    public Image img;

    public Vector2Int gridPos;
    public Vector2Int correctPos;
    public int gridMark = -1; // Đánh dấu thứ tự trong grid matrix (-1 = chưa đánh dấu)

    public BlockGroup group;
    public Vector2 targetPosition;
    public void UpdateTransform(float blockSize)
    {
        GetComponent<RectTransform>()
            .DOAnchorPos(targetPosition, 0.2f)
            .SetEase(Ease.OutBack);
    }
}