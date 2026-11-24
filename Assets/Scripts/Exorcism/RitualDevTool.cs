using UnityEngine;
using UnityEngine.InputSystem;

public class RitualDevTool : MonoBehaviour
{
    [SerializeField] private RitualController ritual;
    [SerializeField] private GhostController ghost;
    [SerializeField] private Hud hud;
    [SerializeField] private float autoStepDelay = 3f;

    private bool _auto;
    private float _nextStep;
    private bool _wasAwaiting;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard.f5Key.wasPressedThisFrame && ritual.Begin(ghost.BoundObject))
            Report($"started the {ghost.Type.GhostName} ritual at the {ghost.BoundObject.DisplayName}");
        if (keyboard.f6Key.wasPressedThisFrame) ritual.ForceSucceedStep();
        if (keyboard.f7Key.wasPressedThisFrame) ritual.ForceFailStep();
        if (keyboard.f8Key.wasPressedThisFrame) ritual.ForceBanishment();
        if (keyboard.f9Key.wasPressedThisFrame)
        {
            _auto = !_auto;
            Report(_auto ? "auto play on" : "auto play off");
        }

        bool awaiting = ritual.IsAwaiting;
        if (awaiting && !_wasAwaiting) _nextStep = Time.time + autoStepDelay;
        _wasAwaiting = awaiting;
        if (_auto && awaiting && Time.time >= _nextStep) ritual.ForceSucceedStep();
    }

    private void Report(string message)
    {
        hud.ShowMessage($"Dev: {message}", 2f);
    }
}
