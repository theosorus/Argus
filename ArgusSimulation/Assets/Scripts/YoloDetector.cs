using UnityEngine;
using Unity.Sentis;
using System.Collections.Generic;

public class YoloDetector : MonoBehaviour
{
    [Header("Model")]
    public ModelAsset modelAsset;
    private Worker worker;

    private string[] classNames = { "Plane" };

    [Header("Camera")]
    public Camera cam;
    private RenderTexture renderTexture;

    [Header("Settings")]
    public int inputSize = 640;
    public float confidenceThreshold = 0.9f;

    [Header("Missile Control")]
    public float rotationSpeed = 2f;  // Vitesse de rotation du missile
    public float moveSpeed = 10f;     // Vitesse de déplacement du missile

    [Header("Visualization")]
    public bool drawBoxes = true;
    public Color boxColor = Color.green;
    public float boxThickness = 3f;
    public bool showModelInput = true;
    public bool showLabels = true;  // Nouveau paramètre pour activer/désactiver les labels
    public Color labelColor = Color.white;  // Couleur du texte
    public Color labelBackgroundColor = new Color(0, 0, 0, 0.7f);  // Couleur de fond du label

    private Tensor<float> inputTensor;
    private readonly List<Detection> detections = new List<Detection>();  // Changé pour stocker plus d'infos
    private Texture2D lineTexture;
    private GUIStyle labelStyle;  // Style pour le texte

    private class Detection
    {
        public Rect box;
        public float confidence;
        public int classIndex;
        public string className;
    }

    void Start()
    {
        // load model and create GPU worker
        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, BackendType.GPUCompute);

        renderTexture = new RenderTexture(inputSize, inputSize, 0);

        // Input tensor : 1 batch, 3 channels, WxH
        inputTensor = new Tensor<float>(new TensorShape(1, 3, inputSize, inputSize));

        // Texture 1x1 
        lineTexture = new Texture2D(1, 1);
        lineTexture.SetPixel(0, 0, Color.white);
        lineTexture.Apply();

