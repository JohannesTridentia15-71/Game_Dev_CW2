using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class DialogueLine
{
    public string speakerName;
    [TextArea(2, 4)] public string text;
}

public class DialogueBox : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject boxRoot;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private GameObject continueIndicator;

    [Header("Optional Choice Buttons")]
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Button primaryChoiceButton;
    [SerializeField] private TMP_Text primaryChoiceText;
    [SerializeField] private Button secondaryChoiceButton;
    [SerializeField] private TMP_Text secondaryChoiceText;

    [Header("Typewriter Settings")]
    [SerializeField] private float charactersPerSecond = 45f;
    [SerializeField] private KeyCode advanceKey = KeyCode.E;

    private readonly Queue<DialogueLine> lineQueue = new Queue<DialogueLine>();
    private Coroutine typingCoroutine;
    private string currentFullLine;
    private bool isTyping;
    private bool openedThisFrame;
    private bool waitingForChoice;
    private Action onDialogueFinished;

    // Pending two-button prompt shown after the last line finishes typing
    private bool hasPendingChoice;
    private string pendingPrimaryLabel;
    private Action pendingPrimaryCallback;
    private string pendingSecondaryLabel;
    private Action pendingSecondaryCallback;

    public bool IsOpen => boxRoot != null && boxRoot.activeSelf;

    private void Awake()
    {
        if (boxRoot == null && transform.childCount > 0)
            boxRoot = transform.GetChild(0).gameObject;

        if (choicePanel != null)
            choicePanel.SetActive(false);

        if (boxRoot != null)
            boxRoot.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen || openedThisFrame) return;

        bool advancePressed = Input.GetKeyDown(advanceKey)
            || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetMouseButtonDown(0);

        if (advancePressed)
        {
            if (isTyping)
                CompleteCurrentLineImmediately();
            else if (!waitingForChoice)
                DisplayNextLine();
        }
    }

    private void LateUpdate()
    {
        openedThisFrame = false;
    }

    public void StartDialogue(IEnumerable<DialogueLine> lines, Action onComplete = null)
    {
        ClearChoiceState();
        lineQueue.Clear();
        foreach (var line in lines)
            lineQueue.Enqueue(line);

        onDialogueFinished = onComplete;
        openedThisFrame = true;

        if (boxRoot != null)
            boxRoot.SetActive(true);

        DisplayNextLine();
    }

    public void StartDialogue(string speaker, string[] lines, Action onComplete = null)
    {
        var formatted = new List<DialogueLine>();
        foreach (string text in lines)
            formatted.Add(new DialogueLine { speakerName = speaker, text = text });

        StartDialogue(formatted, onComplete);
    }

    /// <summary>
    /// Plays lines and presents two choice buttons once the final line finishes typing.
    /// </summary>
    public void StartDialogueWithChoices(
        string speaker,
        string[] lines,
        string primaryLabel,
        Action onPrimarySelected,
        string secondaryLabel,
        Action onSecondarySelected)
    {
        ClearChoiceState();
        lineQueue.Clear();
        foreach (string text in lines)
            lineQueue.Enqueue(new DialogueLine { speakerName = speaker, text = text });

        hasPendingChoice = true;
        pendingPrimaryLabel = primaryLabel;
        pendingPrimaryCallback = onPrimarySelected;
        pendingSecondaryLabel = secondaryLabel;
        pendingSecondaryCallback = onSecondarySelected;
        openedThisFrame = true;

        if (boxRoot != null)
            boxRoot.SetActive(true);

        DisplayNextLine();
    }

    private void DisplayNextLine()
    {
        if (lineQueue.Count == 0)
        {
            if (hasPendingChoice)
            {
                ShowChoiceButtons();
                return;
            }

            CloseDialogue();
            return;
        }

        DialogueLine next = lineQueue.Dequeue();
        currentFullLine = next.text;

        if (speakerText != null)
        {
            bool hasSpeaker = !string.IsNullOrWhiteSpace(next.speakerName);
            speakerText.gameObject.SetActive(hasSpeaker);
            speakerText.text = next.speakerName;
        }

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeLineRoutine(currentFullLine));
    }

    private IEnumerator TypeLineRoutine(string line)
    {
        isTyping = true;
        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        bodyText.text = line;
        bodyText.ForceMeshUpdate();
        bodyText.maxVisibleCharacters = 0;

        float delay = charactersPerSecond > 0f ? 1f / charactersPerSecond : 0f;
        int totalChars = bodyText.textInfo.characterCount;

        for (int i = 1; i <= totalChars; i++)
        {
            bodyText.maxVisibleCharacters = i;
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
        }

        OnLineFinishedTyping();
    }

    private void CompleteCurrentLineImmediately()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        bodyText.text = currentFullLine;
        bodyText.maxVisibleCharacters = int.MaxValue;
        OnLineFinishedTyping();
    }

    private void OnLineFinishedTyping()
    {
        isTyping = false;

        if (lineQueue.Count == 0 && hasPendingChoice)
        {
            ShowChoiceButtons();
        }
        else if (continueIndicator != null)
        {
            continueIndicator.SetActive(true);
        }
    }

    private void ShowChoiceButtons()
    {
        waitingForChoice = true;
        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        if (choicePanel != null)
            choicePanel.SetActive(true);

        if (primaryChoiceText != null)
            primaryChoiceText.text = pendingPrimaryLabel;
        if (secondaryChoiceText != null)
            secondaryChoiceText.text = pendingSecondaryLabel;

        if (primaryChoiceButton != null)
        {
            primaryChoiceButton.onClick.RemoveAllListeners();
            primaryChoiceButton.onClick.AddListener(() => SelectChoice(pendingPrimaryCallback));
        }

        if (secondaryChoiceButton != null)
        {
            secondaryChoiceButton.onClick.RemoveAllListeners();
            secondaryChoiceButton.onClick.AddListener(() => SelectChoice(pendingSecondaryCallback));
        }
    }

    private void SelectChoice(Action chosenCallback)
    {
        ClearChoiceState();
        CloseDialogue();
        chosenCallback?.Invoke();
    }

    private void ClearChoiceState()
    {
        waitingForChoice = false;
        hasPendingChoice = false;
        pendingPrimaryCallback = null;
        pendingSecondaryCallback = null;

        if (choicePanel != null)
            choicePanel.SetActive(false);
    }

    public void CloseDialogue()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        isTyping = false;
        lineQueue.Clear();
        ClearChoiceState();

        if (boxRoot != null)
            boxRoot.SetActive(false);

        Action callback = onDialogueFinished;
        onDialogueFinished = null;
        callback?.Invoke();
    }
}