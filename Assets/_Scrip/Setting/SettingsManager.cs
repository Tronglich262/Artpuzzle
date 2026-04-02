using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    private const string SaveKey = "GAME_SETTINGS_JSON";

    public GameSettingsData Data { get; private set; }

    public bool SoundsOn => Data != null && Data.soundsOn;
    public bool MusicOn => Data != null && Data.musicOn;
    public bool VibrationsOn => Data != null && Data.vibrationsOn;
    public bool ColorBlindAOn => Data != null && Data.colorBlindAOn;
    public bool RemoveAdsPurchased => Data != null && Data.removeAdsPurchased;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    private void Start()
    {
        ApplyAll();
    }

    public void SetSounds(bool value)
    {
        Data.soundsOn = value;
        Save();
        ApplySounds();
    }

    public void SetMusic(bool value)
    {
        Data.musicOn = value;
        Save();
        ApplyMusic();
    }

    public void SetVibrations(bool value)
    {
        Data.vibrationsOn = value;
        Save();
    }

    public void SetColorBlindA(bool value)
    {
        Data.colorBlindAOn = value;
        Save();
        ApplyColorBlindA();
    }

    public void SetRemoveAdsPurchased(bool value)
    {
        Data.removeAdsPurchased = value;
        Save();
        ApplyRemoveAds();
    }

    public void BuyRemoveAds()
    {
        SetRemoveAdsPurchased(true);

        Debug.Log("Da mua Remove Ads");

        if (ButtonSystem.instace != null)
            ButtonSystem.instace.RemoveAds();
    }

    public void ResetLevelNow()
    {
        Debug.Log("Reset Level");

        PlayerPrefs.DeleteKey("CURRENT_LEVEL");
        PlayerPrefs.Save();

        // Nếu bạn có manager level riêng thì gọi ở đây
        // Example:
        // LevelManager.Instance.ResetCurrentLevel();
    }

    public void Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            Data = new GameSettingsData();
            Save();
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey, "");

        if (string.IsNullOrEmpty(json))
        {
            Data = new GameSettingsData();
            Save();
            return;
        }

        Data = JsonUtility.FromJson<GameSettingsData>(json);

        if (Data == null)
            Data = new GameSettingsData();
    }

    public void Save()
    {
        string json = JsonUtility.ToJson(Data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public void ApplyAll()
    {
        ApplySounds();
        ApplyMusic();
        ApplyColorBlindA();
    }

    private void ApplySounds()
    {
        Debug.Log("Sounds");
        // SFXManager.Instance.SetEnable(SoundsOn);
    }

    private void ApplyMusic()
    {
        Debug.Log("Music");
        // MusicManager.Instance.SetEnable(MusicOn);
    }

    private void ApplyColorBlindA()
    {
        // ColorBlindManager.Instance.SetModeA(ColorBlindAOn);
    }

    private void ApplyRemoveAds()
    {
        Debug.Log("RemoveAdsPurchased: " + RemoveAdsPurchased);

        if (ButtonSystem.instace != null)
        {
            if (RemoveAdsPurchased)
                ButtonSystem.instace.RemoveAds();
        }
    }
}