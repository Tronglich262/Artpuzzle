using UnityEngine;

[CreateAssetMenu(fileName = "New Level", menuName = "Puzzle/Level")]
public class PuzzleLevel : ScriptableObject
{
    public Sprite levelImage;
    public int rows;
    public int cols;
    public int hintLimit = 3;
}