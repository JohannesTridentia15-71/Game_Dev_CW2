using UnityEngine;
using UnityEngine.SceneManagement;

public class AnimalInteractable : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Enable for townsfolk/bystanders who never fight and don't map to a combatant's StoryManager state")]
    [SerializeField] private bool isNeutralNPC = false;
    [SerializeField] private AnimalType animalType = AnimalType.Pig;
    [SerializeField] private string speakerName = "Truff";

    [Header("Visuals & Defeated Gravestone")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite normalAnimalSprite;
    [SerializeField] private Sprite gravestoneSprite;
    [SerializeField] private string gravestoneSpeakerName = "Gravestone";
    [SerializeField] private bool disableColliderWhenDefeated = false; // Keep false if you want players to inspect the gravestone

    [Header("Pre-Encounter / Neutral Dialogue by Pride Tier")]
    [Tooltip("Pride 0-1 (Humble) — also used as default dialogue for Neutral NPCs")]
    [TextArea(2, 4)]
    [SerializeField] private string[] humbleLines = new[]
    {
        "*Snort snort!* Hey there, little egg! Smell that? Fresh truffles under the oak!",
        "Care for a friendly spar in the pen?"
    };

    [Tooltip("Pride 2-3 (Proud) — optional for Neutral NPCs (falls back to Humble Lines if empty)")]
    [TextArea(2, 4)]
    [SerializeField] private string[] proudLines = new[]
    {
        "*Snort...* Word travels fast around the farm. You've been throwing your weight around.",
        "Think you're big enough to push me around too?"
    };

    [Tooltip("Pride 4-5 (Arrogant) — optional for Neutral NPCs (falls back to Humble Lines if empty)")]
    [TextArea(2, 4)]
    [SerializeField] private string[] arrogantLines = new[]
    {
        "*Backs away nervously* Look at the size of you... Everyone's talking about what you did.",
        "Stay back—I won't go down without a fight!"
    };

    [Header("Post-Encounter Dialogue (Combatants Only)")]
    [TextArea(2, 4)]
    [SerializeField] private string[] befriendedLines = new[]
    {
        "*Snort!* Good to see you again, partner! My Truffle Charm is with you."
    };

    [TextArea(2, 4)]
    [SerializeField] private string[] gravestoneLines = new[]
    {
        "Here lies Truff. Loved the oak shade and fresh truffles.",
        "A wooden marker stands where they used to graze."
    };

    [Header("Scene & UI References")]
    [SerializeField] private DialogueBox dialogueBox;
    [SerializeField] private string combatSceneName = "Combat";
    [SerializeField] private bool startBattleAfterDialogue = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool playerInRange;
    private Transform playerTransform;
    private ChickenMovement cachedPlayer;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (normalAnimalSprite == null && spriteRenderer != null)
            normalAnimalSprite = spriteRenderer.sprite;
    }

    private void OnEnable()
    {
        if (!isNeutralNPC && StoryManager.Instance != null)
            StoryManager.Instance.OnAnimalResolved += HandleAnimalResolved;
    }

    private void Start()
    {
        RefreshVisualState();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnAnimalResolved -= HandleAnimalResolved;
    }

    private void HandleAnimalResolved(AnimalType resolvedAnimal, EncounterOutcome outcome)
    {
        if (!isNeutralNPC && resolvedAnimal == animalType)
            RefreshVisualState();
    }

    public void RefreshVisualState()
    {
        if (isNeutralNPC)
        {
            if (spriteRenderer != null && normalAnimalSprite != null)
                spriteRenderer.sprite = normalAnimalSprite;
            return;
        }

        EncounterOutcome outcome = StoryManager.Instance != null
            ? StoryManager.Instance.GetAnimalState(animalType)
            : EncounterOutcome.Unvisited;

        bool isDefeated = outcome == EncounterOutcome.Defeated;

        if (spriteRenderer != null)
        {
            if (isDefeated && gravestoneSprite != null)
                spriteRenderer.sprite = gravestoneSprite;
            else if (normalAnimalSprite != null)
                spriteRenderer.sprite = normalAnimalSprite;
        }

        if (isDefeated && disableColliderWhenDefeated)
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }

    private void Update()
    {
        if (!playerInRange) return;
        if (dialogueBox != null && dialogueBox.IsOpen) return;

        if (Input.GetKeyDown(interactKey))
        {
            TriggerInteraction();
        }
    }

    private void TriggerInteraction()
    {
        if (isNeutralNPC)
        {
            string[] neutralLines = GetPrideScaledDialogue();
            if (dialogueBox != null)
                dialogueBox.StartDialogue(speakerName, neutralLines);
            return;
        }

        EncounterOutcome outcome = StoryManager.Instance != null
            ? StoryManager.Instance.GetAnimalState(animalType)
            : EncounterOutcome.Unvisited;

        bool alreadyResolved = outcome != EncounterOutcome.Unvisited;
        string[] linesToPlay = GetDialogueForCurrentState(outcome);
        string activeSpeaker = (outcome == EncounterOutcome.Defeated && !string.IsNullOrWhiteSpace(gravestoneSpeakerName))
            ? gravestoneSpeakerName
            : speakerName;

        if (dialogueBox != null)
        {
            dialogueBox.StartDialogue(activeSpeaker, linesToPlay, onComplete: () =>
            {
                if (!alreadyResolved && startBattleAfterDialogue)
                    BeginCombatEncounter();
            });
        }
        else if (!alreadyResolved && startBattleAfterDialogue)
        {
            BeginCombatEncounter();
        }
    }

    private string[] GetDialogueForCurrentState(EncounterOutcome outcome)
    {
        if (outcome == EncounterOutcome.Befriended)
            return befriendedLines;

        if (outcome == EncounterOutcome.Defeated)
            return gravestoneLines;

        // Check if a counterpart relationship rule overrides pre-encounter dialogue (e.g., Cow reacting to Sheep)
        if (StoryManager.Instance != null)
        {
            BattleRelationshipModifiers relMods = StoryManager.Instance.GetBattleModifiersFor(animalType);
            if (relMods.hasActiveRule && relMods.overworldDialogueLines != null && relMods.overworldDialogueLines.Length > 0)
            {
                return relMods.overworldDialogueLines;
            }
        }

        return GetPrideScaledDialogue();
    }

    private string[] GetPrideScaledDialogue()
    {
        PrideTier tier = StoryManager.Instance != null
            ? StoryManager.Instance.GetPrideTier()
            : PrideTier.Low;

        if (tier == PrideTier.High && arrogantLines != null && arrogantLines.Length > 0)
            return arrogantLines;

        if (tier == PrideTier.Medium && proudLines != null && proudLines.Length > 0)
            return proudLines;

        return humbleLines;
    }

    private void BeginCombatEncounter()
    {
        if (StoryManager.Instance != null && playerTransform != null)
            StoryManager.Instance.StartEncounter(animalType, playerTransform.position);

        if (Application.CanStreamedLevelBeLoaded(combatSceneName))
            SceneManager.LoadScene(combatSceneName);
        else
            Debug.Log($"Starting encounter with {animalType} (Add {combatSceneName} to Build Settings).");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            playerTransform = other.transform;
            cachedPlayer = other.GetComponent<ChickenMovement>();
            if (cachedPlayer != null)
                cachedPlayer.RegisterInteractableInRange(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            playerTransform = null;
            if (cachedPlayer != null)
            {
                cachedPlayer.RegisterInteractableInRange(false);
                cachedPlayer = null;
            }
        }
    }
}