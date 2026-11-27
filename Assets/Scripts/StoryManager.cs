using System;
using System.Collections.Generic;
using UnityEngine;

public enum AnimalType
{
    Rooster,    // Rite of passage opponent
    Sheep,      // Molly
    Cow,        // Mootilda
    Dog,        // Indiana Bones / Doglas
    Horse,      // Al Capony
    Pig,        // Truff
    Eagle       // Wingston Churchill (Final Boss)
}

public enum EncounterOutcome
{
    Unvisited,
    Defeated,   // HP reached 0 -> +1 Pride, +Health
    Befriended  // RP filled -> +Ability unlocked
}

public enum PrideTier
{
    Heartbroken, // -1 Pride (Midgame tragedy active until Rooster training)
    Low,         // 0-1 Pride (Befriended most/all)
    Medium,      // 2-3 Pride (Fought some, befriended others)
    High         // 4-5+ Pride (Fought most/all)
}

public enum GameEnding
{
    None,
    GameOverLostFight,
    GameOverGaveUp,
    // Eagle Defeated (Win) Endings
    Win_LowPride_BarnParty,
    Win_MediumPride_HoardFood,
    Win_HighPride_ShotByGustav,
    // Eagle Befriended Endings
    Befriend_LowPride_EagleRepents,
    Befriend_MediumPride_UnstableTruce,
    Befriend_HighPride_KilledByEagle
}

