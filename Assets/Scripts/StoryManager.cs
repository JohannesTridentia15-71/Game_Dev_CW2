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
    Low,    // 0-1 Pride (Befriended most/all)
    Medium, // 2-3 Pride (Fought some, befriended others)
    High    // 4-5+ Pride (Fought most/all)
}

public enum GameEnding
{
    None,
    GameOverLostFight,
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

    [Header("Active Battle Context")]
    public AnimalType CurrentOpponent { get; private set; }
    public Vector3 LastOverworldPosition { get; set; }
    public bool HasSavedOverworldPosition { get; set; }

    private Dictionary<AnimalType, EncounterOutcome> animalStates = new Dictionary<AnimalType, EncounterOutcome>();
    private HashSet<AnimalType> unlockedAbilities = new HashSet<AnimalType>();

    public event Action OnStatsChanged;
    public event Action<AnimalType, EncounterOutcome> OnAnimalResolved;
    public event Action<GameEnding> OnGameEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResetStoryState();
    }

    public void ResetStoryState()
    {
        CurrentLives = startingLives;
        CurrentPride = 0;
        HasSavedOverworldPosition = false;
        animalStates.Clear();
        unlockedAbilities.Clear();

        foreach (AnimalType animal in Enum.GetValues(typeof(AnimalType)))
        {
            animalStates[animal] = EncounterOutcome.Unvisited;
        }

        OnStatsChanged?.Invoke();
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

        if (animal != AnimalType.Rooster)
        {
            CurrentPride = Mathf.Clamp(CurrentPride + 1, 0, maxPride);
        }

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
        unlockedAbilities.Add(animal);

        OnStatsChanged?.Invoke();
        OnAnimalResolved?.Invoke(animal, EncounterOutcome.Befriended);
    }

    public void ResolvePlayerLostBattle(AnimalType animal)
    {
        if (animal == AnimalType.Eagle)
        {
            CurrentLives = 0;
            OnStatsChanged?.Invoke();
            OnGameEnded?.Invoke(GameEnding.GameOverLostFight);
            return;
        }

        // First tutorial fight: treat loss as getting kicked out into Madam Henriette's route
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
}