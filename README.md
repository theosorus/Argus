# 🎯 Argus - Vision-Guided Missile Simulation

<div align="center">
<img src="assets/argus_gif.gif" width="600">
</div>

## 🌟 Overview

**Argus** is an advanced simulation that combines **machine learning** and **real-time 3D physics** to create a self-guided missile system. The project demonstrates how computer vision can be integrated into game engines for autonomous target tracking and interception.

Named after the **Argus Panoptes** from Greek mythology—a giant with a hundred eyes—this project embodies the concept of constant visual surveillance and intelligent tracking.

---

## Vision Model

### 🔬 Dataset & Training

- **Dataset**: [Kaggle Military Aircraft Detection Dataset](https://www.kaggle.com/datasets/a2015003713/militaryaircraftdetectiondataset)
  - All aircraft types merged into a single **"Plane"** class for maximum accuracy
- **GPU**: Kaggle P100 - Training time: ~10 minutes
- **Architecture**: YOLOv8n (nano variant for real-time performance)

### 🤖 Model Specifications

- **Input Size**: 640×640 RGB
- **Output**: Bounding boxes `[x, y, w, h, confidence]`
- **Inference Time**: ~16-30ms (GPU)
- **FPS**: 30-60
- **Confidence Threshold**: 0.9

### 📦 ONNX Export for Unity

```python
# main.py - Optimized export for Unity Sentis
model.export(
    format='onnx',
    imgsz=640,
    simplify=True,
    opset=15,        # Sentis compatibility
    batch=1,         # Fixed batch size
    dynamic=False    # Static shapes for performance
)
```

---

## Simulation

### 🎮 Unity Implementation

**Engine**: Unity 2023.2.20f1

The Unity simulation integrates the trained YOLOv8 model with realistic physics to create an autonomous missile guidance system.

### 🧩 3D Assets

- **Source**: Sketchfab For Unity
- **VFX**: HQ Explosions Pack (Free)

### 🔧 Use YOLO Model in Unity

To use a YOLO model in Unity, I used a package named **Sentis** (`com.unity.sentis: 2.1.3`). The model is exported in ONNX format and runs on GPU using Unity's neural network inference engine.

**Key Features**:
- Runs YOLOv8 model on GPU using Unity Sentis (BackendType.GPUCompute)
- Processes camera feed → 640×640 tensor input
- Detects aircraft in real-time (60 FPS)
- Continues detection even when camera is not actively displayed

### ✈️ Physics Implementation

#### Realistic Flight Dynamics

**PlaneController.cs** - Aerodynamic model with:
- Lift/drag calculations based on air density, wing area, angle of attack (AoA)
- Coefficients: Cl0 (camber lift), ClAlpha (lift slope), Cd0 (profile drag), induced drag
- Manual controls: Space (throttle up), Left Control (throttle down), Arrow keys (pitch/roll)

| Parameter | Default Value |
|-----------|---------------|
| Air Density | 1.225 kg/m³ |
| Wing Area | 16 m² |
| Max Thrust | 190 N |
| Lift Slope (ClAlpha) | 5.5 |
| Induced Drag (k) | 0.04 |

#### Self-Guidance System

**YoloDetector.cs** - The heart of the guidance system:

<div align="center">
<img src="assets/missile_camera_assets.png" width="500">
</div>

**How Guidance Works**:
1. Camera captures view → 640×640 render texture
2. Sentis runs YOLOv8 inference → detections `[x, y, w, h, confidence]`
3. Target center computed in screen space
4. Offset calculated: `(targetX - screenCenterX, targetY - screenCenterY)`
5. Missile rotates toward target: yaw ∝ offsetX, pitch ∝ offsetY

**Proportional Navigation Algorithm**:
1. **Detection**: YOLOv8 finds target bounding box `[x, y, w, h]`
2. **Center Calculation**: `targetCenter = (x + w/2, y + h/2)`
3. **Screen Offset**: `offset = targetCenter - screenCenter`
4. **Normalize**: `offsetNormalized = offset / screenSize × 2` → range `[-1, 1]`
5. **Apply Rotation**:
   ```csharp
   yaw += offsetX × rotationSpeed × deltaTime
   pitch += offsetY × rotationSpeed × deltaTime
   ```

**The Complete Pipeline**:
```
[Camera Feed] → [640×640 Tensor] → [YOLOv8 Sentis] → [Detections]
     ↑                                                      ↓
     └──[Missile Rotation]←[Proportional Navigation]←[Offset Calculation]
```

---

## 🚀 Improvements

For the future, I can imagine:
- [ ] **Hybrid Model**: Combine detection and tracking (e.g., YOLO + Kalman filtering)
- [ ] **Better Physics**: Add wind resistance, thrust vectoring, fuel consumption
- [ ] **Enhanced UI**: HUD with radar, lock indicators, target information
- [ ] **Camera Features**: Advanced camera modes and replay system
- [ ] **Multi-Target**: Track and prioritize multiple aircraft
- [ ] **Multiplayer**: Network-based dogfighting simulation

---

## 📚 Acknowledgments

### Learning Resources
- **Plane Physics Tutorials**:
  - [Unity Flight Physics Part 1](https://www.youtube.com/watch?v=fThb5M2OBJ8)
  - [Unity Flight Physics Part 2](https://www.youtube.com/watch?v=7vAHo2B1zLc)

### Technologies & Tools
- **YOLOv8**: [Ultralytics](https://github.com/ultralytics/ultralytics)
- **Unity Sentis**: [Unity ML Inference](https://unity.com/products/sentis)
- **Dataset**: [Kaggle Military Aircraft Detection](https://www.kaggle.com/datasets/a2015003713/militaryaircraftdetectiondataset)