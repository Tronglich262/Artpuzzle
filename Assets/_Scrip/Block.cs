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
    public void UpdateTransform()
{
    float duration = 0.2f;
    
    GetComponent<RectTransform>()
        .DOAnchorPos(targetPosition, duration)
        .SetEase(Ease.OutCubic); 
    transform.DOScale(Vector3.one, duration);
}
}