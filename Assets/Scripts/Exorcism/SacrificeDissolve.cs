using System.Collections;
using UnityEngine;

public class SacrificeDissolve : MonoBehaviour
{
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");

    [SerializeField] private Material dissolveMaterial;
    [SerializeField] private ParticleSystem embers;
    [SerializeField] private ParticleSystem smoke;
    [SerializeField] private Light flame;
    [SerializeField] private float duration = 2.5f;
    [SerializeField] private float riseHeight = 0.2f;
    [SerializeField] private float spinSpeed = 60f;
    [SerializeField] private float flameIntensity = 400f;
    [SerializeField] [ColorUsage(false, true)] private Color acceptedEdge = new(6f, 2.2f, 0.4f);
    [SerializeField] [ColorUsage(false, true)] private Color rejectedEdge = new(8f, 0.25f, 0.15f);
    [SerializeField] private Color acceptedTint = new(1f, 0.6f, 0.2f);
    [SerializeField] private Color rejectedTint = new(1f, 0.15f, 0.08f);

    public void Burn(RitualItem item, bool accepted)
    {
        StartCoroutine(BurnRoutine(item, accepted ? acceptedEdge : rejectedEdge, accepted ? acceptedTint : rejectedTint));
    }

    private IEnumerator BurnRoutine(RitualItem item, Color edge, Color tint)
    {
        Renderer[] renderers = item.Renderers;
        MaterialPropertyBlock block = new();
        foreach (Renderer target in renderers)
        {
            Material source = target.sharedMaterial;
            block.Clear();
            if (source.HasColor(BaseColorId)) block.SetColor(BaseColorId, source.GetColor(BaseColorId));
            if (source.HasTexture(BaseColorMapId) && source.GetTexture(BaseColorMapId) != null)
                block.SetTexture(BaseColorMapId, source.GetTexture(BaseColorMapId));
            block.SetColor(EdgeColorId, edge);
            block.SetFloat(DissolveId, 0f);
            target.SetPropertyBlock(block);
            Material[] materials = new Material[target.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = dissolveMaterial;
            target.sharedMaterials = materials;
        }

        ParticleSystem.MainModule main = embers.main;
        main.startColor = tint;
        flame.color = tint;
        embers.Play();
        smoke.Play();
        Vector3 start = item.transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            item.transform.position = start + Vector3.up * (riseHeight * Mathf.SmoothStep(0f, 1f, t));
            item.transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            flame.intensity = flameIntensity * Mathf.Sin(t * Mathf.PI) * Random.Range(0.75f, 1.25f);
            foreach (Renderer target in renderers)
            {
                target.GetPropertyBlock(block);
                block.SetFloat(DissolveId, t);
                target.SetPropertyBlock(block);
            }

            yield return null;
        }

        flame.intensity = 0f;
        embers.Stop();
        smoke.Stop();
        Destroy(item.gameObject);
    }
}
