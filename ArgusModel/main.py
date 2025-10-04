import torch


device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
model_path = "yolov8n-military/weights/best.pt"


def export_model(path: str):
    from ultralytics import YOLO

    model = YOLO(path)

    # Export optimisé pour Sentis
    model.export(
        format='onnx',
        imgsz=640,  # ou 416 pour plus de performance
        simplify=True,
        opset=15,  # Sentis supporte jusqu'à opset 15
        batch=1,  # Batch size fixe de 1
        dynamic=False  # Pas de dimensions dynamiques
    )

if __name__ == "__main__":
    print(f"Using device: {device}")
    export_model(model_path)