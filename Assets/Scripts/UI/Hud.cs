using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

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
    [SerializeField] private TMP_Text toolNameText;
    [SerializeField] private TMP_Text toolReadingText;
    [SerializeField] private TMP_Text statusText;
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

        Carryable held = interactor.Held;
        toolNameText.text = held != null ? held.DisplayName : "Empty hands";
        toolReadingText.text = held switch
        {
            Tool tool => tool.Reading,
            RitualItem => "Ritual item",
            _ => "-"
        };
        _builder.Clear();
        for (int i = 0; i < interactor.SlotCount; i++)
        {
            if (i > 0) _builder.Append("     ");
            Carryable slot = interactor.GetSlot(i);
            string slotName = slot != null ? slot.DisplayName : "Empty";
            if (i == interactor.ActiveSlot)
                _builder.Append("<color=#FFFFFF><b>").Append(slotName).Append("</b></color>");
            else
                _builder.Append(slotName);
        }

        _builder.Append("\nFlashlight: ").Append(flashlight.IsOn ? "On" : "Off");
        if (headCamera.IsWorn)
            _builder.Append("     ").Append(headCamera.Status);
        statusText.text = _builder.ToString();

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