using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class MapGenerator : MonoBehaviour
{
    [Header("Terrain Dimensions")]
    public int width = 10000;
    public int height = 10000;
    public float terrainScale = 20000f;  // Taille réelle du terrain en unités Unity
    
    [Header("Perlin Noise Settings")]
    public float noiseScale = 20f;  // Échelle du bruit (plus petit = plus de détails)
    public int octaves = 4;  // Nombre de couches de bruit
    public float persistence = 0.5f;  // Diminution d'amplitude entre octaves
    public float lacunarity = 2f;  // Augmentation de fréquence entre octaves
    public float heightMultiplier = 10f;  // Hauteur maximale des montagnes
    public AnimationCurve heightCurve = AnimationCurve.Linear(0, 0, 1, 1);  // Courbe de redistribution des hauteurs
    
    [Header("Seed & Offset")]
    public int seed = 0;
    public Vector2 offset = Vector2.zero;
    public bool randomizeSeed = true;
    
    [Header("Material & Colors")]
    public Material terrainMaterial;
    public Gradient terrainGradient;
    public bool autoGenerateOnStart = true;
    
    [Header("Mesh Settings")]
    public bool generateCollider = true;
    public bool smoothShading = false;
    
    private Mesh terrainMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private float[,] noiseMap;
    
    void Start()
    {
        // Récupérer les composants
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        
        // Initialiser le gradient si non défini
        if (terrainGradient == null || terrainGradient.colorKeys.Length == 0)
        {
            InitializeDefaultGradient();
        }
        
        // Générer automatiquement au démarrage si activé
        if (autoGenerateOnStart)
        {
            GenerateTerrain();
        }
    }
    
    void InitializeDefaultGradient()
    {
        terrainGradient = new Gradient();
        GradientColorKey[] colorKeys = new GradientColorKey[5];
        colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.3f, 0.6f), 0.0f);  // Eau profonde
        colorKeys[1] = new GradientColorKey(new Color(0.9f, 0.85f, 0.7f), 0.3f);  // Plage
        colorKeys[2] = new GradientColorKey(new Color(0.3f, 0.5f, 0.2f), 0.4f);  // Herbe
        colorKeys[3] = new GradientColorKey(new Color(0.5f, 0.4f, 0.3f), 0.7f);  // Roche
        colorKeys[4] = new GradientColorKey(new Color(1f, 1f, 1f), 1.0f);  // Neige
        
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
        alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);
        
        terrainGradient.SetKeys(colorKeys, alphaKeys);
    }
    
    public void GenerateTerrain()
    {
        // Randomiser le seed si nécessaire
        if (randomizeSeed)
        {
            seed = Random.Range(0, 100000);
        }
        
        // Générer la carte de hauteurs
        noiseMap = GenerateNoiseMap();
        
        // Créer le mesh
        terrainMesh = GenerateMesh(noiseMap);
        
        // Appliquer le mesh
        meshFilter.mesh = terrainMesh;
        
        // Générer le collider si nécessaire
        if (generateCollider && meshCollider != null)
        {
            meshCollider.sharedMesh = terrainMesh;
        }
        
        // Appliquer le matériau
        if (terrainMaterial != null)
        {
            meshRenderer.material = terrainMaterial;
        }
    }
    
    float[,] GenerateNoiseMap()
    {
        float[,] noise = new float[width, height];
        
        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];
        
        // Générer des offsets aléatoires pour chaque octave
        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }
        
        float maxNoiseHeight = float.MinValue;
        float minNoiseHeight = float.MaxValue;
        
        // Centrer l'échelle du bruit
        float halfWidth = width / 2f;
        float halfHeight = height / 2f;
        
        // Générer les valeurs de bruit
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;
                
                // Calculer la valeur de bruit pour chaque octave
                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (x - halfWidth) / noiseScale * frequency + octaveOffsets[i].x;
                    float sampleY = (y - halfHeight) / noiseScale * frequency + octaveOffsets[i].y;
                    
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;
                    
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }
                
                // Stocker les valeurs min et max pour la normalisation
                if (noiseHeight > maxNoiseHeight)
                    maxNoiseHeight = noiseHeight;
                else if (noiseHeight < minNoiseHeight)
                    minNoiseHeight = noiseHeight;
                    
                noise[x, y] = noiseHeight;
            }
        }
        
        // Normaliser les valeurs entre 0 et 1
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalizedHeight = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, noise[x, y]);
                // Appliquer la courbe de hauteur pour plus de contrôle
                noise[x, y] = heightCurve.Evaluate(normalizedHeight);
            }
        }
        
        return noise;
    }
    
    Mesh GenerateMesh(float[,] heightMap)
    {
        int vertexIndex = 0;
        
        // Créer les données du mesh
        Vector3[] vertices = new Vector3[width * height];
        Vector2[] uvs = new Vector2[width * height];
        int[] triangles = new int[(width - 1) * (height - 1) * 6];
        Color[] colors = new Color[width * height];
        
        // Facteurs d'échelle
        float scaleX = terrainScale / (float)(width - 1);
        float scaleZ = terrainScale / (float)(height - 1);
        
        // Générer les vertices, UVs et couleurs
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float heightValue = heightMap[x, y];
                
                // Position du vertex
                vertices[vertexIndex] = new Vector3(
                    x * scaleX - terrainScale / 2f,
                    heightValue * heightMultiplier,
                    y * scaleZ - terrainScale / 2f
                );
                
                // UV mapping
                uvs[vertexIndex] = new Vector2((float)x / (width - 1), (float)y / (height - 1));
                
                // Couleur basée sur la hauteur
                colors[vertexIndex] = terrainGradient.Evaluate(heightValue);
                
                vertexIndex++;
            }
        }
        
        // Générer les triangles
        int triangleIndex = 0;
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                int topLeft = y * width + x;
                int topRight = topLeft + 1;
                int bottomLeft = (y + 1) * width + x;
                int bottomRight = bottomLeft + 1;
                
                // Premier triangle
                triangles[triangleIndex] = topLeft;
                triangles[triangleIndex + 1] = bottomLeft;
                triangles[triangleIndex + 2] = topRight;
                
                // Second triangle
                triangles[triangleIndex + 3] = topRight;
                triangles[triangleIndex + 4] = bottomLeft;
                triangles[triangleIndex + 5] = bottomRight;
                
                triangleIndex += 6;
            }
        }
        
        // Créer et configurer le mesh
        Mesh mesh = new Mesh();
        mesh.name = "Procedural Terrain";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;
        
        // Recalculer les normales et les bounds
        if (smoothShading)
        {
            mesh.RecalculateNormals();
            SmoothNormals(mesh);
        }
        else
        {
            mesh.RecalculateNormals();
        }
        
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        
        return mesh;
    }
    
    void SmoothNormals(Mesh mesh)
    {
        // Récupérer les données du mesh
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;
        
        // Dictionnaire pour stocker les normales moyennées par position de vertex
        Dictionary<Vector3, Vector3> smoothedNormals = new Dictionary<Vector3, Vector3>();
        
        // Calculer les normales moyennées
        for (int i = 0; i < vertices.Length; i++)
        {
            if (!smoothedNormals.ContainsKey(vertices[i]))
            {
                smoothedNormals.Add(vertices[i], normals[i]);
            }
            else
            {
                smoothedNormals[vertices[i]] = (smoothedNormals[vertices[i]] + normals[i]).normalized;
            }
        }
        
        // Appliquer les normales moyennées
        for (int i = 0; i < vertices.Length; i++)
        {
            normals[i] = smoothedNormals[vertices[i]];
        }
        
        mesh.normals = normals;
    }
    
    // Méthode pour régénérer le terrain depuis l'Inspector
    void OnValidate()
    {
        if (width < 1) width = 1;
        if (height < 1) height = 1;
        if (lacunarity < 1) lacunarity = 1;
        if (octaves < 0) octaves = 0;
        if (noiseScale <= 0) noiseScale = 0.001f;
        if (terrainScale <= 0) terrainScale = 1f;
    }
    
    // Méthode publique pour régénérer depuis d'autres scripts
    public void RegenerateTerrain()
    {
        GenerateTerrain();
    }
    
    // Obtenir la hauteur à une position donnée (utile pour placer des objets)
    public float GetHeightAtPosition(float x, float z)
    {
        // Convertir la position mondiale en indices de grille
        x += terrainScale / 2f;
        z += terrainScale / 2f;
        
        int gridX = Mathf.RoundToInt((x / terrainScale) * (width - 1));
        int gridZ = Mathf.RoundToInt((z / terrainScale) * (height - 1));
        
        // Vérifier les limites
        gridX = Mathf.Clamp(gridX, 0, width - 1);
        gridZ = Mathf.Clamp(gridZ, 0, height - 1);
        
        return noiseMap[gridX, gridZ] * heightMultiplier + transform.position.y;
    }
}