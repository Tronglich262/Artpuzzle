using UnityEngine;

public class Block : MonoBehaviour
{
    public Vector2Int gridPos;
    public Vector2Int correctPos;
    public Vector2 targetPosition;

    public BlockGroup group;

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void UpdateTransform(float blockSize)
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(blockSize, blockSize);
        rt.anchoredPosition = targetPosition;
    }

    public void PlaySelect()
    {
        if (animator != null)
        {
            animator.SetTrigger("Selected");
        }
    }
}