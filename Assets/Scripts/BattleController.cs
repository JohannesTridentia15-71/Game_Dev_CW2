using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[System.Serializable]
public class AnimalBattleProfile
{
    public AnimalType animal;
    public string displayName;
    public Sprite battleSprite;
    public float maxHP = 100f;
    public float maxRP = 100f;
    public float rpGainPerSecond = 12f;
    public float peckCooldown = 2f;

    [Header("Befriend Ability Unlock Info")]
    public string unlockedAbilityName;
    public string abilityHotkeyLabel;
    [TextArea(2, 3)] public string abilityDescription;
}

public class BattleController : MonoBehaviour
{
    [Header("Scene & Object References")]
    [SerializeField] private EggMovement eggMovement;
    [SerializeField] private EggHealth eggHealth;
    [SerializeField] private ProjectileSpawner projectileSpawner;
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private Transform eggSpawnPoint;
    [SerializeField] private string overworldSceneName = "FarmOverworld";

    [Header("Enemy Stats (Tunable per Animal)")]
    [SerializeField] private float maxEnemyHP = 100f;
    [SerializeField] private float maxEnemyRP = 100f;
    [SerializeField] private float rpGainPerSecond = 12f;

    [SerializeField] private SpriteRenderer opponentSpriteRenderer;
    [SerializeField] private List<AnimalBattleProfile> animalProfiles = new List<AnimalBattleProfile>();

    [Header("Peck Settings")]
    [SerializeField] private float defaultPeckCooldown = 2f;
    [SerializeField] private float peckHPDamage = 25f;
    [SerializeField] private float peckRPPenalty = 20f;

    [Header("UI References")]
    [SerializeField] private Slider rpSlider;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Button fightButton;
    [SerializeField] private Button runButton;
    [SerializeField] private Button peckButton;
    [SerializeField] private TMP_Text peckButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text opponentNameText;

