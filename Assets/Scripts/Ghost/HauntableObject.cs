using UnityEngine;

public class HauntableObject : MonoBehaviour
{
    [SerializeField] private string displayName;
    [SerializeField] private GameObject uvPrints;

    public string DisplayName => displayName;

    public void ShowUvPrints()
    {
        uvPrints.SetActive(true);
    }
}