public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance { get; private set; }

    [Header("Player Core Stats")]
    [SerializeField] private int startingLives = 3;
    [SerializeField] private int maxLives = 5;
    [SerializeField] private int maxPride = 5;

    public int CurrentLives { get; private set; }
    public int CurrentPride { get; private set; }

    [Header("Midgame Tragedy & Heartbreak")]
    [SerializeField] private int mainEncountersBeforeTragedy = 3;
    [SerializeField] private float heartbrokenAttackMultiplier = 0.5f;
    [SerializeField] private float heartbrokenRPMultiplier = 0.5f;

    public bool HasTriggeredMidpointTragedy { get; private set; }
    public bool IsHeartbroken { get; private set; }
    public float ActiveAttackMultiplier => IsHeartbroken ? heartbrokenAttackMultiplier : 1f;
    public float ActiveRPMultiplier => IsHeartbroken ? heartbrokenRPMultiplier : 1f;

    private int savedPrideBeforeHeartbreak;
    private HashSet<AnimalType> savedAbilitiesBeforeHeartbreak = new HashSet<AnimalType>();

    [Header("Active Battle Context")]
    [SerializeField] private AnimalType defaultOpponent = AnimalType.Sheep;
    public AnimalType CurrentOpponent { get; private set; }
    public Vector3 LastOverworldPosition { get; set; }
    public bool HasSavedOverworldPosition { get; set; }

    private Dictionary<AnimalType, EncounterOutcome> animalStates = new Dictionary<AnimalType, EncounterOutcome>();
    private HashSet<AnimalType> unlockedAbilities = new HashSet<AnimalType>();

    public event Action OnStatsChanged;
    public event Action<AnimalType, EncounterOutcome> OnAnimalResolved;
    public event Action<GameEnding> OnGameEnded;
    public string[] PendingOverworldThought { get; set; }

    [Header("Animal Social Network & Counter Scaling")]
    [SerializeField] private List<AnimalSocialNetwork> socialNetworks = new List<AnimalSocialNetwork>();

    [Tooltip("Stat step per negative sentiment point (e.g. -2 score = +24% HP, +20% speed)")]
    [SerializeField] private float hostileHpStep = 0.12f;
    [SerializeField] private float hostileRpStep = 0.10f;
    [SerializeField] private float hostileSpeedStep = 0.10f;
    [SerializeField] private float hostileIntervalStep = 0.08f;

    [Tooltip("Stat step per positive sentiment point (e.g. +2 score = -16% RP needed)")]
    [SerializeField] private float friendlyRpStep = 0.08f;
    [SerializeField] private float friendlyHpStep = 0.05f;

    [SerializeField] private Color enragedTint = new Color(1f, 0.72f, 0.72f, 1f);
    [SerializeField] private Color friendlyTint = new Color(0.85f, 1f, 0.85f, 1f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CurrentOpponent = defaultOpponent;
        ResetStoryState();
    }

    public void ResetStoryState()
    {
        CurrentLives = startingLives;
        CurrentPride = 0;
        HasSavedOverworldPosition = false;
        HasTriggeredMidpointTragedy = false;
        IsHeartbroken = false;
        savedPrideBeforeHeartbreak = 0;
        savedAbilitiesBeforeHeartbreak.Clear();
        animalStates.Clear();
        unlockedAbilities.Clear();

        foreach (AnimalType animal in Enum.GetValues(typeof(AnimalType)))
        {
            animalStates[animal] = EncounterOutcome.Unvisited;
        }

        OnStatsChanged?.Invoke();
        PendingOverworldThought = null;
    }

    // --- Overworld -> Battle Setup ---
    public void StartEncounter(AnimalType opponent, Vector3 playerPosition)
    {
        CurrentOpponent = opponent;
        LastOverworldPosition = playerPosition;
        HasSavedOverworldPosition = true;
    }

    // --- Battle -> Story Resolution ---
    public void ResolveDefeatOpponent(AnimalType animal)
    {
        if (animal == AnimalType.Eagle)
        {
            TriggerEagleEnding(wasBefriended: false);
            return;
        }

        animalStates[animal] = EncounterOutcome.Defeated;
        CurrentLives = Mathf.Clamp(CurrentLives + 1, 0, maxLives);

        if (animal != AnimalType.Rooster && !IsHeartbroken)
        {
            CurrentPride = Mathf.Clamp(CurrentPride + 1, 0, maxPride);
        }

        CheckMidpointTragedyTrigger();
        OnStatsChanged?.Invoke();
        OnAnimalResolved?.Invoke(animal, EncounterOutcome.Defeated);
    }

    public void ResolveBefriendOpponent(AnimalType animal)
    {
        if (animal == AnimalType.Eagle)
        {
            TriggerEagleEnding(wasBefriended: true);
            return;
        }

        animalStates[animal] = EncounterOutcome.Befriended;
        if (!IsHeartbroken)
        {
            unlockedAbilities.Add(animal);
        }
        Debug.Log($"[StoryManager] Befriended {animal}. Total unlocked: {unlockedAbilities.Count}");

        CheckMidpointTragedyTrigger();
        OnStatsChanged?.Invoke();
        OnAnimalResolved?.Invoke(animal, EncounterOutcome.Befriended);
    }

    private void CheckMidpointTragedyTrigger()
    {
        if (HasTriggeredMidpointTragedy) return;

        int resolvedMainAnimals = 0;
        foreach (var kvp in animalStates)
        {
            if (kvp.Key == AnimalType.Rooster || kvp.Key == AnimalType.Eagle) continue;
            if (kvp.Value != EncounterOutcome.Unvisited)
                resolvedMainAnimals++;
        }

        if (resolvedMainAnimals >= mainEncountersBeforeTragedy)
        {
            TriggerMidpointTragedy();
        }
    }

    public void TriggerMidpointTragedy()
    {
        if (HasTriggeredMidpointTragedy) return;

        HasTriggeredMidpointTragedy = true;
        IsHeartbroken = true;

        savedPrideBeforeHeartbreak = Mathf.Max(0, CurrentPride);
        savedAbilitiesBeforeHeartbreak = new HashSet<AnimalType>(unlockedAbilities);

        CurrentPride = -1;
        unlockedAbilities.Clear();
    }

    public void CompleteRoosterTraining()
    {
        if (!IsHeartbroken) return;

        IsHeartbroken = false;
        CurrentPride = savedPrideBeforeHeartbreak;
        unlockedAbilities = new HashSet<AnimalType>(savedAbilitiesBeforeHeartbreak);

        OnStatsChanged?.Invoke();
    }

    public void ResolvePlayerLostBattle(AnimalType animal)
    {
        if (animal == AnimalType.Rooster)
        {
            animalStates[animal] = EncounterOutcome.Befriended;
            OnAnimalResolved?.Invoke(animal, EncounterOutcome.Befriended);
            return;
        }

        CurrentLives = Mathf.Max(0, CurrentLives - 1);
        OnStatsChanged?.Invoke();

        if (CurrentLives <= 0)
        {
            OnGameEnded?.Invoke(GameEnding.GameOverLostFight);
        }
    }

    // --- Query Helpers ---
    public EncounterOutcome GetAnimalState(AnimalType animal)
    {
        return animalStates.TryGetValue(animal, out var state) ? state : EncounterOutcome.Unvisited;
    }

    public bool HasAbilityFrom(AnimalType animal)
    {
        return unlockedAbilities.Contains(animal);
    }

    public IReadOnlyCollection<AnimalType> GetUnlockedAbilities()
    {
        return unlockedAbilities;
    }

    public PrideTier GetPrideTier()
    {
        if (IsHeartbroken || CurrentPride < 0) return PrideTier.Heartbroken;
        if (CurrentPride <= 1) return PrideTier.Low;
        if (CurrentPride <= 3) return PrideTier.Medium;
        return PrideTier.High;
    }

    private void TriggerEagleEnding(bool wasBefriended)
    {
        PrideTier tier = GetPrideTier();
        GameEnding ending;

        if (!wasBefriended)
        {
            ending = tier switch
            {
                PrideTier.Low => GameEnding.Win_LowPride_BarnParty,
                PrideTier.Medium => GameEnding.Win_MediumPride_HoardFood,
                _ => GameEnding.Win_HighPride_ShotByGustav
            };
        }
        else
        {
            ending = tier switch
            {
                PrideTier.Low => GameEnding.Befriend_LowPride_EagleRepents,
                PrideTier.Medium => GameEnding.Befriend_MediumPride_UnstableTruce,
                _ => GameEnding.Befriend_HighPride_KilledByEagle
            };
        }

        OnGameEnded?.Invoke(ending);
    }

    // --- Social Network Sentiment & Battle Scaling ---
    public int GetSentimentScore(AnimalType targetAnimal)
    {
        AnimalSocialNetwork net = socialNetworks.Find(n => n.animal == targetAnimal);
        if (net == null) return 0;

        int score = 0;

        for (int i = 0; i < net.friends.Count; i++)
        {
            EncounterOutcome state = GetAnimalState(net.friends[i].animal);
            if (state == EncounterOutcome.Befriended) score += 1;
            else if (state == EncounterOutcome.Defeated) score -= 1;
        }

        for (int i = 0; i < net.enemies.Count; i++)
        {
            EncounterOutcome state = GetAnimalState(net.enemies[i].animal);
            if (state == EncounterOutcome.Defeated) score += 1;
            else if (state == EncounterOutcome.Befriended) score -= 1;
        }

        return score;
    }

    public BattleRelationshipModifiers GetBattleModifiersFor(AnimalType targetAnimal)
    {
        BattleRelationshipModifiers mods = BattleRelationshipModifiers.Default;
        AnimalSocialNetwork net = socialNetworks.Find(n => n.animal == targetAnimal);
        if (net == null) return mods;

        int score = 0;
        var gatheredDialogue = new List<string>();
        var angerReasons = new List<string>();
        var happyReasons = new List<string>();

        for (int i = 0; i < net.friends.Count; i++)
        {
            AnimalRelationLink link = net.friends[i];
            EncounterOutcome state = GetAnimalState(link.animal);
            if (state == EncounterOutcome.Unvisited) continue;

            string otherName = string.IsNullOrWhiteSpace(link.displayName)
                ? link.animal.ToString()
                : link.displayName;

            if (state == EncounterOutcome.Defeated)
            {
                score -= 1;
                angerReasons.Add(otherName);
                AddReactionLines(gatheredDialogue, link.onDefeatedLines,
                    $"You hurt {otherName}?! They were my friend—I won't go easy on you!");
            }
            else if (state == EncounterOutcome.Befriended)
            {
                score += 1;
                happyReasons.Add(otherName);
                AddReactionLines(gatheredDialogue, link.onBefriendedLines,
                    $"{otherName} told me how kind you were! Any friend of theirs is a friend of mine.");
            }
        }

        for (int i = 0; i < net.enemies.Count; i++)
        {
            AnimalRelationLink link = net.enemies[i];
            EncounterOutcome state = GetAnimalState(link.animal);
            if (state == EncounterOutcome.Unvisited) continue;

            string otherName = string.IsNullOrWhiteSpace(link.displayName)
                ? link.animal.ToString()
                : link.displayName;

            if (state == EncounterOutcome.Defeated)
            {
                score += 1;
                happyReasons.Add(otherName);
                AddReactionLines(gatheredDialogue, link.onDefeatedLines,
                    $"Ha! You finally put {otherName} in their place! You've got my respect.");
            }
            else if (state == EncounterOutcome.Befriended)
            {
                score -= 1;
                angerReasons.Add(otherName);
                AddReactionLines(gatheredDialogue, link.onBefriendedLines,
                    $"So you're buddy-buddy with {otherName} now?! That makes you my problem too!");
            }
        }

        mods.sentimentScore = score;

        if (gatheredDialogue.Count > 0)
        {
            mods.hasActiveRule = true;
            mods.overworldDialogueLines = gatheredDialogue.ToArray();
        }

        if (score < 0)
        {
            int anger = Mathf.Abs(score);
            mods.hasActiveRule = true;
            mods.isEnraged = true;
            mods.hpMultiplier = 1f + (anger * hostileHpStep);
            mods.rpMultiplier = 1f + (anger * hostileRpStep);
            mods.bulletSpeedMultiplier = 1f + (anger * hostileSpeedStep);
            mods.burstIntervalMultiplier = Mathf.Max(0.60f, 1f - (anger * hostileIntervalStep));
            mods.spriteTint = enragedTint.a > 0.01f ? enragedTint : new Color(1f, 0.72f, 0.72f, 1f);

            string reasonList = angerReasons.Count > 0 ? string.Join(" and ", angerReasons) : "what happened on the farm";
            mods.customBattlePrompt = $"{targetAnimal} is furious about {reasonList}! Fight or Run?";
        }
        else if (score > 0)
        {
            mods.hasActiveRule = true;
            mods.isEnraged = false;
            mods.hpMultiplier = Mathf.Max(0.75f, 1f - (score * friendlyHpStep));
            mods.rpMultiplier = Mathf.Max(0.65f, 1f - (score * friendlyRpStep));
            mods.bulletSpeedMultiplier = 1f;
            mods.burstIntervalMultiplier = 1f;
            mods.spriteTint = friendlyTint.a > 0.01f ? friendlyTint : Color.white;

            string reasonList = happyReasons.Count > 0 ? string.Join(" and ", happyReasons) : "your reputation";
            mods.customBattlePrompt = $"{targetAnimal} respects you because of {reasonList}! Fight or Run?";
        }

        return mods;
    }

    private static void AddReactionLines(List<string> target, string[] customLines, string fallbackLine)
    {
        if (customLines != null && customLines.Length > 0)
        {
            for (int i = 0; i < customLines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(customLines[i]))
                    target.Add(customLines[i]);
            }
        }
        else
        {
            target.Add(fallbackLine);
        }
    }

    public void TriggerGaveUpEnding()
    {
        CurrentLives = 0;
        OnStatsChanged?.Invoke();
        OnGameEnded?.Invoke(GameEnding.GameOverGaveUp);
    }
}