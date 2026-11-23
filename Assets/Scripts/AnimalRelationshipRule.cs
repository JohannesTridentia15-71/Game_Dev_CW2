using UnityEngine;

[System.Serializable]
public class AnimalRelationshipRule
{
    [Tooltip("The animal currently being interacted with or fought")]
    public AnimalType targetAnimal;

    [Tooltip("The counterpart animal whose outcome triggers this reaction")]
    public AnimalType relatedAnimal;

    [Tooltip("Trigger when relatedAnimal was Defeated or Befriended")]
    public EncounterOutcome triggerOutcome = EncounterOutcome.Defeated;

    [Tooltip("If true, appends (Enraged) to the opponent's name in battle")]
    public bool isEnraged = true;

    [Header("Overworld Pre-Battle Dialogue")]
    [Tooltip("Replaces the animal's normal pre-battle dialogue if non-empty")]
    [TextArea(2, 4)] public string[] overworldDialogueLines;

    [Header("Battle Modifiers")]
    public float hpMultiplier = 1.25f;
    public float rpMultiplier = 1.20f;
    public float bulletSpeedMultiplier = 1.20f;
    public float burstIntervalMultiplier = 0.80f;

    [Header("Battle Presentation")]
    [TextArea] public string customBattlePrompt = "Mootilda is furious about Molly! Fight or Run?";
    public Color spriteTint = new Color(1f, 0.72f, 0.72f, 1f);
}

public struct BattleRelationshipModifiers
{
    public bool hasActiveRule;
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