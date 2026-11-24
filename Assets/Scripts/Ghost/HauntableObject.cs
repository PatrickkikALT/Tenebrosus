using UnityEngine;

public class HauntableObject : MonoBehaviour
{
    [SerializeField] private string displayName;
    [SerializeField] private GameObject uvPrints;
    [SerializeField] private Renderer[] renderers;
    [Tooltip("Short phrases the ghost spells on the Ouija board, revealed one by one. Five words or fewer read best.")]
    [SerializeField] [TextArea] private string[] memories;

    public string DisplayName => displayName;
    public string[] Memories => memories;
    public Renderer[] Renderers => renderers;

    public void ShowUvPrints()
    {
        uvPrints.SetActive(true);
    }
}
