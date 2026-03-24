using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class ButtonSystem : MonoBehaviour
{
    //Button MenuGame
    [Header("Không quảng cáo")]
    public GameObject NoAdsPanel;
    [Header("Cài đặt game")]
    public GameObject SettingPanel;
    [Header("Collection")]
    public GameObject CollectionPanel;
    [Header("DaillyPuzzle")]
    public GameObject DaillyPanel;



    //Dotween
    [Header("List Button UI")]
    public List<RectTransform> buttons;
    [Header("Tween Settings")]
    public float moveDistance = 500f;
    public float duration = 0.5f;
    public Ease ease = Ease.OutQuad;
    private List<Vector2> originalPositions = new List<Vector2>();
    void Start()
    {
        foreach (RectTransform btn in buttons)
        {
            originalPositions.Add(btn.anchoredPosition);
        }
    }
    //hệ thống button MenuGame
    //NoAds
    public void ActiveNoAds()
    {
        ShowPopup(NoAdsPanel);
    }
    public void DisNoAds()
    {
        HidePopup(NoAdsPanel);

    }
    //Setting
    public void ActiveSetting()
    {
        ShowPopup(SettingPanel);
    }

    public void DisSetting()
    {
        HidePopup(SettingPanel);
    }
    //GamePlay
    public void GamePlay()
    {
        SceneManager.LoadScene("GamePlay");
    }
    //Collection
    public void ActiveCollection()
    {
        ShowPopup(CollectionPanel);
    }

    public void DisCollection()
    {
        HidePopup(CollectionPanel);
    }

    //Home
    //DaillyPuzzle
    public void ActiveDailly()
    {
        ShowPopup(DaillyPanel);
    }

    public void DisDailly()
    {
        HidePopup(DaillyPanel);
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
