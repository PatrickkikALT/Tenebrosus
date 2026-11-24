using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class GhostController : MonoBehaviour
{
    [Header("Setup")] [SerializeField] private GhostType[] possibleTypes;

    [SerializeField] private Room[] rooms;
    [SerializeField] private Transform player;
    [SerializeField] private Renderer visual;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Hud hud;
    [SerializeField] private HeadCamera headCamera;
    [SerializeField] private Sanity sanity;
    [SerializeField] private AudioSource tellSource;

    [Header("Ghost Orbs")] [SerializeField]
    private Renderer[] orbs;

    [SerializeField] private float orbRadius = 1.2f;
    [SerializeField] private float orbSpeed = 0.4f;
    [SerializeField] private float orbBob = 0.4f;

    [Header("Wandering")] [SerializeField] private float hoverHeight = 1.2f;

    [SerializeField] private float wanderSpeed = 0.6f;
    [SerializeField] private Vector2 wanderIntervalRange = new(3f, 8f);

    [Header("Activity")] [SerializeField] private Vector2 calmIntervalRange = new(35f, 70f);

    [SerializeField] private Vector2 frenziedIntervalRange = new(6f, 14f);
    [SerializeField] private float throwForce = 3f;
    [SerializeField] private float glimpseDuration = 0.4f;

    [Header("Sanity")] [SerializeField] private float activitySanityLoss = 3f;

    [Header("Rage")] [SerializeField] [Range(0f, 1f)] private float rageInsanity = 0.35f;

    [SerializeField] private float rageHuntSanity = 25f;

    [Header("Hunt")] [SerializeField] private float huntSanity = 40f;

    [SerializeField] [Range(0f, 1f)] private float maxHuntChance = 0.6f;
    [SerializeField] private float huntCooldown = 60f;
    [SerializeField] private float huntDuration = 20f;

    [SerializeField] private float huntSpeed = 2.5f;
    [SerializeField] private float catchDistance = 1f;

    [Header("Evidence")] [SerializeField] private float emfRange = 1.5f;

    [SerializeField] private float emfDuration = 20f;
    [SerializeField] [Range(0f, 1f)] private float emf5Chance = 0.35f;
    [SerializeField] private float outsideTemperature = 12f;
    [SerializeField] private float remPodRange = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float evpChance = 0.5f;

    [SerializeField] private string[] evpResponses =
        { "...behind you...", "...leave...", "...here...", "...mine...", "...so cold...", "...get out..." };

    private readonly List<(Vector3 position, int level, float until)> _emfSources = new();
    private float _nextActivityTime;
    private float _nextHuntTime;
    private float _nextWanderTime;

    private Vector3 _wanderTarget;
    private bool _exorcising;
    private float _nextAmbientTime;
    private int _rage;

    public GhostType Type { get; private set; }
    public Room HauntedRoom { get; private set; }
    public HauntableObject BoundObject { get; private set; }
    public float PlayerSanity => sanity.Value;
    public bool IsHunting { get; private set; }
    public bool IsBanished { get; private set; }

    private void Start()
    {
        Type = possibleTypes[Random.Range(0, possibleTypes.Length)];
        Room[] candidates = Array.FindAll(rooms, room => room.Hauntables.Length > 0);
        HauntedRoom = candidates[Random.Range(0, candidates.Length)];
        BoundObject = HauntedRoom.Hauntables[Random.Range(0, HauntedRoom.Hauntables.Length)];
        if (Type.Has(EvidenceType.UvPrints))
            BoundObject.ShowUvPrints();
        transform.position = HauntedRoom.RandomPoint(hoverHeight);
        _wanderTarget = transform.position;
        visual.enabled = false;
        ScheduleActivity();
        ScheduleAmbient();
    }

    private void Update()
    {
        UpdateOrbs();
        if (IsBanished || _exorcising || !gameManager.IsPlaying) return;
        if (IsHunting)
        {
            Chase();
            return;
        }

        Wander();
        PlayAmbient();
        if (Time.time < _nextActivityTime) return;
        if (ShouldHunt())
            StartCoroutine(Hunt());
        else if (Type.OmenClip != null)
            StartCoroutine(OmenThenAct());
        else
            DoActivity();
        ScheduleActivity();
    }

    public Room FindRoom(Vector3 position)
    {
        foreach (Room room in rooms)
            if (room.Contains(position))
                return room;
        return null;
    }

    public int GetEmfLevel(Vector3 position)
    {
        int level = 1;
        if (IsBanished) return level;
        foreach ((Vector3 sourcePosition, int sourceLevel, float until) in _emfSources)
            if (Time.time < until && Vector3.Distance(position, sourcePosition) < emfRange)
                level = Mathf.Max(level, sourceLevel);
        return level;
    }

    public float GetTemperature(Vector3 position)
    {
        Room room = FindRoom(position);
        if (room == null) return outsideTemperature;
        if (room == HauntedRoom && !IsBanished)
            return Type.Has(EvidenceType.Freezing) ? Random.Range(-4f, -0.5f) : Random.Range(5f, 10f);
        return room.BaseTemperature + Random.Range(-0.5f, 0.5f);
    }

    public bool TryGetEvpResponse(Vector3 position, out string response)
    {
        response = null;
        if (IsBanished || !Type.Has(EvidenceType.Evp) || !HauntedRoom.Contains(position) || Random.value > evpChance)
            return false;
        string[] responses = Type.EvpResponses.Length > 0 ? Type.EvpResponses : evpResponses;
        response = responses[Random.Range(0, responses.Length)];
        return true;
    }

    public bool TriggersRemPod(Vector3 position)
    {
        return !IsBanished && Type.Has(EvidenceType.RemPod) &&
               Vector3.Distance(transform.position, position) < remPodRange;
    }

    public void RitualBacklash(float sanityLoss)
    {
        if (IsBanished) return;
        StartCoroutine(Flicker(HauntedRoom, 2f));
        if (Type.ThrowWeight > 0f) ThrowSomething();
        LeaveEmf(BoundObject.transform.position);
        sanity.Drain(sanityLoss);
        _nextActivityTime = Time.time;
    }

    public void Enrage(float sanityLoss)
    {
        _rage++;
        RitualBacklash(sanityLoss);
        _nextHuntTime = Time.time;
    }

    public void BeginBanishment(Vector3 position)
    {
        StopAllCoroutines();
        _exorcising = true;
        IsHunting = false;
        foreach (Room room in rooms)
            room.RestoreLights();
        transform.position = position;
        visual.enabled = true;
    }

    private void Wander()
    {
        if (Time.time >= _nextWanderTime)
        {
            _wanderTarget = HauntedRoom.RandomPoint(hoverHeight);
            _nextWanderTime = Time.time + Random.Range(wanderIntervalRange.x, wanderIntervalRange.y);
        }

        transform.position = Vector3.MoveTowards(transform.position, _wanderTarget, wanderSpeed * Time.deltaTime);
    }

    private void UpdateOrbs()
    {
        bool visible = !IsBanished && Type.Has(EvidenceType.GhostOrb) && headCamera.NightVisionActive;
        for (int i = 0; i < orbs.Length; i++)
        {
            orbs[i].enabled = visible;
            if (!visible) continue;
            float angle = Time.time * orbSpeed + i * Mathf.PI * 2f / orbs.Length;
            orbs[i].transform.position = transform.position + new Vector3(
                Mathf.Cos(angle) * orbRadius,
                Mathf.Sin(Time.time * 0.7f + i) * orbBob,
                Mathf.Sin(angle) * orbRadius);
        }
    }

    private bool ShouldHunt()
    {
        float threshold = huntSanity + _rage * rageHuntSanity;
        if (sanity.Value > threshold || Time.time < _nextHuntTime) return false;
        return Random.value < Mathf.Lerp(maxHuntChance, 0f, sanity.Value / threshold);
    }

    private void ScheduleActivity()
    {
        float insanity = Mathf.Clamp01(sanity.Insanity + _rage * rageInsanity);
        float min = Mathf.Lerp(calmIntervalRange.x, frenziedIntervalRange.x, insanity);
        float max = Mathf.Lerp(calmIntervalRange.y, frenziedIntervalRange.y, insanity);
        _nextActivityTime = Time.time + Random.Range(min, max) / Type.ActivityRate;
    }

    private void ScheduleAmbient()
    {
        _nextAmbientTime = Time.time + Random.Range(Type.AmbientInterval.x, Type.AmbientInterval.y);
    }

    private void PlayAmbient()
    {
        if (Type.AmbientClip == null || Time.time < _nextAmbientTime) return;
        tellSource.PlayOneShot(Type.AmbientClip);
        ScheduleAmbient();
    }

    private IEnumerator OmenThenAct()
    {
        for (int i = 0; i < Type.OmenRepeats; i++)
        {
            tellSource.PlayOneShot(Type.OmenClip);
            yield return new WaitForSeconds(Type.OmenInterval);
        }

        if (!IsHunting) DoActivity();
    }

    private void LeaveEmf(Vector3 position)
    {
        _emfSources.RemoveAll(source => Time.time >= source.until);
        int level = Type.Has(EvidenceType.Emf5) && Random.value < emf5Chance ? 5 : Random.Range(2, 5);
        _emfSources.Add((position, level, Time.time + emfDuration));
    }

    private void DoActivity()
    {
        if (HauntedRoom.Contains(player.position))
            sanity.Drain(activitySanityLoss);
        LeaveEmf(transform.position);
        float roll = Random.value *
                     (Type.FlickerWeight + Type.ThrowWeight + Type.LightsOffWeight + Type.GlimpseWeight);
        if ((roll -= Type.FlickerWeight) < 0f)
            StartCoroutine(Flicker(HauntedRoom, 1.5f));
        else if ((roll -= Type.ThrowWeight) < 0f)
            ThrowSomething();
        else if ((roll -= Type.LightsOffWeight) < 0f && HauntedRoom.LightsOn)
            HauntedRoom.SetLights(false);
        else if (roll < 0f)
            StartCoroutine(Flicker(HauntedRoom, 1.5f));
        else
            StartCoroutine(Glimpse());
    }

    private void ThrowSomething()
    {
        Rigidbody body = HauntedRoom.Throwables[Random.Range(0, HauntedRoom.Throwables.Length)];
        float force = throwForce * Type.ThrowForceMultiplier;
        body.AddForce((Random.insideUnitSphere + Vector3.up).normalized * force, ForceMode.Impulse);
        body.AddTorque(Random.insideUnitSphere * force, ForceMode.Impulse);
        LeaveEmf(body.position);
    }

    private IEnumerator Glimpse()
    {
        visual.enabled = true;
        yield return new WaitForSeconds(glimpseDuration);
        if (!IsHunting)
            visual.enabled = false;
    }

    private IEnumerator Flicker(Room room, float duration)
    {
        float end = Time.time + duration;
        while (Time.time < end)
        {
            foreach (Light roomLight in room.Lights)
                roomLight.enabled = Random.value > 0.5f;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.25f));
        }

        room.RestoreLights();
    }

    private IEnumerator Hunt()
    {
        IsHunting = true;
        visual.enabled = true;
        hud.ShowMessage("The lights go wild. It's hunting you. RUN!", 4f);
        float duration = huntDuration * Type.HuntDurationMultiplier;
        foreach (Room room in rooms)
            StartCoroutine(Flicker(room, duration));
        yield return new WaitForSeconds(duration);
        visual.enabled = false;
        IsHunting = false;
        _nextHuntTime = Time.time + huntCooldown;
        transform.position = HauntedRoom.RandomPoint(hoverHeight);
        _wanderTarget = transform.position;
        if (gameManager.IsPlaying)
            hud.ShowMessage("The house falls silent again...");
    }

    private void Chase()
    {
        Vector3 target = new(player.position.x, hoverHeight, player.position.z);
        transform.position = Vector3.MoveTowards(transform.position, target,
            huntSpeed * Type.HuntSpeedMultiplier * Time.deltaTime);
        if (Vector3.Distance(transform.position, target) <= catchDistance)
            gameManager.Lose("The ghost caught you.");
    }

    public void Banish()
    {
        StopAllCoroutines();
        _exorcising = false;
        IsBanished = true;
        IsHunting = false;
        visual.enabled = false;
        foreach (Room room in rooms)
            room.RestoreLights();
        gameManager.Win(Type.GhostName, BoundObject.DisplayName);
    }
}