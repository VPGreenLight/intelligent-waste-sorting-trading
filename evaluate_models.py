import os
import sys
import time
from pathlib import Path
from PIL import Image

# Đảm bảo in UTF-8 không lỗi trên Windows
if sys.stdout and hasattr(sys.stdout, 'reconfigure'):
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

import torch
import torch.nn as nn
from torchvision import transforms, models
from ultralytics import YOLO

DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")
BASE_DIR = Path(__file__).resolve().parent
VAL_DIR = BASE_DIR / "data" / "trashnet_split" / "val"
MODELS_DIR = BASE_DIR / "models"

CLASS_NAMES = ["cardboard", "glass", "metal", "paper", "plastic", "trash"]

TORCH_TRANSFORMS = transforms.Compose([
    transforms.Resize(256),
    transforms.CenterCrop(224),
    transforms.ToTensor(),
    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225])
])

def evaluate_yolo(model_path):
    if not model_path.exists():
        return None
    model = YOLO(str(model_path))
    correct = 0
    total = 0
    total_time = 0.0

    class_correct = {c: 0 for c in CLASS_NAMES}
    class_total = {c: 0 for c in CLASS_NAMES}

    for true_class in CLASS_NAMES:
        folder = VAL_DIR / true_class
        if not folder.exists():
            continue
        for img_path in folder.glob("*.*"):
            if img_path.suffix.lower() not in [".jpg", ".jpeg", ".png", ".webp"]:
                continue
            
            t0 = time.perf_counter()
            results = model(str(img_path), verbose=False)
            t1 = time.perf_counter()
            total_time += (t1 - t0)

            top1_idx = results[0].probs.top1
            pred_class = results[0].names[top1_idx].lower().strip()

            total += 1
            class_total[true_class] += 1
            if pred_class == true_class:
                correct += 1
                class_correct[true_class] += 1

    acc = (correct / total) * 100 if total > 0 else 0
    avg_latency = (total_time / total) * 1000 if total > 0 else 0
    file_size_mb = model_path.stat().st_size / (1024 * 1024)

    return {
        "name": "YOLOv8n-cls",
        "family": "CNN (Ultralytics)",
        "params": "1.44M",
        "accuracy": acc,
        "latency_ms": avg_latency,
        "size_mb": file_size_mb,
        "class_acc": {c: (class_correct[c]/class_total[c])*100 if class_total[c]>0 else 0 for c in CLASS_NAMES}
    }

def evaluate_torchvision(model_type, model_path):
    if not model_path.exists():
        return None

    if model_type == "mobilenet_v3":
        model = models.mobilenet_v3_small(weights=None)
        model.classifier[3] = nn.Linear(model.classifier[3].in_features, len(CLASS_NAMES))
        name = "MobileNetV3-Small"
        family = "Depthwise Separable CNN"
        params = "2.54M"
    elif model_type == "efficientnet_b0":
        model = models.efficientnet_b0(weights=None)
        model.classifier[1] = nn.Linear(model.classifier[1].in_features, len(CLASS_NAMES))
        name = "EfficientNet-B0"
        family = "Compound Scaling CNN"
        params = "5.29M"
    else:
        return None

    checkpoint = torch.load(model_path, map_location=DEVICE)
    model.load_state_dict(checkpoint['model_state_dict'])
    model = model.to(DEVICE)
    model.eval()

    correct = 0
    total = 0
    total_time = 0.0

    class_correct = {c: 0 for c in CLASS_NAMES}
    class_total = {c: 0 for c in CLASS_NAMES}

    with torch.no_grad():
        for true_class in CLASS_NAMES:
            folder = VAL_DIR / true_class
            if not folder.exists():
                continue
            for img_path in folder.glob("*.*"):
                if img_path.suffix.lower() not in [".jpg", ".jpeg", ".png", ".webp"]:
                    continue
                
                img = Image.open(img_path).convert("RGB")
                tensor = TORCH_TRANSFORMS(img).unsqueeze(0).to(DEVICE)

                t0 = time.perf_counter()
                out = model(tensor)
                pred_idx = torch.argmax(out, dim=1).item()
                t1 = time.perf_counter()
                total_time += (t1 - t0)

                pred_class = CLASS_NAMES[pred_idx]

                total += 1
                class_total[true_class] += 1
                if pred_class == true_class:
                    correct += 1
                    class_correct[true_class] += 1

    acc = (correct / total) * 100 if total > 0 else 0
    avg_latency = (total_time / total) * 1000 if total > 0 else 0
    file_size_mb = model_path.stat().st_size / (1024 * 1024)

    return {
        "name": name,
        "family": family,
        "params": params,
        "accuracy": acc,
        "latency_ms": avg_latency,
        "size_mb": file_size_mb,
        "class_acc": {c: (class_correct[c]/class_total[c])*100 if class_total[c]>0 else 0 for c in CLASS_NAMES}
    }

def main():
    print("=" * 70)
    print("BAT DAU DANH GIA VA SO SANH DA MO HINH TRANHNET (508 ANH VAL)")
    print("=" * 70)

    results = []

    # 1. Evaluate YOLOv8n
    print("[1/3] Đang đánh giá YOLOv8n...", flush=True)
    res_yolo = evaluate_yolo(MODELS_DIR / "best.pt")
    if res_yolo:
        results.append(res_yolo)

    # 2. Evaluate MobileNetV3
    print("[2/3] Đang đánh giá MobileNetV3-Small...", flush=True)
    res_mobile = evaluate_torchvision("mobilenet_v3", MODELS_DIR / "mobilenet_v3_best.pth")
    if res_mobile:
        results.append(res_mobile)

    # 3. Evaluate EfficientNet-B0
    print("[3/3] Đang đánh giá EfficientNet-B0...", flush=True)
    res_eff = evaluate_torchvision("efficientnet_b0", MODELS_DIR / "efficientnet_b0_best.pth")
    if res_eff:
        results.append(res_eff)

    print("\n" + "=" * 80)
    print("BANG KET QUA SO SANH DA MO HINH (BENCHMARK COMPARISON TABLE)")
    print("=" * 80)
    print(f"{'Tên Mô hình':<20} | {'Kiến trúc':<24} | {'Params':<8} | {'Size (MB)':<10} | {'Latency':<10} | {'Top-1 Acc':<10}")
    print("-" * 88)
    for r in results:
        print(f"{r['name']:<20} | {r['family']:<24} | {r['params']:<8} | {r['size_mb']:<10.2f} | {r['latency_ms']:<8.2f}ms | {r['accuracy']:<9.2f}%")
    print("=" * 80)

    # Chi tiết theo từng class
    print("\nChi tiết độ chính xác theo từng loại rác:")
    print(f"{'Loại rác':<12} | " + " | ".join([f"{r['name']:<18}" for r in results]))
    print("-" * 75)
    for c in CLASS_NAMES:
        row = f"{c:<12} | " + " | ".join([f"{r['class_acc'][c]:<17.1f}%" for r in results])
        print(row)

if __name__ == "__main__":
    main()
