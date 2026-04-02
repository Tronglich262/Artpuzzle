using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class SettingToggleUI : MonoBehaviour
{
    public enum SettingType
    {
        Sounds,
        Music,
        Vibrations,
        ColorBlindA
    }

    [Header("Setting Type")]
    [SerializeField] private SettingType settingType;

    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform knob;
    [SerializeField] private Button clickButton;

    [Header("Visual")]
    [SerializeField] private Color onColor = new Color32(255, 210, 0, 255);
    [SerializeField] private Color offColor = new Color32(35, 35, 35, 255);

    [Header("Knob Position")]
    [SerializeField] private float knobOffX = -18f;
    [SerializeField] private float knobOnX = 18f;

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.15f;

    private bool currentValue;

    private void Awake()
    {
        if (clickButton == null)
            clickButton = GetComponent<Button>();

        if (clickButton != null)
            clickButton.onClick.AddListener(OnClickToggle);
    }

    private void OnDestroy()
    {
        if (clickButton != null)
            clickButton.onClick.RemoveListener(OnClickToggle);
    }

    private void Start()
    {
        RefreshUI(true);
    }

    public void RefreshUI(bool instant = false)
    {
        currentValue = GetSettingValue();
        ApplyVisual(currentValue, instant);
    }

    private void OnClickToggle()
    {
        currentValue = !currentValue;
        SetSettingValue(currentValue);
        ApplyVisual(currentValue, false);

        if (SettingsManager.Instance != null && SettingsManager.Instance.VibrationsOn)
            Handheld.Vibrate();
    }

    private bool GetSettingValue()
    {
        if (SettingsManager.Instance == null)
            return false;

        switch (settingType)
        {
            case SettingType.Sounds:
                return SettingsManager.Instance.SoundsOn;

            case SettingType.Music:
                return SettingsManager.Instance.MusicOn;

            case SettingType.Vibrations:
                return SettingsManager.Instance.VibrationsOn;

            case SettingType.ColorBlindA:
                return SettingsManager.Instance.ColorBlindAOn;

            default:
                return false;
        }
    }

    private void SetSettingValue(bool value)
    {
        if (SettingsManager.Instance == null)
            return;

        switch (settingType)
        {
            case SettingType.Sounds:
                SettingsManager.Instance.SetSounds(value);
                break;

            case SettingType.Music:
                SettingsManager.Instance.SetMusic(value);
                break;

            case SettingType.Vibrations:
                SettingsManager.Instance.SetVibrations(value);
                break;

            case SettingType.ColorBlindA:
                SettingsManager.Instance.SetColorBlindA(value);
                break;
        }
    }

    private void ApplyVisual(bool isOn, bool instant)
    {
        if (backgroundImage != null)
        {
            backgroundImage.DOKill();
            Color targetColor = isOn ? onColor : offColor;

            if (instant)
                backgroundImage.color = targetColor;
            else
                backgroundImage.DOColor(targetColor, animDuration);
        }

        if (knob != null)
        {
            knob.DOKill();

            Vector2 targetPos = knob.anchoredPosition;
            targetPos.x = isOn ? knobOnX : knobOffX;

            if (instant)
                knob.anchoredPosition = targetPos;
            else
                knob.DOAnchorPos(targetPos, animDuration).SetEase(Ease.OutCubic);
        }
    }
}