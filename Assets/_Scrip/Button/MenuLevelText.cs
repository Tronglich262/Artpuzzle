using TMPro;
using UnityEngine;

public class MenuLevelText : MonoBehaviour
{
    [SerializeField] private TMP_Text levelText;

    private void Start()
    {
        GameSaveData save = SaveManager.Load();
        int levelNumber = save.currentLevelIndex + 1;
        levelText.text = "Level " + levelNumber;
    }
}