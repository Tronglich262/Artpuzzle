using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonSystem : MonoBehaviour
{
    [Header("Không quảng cáo")]
    public GameObject NoAdsPanel;

    [Header("Cài đặt game")]
    public GameObject SettingPanel;

    [Header("List Button UI")]
    public List<RectTransform> buttons;

    [Header("Tween Settings")]
    public float moveDistance = 500f;
    public float duration = 0.5f;
    public Ease ease = Ease.OutQuad;

    private List<Vector2> originalPositions = new List<Vector2>();

    public static ButtonSystem instace;

    // ghi nhớ NoAds có được mở từ Setting hay không
    private bool noAdsOpenedFromSetting = false;

    void Awake()
    {
        instace = this;
    }

    void Start()
    {
        foreach (RectTransform btn in buttons)
        {
            originalPositions.Add(btn.anchoredPosition);
        }
    }

    // =========================
    // No Ads
    // =========================

    // mở NoAds từ ngoài menu
    public void ActiveNoAds()
    {
        noAdsOpenedFromSetting = false;
        ShowPopup(NoAdsPanel);
    }

    // mở NoAds từ trong Setting
    public void ActiveNoAdsFromSetting()
    {
        noAdsOpenedFromSetting = true;

        if (SettingPanel != null)
            SettingPanel.SetActive(false);

        ShowPopup(NoAdsPanel);
    }

    // đóng NoAds
    public void DisNoAds()
    {
        StartCoroutine(HideNoAdsAndReturnIfNeeded());
    }

    private IEnumerator HideNoAdsAndReturnIfNeeded()
    {
        yield return StartCoroutine(Popup(NoAdsPanel, false));

        if (noAdsOpenedFromSetting)
        {
            noAdsOpenedFromSetting = false;
            ShowPopup(SettingPanel);
        }
        else
        {
            OnPopupClose();
        }
    }

    // =========================
    // Setting
    // =========================
    public void ActiveSetting()
    {
        ShowPopup(SettingPanel);
    }

    public void DisSetting()
    {
        HidePopup(SettingPanel);
    }

    // =========================
    // Gameplay
    // =========================
    public void GamePlay()
    {
        SceneManager.LoadScene("GamePlay");
    }


    // =========================
    // sau khi bấm Remove Ads / Buy
    // =========================
    public void RemoveAds()
    {
        // nếu đang mở setting thì nhớ là mở từ setting
        noAdsOpenedFromSetting = SettingPanel != null && SettingPanel.activeSelf;

        if (noAdsOpenedFromSetting && SettingPanel != null)
            SettingPanel.SetActive(false);

        ShowPopup(NoAdsPanel);
    }

    /// <summary>
    /// Dotween trượt ra khỏi màn hình khi 1 panel được gọi
    /// </summary>
    public void OnPopupOpen()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            RectTransform btn = buttons[i];
            Vector2 targetPos = originalPositions[i] + new Vector2(-moveDistance, 0);
            btn.DOAnchorPos(targetPos, duration).SetEase(ease);
        }
    }

    public void OnPopupClose()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].DOAnchorPos(originalPositions[i], duration).SetEase(ease);
        }
    }

    /// <summary>
    /// hàm Popup tắt bật
    /// </summary>
    public void ShowPopup(GameObject panel)
    {
        if (panel == null) return;

        panel.SetActive(true);
        StartCoroutine(Popup(panel, true));
        OnPopupOpen();
    }

    public void HidePopup(GameObject panel)
    {
        if (panel == null) return;

        StartCoroutine(Popup(panel, false));
        OnPopupClose();
    }

    IEnumerator Popup(GameObject panel, bool isShow)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect == null) yield break;

        rect.DOKill();

        if (isShow)
        {
            rect.localScale = Vector3.zero;
            rect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(0.3f);
        }
        else
        {
            rect.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack);
            yield return new WaitForSeconds(0.2f);
            panel.SetActive(false);
        }
    }
}