using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RitualController : MonoBehaviour
{
    [Header("Setup")] [SerializeField] private ExorcismRitualData[] rituals;
    [SerializeField] private GhostController ghost;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private VoiceRecognition voice;
    [SerializeField] private Hud hud;
    [SerializeField] private Transform player;

    [Header("Ritual Site")] [SerializeField] private Transform site;
    [SerializeField] private LayerMask floorMask = 1;
    [SerializeField] private float floorProbe = 3f;
    [SerializeField] private float ghostHeight = 1.2f;

    [Header("Voice")] [SerializeField] private string ritualLanguage = "la";
    [SerializeField] private float listenRange = 5f;
    [SerializeField] private float duringGrace = 1.5f;
    [SerializeField] [Min(1f)] private float heardWordsKept = 2.5f;

    [Header("Pacing")] [SerializeField] private float preparationTime = 3f;
    [SerializeField] private float stepPause = 1.5f;
    [SerializeField] private float failurePause = 2.5f;
    [SerializeField] private float banishmentDuration = 4.5f;
    [SerializeField] private float calmDuration = 3f;
    [SerializeField] private float abortDuration = 2.5f;
    [SerializeField] [Range(0f, 0.2f)] private float preparationIntensity = 0.08f;
    [SerializeField] private float wrongTargetSanityLoss = 5f;

    [Header("Events")] [SerializeField] private UnityEvent ritualStarted = new();
    [SerializeField] private UnityEvent<float> intensityChanged = new();
    [SerializeField] private UnityEvent stepStarted = new();
    [SerializeField] private UnityEvent phraseAccepted = new();
    [SerializeField] private UnityEvent sacrificeAccepted = new();
    [SerializeField] private UnityEvent stepFailed = new();
    [SerializeField] private UnityEvent banishmentStarted = new();
    [SerializeField] private UnityEvent banished = new();
    [SerializeField] private UnityEvent aborted = new();
    [SerializeField] private UnityEvent ritualEnded = new();

    private readonly List<string> _heard = new();
    private float _deadline;
    private int _failures;
    private float _lastSpeakingTime = float.NegativeInfinity;
    private string _previousLanguage;
    private bool _sacrificed;
    private Coroutine _sequence;
    private float _stepFraction;
    private string[] _target = Array.Empty<string>();

    public RitualState State { get; private set; }
    public ExorcismRitualData Ritual { get; private set; }
    public HauntableObject Target { get; private set; }
    public int StepIndex { get; private set; }
    public float Intensity { get; private set; }
    public Room Room => ghost.HauntedRoom;
    public Transform Site => site;
    public RitualStep CurrentStep => Ritual.Steps[StepIndex];
    public float TimeRemaining => Mathf.Max(0f, _deadline - Time.time);
    public bool IsActive => State != RitualState.Idle;
    public bool IsAwaiting => State is RitualState.AwaitingPhrase or RitualState.AwaitingSacrifice;

    public UnityEvent RitualStarted => ritualStarted;
    public UnityEvent<float> IntensityChanged => intensityChanged;
    public UnityEvent StepStarted => stepStarted;
    public UnityEvent PhraseAccepted => phraseAccepted;
    public UnityEvent SacrificeAccepted => sacrificeAccepted;
    public UnityEvent StepFailed => stepFailed;
    public UnityEvent BanishmentStarted => banishmentStarted;
    public UnityEvent Banished => banished;
    public UnityEvent Aborted => aborted;
    public UnityEvent RitualEnded => ritualEnded;

    private bool InRange => Vector3.Distance(player.position, site.position) <= listenRange;

    private void Update()
    {
        if (!IsActive) return;
        if (!gameManager.IsPlaying)
        {
            Finish();
            return;
        }

        if (voice.IsSpeaking) _lastSpeakingTime = Time.time;
        if (IsAwaiting && Time.time > _deadline)
            Fail(State == RitualState.AwaitingPhrase
                ? "The words came too late."
                : "The offering came too late.");
        PublishIntensity();
    }

    private void OnEnable()
    {
        voice.PartialTextRecognized += OnPartialText;
        voice.TextRecognized += OnText;
    }

    private void OnDisable()
    {
        voice.PartialTextRecognized -= OnPartialText;
        voice.TextRecognized -= OnText;
    }

    public bool Begin(HauntableObject target)
    {
        if (IsActive || ghost.IsBanished || !gameManager.IsPlaying) return false;
        if (target != ghost.BoundObject)
        {
            hud.ShowMessage(
                $"You open the book over the {target.DisplayName}. Nothing answers... but something stirs.", 4f);
            ghost.RitualBacklash(wrongTargetSanityLoss);
            return false;
        }

        Ritual = Array.Find(rituals, ritual => ritual.GhostType == ghost.Type);
        Target = target;
        StepIndex = 0;
        _failures = 0;
        _stepFraction = 0f;
        PlaceSite(target.transform.position);
        _previousLanguage = voice.Language;
        voice.Language = ritualLanguage;
        State = RitualState.Preparing;
        ritualStarted.Invoke();
        hud.ShowMessage("The circle ignites. Read the rite aloud.", 3f);
        _sequence = StartCoroutine(Advance(0, preparationTime));
        return true;
    }

    public void ForceSucceedStep()
    {
        if (!IsAwaiting) return;
        if (CurrentStep.RequiresSacrifice && !_sacrificed)
        {
            _sacrificed = true;
            sacrificeAccepted.Invoke();
        }

        phraseAccepted.Invoke();
        CompleteStep();
    }

    public void ForceFailStep()
    {
        if (IsAwaiting) Fail("Dev: forced failure.");
    }

    public void ForceBanishment()
    {
        if (!IsActive || State == RitualState.Success) return;
        if (_sequence != null) StopCoroutine(_sequence);
        _sequence = StartCoroutine(Banishment());
    }

    public SacrificeResult OfferSacrifice(RitualItemType item)
    {
        if (!IsAwaiting) return SacrificeResult.Refused;
        RitualStep step = CurrentStep;
        string itemName = RitualItemNames.Get(item);
        if (!SacrificeExpected(step))
        {
            Fail($"The {itemName} was offered at the wrong moment.");
            return SacrificeResult.Rejected;
        }

        if (item != step.Sacrifice)
        {
            Fail($"The flames spit the {itemName} back out. The wrong offering.");
            return SacrificeResult.Rejected;
        }

        _sacrificed = true;
        sacrificeAccepted.Invoke();
        if (State == RitualState.AwaitingPhrase)
        {
            _stepFraction = Mathf.Max(_stepFraction, 0.5f);
            return SacrificeResult.Accepted;
        }

        if (step.Timing == SacrificeTiming.Before)
        {
            _stepFraction = 0.5f;
            Enter(RitualState.AwaitingPhrase);
        }
        else
        {
            CompleteStep();
        }

        return SacrificeResult.Accepted;
    }

    private bool SacrificeExpected(RitualStep step)
    {
        if (!step.RequiresSacrifice || _sacrificed) return false;
        if (State == RitualState.AwaitingSacrifice) return true;
        return step.Timing == SacrificeTiming.During &&
               (voice.IsSpeaking || Time.time - _lastSpeakingTime <= duringGrace);
    }

    private void OnPartialText(string text)
    {
        if (State != RitualState.AwaitingPhrase || !InRange) return;
        string[] partial = PhraseMatcher.Tokens(text);
        string[] combined = new string[_heard.Count + partial.Length];
        _heard.CopyTo(combined);
        partial.CopyTo(combined, _heard.Count);
        RitualStep step = CurrentStep;
        bool waitForOffering = step.RequiresSacrifice && step.Timing == SacrificeTiming.During && !_sacrificed;
        Evaluate(combined, false, !waitForOffering);
    }

    private void OnText(string text)
    {
        if (State != RitualState.AwaitingPhrase || !InRange) return;
        _heard.AddRange(PhraseMatcher.Tokens(text));
        int kept = Mathf.CeilToInt(_target.Length * heardWordsKept);
        if (_heard.Count > kept) _heard.RemoveRange(0, _heard.Count - kept);
        Evaluate(_heard.ToArray(), true, true);
    }

    private void Evaluate(string[] tokens, bool final, bool canAccept)
    {
        float score = PhraseMatcher.Score(tokens, _target);
        float threshold = CurrentStep.MatchThreshold;
        _stepFraction = Mathf.Max(_stepFraction, 0.4f * Mathf.Clamp01(score / threshold));
        if (score >= threshold && canAccept)
        {
            AcceptPhrase();
            return;
        }

        if (final && tokens.Length >= _target.Length)
            Fail("The words twist in your mouth. The rite falters.");
    }

    private void AcceptPhrase()
    {
        RitualStep step = CurrentStep;
        if (step.RequiresSacrifice && step.Timing == SacrificeTiming.During && !_sacrificed)
        {
            Fail("The verse rang hollow. Nothing was offered while you spoke.");
            return;
        }

        phraseAccepted.Invoke();
        if (step.RequiresSacrifice && step.Timing == SacrificeTiming.After)
        {
            _stepFraction = 0.5f;
            Enter(RitualState.AwaitingSacrifice);
            return;
        }

        CompleteStep();
    }

    private void StartStep(int index)
    {
        StepIndex = index;
        _stepFraction = 0f;
        _sacrificed = false;
        _heard.Clear();
        RitualStep step = CurrentStep;
        _target = PhraseMatcher.Tokens(step.Phrase);
        bool offerFirst = step.RequiresSacrifice && step.Timing == SacrificeTiming.Before;
        Enter(offerFirst ? RitualState.AwaitingSacrifice : RitualState.AwaitingPhrase);
        stepStarted.Invoke();
    }

    private void Enter(RitualState state)
    {
        State = state;
        _deadline = Time.time + CurrentStep.TimeLimit;
    }

    private void CompleteStep()
    {
        _stepFraction = 1f;
        if (StepIndex + 1 >= Ritual.Steps.Length)
        {
            _sequence = StartCoroutine(Banishment());
            return;
        }

        State = RitualState.Preparing;
        _sequence = StartCoroutine(Advance(StepIndex + 1, stepPause));
    }

    private void Fail(string message)
    {
        _failures++;
        stepFailed.Invoke();
        ghost.RitualBacklash(Ritual.FailureSanityLoss);
        if (_failures >= Ritual.MaxFailures)
        {
            Abort();
            return;
        }

        int left = Ritual.MaxFailures - _failures;
        hud.ShowMessage($"{message} The ghost lashes out! ({left} {(left == 1 ? "mistake" : "mistakes")} left)",
            4f);
        State = RitualState.Failed;
        int next = Ritual.FailurePolicy == RitualFailurePolicy.RepeatStep
            ? StepIndex
            : Mathf.Max(0, StepIndex - Ritual.StepsLostOnFailure);
        _stepFraction = 0f;
        _sequence = StartCoroutine(Advance(next, failurePause));
    }

    private void Abort()
    {
        State = RitualState.Aborted;
        aborted.Invoke();
        hud.ShowMessage("The circle shatters! The ghost is enraged.", 4f);
        ghost.Enrage(Ritual.AbortSanityLoss);
        _sequence = StartCoroutine(FinishAfter(abortDuration));
    }

    private IEnumerator Advance(int index, float delay)
    {
        yield return new WaitForSeconds(delay);
        StartStep(index);
    }

    private IEnumerator FinishAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Finish();
    }

    private IEnumerator Banishment()
    {
        State = RitualState.Success;
        ghost.BeginBanishment(site.position + Vector3.up * ghostHeight);
        banishmentStarted.Invoke();
        hud.ShowMessage($"Something is torn screaming from the {Target.DisplayName}!", 4f);
        yield return new WaitForSeconds(banishmentDuration);
        banished.Invoke();
        yield return new WaitForSeconds(calmDuration);
        ghost.Banish();
        Finish();
    }

    private void Finish()
    {
        if (_sequence != null) StopCoroutine(_sequence);
        _sequence = null;
        voice.Language = _previousLanguage;
        State = RitualState.Idle;
        _stepFraction = 0f;
        Intensity = 0f;
        intensityChanged.Invoke(0f);
        ritualEnded.Invoke();
    }

    private void PublishIntensity()
    {
        float target = State switch
        {
            RitualState.Success => 1f,
            RitualState.Aborted => Intensity,
            _ => Mathf.Max(preparationIntensity, (StepIndex + _stepFraction) / Ritual.Steps.Length)
        };
        if (Mathf.Abs(target - Intensity) < 0.001f) return;
        Intensity = target;
        intensityChanged.Invoke(Intensity);
    }

    private void PlaceSite(Vector3 center)
    {
        Vector3 toPlayer = Vector3.ProjectOnPlane(player.position - center, Vector3.up).normalized;
        float floor = Physics.Raycast(player.position, Vector3.down, out RaycastHit hit, floorProbe, floorMask,
            QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : center.y;
        site.SetPositionAndRotation(new Vector3(center.x, floor, center.z), Quaternion.LookRotation(toPlayer));
        site.gameObject.SetActive(true);
    }
}
