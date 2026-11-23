using UnityEngine;

public class HauntableObject : MonoBehaviour
{
    [SerializeField] private string displayName;
    [SerializeField] private GameObject uvPrints;
    [Tooltip("Short phrases the ghost spells on the Ouija board, revealed one by one. Five words or fewer read best.")]
    [SerializeField] [TextArea] private string[] memories;

    public string DisplayName => displayName;
    public string[] Memories => memories;

    public void ShowUvPrints()
    {
        uvPrints.SetActive(true);
    }
}