        // Initialiser le style pour les labels
        InitializeLabelStyle();
    }

    void InitializeLabelStyle()
    {
        labelStyle = new GUIStyle();
        labelStyle.normal.textColor = labelColor;
        labelStyle.fontSize = 14;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleLeft;
        labelStyle.padding = new RectOffset(4, 4, 2, 2);
    }

    void Update()
    {
        // IMPORTANT : On continue TOUJOURS le rendu et la détection
        // même si la caméra n'est pas active (pour le guidage du missile)
        
        // Sauvegarder l'état actuel de la caméra
        RenderTexture previousTarget = cam.targetTexture;
        bool wasCameraEnabled = cam.enabled;
        
        // Forcer l'activation temporaire de la caméra pour le rendu si elle est désactivée
        if (!cam.enabled)
        {
            cam.enabled = true;
        }

        // Rendu de la caméra dans le RenderTexture carré
        cam.targetTexture = renderTexture;
        cam.Render();
        cam.targetTexture = previousTarget;
        
        // Restaurer l'état original de la caméra
        if (!wasCameraEnabled)
        {
            cam.enabled = false;
        }

        // Convertir la RT en tensor [0..1], NCHW
        TextureConverter.ToTensor(renderTexture, inputTensor, new TextureTransform());

        // Exécuter le modèle (asynchrone)
        worker.Schedule(inputTensor);

        // Récupérer la sortie
        var outputTensor = worker.PeekOutput() as Tensor<float>;
        if (outputTensor != null)
        {
            ProcessDetections(outputTensor);
        }

        // Mettre à jour le missile après la détection (TOUJOURS actif)
        UpdateMissile();
    }

    void ProcessDetections(Tensor<float> output)
    {
        var cpuOutput = output.ReadbackAndClone();

        detections.Clear();

        // Formes fréquentes : [1,5,8400], [1,8400,5], ou encore [1,84,8400] (YOLOv8 COCO).
        int dim0 = cpuOutput.shape[0];
        int dim1 = cpuOutput.shape[1];
        int dim2 = cpuOutput.shape[2];

        // [1, 5, 8400]
        int numDetections = dim2;

        // On mappe vers un carré centré dans le pixelRect de la caméra
        Rect pr = cam.pixelRect;
        Rect sq = GetSquareRect(pr);
        float s = sq.width / (float)inputSize;   // même scale en X et Y

        for (int i = 0; i < numDetections; i++)
        {
            float x, y, w, h, confidence;

            x = cpuOutput[0, 0, i];
            y = cpuOutput[0, 1, i];
            w = cpuOutput[0, 2, i];
            h = cpuOutput[0, 3, i];
            confidence = cpuOutput[0, 4, i];

            if (confidence < confidenceThreshold) continue;

            // Coordonnées YOLO (centre) → coin haut-gauche dans l'espace 640x640
            float x_tl = x - w * 0.5f;
            float y_tl = y - h * 0.5f;

            // Mise à l'échelle uniforme vers le carré sq
            float width = w * s;
            float height = h * s;
            float left = sq.x + x_tl * s;

            // Conversion en repère OnGUI (0,0 en haut-gauche de l'écran).
            float top = (Screen.height - (sq.y + sq.height)) + y_tl * s;

            // Créer une détection avec toutes les infos
            Detection det = new Detection
            {
                box = new Rect(left, top, width, height),
                confidence = confidence,
                classIndex = 0,  // Dans votre cas, vous n'avez qu'une classe "Plane"
                className = classNames[0]  // "Plane"
            };

            detections.Add(det);
        }

        cpuOutput.Dispose();
    }

    void OnGUI()
    {
        // MODIFICATION IMPORTANTE : On n'affiche l'overlay QUE si la caméra est active
        // Cela empêche l'affichage parasite sur les autres vues
        if (!cam.gameObject.activeInHierarchy || !cam.enabled) return;

        if (showModelInput && renderTexture != null)
        {
            Rect pr = cam.pixelRect;
            Rect sq = GetSquareRect(pr);
            GUI.DrawTexture(sq, renderTexture, ScaleMode.StretchToFill);
        }

        if (!drawBoxes || detections.Count == 0) return;

        // Dessiner les boîtes et les labels
        foreach (var detection in detections)
        {
            // Dessiner la boîte
            GUI.color = boxColor;
            DrawBox(detection.box);
            
            // Dessiner le label si activé
            if (showLabels)
            {
                DrawLabel(detection);
            }
        }
        
        GUI.color = Color.white;
    }

    void DrawBox(Rect rect)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, boxThickness), lineTexture);                             // haut
        GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - boxThickness, rect.width, boxThickness), lineTexture); // bas
        GUI.DrawTexture(new Rect(rect.x, rect.y, boxThickness, rect.height), lineTexture);                             // gauche
        GUI.DrawTexture(new Rect(rect.x + rect.width - boxThickness, rect.y, boxThickness, rect.height), lineTexture); // droite
    }

    void DrawLabel(Detection detection)
    {
        // Préparer le texte avec le nom de la classe et la confiance
        string labelText = $"{detection.className} {(detection.confidence * 100f):F1}%";
        
        // Calculer la taille du texte
        Vector2 labelSize = labelStyle.CalcSize(new GUIContent(labelText));
        
        // Position du label (au-dessus de la boîte)
        float labelX = detection.box.x;
        float labelY = detection.box.y - labelSize.y - 5; // 5 pixels au-dessus de la boîte
        
        // Si le label sort de l'écran en haut, le mettre à l'intérieur de la boîte
        if (labelY < 0)
        {
            labelY = detection.box.y + 5;
        }
        
        Rect labelRect = new Rect(labelX, labelY, labelSize.x + 8, labelSize.y + 4);
        
        // Dessiner le fond du label
        Color oldColor = GUI.color;
        GUI.color = labelBackgroundColor;
        GUI.DrawTexture(labelRect, lineTexture);
        
        // Dessiner le texte
        GUI.color = labelColor;
        GUI.Label(labelRect, labelText, labelStyle);
        
        GUI.color = oldColor;
    }

    Rect GetSquareRect(Rect pr)
    {
        float side = Mathf.Min(pr.width, pr.height);
        float x = pr.x + (pr.width - side) * 0.5f;
        float y = pr.y + (pr.height - side) * 0.5f;
        return new Rect(x, y, side, side);
    }

    void OnDestroy()
    {
        inputTensor?.Dispose();
        worker?.Dispose();

        if (renderTexture != null)
            renderTexture.Release();

        if (lineTexture != null)
            Destroy(lineTexture);
    }

    void UpdateMissile()
    {
        // Le guidage du missile continue TOUJOURS, même si la caméra n'est pas active
        if (detections.Count == 0) return;

        // Prendre la première détection
        Detection targetDetection = detections[0];
        
        // Calculer le centre de la boîte détectée (en pixels écran)
        Vector2 screenCenter = GetCenter(targetDetection.box);
        
        // Convertir en coordonnées normalisées [0,1]
        float normalizedX = screenCenter.x / Screen.width;
        float normalizedY = 1f - (screenCenter.y / Screen.height); // Inverser Y car OnGUI a Y inversé
        
        // Convertir en offset par rapport au centre de l'écran [-1, 1]
        float offsetX = (normalizedX - 0.5f) * 2f;
        float offsetY = (normalizedY - 0.5f) * 2f;
        
        // Appliquer la rotation au missile
        UpdateMissileTrajectory(offsetX, offsetY);
    }

    Vector2 GetCenter(Rect rect)
    {
        return new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
    }
    
    void UpdateMissileTrajectory(float offsetX, float offsetY)
    {
        // Rotation x
        transform.Rotate(0, offsetX * rotationSpeed * Time.deltaTime * 60f, 0);
        
        // Rotation y
        transform.Rotate(-offsetY * rotationSpeed * Time.deltaTime * 60f, 0, 0);
    }
}