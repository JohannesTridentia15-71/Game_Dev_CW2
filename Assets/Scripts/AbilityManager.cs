using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[System.Serializable]
public class AbilitySlotUI
{
    public AnimalType sourceAnimal;
    public KeyCode hotkey;
    public float cooldownDuration = 10f;
    public GameObject slotRoot;
    public GameObject activeHighlight;
    public Image cooldownOverlay;
    public TMP_Text cooldownText;

    [HideInInspector] public bool isUnlocked;
    [HideInInspector] public float cooldownTimer;
}

public class AbilityManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleController battleController;
    [SerializeField] private EggMovement eggMovement;
    [SerializeField] private EggHealth eggHealth;

    [Header("Testing")]
    [SerializeField] private bool unlockAllForTesting = true;

    [Header("Visuals")]
    [SerializeField] private GameObject woolBarrierVisual; // Ring sprite child on Egg

    [Header("Wool Barrier (Sheep)")]
    [SerializeField] private float woolBarrierDuration = 3.5f;
    [SerializeField] private int woolBarrierMaxCharges = 2;

    [Header("Fresh Milk (Cow)")]
    [SerializeField] private float freshMilkHealAmount = 50f;
    [SerializeField] private float freshMilkInvulnDuration = 1.5f;

    [Header("Pack Frenzy (Dog)")]
    [SerializeField] private float packFrenzyDuration = 5f;
    [SerializeField, Range(0.1f, 1f)] private float packFrenzyCooldownMultiplier = 0.5f;

    [Header("Truffle Charm (Pig)")]
    [SerializeField] private float truffleCharmDuration = 5f;
    [SerializeField] private float truffleCharmRPMultiplier = 2f;

    [Header("Gallop Dash (Horse)")]
    [SerializeField] private float gallopSpeedDuration = 3f;
    [SerializeField] private float gallopSpeedMultiplier = 1.5f;
    [SerializeField] private float gallopPeckDamageMultiplier = 1.5f;

    [Header("Ability Slots (Top to Bottom: 1-5)")]
    [SerializeField] private List<AbilitySlotUI> abilitySlots = new List<AbilitySlotUI>();

    // Active Buff States
    private float woolBarrierTimer;
    private int woolBarrierCharges;
    private float freshMilkIndicatorTimer;
    private float packFrenzyTimer;
    private float truffleCharmTimer;
    private float gallopSpeedTimer;
    private bool gallopPeckEmpowered;

    public bool IsPackFrenzyActive => packFrenzyTimer > 0f;
    public bool IsTruffleCharmActive => truffleCharmTimer > 0f;
    public bool HasGallopPeckEmpowered => gallopPeckEmpowered;

    public float PackFrenzyCooldownMultiplier => IsPackFrenzyActive ? packFrenzyCooldownMultiplier : 1f;
    public float TruffleCharmRPMultiplier => IsTruffleCharmActive ? truffleCharmRPMultiplier : 1f;
    public float GallopPeckDamageMultiplier => gallopPeckEmpowered ? gallopPeckDamageMultiplier : 1f;

    private void Start()
    {
        RefreshUnlockedSlots();
    }

    public void RefreshUnlockedSlots()
    {
        foreach (var slot in abilitySlots)
        {
            slot.isUnlocked = unlockAllForTesting
                || StoryManager.Instance == null
                || StoryManager.Instance.HasAbilityFrom(slot.sourceAnimal);

            if (slot.slotRoot != null)
                slot.slotRoot.SetActive(slot.isUnlocked);

            slot.cooldownTimer = 0f;
            UpdateSlotVisual(slot);
        }
    }

    public void ResetCombatBuffs()
    {
        woolBarrierTimer = 0f;
        woolBarrierCharges = 0;
        freshMilkIndicatorTimer = 0f;
        packFrenzyTimer = 0f;
        truffleCharmTimer = 0f;
        gallopSpeedTimer = 0f;
        gallopPeckEmpowered = false;

        if (woolBarrierVisual != null)
            woolBarrierVisual.SetActive(false);

        if (eggMovement != null)
            eggMovement.SetSpeedMultiplier(1f);

        foreach (var slot in abilitySlots)
            UpdateSlotVisual(slot);
    }

    public void TickAbilities(bool inCombatRound)
    {
        if (!inCombatRound) return;

        // 1. Tick active buff timers
        if (woolBarrierTimer > 0f)
        {
            woolBarrierTimer -= Time.deltaTime;
            if (woolBarrierTimer <= 0f || woolBarrierCharges <= 0)
            {
                woolBarrierTimer = 0f;
                woolBarrierCharges = 0;
                if (woolBarrierVisual != null)
                    woolBarrierVisual.SetActive(false);
            }
        }

        if (freshMilkIndicatorTimer > 0f)
            freshMilkIndicatorTimer = Mathf.Max(0f, freshMilkIndicatorTimer - Time.deltaTime);

        if (packFrenzyTimer > 0f)
            packFrenzyTimer = Mathf.Max(0f, packFrenzyTimer - Time.deltaTime);

        if (truffleCharmTimer > 0f)
            truffleCharmTimer = Mathf.Max(0f, truffleCharmTimer - Time.deltaTime);

        if (gallopSpeedTimer > 0f)
        {
            gallopSpeedTimer -= Time.deltaTime;
            if (gallopSpeedTimer <= 0f && eggMovement != null)
                eggMovement.SetSpeedMultiplier(1f);
        }

        // 2. Tick slot cooldowns & check hotkeys
        foreach (var slot in abilitySlots)
        {
            if (!slot.isUnlocked) continue;

            if (slot.cooldownTimer > 0f)
            {
                slot.cooldownTimer = Mathf.Max(0f, slot.cooldownTimer - Time.deltaTime);
            }
            else if (Input.GetKeyDown(slot.hotkey))
            {
                ActivateAbility(slot);
            }

            UpdateSlotVisual(slot);
        }
    }

    private void ActivateAbility(AbilitySlotUI slot)
    {
        slot.cooldownTimer = slot.cooldownDuration;

        switch (slot.sourceAnimal)
        {
            case AnimalType.Sheep:
                woolBarrierTimer = woolBarrierDuration;
                woolBarrierCharges = woolBarrierMaxCharges;
                if (woolBarrierVisual != null)
                    woolBarrierVisual.SetActive(true);
                break;

            case AnimalType.Cow:
                freshMilkIndicatorTimer = freshMilkInvulnDuration;
                if (eggHealth != null)
                {
                    eggHealth.Heal(freshMilkHealAmount);
                    eggHealth.GrantInvulnerability(freshMilkInvulnDuration);
                }
                break;

            case AnimalType.Dog:
                packFrenzyTimer = packFrenzyDuration;
                break;

            case AnimalType.Pig:
                truffleCharmTimer = truffleCharmDuration;
                break;

            case AnimalType.Horse:
                gallopSpeedTimer = gallopSpeedDuration;
                gallopPeckEmpowered = true;
                if (eggMovement != null)
                    eggMovement.SetSpeedMultiplier(gallopSpeedMultiplier);
                break;
        }

        UpdateSlotVisual(slot);
    }

    public bool TryAbsorbProjectile()
    {
        if (woolBarrierTimer > 0f && woolBarrierCharges > 0)
        {
            woolBarrierCharges--;
            Debug.Log($"[WoolBarrier] Absorbed! Remaining charges: {woolBarrierCharges}, time left: {woolBarrierTimer:F2}s");
            if (woolBarrierCharges <= 0)
            {
                woolBarrierTimer = 0f;
                if (woolBarrierVisual != null)
                    woolBarrierVisual.SetActive(false);
            }
            return true;
        }
        return false;
    }

    public float ConsumePeckDamageMultiplier()
    {
        if (gallopPeckEmpowered)
        {
            gallopPeckEmpowered = false;
            return gallopPeckDamageMultiplier;
        }
        return 1f;
    }

    private bool IsAbilityCurrentlyActive(AnimalType animal)
    {
        return animal switch
        {
            AnimalType.Sheep => woolBarrierTimer > 0f && woolBarrierCharges > 0,
            AnimalType.Cow => freshMilkIndicatorTimer > 0f,
            AnimalType.Dog => packFrenzyTimer > 0f,
            AnimalType.Pig => truffleCharmTimer > 0f,
            AnimalType.Horse => gallopSpeedTimer > 0f || gallopPeckEmpowered,
            _ => false
        };
    }

    private void UpdateSlotVisual(AbilitySlotUI slot)
    {
        bool isActive = IsAbilityCurrentlyActive(slot.sourceAnimal);
        bool onCooldown = slot.cooldownTimer > 0f;

        if (slot.activeHighlight != null)
            slot.activeHighlight.SetActive(isActive);

        if (slot.cooldownOverlay != null)
        {
            slot.cooldownOverlay.gameObject.SetActive(onCooldown);
            if (onCooldown && slot.cooldownDuration > 0f)
                slot.cooldownOverlay.fillAmount = slot.cooldownTimer / slot.cooldownDuration;
        }

        if (slot.cooldownText != null)
        {
            slot.cooldownText.gameObject.SetActive(onCooldown);
            if (onCooldown)
                slot.cooldownText.text = Mathf.CeilToInt(slot.cooldownTimer).ToString();
        }
    }

    public bool IsAbilityUnlocked(AnimalType animal)
    {
        if (unlockAllForTesting) return true;

        var slot = abilitySlots.Find(s => s.sourceAnimal == animal);
        if (slot != null) return slot.isUnlocked;

        return StoryManager.Instance == null || StoryManager.Instance.HasAbilityFrom(animal);
    }
}