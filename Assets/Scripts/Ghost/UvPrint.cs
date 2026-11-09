using System.Collections.Generic;
using UnityEngine;

public class UvPrint : MonoBehaviour
{
    private static readonly List<UvPrint> _all = new();

    [SerializeField] private Renderer renderer;

    public static IReadOnlyList<UvPrint> All => _all;

    private void OnEnable()
    {
        _all.Add(this);
        renderer.enabled = false;
    }

    private void OnDisable()
    {
        _all.Remove(this);
    }

    public void SetRevealed(bool revealed)
    {
        renderer.enabled = revealed;
    }
}