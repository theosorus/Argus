import cv2
import numpy as np
import onnxruntime as ort

def preprocess_image(image_path, input_size=(640, 640)):
    """Prétraiter l'image pour YOLOv8"""
    # Charger l'image
    image = cv2.imread(image_path)
    if image is None:
        raise ValueError("Image non trouvée ou format non supporté")
    
    # Sauvegarder les dimensions originales
    original_shape = image.shape[:2]
    
    # Redimensionner et normaliser
    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    resized = cv2.resize(image_rgb, input_size)
    input_image = resized.astype(np.float32) / 255.0  # Normalisation [0,1] et conversion en float32
    input_image = np.transpose(input_image, (2, 0, 1))  # HWC -> CHW
    input_image = np.expand_dims(input_image, axis=0)  # Ajouter batch dimension
    
    return input_image, image, original_shape

def postprocess_yolov8(outputs, original_shape, input_size=(640, 640), conf_thres=0.5, nms_thres=0.4):
    """
    Post-traitement pour YOLOv8
    YOLOv8 output shape: [1, 84, 8400] où:
    - 84 = 4 (bbox) + 80 (classes) pour COCO, ou 4 + num_classes pour custom
    - 8400 = nombre de prédictions
    """

    predictions = outputs[0]  # Shape: [1, 5, 8400] pour 1 classe
    
    predictions = np.transpose(predictions, (0, 2, 1))[0]  # Shape: [8400, 5]

    
    # Extraire les boxes et scores
    boxes = predictions[:, :4]  # x_center, y_center, width, height
    scores = predictions[:, 4:]  # scores pour chaque classe
    
    # Si une seule classe, reshaper les scores
    if scores.shape[1] == 1:
        class_ids = np.zeros(len(scores), dtype=int)
        confidences = scores.flatten()
    else:
        # Obtenir la classe avec le score maximum pour chaque détection
        class_ids = np.argmax(scores, axis=1)
        confidences = np.max(scores, axis=1)
    
    # Filtrer par seuil de confiance
    mask = confidences > conf_thres
    boxes = boxes[mask]
    confidences = confidences[mask]
    class_ids = class_ids[mask]
    
    if len(boxes) == 0:
        return [], [], []
    
    # Convertir de (x_center, y_center, w, h) à (x1, y1, x2, y2)
    boxes_xyxy = np.zeros_like(boxes)
    boxes_xyxy[:, 0] = boxes[:, 0] - boxes[:, 2] / 2  # x1
    boxes_xyxy[:, 1] = boxes[:, 1] - boxes[:, 3] / 2  # y1
    boxes_xyxy[:, 2] = boxes[:, 0] + boxes[:, 2] / 2  # x2
    boxes_xyxy[:, 3] = boxes[:, 1] + boxes[:, 3] / 2  # y2
    
    # Calculer les facteurs d'échelle
    scale_x = original_shape[1] / input_size[0]
    scale_y = original_shape[0] / input_size[1]
    
    # Mettre à l'échelle les boîtes
    boxes_xyxy[:, [0, 2]] *= scale_x
    boxes_xyxy[:, [1, 3]] *= scale_y
    
    # Appliquer NMS (Non-Maximum Suppression)
    indices = nms(boxes_xyxy, confidences, nms_thres)
    
    # Convertir en entiers et limiter aux dimensions de l'image
    final_boxes = []
    final_confidences = []
    final_classes = []
    
    for i in indices:
        x1 = int(max(0, boxes_xyxy[i, 0]))
        y1 = int(max(0, boxes_xyxy[i, 1]))
        x2 = int(min(original_shape[1], boxes_xyxy[i, 2]))
        y2 = int(min(original_shape[0], boxes_xyxy[i, 3]))
        
        final_boxes.append([x1, y1, x2, y2])
        final_confidences.append(float(confidences[i]))
        final_classes.append(int(class_ids[i]))
    
    return final_boxes, final_confidences, final_classes

def nms(boxes, scores, threshold):
    """Non-Maximum Suppression"""
    x1 = boxes[:, 0]
    y1 = boxes[:, 1]
    x2 = boxes[:, 2]
    y2 = boxes[:, 3]
    
    areas = (x2 - x1) * (y2 - y1)
    order = scores.argsort()[::-1]
    
    keep = []
    while order.size > 0:
        i = order[0]
        keep.append(i)
        
        xx1 = np.maximum(x1[i], x1[order[1:]])
        yy1 = np.maximum(y1[i], y1[order[1:]])
        xx2 = np.minimum(x2[i], x2[order[1:]])
        yy2 = np.minimum(y2[i], y2[order[1:]])
        
        w = np.maximum(0.0, xx2 - xx1)
        h = np.maximum(0.0, yy2 - yy1)
        inter = w * h
        
        ovr = inter / (areas[i] + areas[order[1:]] - inter)
        inds = np.where(ovr <= threshold)[0]
        order = order[inds + 1]
    
    return keep

