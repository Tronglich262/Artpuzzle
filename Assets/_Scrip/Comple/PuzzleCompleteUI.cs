using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PuzzleCompleteUI : MonoBehaviour
{
    public static PuzzleCompleteUI Instance;

    [Header("Canvas")]
    [SerializeField] private Canvas canvas;
    private RectTransform canvasRect;
    [SerializeField] private CanvasGroup completeCanvasGroup;
    [Header("Complete UI")]
    [SerializeField] private GameObject completePanel;
    [SerializeField] private RectTransform completePanelRect;
    [SerializeField] private Image previewImage;
    [SerializeField] private RectTransform previewRect;
    [SerializeField] private Button nextButton;

    [Header("Flying Image")]
    [SerializeField] private Image flyingImage;

    [Header("Puzzle Board")]
    [SerializeField] private RectTransform boardRoot;
    [SerializeField] private CanvasGroup boardCanvasGroup;

    [Header("Animation")]
    [SerializeField] private float shrinkDuration = 0.2f;
    [SerializeField] private float popupShowDuration = 0.2f;
    [SerializeField] private float flyDelay = 0.05f;
    [SerializeField] private float flyDuration = 0.55f;
    [SerializeField] private float endScale = 0.35f;

    private bool isPlaying;

    private void Awake()
    {
        Instance = this;

        if (canvas != null)
            canvasRect = canvas.GetComponent<RectTransform>();

        if (completePanel != null)
            completePanel.SetActive(false);

        if (flyingImage != null)
            flyingImage.gameObject.SetActive(false);

        if (nextButton != null)
            nextButton.gameObject.SetActive(false);

        if (boardCanvasGroup == null && boardRoot != null)
            boardCanvasGroup = boardRoot.GetComponent<CanvasGroup>();
    }

    public void PlayComplete(Sprite fullSprite)
    {
        if (isPlaying)
            return;

        if (fullSprite == null || canvas == null || boardRoot == null || previewRect == null || flyingImage == null)
        {
            Debug.LogError("PlayComplete STOP: missing references");
            return;
        }

        isPlaying = true;

        Vector2 startPos = WorldToCanvasPosition(boardRoot.position);
        Vector2 endPos = WorldToCanvasPosition(previewRect.position);
        Vector2 boardSize = boardRoot.rect.size;

        if (previewImage != null)
        {
            previewImage.sprite = fullSprite;
            previewImage.preserveAspect = true;

            Color c = previewImage.color;
            c.a = 0f;
            previewImage.color = c;
        }

        if (nextButton != null)
            nextButton.gameObject.SetActive(false);

        // Hiện popup nhưng không scale nảy
        if (completePanel != null)
            completePanel.SetActive(true);

        if (completePanelRect != null)
        {
            completePanelRect.DOKill();
            completePanelRect.localScale = Vector3.one;
        }

        if (completeCanvasGroup != null)
        {
            completeCanvasGroup.DOKill();
            completeCanvasGroup.alpha = 0f;
        }

        // Ẩn board thật
        if (boardCanvasGroup != null)
        {
            boardCanvasGroup.alpha = 0f;
            boardCanvasGroup.blocksRaycasts = false;
            boardCanvasGroup.interactable = false;
        }
        else
        {
            boardRoot.gameObject.SetActive(false);
        }

        // Ảnh clone để bay
        flyingImage.gameObject.SetActive(true);
        flyingImage.sprite = fullSprite;
        flyingImage.preserveAspect = true;
        flyingImage.color = Color.white;

        RectTransform flyRect = flyingImage.rectTransform;
        flyRect.SetParent(canvas.transform, false);
        flyRect.anchoredPosition = startPos;
        flyRect.sizeDelta = boardSize;

        // KHÔNG để scale từ 1 xuống quá nhỏ
        flyRect.localScale = Vector3.one * 0.96f;
        flyRect.SetAsLastSibling();

        Sequence seq = DOTween.Sequence();

        // popup fade mượt
        if (completeCanvasGroup != null)
            seq.Append(completeCanvasGroup.DOFade(1f, 0.18f));

        seq.AppendInterval(0.02f);

        // ảnh bay mượt vào khung
        seq.Append(flyRect.DOAnchorPos(endPos, 0.45f).SetEase(Ease.OutCubic));
        seq.Join(flyRect.DOScale(0.82f, 0.45f).SetEase(Ease.OutCubic));

        seq.AppendCallback(() =>
        {
            if (previewImage != null)
                previewImage.DOFade(1f, 0.12f);

            flyingImage.gameObject.SetActive(false);

            if (nextButton != null)
                nextButton.gameObject.SetActive(true);

            isPlaying = false;
        });
    }
    public void ResetCompleteUIForNextLevel()
    {
        isPlaying = false;

        if (completePanel != null)
            completePanel.SetActive(false);

        if (flyingImage != null)
        {
            flyingImage.DOKill();
            flyingImage.gameObject.SetActive(false);
        }

        if (previewImage != null)
        {
            previewImage.DOKill();
            previewImage.sprite = null;

            Color c = previewImage.color;
            c.a = 0f;
            previewImage.color = c;
        }

        if (nextButton != null)
            nextButton.gameObject.SetActive(false);

        if (completePanelRect != null)
        {
            completePanelRect.DOKill();
            completePanelRect.localScale = Vector3.one;
        }

        if (boardRoot != null)
        {
            boardRoot.DOKill();
            boardRoot.localScale = Vector3.one;
            boardRoot.gameObject.SetActive(true);
        }

        if (boardCanvasGroup != null)
        {
            boardCanvasGroup.alpha = 1f;
            boardCanvasGroup.blocksRaycasts = true;
            boardCanvasGroup.interactable = true;
        }
    }

    public void OnClickNext()
    {
        if (PuzzleManager.Instance != null)
            PuzzleManager.Instance.LoadNextLevel();
    }

    private Vector2 WorldToCanvasPosition(Vector3 worldPos)
    {
        if (canvasRect == null)
            return Vector2.zero;

        Camera cam = null;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(cam, worldPos),
            cam,
            out Vector2 localPoint
        );

        return localPoint;
    }
}