    [Header("Tutorial Popup")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button helpButton;
    [SerializeField] private TMP_Text abilitiesGuideText;
    [SerializeField] private bool hideLockedAbilitiesInGuide = true;

    [Header("Arena Outcome UI")]
    [SerializeField] private GameObject outcomeOverlay;
    [SerializeField] private TMP_Text outcomeText;
    [SerializeField] private Button returnButton;

    private float currentEnemyHP;
    private float currentEnemyRP;
    private float activePeckCooldown;
    private float peckTimer;
    private bool inCombatRound;
    private AnimalType currentOpponent;
    private AnimalBattleProfile activeProfile;
    private string opponentLabel;

    private void Start()
    {
        currentOpponent = StoryManager.Instance != null
            ? StoryManager.Instance.CurrentOpponent
            : AnimalType.Sheep;

        activePeckCooldown = defaultPeckCooldown;
        opponentLabel = currentOpponent.ToString();

        foreach (var profile in animalProfiles)
        {
            if (profile.animal == currentOpponent)
            {
                activeProfile = profile;
                maxEnemyHP = profile.maxHP;
                maxEnemyRP = profile.maxRP;
                rpGainPerSecond = profile.rpGainPerSecond;
                if (profile.peckCooldown > 0f)
                    activePeckCooldown = profile.peckCooldown;
                if (!string.IsNullOrEmpty(profile.displayName))
                    opponentLabel = profile.displayName;
                if (opponentSpriteRenderer != null && profile.battleSprite != null)
                    opponentSpriteRenderer.sprite = profile.battleSprite;
                break;
            }
        }

        if (opponentNameText != null)
            opponentNameText.text = opponentLabel;

        currentEnemyHP = maxEnemyHP;
        currentEnemyRP = 0f;
        UpdateMeters();

        fightButton.onClick.AddListener(StartFightRound);
        runButton.onClick.AddListener(OnRunClicked);
        peckButton.onClick.AddListener(OnPeckClicked);
        eggHealth.OnEggCracked += HandleEggCracked;

        if (confirmButton != null)
            confirmButton.onClick.AddListener(CloseTutorialPopup);

        if (helpButton != null)
            helpButton.onClick.AddListener(OpenTutorialPopup);

        if (returnButton != null)
            returnButton.onClick.AddListener(ReturnToOverworld);

        if (outcomeOverlay != null)
            outcomeOverlay.SetActive(false);

        if (tutorialPanel != null)
        {
            bool showAtStart = currentOpponent == AnimalType.Rooster;
            if (showAtStart)
                RefreshAbilitiesGuide();
            tutorialPanel.SetActive(showAtStart);
            Time.timeScale = showAtStart ? 0f : 1f;
        }

        EnterChoicePhase($"{opponentLabel} blocks your path! Fight or Run?");
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        if (eggHealth != null)
            eggHealth.OnEggCracked -= HandleEggCracked;
    }

    private void Update()
    {
        if (!inCombatRound) return;

        // 0. Tick active abilities & hotkey inputs (1-5)
        if (abilityManager != null)
            abilityManager.TickAbilities(inCombatRound);

        // 1. Respect Points (RP) increase while dodging (doubled during Truffle Charm)
        float rpMultiplier = abilityManager != null ? abilityManager.TruffleCharmRPMultiplier : 1f;
        currentEnemyRP = Mathf.Min(maxEnemyRP, currentEnemyRP + rpGainPerSecond * rpMultiplier * Time.deltaTime);
        UpdateMeters();

        if (currentEnemyRP >= maxEnemyRP)
        {
            FinishBattleBefriended();
            return;
        }

        // 2. Peck cooldown timer (Spacebar or UI click)
        if (peckTimer > 0f)
        {
            peckTimer -= Time.deltaTime;
            peckButton.interactable = false;
            if (peckButtonText != null)
                peckButtonText.text = $"Peck ({Mathf.CeilToInt(peckTimer)}s)";
        }
        else
        {
            peckButton.interactable = true;
            if (peckButtonText != null)
                peckButtonText.text = (abilityManager != null && abilityManager.HasGallopPeckEmpowered)
                ? $"Peck! ({abilityManager.GallopPeckDamageMultiplier:0.##}x)"
                : "Peck!";

            if (Input.GetKeyDown(KeyCode.Space))
            {
                OnPeckClicked();
            }
        }
    }

    private void EnterChoicePhase(string message)
    {
        inCombatRound = false;
        eggMovement.canMove = false;
        projectileSpawner.isSpawning = false;

        if (abilityManager != null)
            abilityManager.ResetCombatBuffs();

        // Clear any leftover projectiles in the arena
        foreach (var proj in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
        {
            Destroy(proj.gameObject);
        }

        fightButton.gameObject.SetActive(true);
        runButton.gameObject.SetActive(true);
        peckButton.interactable = false;
        if (peckButtonText != null) peckButtonText.text = "Peck";

        if (statusText != null) statusText.text = message;
    }

    private void StartFightRound()
    {
        inCombatRound = true;

        if (abilityManager != null)
            abilityManager.ResetCombatBuffs();
        
        float cooldownMult = abilityManager != null ? abilityManager.PackFrenzyCooldownMultiplier : 1f;
        peckTimer = activePeckCooldown * cooldownMult;

        if (outcomeOverlay != null)
            outcomeOverlay.SetActive(false);

        if (eggMovement != null)
        {
            eggMovement.gameObject.SetActive(true);
            if (eggSpawnPoint != null)
                eggMovement.transform.position = eggSpawnPoint.position;
            eggMovement.canMove = true;
        }

        eggHealth.ResetHP();
        projectileSpawner.isSpawning = true;

        fightButton.gameObject.SetActive(false);
        runButton.gameObject.SetActive(false);

        if (statusText != null)
            statusText.text = "Dodge to fill RP, or press Space / Peck to damage HP!";
    }

    private void OnPeckClicked()
    {
        if (!inCombatRound || peckTimer > 0f) return;

        float damageMult = abilityManager != null ? abilityManager.ConsumePeckDamageMultiplier() : 1f;
        float cooldownMult = abilityManager != null ? abilityManager.PackFrenzyCooldownMultiplier : 1f;

        // Attacking lowers HP, but also lowers Respect (RP)
        currentEnemyHP = Mathf.Max(0f, currentEnemyHP - peckHPDamage * damageMult);
        currentEnemyRP = Mathf.Max(0f, currentEnemyRP - peckRPPenalty);
        peckTimer = activePeckCooldown * cooldownMult;
        UpdateMeters();

        if (currentEnemyHP <= 0f)
        {
            FinishBattleDefeated();
        }
    }

    private void HandleEggCracked()
    {
        // Reset both enemy RP and HP on defeat
        currentEnemyRP = 0f;
        currentEnemyHP = maxEnemyHP;
        UpdateMeters();

        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.ResolvePlayerLostBattle(currentOpponent);
            if (currentOpponent != AnimalType.Rooster && StoryManager.Instance.CurrentLives <= 0)
            {
                EnterChoicePhase("Game Over! You ran out of lives.");
                fightButton.gameObject.SetActive(false);
                ShowOutcome("YOUR EGG CRACKED!\nOUT OF LIVES...", false);
                return;
            }
        }

        string lossBanner = currentOpponent == AnimalType.Rooster
            ? "YOUR EGG CRACKED!\n(NO LIVES LOST IN TUTORIAL)"
            : "YOUR EGG CRACKED!\n-1 LIFE";

        EnterChoicePhase("Fight again or Run?");
        ShowOutcome(lossBanner, false);
    }

    private void OnRunClicked()
    {
        ReturnToOverworld();
    }

    private void FinishBattleDefeated()
    {
        Time.timeScale = 1f;
        EnterChoicePhase("");
        fightButton.gameObject.SetActive(false);
        runButton.gameObject.SetActive(false);

        if (StoryManager.Instance != null)
            StoryManager.Instance.ResolveDefeatOpponent(currentOpponent);

        ShowOutcome($"YOU DEFEATED {opponentLabel.ToUpper()}!\n(+HEALTH, +PRIDE)", true);
    }

    private void FinishBattleBefriended()
    {
        Time.timeScale = 1f;
        EnterChoicePhase("");
        fightButton.gameObject.SetActive(false);
        runButton.gameObject.SetActive(false);

        bool wasAlreadyUnlocked = StoryManager.Instance != null
            && StoryManager.Instance.HasAbilityFrom(currentOpponent);

        if (StoryManager.Instance != null)
            StoryManager.Instance.ResolveBefriendOpponent(currentOpponent);
        
        if (abilityManager != null)
            abilityManager.RefreshUnlockedSlots();

        string unlockDetails = GetAbilityUnlockSummary(currentOpponent, wasAlreadyUnlocked);
        ShowOutcome($"YOU BEFRIENDED {opponentLabel.ToUpper()}!\n{unlockDetails}", true);
    }

    private string GetAbilityUnlockSummary(AnimalType animal, bool wasAlreadyUnlocked)
    {
        if (animal == AnimalType.Rooster || animal == AnimalType.Eagle)
            return "";

        if (wasAlreadyUnlocked)
            return "(ABILITY ALREADY UNLOCKED)";

        if (activeProfile != null && !string.IsNullOrWhiteSpace(activeProfile.abilityDescription))
        {
            string title = string.IsNullOrWhiteSpace(activeProfile.unlockedAbilityName)
                ? "NEW ABILITY UNLOCKED"
                : activeProfile.unlockedAbilityName.ToUpper();

            string keyTag = string.IsNullOrWhiteSpace(activeProfile.abilityHotkeyLabel)
                ? ""
                : $" [{activeProfile.abilityHotkeyLabel}]";

            return $"UNLOCKED {title}{keyTag}:\n{activeProfile.abilityDescription}";
        }

        // Fallback defaults if Inspector fields are left blank
        return animal switch
        {
            AnimalType.Sheep => "\nUNLOCKED WOOL BARRIER [1]:\n3.5s barrier that absorbs up to 2 projectiles.",
            AnimalType.Cow => "UNLOCKED FRESH MILK [2]:\nRestores +50 HP and grants 1.5s of invulnerability.",
            AnimalType.Dog => "UNLOCKED PACK FRENZY [3]:\nHalves Peck cooldown for 5 seconds.",
            AnimalType.Pig => "UNLOCKED TRUFFLE CHARM [4]:\nDoubles RP gain for 5 seconds.",
            AnimalType.Horse => "UNLOCKED GALLOP DASH [5]:\n1.5x speed for 3s + next Peck deals 1.5x damage.",
            _ => "(ABILITY UNLOCKED)"
        };
    }

    private void ShowOutcome(string message, bool showReturnButton)
    {
        if (eggMovement != null)
            eggMovement.gameObject.SetActive(false);

        if (outcomeOverlay != null)
            outcomeOverlay.SetActive(true);

        if (outcomeText != null)
            outcomeText.text = message;

        if (returnButton != null)
            returnButton.gameObject.SetActive(showReturnButton);
    }

    private void ReturnToOverworld()
    {
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded(overworldSceneName))
            SceneManager.LoadScene(overworldSceneName);
        else
            Debug.Log("Return clicked! (Add FarmOverworld to Build Settings to return)");
    }

