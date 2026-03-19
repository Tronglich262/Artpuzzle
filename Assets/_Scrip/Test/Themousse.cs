using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Collections;
using UnityEngine.UI;

public class Themousse : MonoBehaviour , IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public Image target;
    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector2 Position1;
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Position1 = rectTransform.anchoredPosition;
        rectTransform.SetAsLastSibling();
        StartCoroutine(Popup());

    }
    IEnumerator Popup()
    {
        rectTransform.DOScale(2f, 0.5f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(0.5f);
        Vector2 targetPos = transform.position;
        rectTransform.DOAnchorPos(targetPos, 0.5f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(0.5f) ;
        Destroy(this.gameObject);
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Không làm gì cũng được
        rectTransform.anchoredPosition = Position1;

        Debug.Log("Thả rồi");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
}
