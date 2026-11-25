using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AnimalRelationLink
{
    public AnimalType animal;

    [Tooltip("Name shown in auto-generated lines if custom lines are left blank (e.g., Mootilda)")]
    public string displayName;

    [Header("Custom Overworld Lines (Optional — auto-generates if empty)")]
    [TextArea(2, 3)] public string[] onDefeatedLines;
    [TextArea(2, 3)] public string[] onBefriendedLines;
}

[System.Serializable]
public class AnimalSocialNetwork
{
    public AnimalType animal;

    [Tooltip("Animals this character likes (Defeating them lowers score; Befriending them raises score)")]
    public List<AnimalRelationLink> friends = new List<AnimalRelationLink>();

    [Tooltip("Animals this character dislikes (Defeating them raises score; Befriending them lowers score)")]
    public List<AnimalRelationLink> enemies = new List<AnimalRelationLink>();
}

public struct BattleRelationshipModifiers
{
    public bool hasActiveRule;
    public int sentimentScore; // < 0 = Hostile/Enraged, 0 = Neutral, > 0 = Friendly
    public bool isEnraged;
    public float hpMultiplier;
    public float rpMultiplier;
    public float bulletSpeedMultiplier;
    public float burstIntervalMultiplier;
    public string customBattlePrompt;
    public Color spriteTint;
    public string[] overworldDialogueLines;

    public static BattleRelationshipModifiers Default => new BattleRelationshipModifiers
    {
        hasActiveRule = false,
        sentimentScore = 0,
        isEnraged = false,
        hpMultiplier = 1f,
        rpMultiplier = 1f,
        bulletSpeedMultiplier = 1f,
        burstIntervalMultiplier = 1f,
        customBattlePrompt = string.Empty,
        spriteTint = Color.white,
        overworldDialogueLines = null
    };
}