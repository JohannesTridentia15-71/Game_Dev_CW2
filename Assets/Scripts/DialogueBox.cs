using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    [SerializeField] private GameObject continueIndicator; // Optional blinking arrow/prompt

    [Header("Typewriter Settings")]
    [SerializeField] private float charactersPerSecond = 45f;
    [SerializeField] private KeyCode advanceKey = KeyCode.E;

    private readonly Queue<DialogueLine> lineQueue = new Queue<DialogueLine>();
    private Coroutine typingCoroutine;
    private string currentFullLine;
    private bool isTyping;
    private bool openedThisFrame;
    private Action onDialogueFinished;

    public bool IsOpen => boxRoot != null && boxRoot.activeSelf;

    private void Awake()
    {
        if (boxRoot == null && transform.childCount > 0)
            boxRoot = transform.GetChild(0).gameObject;

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
            else
                DisplayNextLine();
        }
    }

    private void LateUpdate()
    {
        openedThisFrame = false;
    }

    /// <summary>
    /// Starts a multi-line conversation with speaker names.
    /// </summary>
    public void StartDialogue(IEnumerable<DialogueLine> lines, Action onComplete = null)
    {
        lineQueue.Clear();
        foreach (var line in lines)
            lineQueue.Enqueue(line);

        onDialogueFinished = onComplete;
        openedThisFrame = true;

        if (boxRoot != null)
            boxRoot.SetActive(true);

        DisplayNextLine();
    }

    /// <summary>
    /// Convenience overload for quick single-speaker lines.
    /// </summary>
    public void StartDialogue(string speaker, string[] lines, Action onComplete = null)
    {
        var formatted = new List<DialogueLine>();
        foreach (string text in lines)
            formatted.Add(new DialogueLine { speakerName = speaker, text = text });

        StartDialogue(formatted, onComplete);
    }

    private void DisplayNextLine()
    {
        if (lineQueue.Count == 0)
        {
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

        isTyping = false;
        if (continueIndicator != null)
            continueIndicator.SetActive(true);
    }

    private void CompleteCurrentLineImmediately()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        bodyText.text = currentFullLine;
        bodyText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;

        if (continueIndicator != null)
            continueIndicator.SetActive(true);
    }

    public void CloseDialogue()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        isTyping = false;
        lineQueue.Clear();

        if (boxRoot != null)
            boxRoot.SetActive(false);

        Action callback = onDialogueFinished;
        onDialogueFinished = null;
        callback?.Invoke();
    }
}