using UnityEngine;
using UnityEngine.UI;

public class Block : MonoBehaviour
{
    public Image img;

    public Vector2Int gridPos;
    public Vector2Int correctPos;

    public BlockGroup group;
    public Vector2 targetPosition;
    public void UpdateTransform(float blockSize)
    {
        GetComponent<RectTransform>().anchoredPosition = targetPosition;
    }
}