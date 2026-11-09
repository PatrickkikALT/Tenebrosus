using System;
using System.Collections;
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

    [Header("Ghost Orbs")] [SerializeField]
    private Renderer[] orbs;

    [SerializeField] private float orbRadius = 1.2f;
    [SerializeField] private float orbSpeed = 0.4f;
    [SerializeField] private float orbBob = 0.4f;

    [Header("Wandering")] [SerializeField] private float hoverHeight = 1.2f;

    [SerializeField] private float wanderSpeed = 0.6f;
    [SerializeField] private Vector2 wanderIntervalRange = new(3f, 8f);

    [Header("Activity")] [SerializeField] private Vector2 activityIntervalRange = new(12f, 30f);

    [SerializeField] private float throwForce = 3f;
    [SerializeField] private float glimpseDuration = 0.4f;

    [Header("Anger")] [SerializeField] private float baseAngerRate = 0.3f;

    [SerializeField] private float presenceAngerRate = 0.7f;
    [SerializeField] private float wrongRitualAnger = 35f;
    [SerializeField] private float angerAfterHunt = 40f;

    [Header("Hunt")] [SerializeField] private float huntDuration = 20f;

    [SerializeField] private float huntSpeed = 2.5f;
    [SerializeField] private float catchDistance = 1f;

    [Header("Evidence")] [SerializeField] private float emfPeakRange = 1.5f;

    [SerializeField] private float emfHighRange = 3.5f;
    [SerializeField] private float emfLowRange = 6f;
    [SerializeField] private float outsideTemperature = 12f;
    [SerializeField] private float remPodRange = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float evpChance = 0.5f;

    [SerializeField] private string[] evpResponses =
        { "...behind you...", "...leave...", "...here...", "...mine...", "...so cold...", "...get out..." };

    private float _nextActivityTime;
    private float _nextWanderTime;

    private Vector3 _wanderTarget;

    public GhostType Type { get; private set; }
    public Room HauntedRoom { get; private set; }
    public HauntableObject BoundObject { get; private set; }
    public float Anger { get; private set; }
    public int RitualProgress { get; private set; }
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
    }

    private void Update()
    {
        UpdateOrbs();
        if (IsBanished || !gameManager.IsPlaying) return;
        if (IsHunting)
        {
            Chase();
            return;
        }

        Wander();
        AddAnger((baseAngerRate + (HauntedRoom.Contains(player.position) ? presenceAngerRate : 0f)) *
                 Time.deltaTime);
        if (Time.time >= _nextActivityTime)
        {
            DoActivity();
            ScheduleActivity();
        }
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
        if (IsBanished) return 1;
        float distance = Vector3.Distance(position, BoundObject.transform.position);
        if (distance < emfPeakRange) return Type.Has(EvidenceType.Emf5) ? 5 : 4;
        if (distance < emfHighRange) return 3;
        if (distance < emfLowRange) return 2;
        return 1;
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
        response = evpResponses[Random.Range(0, evpResponses.Length)];
        return true;
    }

    public bool TriggersRemPod(Vector3 position)
    {
        return !IsBanished && Type.Has(EvidenceType.RemPod) &&
               Vector3.Distance(transform.position, position) < remPodRange;
    }

    public void ApplyRitualItem(HauntableObject target, RitualItemType item)
    {
        if (IsBanished) return;
        string itemName = RitualItemNames.Get(item);
        if (target != BoundObject)
        {
            hud.ShowMessage(
                $"You use the {itemName} on the {target.DisplayName}. Nothing happens... but something stirs.", 4f);
            AddAnger(wrongRitualAnger * 0.5f);
            return;
        }

        if (Type.RitualSteps[RitualProgress] != item)
        {
            RitualProgress = 0;
            hud.ShowMessage($"The {itemName} sizzles and dies. The ritual is broken and the ghost is furious!", 4f);
            StartCoroutine(Flicker(HauntedRoom, 2f));
            AddAnger(wrongRitualAnger);
            return;
        }

        RitualProgress++;
        if (RitualProgress >= Type.RitualSteps.Length)
        {
            Banish();
            return;
        }

        hud.ShowMessage(
            $"The {target.DisplayName} shudders as you use the {itemName}... ({RitualProgress}/{Type.RitualSteps.Length})",
            4f);
        StartCoroutine(Flicker(HauntedRoom, 1f));
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

    private void AddAnger(float amount)
    {
        if (IsHunting) return;
        Anger = Mathf.Min(100f, Anger + amount);
        if (Anger >= 100f)
            StartCoroutine(Hunt());
    }

    private void ScheduleActivity()
    {
        _nextActivityTime = Time.time + Random.Range(activityIntervalRange.x, activityIntervalRange.y);
    }

    private void DoActivity()
    {
        switch (Random.Range(0, 4))
        {
            case 0:
                StartCoroutine(Flicker(HauntedRoom, 1.5f));
                break;
            case 1:
                ThrowSomething();
                break;
            case 2:
                if (HauntedRoom.LightsOn)
                    HauntedRoom.SetLights(false);
                else
                    StartCoroutine(Flicker(HauntedRoom, 1.5f));
                break;
            default:
                StartCoroutine(Glimpse());
                break;
        }
    }

    private void ThrowSomething()
    {
        Rigidbody body = HauntedRoom.Throwables[Random.Range(0, HauntedRoom.Throwables.Length)];
        body.AddForce((Random.insideUnitSphere + Vector3.up).normalized * throwForce, ForceMode.Impulse);
        body.AddTorque(Random.insideUnitSphere * throwForce, ForceMode.Impulse);
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
        foreach (Room room in rooms)
            StartCoroutine(Flicker(room, huntDuration));
        yield return new WaitForSeconds(huntDuration);
        visual.enabled = false;
        IsHunting = false;
        Anger = angerAfterHunt;
        transform.position = HauntedRoom.RandomPoint(hoverHeight);
        _wanderTarget = transform.position;
        if (gameManager.IsPlaying)
            hud.ShowMessage("The house falls silent again...");
    }

    private void Chase()
    {
        Vector3 target = new(player.position.x, hoverHeight, player.position.z);
        transform.position = Vector3.MoveTowards(transform.position, target, huntSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, target) <= catchDistance)
            gameManager.Lose("The ghost caught you.");
    }

    private void Banish()
    {
        StopAllCoroutines();
        IsBanished = true;
        IsHunting = false;
        visual.enabled = false;
        foreach (Room room in rooms)
            room.RestoreLights();
        gameManager.Win(Type.GhostName, BoundObject.DisplayName);
    }
}