    private void UpdateMeters()
    {
        if (rpSlider != null)
        {
            rpSlider.value = currentEnemyRP / maxEnemyRP;
            if (rpSlider.fillRect != null)
                rpSlider.fillRect.gameObject.SetActive(rpSlider.value > 0f);
        }

        if (hpSlider != null)
        {
            hpSlider.value = currentEnemyHP / maxEnemyHP;
            if (hpSlider.fillRect != null)
                hpSlider.fillRect.gameObject.SetActive(hpSlider.value > 0f);
        }
    }

    public void CloseTutorialPopup()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    public void OpenTutorialPopup()
    {
        if (tutorialPanel != null)
        {
            RefreshAbilitiesGuide();
            tutorialPanel.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    private void RefreshAbilitiesGuide()
    {
        if (abilitiesGuideText == null) return;

        AnimalType[] abilityAnimals =
        {
            AnimalType.Sheep,
            AnimalType.Cow,
            AnimalType.Dog,
            AnimalType.Pig,
            AnimalType.Horse
        };

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < abilityAnimals.Length; i++)
        {
            AnimalType animal = abilityAnimals[i];
            int keyNum = i + 1;
            bool isUnlocked = !hideLockedAbilitiesInGuide
                || (abilityManager != null && abilityManager.IsAbilityUnlocked(animal))
                || (abilityManager == null && (StoryManager.Instance == null || StoryManager.Instance.HasAbilityFrom(animal)));

            if (!isUnlocked)
            {
                sb.AppendLine($"<b>[{keyNum}] ???</b> — Befriend {animal} to unlock.");
                continue;
            }

            AnimalBattleProfile profile = animalProfiles.Find(p => p.animal == animal);
            if (profile != null && !string.IsNullOrWhiteSpace(profile.abilityDescription))
            {
                string abilityName = string.IsNullOrWhiteSpace(profile.unlockedAbilityName)
                    ? $"{animal} Ability"
                    : profile.unlockedAbilityName;
                sb.AppendLine($"<b>[{keyNum}] {abilityName}</b> — {profile.abilityDescription}");
            }
            else
            {
                sb.AppendLine(GetDefaultAbilityGuideLine(animal, keyNum));
            }
        }

        abilitiesGuideText.text = sb.ToString();
    }

    private string GetDefaultAbilityGuideLine(AnimalType animal, int keyNum)
    {
        return animal switch
        {
            AnimalType.Sheep => $"<b>[{keyNum}] Wool Barrier</b> - 3.5s shield that absorbs up to 2 projectiles.",
            AnimalType.Cow => $"\n<b>[{keyNum}] Fresh Milk</b> - Restores +50 HP and grants 1.5s of invulnerability.",
            AnimalType.Dog => $"\n<b>[{keyNum}] Pack Frenzy</b> - Halves Peck cooldown for 5 seconds.",
            AnimalType.Pig => $"\n<b>[{keyNum}] Truffle Charm</b> - Doubles RP gain for 5 seconds.",
            AnimalType.Horse => $"\n<b>[{keyNum}] Gallop Dash</b> - 1.5x move speed for 3s + next Peck deals 1.5x damage.",
            _ => ""
        };
    }
}