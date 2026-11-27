using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BackroomsGenerator : MonoBehaviour
{
    private const int FloorSubmesh = 0;
    private const int CeilingSubmesh = 1;
    private const int WallSubmesh = 2;
    private const int LightSubmesh = 3;

    [Header("Materials")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material ceilingMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material lightMaterial;

    [Header("Layout")]
    [SerializeField] private int seed = 1337;
    [SerializeField] private float cellSize = 4f;
    [SerializeField] private float wallHeight = 3f;
    [SerializeField] private float wallThickness = 0.2f;
    [SerializeField] private float pillarSize = 0.7f;
    [SerializeField] private int chunkCells = 6;
    [SerializeField] private int viewRadius = 2;
    [SerializeField] private int chunksPerFrame = 1;
    [SerializeField] [Range(0f, 1f)] private float wallChance = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float pillarChance = 0.08f;
    [SerializeField] private float roomNoiseScale = 0.07f;

    [Header("Lighting")]
    [SerializeField] private Light lightPrefab;
    [SerializeField] private int panelSpacing = 2;
    [SerializeField] private int realLightEvery = 2;
    [SerializeField] private float panelSize = 1.2f;
    [SerializeField] [Range(0f, 1f)] private float brokenPanelChance = 0.12f;
    [SerializeField] [Range(0f, 1f)] private float flickerChance = 0.15f;
    [SerializeField] private float lightDrop = 0.3f;

    [Header("Textures")]
    [SerializeField] private float floorTextureSize = 2f;
    [SerializeField] private float ceilingTextureSize = 2f;
    [SerializeField] private float wallTextureSize = 2f;

    [Header("Spawn")]
    [SerializeField] private float spawnHeight = 1f;

    private readonly Dictionary<Vector2Int, GameObject> _chunks = new();
    private readonly List<Vector2Int> _missing = new();
    private readonly List<Vector2Int> _expired = new();
    private readonly List<Vector3> _vertices = new();
    private readonly List<Vector3> _normals = new();
    private readonly List<Vector2> _uvs = new();
    private readonly List<int>[] _triangles = { new(), new(), new(), new() };
    private Material[] _materials;
    private Vector2 _noiseOffset;
    private Vector3 _chunkOrigin;
    private Transform _player;

    public bool IsActive => _player != null;

    private void Awake()
    {
        _materials = new[] { floorMaterial, ceilingMaterial, wallMaterial, lightMaterial };
        _noiseOffset = new Vector2(Hash(17, 31, 101) * 10000f, Hash(53, 7, 211) * 10000f);
    }

    private void Update()
    {
        if (_player == null) return;

        Vector2Int center = ChunkAt(_player.position);

        _missing.Clear();
        for (int x = -viewRadius; x <= viewRadius; x++)
        for (int z = -viewRadius; z <= viewRadius; z++)
        {
            Vector2Int coord = new(center.x + x, center.y + z);
            if (!_chunks.ContainsKey(coord)) _missing.Add(coord);
        }

        _missing.Sort((a, b) => (a - center).sqrMagnitude.CompareTo((b - center).sqrMagnitude));
        for (int i = 0; i < Mathf.Min(chunksPerFrame, _missing.Count); i++)
            BuildChunk(_missing[i]);

        _expired.Clear();
        foreach (Vector2Int coord in _chunks.Keys)
            if (Mathf.Abs(coord.x - center.x) > viewRadius + 1 || Mathf.Abs(coord.y - center.y) > viewRadius + 1)
                _expired.Add(coord);

        foreach (Vector2Int coord in _expired)
            DestroyChunk(coord);
    }

    public void Enter(Rigidbody body)
    {
        _player = body.transform;

        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
            if (!_chunks.ContainsKey(new Vector2Int(x, z)))
                BuildChunk(new Vector2Int(x, z));

        Vector3 spawn = transform.TransformPoint(new Vector3(cellSize * 0.5f, spawnHeight, cellSize * 0.5f));
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = spawn;
        body.transform.position = spawn;
        Physics.SyncTransforms();
    }

    private Vector2Int ChunkAt(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float chunkSize = cellSize * chunkCells;
        return new Vector2Int(Mathf.FloorToInt(local.x / chunkSize), Mathf.FloorToInt(local.z / chunkSize));
    }

    private void BuildChunk(Vector2Int coord)
    {
        float chunkSize = cellSize * chunkCells;
        _chunkOrigin = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);

        GameObject chunk = new($"Backrooms Chunk {coord.x},{coord.y}");
        chunk.layer = gameObject.layer;
        chunk.transform.SetParent(transform, false);
        chunk.transform.localPosition = _chunkOrigin;

        _vertices.Clear();
        _normals.Clear();
        _uvs.Clear();
        foreach (List<int> triangles in _triangles) triangles.Clear();

        AddFace(FloorSubmesh, Vector3.zero, new Vector3(chunkSize, 0f, 0f), new Vector3(0f, 0f, chunkSize), floorTextureSize);
        AddFace(CeilingSubmesh, new Vector3(0f, wallHeight, 0f), new Vector3(0f, 0f, chunkSize), new Vector3(chunkSize, 0f, 0f), ceilingTextureSize);

        float halfThickness = wallThickness * 0.5f;
        float halfPillar = pillarSize * 0.5f;

        for (int i = 0; i < chunkCells; i++)
        for (int j = 0; j < chunkCells; j++)
        {
            int cellX = coord.x * chunkCells + i;
            int cellZ = coord.y * chunkCells + j;
            float x = i * cellSize;
            float z = j * cellSize;

            if (HasWestWall(cellX, cellZ))
                AddBox(new Vector3(x - halfThickness, 0f, z - halfThickness), new Vector3(x + halfThickness, wallHeight, z + cellSize + halfThickness));

            if (HasSouthWall(cellX, cellZ))
                AddBox(new Vector3(x - halfThickness, 0f, z - halfThickness), new Vector3(x + cellSize + halfThickness, wallHeight, z + halfThickness));

            if (HasPillar(cellX, cellZ))
                AddBox(new Vector3(x - halfPillar, 0f, z - halfPillar), new Vector3(x + halfPillar, wallHeight, z + halfPillar));

            if (HasPanel(cellX, cellZ))
                AddPanel(chunk.transform, cellX, cellZ, new Vector3(x + cellSize * 0.5f, 0f, z + cellSize * 0.5f));
        }

        Mesh mesh = new() { name = chunk.name, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(_vertices);
        mesh.SetNormals(_normals);
        mesh.SetUVs(0, _uvs);
        mesh.subMeshCount = _triangles.Length;
        for (int i = 0; i < _triangles.Length; i++)
            mesh.SetTriangles(_triangles[i], i);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
        chunk.AddComponent<MeshRenderer>().sharedMaterials = _materials;
        chunk.AddComponent<MeshCollider>().sharedMesh = mesh;

        _chunks.Add(coord, chunk);
    }

    private void DestroyChunk(Vector2Int coord)
    {
        GameObject chunk = _chunks[coord];
        Destroy(chunk.GetComponent<MeshFilter>().sharedMesh);
        Destroy(chunk);
        _chunks.Remove(coord);
    }

    private void AddPanel(Transform chunk, int cellX, int cellZ, Vector3 center)
    {
        float half = panelSize * 0.5f;
        float y = wallHeight - 0.01f;
        AddFace(LightSubmesh, new Vector3(center.x - half, y, center.z - half), new Vector3(0f, 0f, panelSize), new Vector3(panelSize, 0f, 0f), panelSize);

        if (Mod(cellX / panelSpacing, realLightEvery) != 0 || Mod(cellZ / panelSpacing, realLightEvery) != 0) return;

        Light panelLight = Instantiate(lightPrefab, chunk);
        panelLight.transform.localPosition = new Vector3(center.x, wallHeight - lightDrop, center.z);
        if (Hash(cellX, cellZ, 6) < flickerChance)
            panelLight.gameObject.AddComponent<BackroomsLightFlicker>();
    }

    private void AddBox(Vector3 min, Vector3 max)
    {
        Vector3 size = max - min;
        Vector3 up = new(0f, size.y, 0f);
        Vector3 alongX = new(size.x, 0f, 0f);
        Vector3 alongZ = new(0f, 0f, size.z);

        AddFace(WallSubmesh, new Vector3(max.x, min.y, min.z), alongZ, up, wallTextureSize);
        AddFace(WallSubmesh, min, up, alongZ, wallTextureSize);
        AddFace(WallSubmesh, new Vector3(min.x, min.y, max.z), up, alongX, wallTextureSize);
        AddFace(WallSubmesh, min, alongX, up, wallTextureSize);
    }

    private void AddFace(int submesh, Vector3 corner, Vector3 u, Vector3 v, float textureSize)
    {
        Vector3 normal = Vector3.Cross(v, u).normalized;
        int start = _vertices.Count;

        AddVertex(corner, normal, textureSize);
        AddVertex(corner + v, normal, textureSize);
        AddVertex(corner + u + v, normal, textureSize);
        AddVertex(corner + u, normal, textureSize);

        List<int> triangles = _triangles[submesh];
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private void AddVertex(Vector3 position, Vector3 normal, float textureSize)
    {
        Vector3 world = position + _chunkOrigin;
        Vector2 uv = Mathf.Abs(normal.y) > 0.5f
            ? new Vector2(world.x, world.z)
            : new Vector2(Vector3.Dot(world, Vector3.Cross(normal, Vector3.up)), world.y);

        _vertices.Add(position);
        _normals.Add(normal);
        _uvs.Add(uv / textureSize);
    }

    private float WallDensity(int x, int z)
    {
        float noise = Mathf.PerlinNoise(x * roomNoiseScale + _noiseOffset.x, z * roomNoiseScale + _noiseOffset.y);
        return wallChance * Mathf.Lerp(0.15f, 1.85f, noise);
    }

    private bool HasWestWall(int x, int z)
    {
        if (z == 0 && (x == 0 || x == 1)) return false;
        return Hash(x, z, 1) < WallDensity(x, z);
    }

    private bool HasSouthWall(int x, int z)
    {
        if (x == 0 && (z == 0 || z == 1)) return false;
        return Hash(x, z, 2) < WallDensity(x, z);
    }

    private bool HasPillar(int x, int z)
    {
        if ((x == 0 || x == 1) && (z == 0 || z == 1)) return false;
        return Hash(x, z, 4) < pillarChance;
    }

    private bool HasPanel(int x, int z)
    {
        if (Mod(x, panelSpacing) != 0 || Mod(z, panelSpacing) != 0) return false;
        if (x == 0 && z == 0) return true;
        return Hash(x, z, 5) >= brokenPanelChance;
    }

    private static int Mod(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private float Hash(int x, int z, int salt)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9e3779b9u;
            h ^= (uint)x * 0x8da6b343u;
            h ^= (uint)z * 0xd8163841u;
            h ^= (uint)salt * 0xcb1ab31fu;
            h = (h ^ (h >> 16)) * 0x7feb352du;
            h = (h ^ (h >> 15)) * 0x846ca68bu;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
