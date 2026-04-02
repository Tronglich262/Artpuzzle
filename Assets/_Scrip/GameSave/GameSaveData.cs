using System;
using System.Collections.Generic;

[Serializable]
public class BlockSaveData
{
    public int correctRow;
    public int correctCol;
    public int gridRow;
    public int gridCol;
}

[Serializable]
public class LevelSessionSaveData
{
    public int levelIndex;
    public List<BlockSaveData> blocks = new();
}

[Serializable]
public class GameSaveData
{
    public int currentLevelIndex;
    public LevelSessionSaveData currentSession = new();
}