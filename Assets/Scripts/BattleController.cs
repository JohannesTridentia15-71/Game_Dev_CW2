using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class BattleController : MonoBehaviour
{
    [Header("Scene & Object References")]
    [SerializeField] private EggMovement eggMovement;
    [SerializeField] private EggHealth eggHealth;
    [SerializeField] private ProjectileSpawner projectileSpawner;
    [SerializeField] private Transform eggSpawnPoint;
    [SerializeField] private string overworldSceneName = "FarmOverworld";

    [Header("Enemy Stats (Tunable per Animal)")]
    [SerializeField] private float maxEnemyHP = 100f;
    [SerializeField] private float maxEnemyRP = 100f;
    [SerializeField] private float rpGainPerSecond = 12f;

    [Header("Peck Settings")]
    [SerializeField] private float peckCooldown = 2f;
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

    private float currentEnemyHP;
    private float currentEnemyRP;
    private float peckTimer;
    private bool inCombatRound;
    private AnimalType currentOpponent;

    private void Start()
    {
        currentOpponent = StoryManager.Instance != null
            ? StoryManager.Instance.CurrentOpponent
            : AnimalType.Sheep;

        currentEnemyHP = maxEnemyHP;
        currentEnemyRP = 0f;
        UpdateMeters();

        fightButton.onClick.AddListener(StartFightRound);
        runButton.onClick.AddListener(OnRunClicked);
        peckButton.onClick.AddListener(OnPeckClicked);
        eggHealth.OnEggCracked += HandleEggCracked;

        EnterChoicePhase($"{currentOpponent} blocks your path! Fight or Run?");
    }

    private void OnDestroy()
    {
        if (eggHealth != null)
            eggHealth.OnEggCracked -= HandleEggCracked;
    }

    private void Update()
    {
        if (!inCombatRound) return;

        // 1. Respect Points (RP) increase while dodging
        currentEnemyRP = Mathf.Min(maxEnemyRP, currentEnemyRP + rpGainPerSecond * Time.deltaTime);
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
                peckButtonText.text = "Peck!";

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
        peckTimer = peckCooldown;

        if (eggSpawnPoint != null)
            eggMovement.transform.position = eggSpawnPoint.position;

        eggHealth.ResetHP();
        eggMovement.canMove = true;
        projectileSpawner.isSpawning = true;

        fightButton.gameObject.SetActive(false);
        runButton.gameObject.SetActive(false);

        if (statusText != null)
            statusText.text = "Dodge to fill RP, or press Space / Peck to damage HP!";
    }

    private void OnPeckClicked()
    {
        if (!inCombatRound || peckTimer > 0f) return;

        // Attacking lowers HP, but also lowers Respect (RP)
        currentEnemyHP = Mathf.Max(0f, currentEnemyHP - peckHPDamage);
        currentEnemyRP = Mathf.Max(0f, currentEnemyRP - peckRPPenalty);
        peckTimer = peckCooldown;
        UpdateMeters();

        if (currentEnemyHP <= 0f)
        {
            FinishBattleDefeated();
        }
    }

    private void HandleEggCracked()
    {
        // Per design: losing round HP resets RP and costs 1 life
        currentEnemyRP = 0f;
        UpdateMeters();

        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.ResolvePlayerLostBattle(currentOpponent);
            if (StoryManager.Instance.CurrentLives <= 0)
            {
                EnterChoicePhase("Game Over! You ran out of lives.");
                fightButton.gameObject.SetActive(false);
                return;
            }
        }

        EnterChoicePhase("Your egg cracked! RP reset & -1 life. Fight again or Run?");
    }

    private void OnRunClicked()
    {
        if (Application.CanStreamedLevelBeLoaded(overworldSceneName))
            SceneManager.LoadScene(overworldSceneName);
        else
            Debug.Log("Run clicked! (Add FarmOverworld to Build Settings to return)");
    }

    private void FinishBattleDefeated()
    {
        EnterChoicePhase($"You defeated {currentOpponent}! (+Health, +Pride)");

        if (StoryManager.Instance != null)
            StoryManager.Instance.ResolveDefeatOpponent(currentOpponent);

        if (Application.CanStreamedLevelBeLoaded(overworldSceneName))
            SceneManager.LoadScene(overworldSceneName);
    }

    private void FinishBattleBefriended()
    {
        EnterChoicePhase($"You befriended {currentOpponent}! (Ability Unlocked)");

        if (StoryManager.Instance != null)
            StoryManager.Instance.ResolveBefriendOpponent(currentOpponent);

        if (Application.CanStreamedLevelBeLoaded(overworldSceneName))
            SceneManager.LoadScene(overworldSceneName);
    }

    private void UpdateMeters()
    {
        if (rpSlider != null) rpSlider.value = currentEnemyRP / maxEnemyRP;
        if (hpSlider != null) hpSlider.value = currentEnemyHP / maxEnemyHP;
    }
}