using UnityEngine;
using UnityEngine.InputSystem;

public class LevelSystem : MonoBehaviour
{
    //btn help
    [SerializeField] public int maxHints = 100;
    [SerializeField] public int currentHintsUsed = 0;
    public static LevelSystem Instance {  get; private set; }
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
    private void LoadLevel(int index)
    {
        if (PuzzleManager.Instance.levels == null || PuzzleManager.Instance.levels.Count == 0)
        {
            Debug.Log("Level null or == 0");
            return;
        }

        PuzzleManager.Instance.currentLevelIndex = index;
        PuzzleLevel data = PuzzleManager.Instance.levels[index];
        // Cập nhật thông số từ ScriptableObject
        PuzzleManager.Instance.sourceImage = data.levelImage;
        PuzzleManager.Instance.rows = data.rows;
        PuzzleManager.Instance.cols = data.cols;
        this.maxHints = data.hintLimit;
        Debug.Log("SourceImage: " + (PuzzleManager.Instance.sourceImage == null ? "NULL" : "true" + "Level: " + PuzzleManager.Instance.currentLevelIndex));
        this.currentHintsUsed = 0;
        PuzzleManager.Instance.GeneratePuzzle();
        PuzzleManager.Instance.ShuffleBlocks();
    }
}
