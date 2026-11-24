using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ExorcismBook : Carryable
{
    private static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };

    [SerializeField] private ExorcismRitualData[] pages;
    [SerializeField] private RitualController ritual;
    [SerializeField] private Movement movement;

    [Header("Holding")] [SerializeField] private Transform inspectPoint;
    [SerializeField] private Light readingLight;
    [SerializeField] private float poseSpeed = 12f;
    [SerializeField] private float pageTurnCooldown = 0.2f;

    [Header("Model")] [SerializeField] private GameObject closedModel;
    [SerializeField] private GameObject openModel;

    [Header("Pages")] [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text flavorText;
    [SerializeField] private TMP_Text leftVersesText;
    [SerializeField] private TMP_Text rightVersesText;
    [SerializeField] private TMP_Text pageNumberText;
    [SerializeField] private bool showItemIcons;
    [SerializeField] private Color doneColor = new(0.22f, 0.38f, 0.2f);
    [SerializeField] private Color currentColor = new(0.55f, 0.06f, 0.04f);
    [SerializeField] private Color sacrificeColor = new(0.42f, 0.2f, 0.05f);
    [SerializeField] private Color translationColor = new(0.35f, 0.3f, 0.25f);

    private readonly StringBuilder _builder = new();
    private bool _inspecting;
    private float _nextPageTurn;
    private int _page;
    private bool _shownOpen = true;
    private int _shownPage = -1;
    private RitualState _shownState;
    private int _shownStep = -1;

    public string Hint => $"{pages[_page].GhostType.GhostName}  [Q] read  [Z/X] turn page";

    private void Update()
    {
        readingLight.enabled = IsHeld;
        if (IsHeld) FollowPose();
        else _inspecting = false;
        if (_shownOpen != IsHeld || _shownPage != _page || _shownStep != ritual.StepIndex ||
            _shownState != ritual.State)
            Render();
    }

    public void OnInspect(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !IsHeld || !movement.canMove) return;
        _inspecting = !_inspecting;
    }

    public void OnTurnPage(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !IsHeld || !movement.canMove || Time.time < _nextPageTurn) return;
        float direction = ctx.ReadValue<float>();
        if (Mathf.Abs(direction) < 0.01f) return;
        _nextPageTurn = Time.time + pageTurnCooldown;
        _page = (_page + (direction > 0f ? 1 : -1) + pages.Length) % pages.Length;
    }

    private void FollowPose()
    {
        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;
        if (_inspecting)
        {
            Transform hand = transform.parent;
            position = hand.InverseTransformPoint(inspectPoint.position);
            rotation = Quaternion.Inverse(hand.rotation) * inspectPoint.rotation;
        }

        float blend = 1f - Mathf.Exp(-poseSpeed * Time.deltaTime);
        transform.SetLocalPositionAndRotation(Vector3.Lerp(transform.localPosition, position, blend),
            Quaternion.Slerp(transform.localRotation, rotation, blend));
    }

    private void Render()
    {
        _shownOpen = IsHeld;
        _shownPage = _page;
        _shownStep = ritual.StepIndex;
        _shownState = ritual.State;
        closedModel.SetActive(!_shownOpen);
        openModel.SetActive(_shownOpen);
        if (!_shownOpen) return;
        ExorcismRitualData data = pages[_page];
        titleText.text = data.GhostType.GhostName;
        flavorText.text = data.FlavorLine;
        pageNumberText.text = $"- {_page + 1} -";
        bool live = ritual.IsActive && ritual.Ritual == data;
        int split = data.Steps.Length / 2;
        leftVersesText.text = Verses(data, live, 0, split);
        rightVersesText.text = Verses(data, live, split, data.Steps.Length);
    }

    private string Verses(ExorcismRitualData data, bool live, int from, int to)
    {
        _builder.Clear();
        for (int i = from; i < to; i++)
        {
            RitualStep step = data.Steps[i];
            bool done = live && (i < ritual.StepIndex || ritual.State == RitualState.Success);
            bool current = live && !done && i == ritual.StepIndex;
            if (done) OpenColor(doneColor);
            else if (current) OpenColor(currentColor).Append("<b>");
            _builder.Append(i < Numerals.Length ? Numerals[i] : (i + 1).ToString()).Append(".  ");
            if (step.RequiresSacrifice && step.Timing == SacrificeTiming.Before)
                AppendSacrifice(step, "First offer", false).Append('\n');
            _builder.Append("<i>").Append(step.Phrase).Append("</i>");
            if (!string.IsNullOrEmpty(step.Translation))
            {
                _builder.Append("\n<size=70%>");
                OpenColor(translationColor).Append(step.Translation).Append("</color></size>");
            }

            if (step.RequiresSacrifice && step.Timing == SacrificeTiming.During)
                AppendSacrifice(step, "While speaking, offer", true);
            if (step.RequiresSacrifice && step.Timing == SacrificeTiming.After)
                AppendSacrifice(step, "Then offer", true);
            if (current) _builder.Append("</b>");
            if (done || current) _builder.Append("</color>");
            _builder.Append("\n\n");
        }

        return _builder.ToString();
    }

    private StringBuilder OpenColor(Color color)
    {
        return _builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>');
    }

    private StringBuilder AppendSacrifice(RitualStep step, string verb, bool newLine)
    {
        if (newLine) _builder.Append('\n');
        _builder.Append("<size=80%>");
        OpenColor(sacrificeColor).Append(verb).Append(' ');
        if (showItemIcons) _builder.Append("<sprite name=\"").Append(step.Sacrifice).Append("\"> ");
        return _builder.Append("<b>").Append(RitualItemNames.Get(step.Sacrifice)).Append("</b></color></size>");
    }
}
