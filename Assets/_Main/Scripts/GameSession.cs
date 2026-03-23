using UnityEngine;

public enum ActivityType
{
    BarrelActivity,
    NormalActivity
}

public enum ModeType
{
    Tutorial,
    Practice
}

public static class GameSession
{
    public static ActivityType SelectedActivity;
    public static ModeType SelectedMode;
}