using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ExorcismBook : Carryable
{
    private static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };

    [SerializeField] private BookPage[] pages;
    [SerializeField] private RitualController ritual;
    [SerializeField] private Movement movement;

    [Header("Holding")] [SerializeField] private Transform inspectPoint;
    [SerializeField] private Light readingLight;
    [SerializeField] private float poseSpeed = 12f;

    [Header("Page Flip")] [SerializeField] private Transform flipPivot;
    [SerializeField] private float flipDuration = 0.4f;
    [SerializeField] private BookRightPage flipFront;
    [SerializeField] private BookLeftPage flipBack;

    [Header("Covers")] [SerializeField] private Transform openModel;
    [SerializeField] private Transform leftHinge;
    [SerializeField] private Transform rightHinge;
    [SerializeField] private float halfWidth = 0.135f;
    [SerializeField] private float coverDuration = 0.5f;

    [Header("Pages")] [SerializeField] private BookLeftPage leftPage;
    [SerializeField] private BookRightPage rightPage;
    [SerializeField] private bool showItemIcons;
    [SerializeField] private Color doneColor = new(0.22f, 0.38f, 0.2f);
    [SerializeField] private Color currentColor = new(0.55f, 0.06f, 0.04f);
    [SerializeField] private Color sacrificeColor = new(0.42f, 0.2f, 0.05f);
    [SerializeField] private Color translationColor = new(0.35f, 0.3f, 0.25f);

    private readonly StringBuilder _builder = new();
    private int _flipDirection;
    private float _flipProgress = 1f;
    private int _flipTarget;
    private bool _inspecting;
    private float _leftFold = 1f;
    private int _page = -1;
    private float _rightFold;
    private int _shownPage = -1;
    private RitualState _shownState;
    private int _shownStep = -1;

    public string Hint => IsOpen ? $"{pages[_page].Title}  [Q] read  [Z/X] turn page" : $"{DisplayName}  [Z/X] open";

    private bool IsOpen => IsHeld && _page >= 0 && _page < pages.Length;
    private int ShownPage => Mathf.Clamp(_page, 0, pages.Length - 1);

    private bool Settled => _flipProgress >= 1f && (_leftFold == 0f || _leftFold == 1f) &&
                            (_rightFold == 0f || _rightFold == 1f);

    private void Update()
    {
        readingLight.enabled = IsHeld;
        if (IsHeld) FollowPose();
        else _inspecting = false;
        if (_flipProgress < 1f) AnimateFlip();
        AnimateCovers();
        if (_shownPage != ShownPage || _shownStep != ritual.StepIndex ||
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
        if (!ctx.performed || !IsHeld || !movement.canMove || !Settled) return;
        float direction = ctx.ReadValue<float>();
        if (Mathf.Abs(direction) < 0.01f) return;
        _flipDirection = direction > 0f ? 1 : -1;
        int target = Mathf.Clamp(_page + _flipDirection, -1, pages.Length);
        if (target == _page) return;
        if (!IsOpen || target < 0 || target >= pages.Length)
        {
            _page = target;
            return;
        }

        _flipTarget = target;
        _flipProgress = 0f;
        flipPivot.gameObject.SetActive(true);
        Render();
        AnimateFlip();
    }

    private void AnimateFlip()
    {
        _flipProgress = IsHeld ? Mathf.MoveTowards(_flipProgress, 1f, Time.deltaTime / flipDuration) : 1f;
        if (_flipProgress >= 1f) _page = _flipTarget;
        float eased = Mathf.SmoothStep(0f, 1f, _flipProgress);
        float angle = 180f * (_flipDirection > 0 ? eased : 1f - eased);
        flipPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        flipPivot.gameObject.SetActive(_flipProgress < 1f);
    }

    private void AnimateCovers()
    {
        float leftTarget = IsHeld && _page >= 0 ? 0f : 1f;
        float rightTarget = IsHeld && _page >= pages.Length ? 1f : 0f;
        float step = IsHeld ? Time.deltaTime / coverDuration : 1f;
        _leftFold = Mathf.MoveTowards(_leftFold, leftTarget, step);
        _rightFold = Mathf.MoveTowards(_rightFold, rightTarget, step);
        float left = Mathf.SmoothStep(0f, 1f, _leftFold);
        float right = Mathf.SmoothStep(0f, 1f, _rightFold);
        leftHinge.localRotation = Quaternion.Euler(0f, 0f, -180f * left);
        rightHinge.localRotation = Quaternion.Euler(0f, 0f, 180f * right);
        openModel.localPosition = new Vector3(halfWidth * (right - left), 0f, 0f);
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
        _shownPage = ShownPage;
        _shownStep = ritual.StepIndex;
        _shownState = ritual.State;
        bool flipping = _flipProgress < 1f;
        bool forward = _flipDirection > 0;
        int older = _shownPage;
        int newer = flipping ? _flipTarget : _shownPage;
        WriteLeft(leftPage, flipping && forward ? older : newer);
        WriteRight(rightPage, flipping && !forward ? older : newer);
        if (!flipping) return;
        WriteLeft(flipBack, forward ? newer : older);
        WriteRight(flipFront, forward ? older : newer);
    }

    private void WriteLeft(BookLeftPage side, int index)
    {
        BookPage page = pages[index];
        side.Title.text = page.Title;
        side.Flavor.text = page.Subtitle;
        if (page is IntroductionPage introduction)
        {
            side.Verses.text = introduction.LeftText;
            return;
        }

        ExorcismRitualData data = (ExorcismRitualData)page;
        side.Verses.text = Verses(data, 0, data.Steps.Length / 2);
    }

    private void WriteRight(BookRightPage side, int index)
    {
        BookPage page = pages[index];
        side.PageNumber.text = $"- {index + 1} -";
        if (page is IntroductionPage introduction)
        {
            side.Verses.text = introduction.RightText;
            return;
        }

        ExorcismRitualData data = (ExorcismRitualData)page;
        side.Verses.text = Verses(data, data.Steps.Length / 2, data.Steps.Length);
    }

    private string Verses(ExorcismRitualData data, int from, int to)
    {
        bool live = ritual.IsActive && ritual.Ritual == data;
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
