using UnityEngine;
using UnityEngine.InputSystem;

public class LevelSystem : MonoBehaviour
{
    //btn help
    [SerializeField] public int maxHints = 100;
    [SerializeField] public int currentHintsUsed = 0;
    public static LevelSystem Instance { get; private set; }
    public void Awake()
    {
        Instance = this;
    }
    void Update()
    {
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            NextLevel();
        }
    }
    public void NextLevel()
    {
        int nextIndex = PuzzleManager.Instance.currentLevelIndex + 1;
        if (nextIndex >= PuzzleManager.Instance.levels.Count)
        {
            nextIndex = 0;
        }
        LoadLevel(nextIndex);
    }
    public void LoadLevel(int index)
    {
        if (PuzzleManager.Instance.levels == null || PuzzleManager.Instance.levels.Count == 0)
        {
            Debug.Log("Level null or == 0");
            return;
        }

        index = Mathf.Clamp(index, 0, PuzzleManager.Instance.levels.Count - 1);

        PuzzleManager.Instance.currentLevelIndex = index;
        PuzzleLevel data = PuzzleManager.Instance.levels[index];

        PuzzleManager.Instance.sourceImage = data.levelImage;
        PuzzleManager.Instance.rows = data.rows;
        PuzzleManager.Instance.cols = data.cols;

        maxHints = data.hintLimit;
        currentHintsUsed = 0;

        PuzzleManager.Instance.GeneratePuzzle();
        PuzzleManager.Instance.ShuffleBlocks();
        PuzzleManager.Instance.SaveCurrentState();
    }
    public void LoadCurrentLevelFromSave()
    {
        GameSaveData save = SaveManager.Load();

        int index = save.currentLevelIndex;
        if (PuzzleManager.Instance.levels == null || PuzzleManager.Instance.levels.Count == 0)
            return;

        index = Mathf.Clamp(index, 0, PuzzleManager.Instance.levels.Count - 1);

        PuzzleManager.Instance.currentLevelIndex = index;
        PuzzleLevel data = PuzzleManager.Instance.levels[index];

        PuzzleManager.Instance.sourceImage = data.levelImage;
        PuzzleManager.Instance.rows = data.rows;
        PuzzleManager.Instance.cols = data.cols;

        maxHints = data.hintLimit;
        currentHintsUsed = 0;

        PuzzleManager.Instance.GeneratePuzzle();

        if (save.currentSession != null &&
            save.currentSession.levelIndex == index &&
            save.currentSession.blocks != null &&
            save.currentSession.blocks.Count > 0)
        {
            PuzzleManager.Instance.ApplySessionData(save.currentSession);
        }
        else
        {
            PuzzleManager.Instance.ShuffleBlocks();
            PuzzleManager.Instance.SaveCurrentState();
        }
    }
}
