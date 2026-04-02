using System.IO;
using UnityEngine;

public static class SaveManager
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "game_save.json");

    public static void Save(GameSaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log("Saved to: " + SavePath);
    }

    public static GameSaveData Load()
    {
        if (!File.Exists(SavePath))
            return new GameSaveData();

        string json = File.ReadAllText(SavePath);

        if (string.IsNullOrWhiteSpace(json))
            return new GameSaveData();

        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
        return data ?? new GameSaveData();
    }

    public static bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }
}