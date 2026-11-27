using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class Hud : MonoBehaviour
{
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Flashlight flashlight;
    [SerializeField] private HeadCamera headCamera;
    [SerializeField] private Journal journal;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;
    [FormerlySerializedAs("toolReadingText")] [SerializeField] private TMP_Text readingText;
    [SerializeField] private GameObject messageRoot;
    [SerializeField] private TMP_Text messageText;
    private readonly StringBuilder _builder = new();

    private readonly List<(string text, float until)> _messages = new();

    private void Update()
    {
        bool visible = gameManager.IsPlaying && !journal.IsOpen;
        if (root.activeSelf != visible)
            root.SetActive(visible);
        if (!visible) return;

        string prompt = interactor.Prompt;
        promptRoot.SetActive(prompt.Length > 0);
        promptText.text = prompt;

        string reading = interactor.Held is Tool tool ? tool.Reading :
            headCamera.IsWorn ? headCamera.Status : "";
        readingText.gameObject.SetActive(reading.Length > 0);
        readingText.text = reading;

        _messages.RemoveAll(message => Time.time > message.until);
        messageRoot.SetActive(_messages.Count > 0);
        _builder.Clear();
        for (int i = 0; i < _messages.Count; i++)
        {
            if (i > 0) _builder.Append('\n');
            _builder.Append(_messages[i].text);
        }

        messageText.text = _builder.ToString();
    }

    public void ShowMessage(string text, float duration = 3f)
    {
        _messages.Add((text, Time.time + duration));
    }
}