using UnityEngine;
using UnityEngine.InputSystem;

public class Flashlight : MonoBehaviour
{
    [SerializeField] private Light light;
    [SerializeField] private Movement movement;

    public bool IsOn => light.enabled;

    public void OnToggle(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !movement.canMove) return;
        light.enabled = !light.enabled;
    }
}