def draw_boxes(image, boxes, confidences, classes, class_names=None):
    """Dessiner les boîtes de détection sur l'image"""
    # Si pas de noms de classes fournis, utiliser des numéros
    if class_names is None:
        class_names = [f"Class_{i}" for i in range(100)]
    
    # Couleurs pour différentes classes
    colors = [(255, 0, 0)]
    
    for box, conf, cls in zip(boxes, confidences, classes):
        x1, y1, x2, y2 = box
        color = colors[cls % len(colors)]
        
        # Dessiner le rectangle
        cv2.rectangle(image, (x1, y1), (x2, y2), color, 2)
        
        # Préparer le label
        label = f"{class_names[cls] if cls < len(class_names) else f'Class_{cls}'}: {conf:.2f}"
        
        # Calculer la taille du texte pour le fond
        (text_width, text_height), baseline = cv2.getTextSize(
            label, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 2
        )
        
        # Dessiner le fond du texte
        cv2.rectangle(
            image, 
            (x1, y1 - text_height - baseline - 5),
            (x1 + text_width, y1),
            color, 
            cv2.FILLED
        )
        
        # Dessiner le texte
        cv2.putText(
            image, 
            label, 
            (x1, y1 - 5), 
            cv2.FONT_HERSHEY_SIMPLEX, 
            0.5, 
            (255, 255, 255), 
            2
        )
    
    return image

def run_inference(onnx_model_path, image_path, conf_thres=0.5, nms_thres=0.4, class_names=None):
    """Fonction principale pour exécuter l'inférence"""
    print(f"Chargement du modèle ONNX: {onnx_model_path}")
    
    # Charger le modèle ONNX
    sess = ort.InferenceSession(onnx_model_path)
    
    # Obtenir les informations sur les entrées du modèle
    input_name = sess.get_inputs()[0].name
    input_shape = sess.get_inputs()[0].shape
    print(f"Nom de l'entrée: {input_name}")
    print(f"Shape de l'entrée: {input_shape}")
    
    # Déterminer la taille d'entrée
    input_size = (input_shape[2], input_shape[3]) if len(input_shape) == 4 else (640, 640)
    
    # Prétraiter l'image
    print(f"Prétraitement de l'image: {image_path}")
    input_image, original_image, original_shape = preprocess_image(image_path, input_size)
    
    # Faire l'inférence
    print("Exécution de l'inférence...")
    outputs = sess.run(None, {input_name: input_image})  # Utiliser input_image directement
    
    # Afficher la shape de la sortie pour debug
    print(f"Shape de la sortie: {outputs[0].shape}")
    
    # Post-traiter les résultats
    print("Post-traitement des résultats...")
    boxes, confidences, classes = postprocess_yolov8(
        outputs, original_shape, input_size, conf_thres, nms_thres
    )
    
    print(f"Nombre de détections: {len(boxes)}")
    
    # Dessiner les boîtes sur l'image
    result_image = draw_boxes(original_image.copy(), boxes, confidences, classes, class_names)
    
    return result_image, boxes, confidences, classes

# Exemple d'utilisation
if __name__ == "__main__":
    # Configuration
    onnx_model_path = "yolov8n-military/weights/best.onnx"
    image_path = "data/sample_game.png"

    class_names = ["Plane"]  # Adapter selon tes classes
    
    # Paramètres de détection
    conf_threshold = 0.5  # Seuil de confiance
    nms_threshold = 0.4   # Seuil pour NMS
    
    try:
        # Exécuter l'inférence
        result_image, boxes, confidences, classes = run_inference(
            onnx_model_path, 
            image_path, 
            conf_threshold,
            nms_threshold,
            class_names
        )
        
        # Afficher les détections
        for i, (box, conf, cls) in enumerate(zip(boxes, confidences, classes)):
            class_name = class_names[cls] if cls < len(class_names) else f"Class_{cls}"
            print(f"Détection {i+1}: {class_name} (conf: {conf:.3f}) - Box: {box}")
        
        # Afficher le résultat
        cv2.imshow("Résultat YOLOv8", result_image)
        print("\nAppuie sur une touche pour fermer la fenêtre...")
        cv2.waitKey(0)
        cv2.destroyAllWindows()
        
        # Sauvegarder le résultat
        output_path = "resultat_yolov8.jpg"
        cv2.imwrite(output_path, result_image)
        print(f"Résultat sauvegardé: {output_path}")
        
    except Exception as e:
        print(f"Erreur: {e}")
        import traceback
        traceback.print_exc()