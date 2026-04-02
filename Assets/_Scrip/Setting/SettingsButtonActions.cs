using UnityEngine;

public class SettingsButtonActions : MonoBehaviour
{
    public void OnClickRemoveAds()
    {
        if (SettingsManager.Instance == null) return;

        SettingsManager.Instance.BuyRemoveAds();

        if (SettingsManager.Instance.VibrationsOn)
            Handheld.Vibrate();
    }

    public void OnClickResetLevel()
    {
        if (SettingsManager.Instance == null) return;

        SettingsManager.Instance.ResetLevelNow();

        if (SettingsManager.Instance.VibrationsOn)
            Handheld.Vibrate();
    }
}