import os
import sys
import tempfile
import time
from pathlib import Path
from PIL import Image

# Đảm bảo in UTF-8 không lỗi trên Windows
if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass


def _ensure_writable_yolo_config_dir() -> None:
    if os.environ.get("YOLO_CONFIG_DIR"):
        return
    appdata = os.environ.get("APPDATA")
    if appdata:
        default_settings = Path(appdata) / "Ultralytics" / "settings.json"
        try:
            if default_settings.exists():
                with open(default_settings, "r", encoding="utf-8"):
                    pass
            return
        except PermissionError:
            pass
    fallback = Path(tempfile.gettempdir()) / "ecolink_ultralytics"
    fallback.mkdir(parents=True, exist_ok=True)
    os.environ["YOLO_CONFIG_DIR"] = str(fallback)


_ensure_writable_yolo_config_dir()

import torch  # noqa: E402
import torch.nn as nn  # noqa: E402
from torchvision import transforms, models  # noqa: E402
from ultralytics import YOLO  # noqa: E402

DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")
BASE_DIR = Path(__file__).resolve().parent
VAL_DIR = BASE_DIR / "data" / "trashnet_split" / "val"
MODELS_DIR = BASE_DIR / "src" / "Ecolink.AiService" / "models"
if not MODELS_DIR.exists():
    MODELS_DIR = BASE_DIR / "models"

CLASS_NAMES = ["cardboard", "glass", "metal", "paper", "plastic", "trash"]

TORCH_TRANSFORMS = transforms.Compose([
    transforms.Resize(256),
    transforms.CenterCrop(224),
    transforms.ToTensor(),
    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
])


def evaluate_yolo(model_path: Path, name: str, family: str, params: str, imgsz: int = 224):
    if not model_path.exists():
        return None
    model = YOLO(str(model_path))
    # Warm-up một lượt trước khi đo latency
    for warm in sorted((VAL_DIR / CLASS_NAMES[0]).glob("*.*"))[:1]:
        model(str(warm), imgsz=imgsz, verbose=False)

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
            results = model(str(img_path), imgsz=imgsz, verbose=False)
            t1 = time.perf_counter()
            total_time += t1 - t0

            # Gộp 12 lớp (nếu là model fine-tune 12 classes) về 6 thư mục đánh giá TrashNet val
            val_group_map = {
                "brown-glass": "glass",
                "green-glass": "glass",
                "white-glass": "glass",
                "battery": "trash",
                "biological": "trash",
                "clothes": "trash",
                "shoes": "trash",
            }
            group_probs = {c: 0.0 for c in CLASS_NAMES}
            names = results[0].names
            for idx, p in enumerate(results[0].probs.data.tolist()):
                raw_lbl = names[idx].lower().strip()
                mapped = val_group_map.get(raw_lbl, raw_lbl)
                if mapped in group_probs:
                    group_probs[mapped] += float(p)
            pred_class = max(CLASS_NAMES, key=lambda c: group_probs[c])

            total += 1
            class_total[true_class] += 1
            if pred_class == true_class:
                correct += 1
                class_correct[true_class] += 1

    acc = (correct / total) * 100 if total > 0 else 0.0
    avg_latency = (total_time / total) * 1000 if total > 0 else 0.0
    file_size_mb = model_path.stat().st_size / (1024 * 1024)

    return {
        "name": name,
        "family": family,
        "params": params,
        "accuracy": acc,
        "latency_ms": avg_latency,
        "size_mb": file_size_mb,
        "class_acc": {
            c: (class_correct[c] / class_total[c]) * 100 if class_total[c] > 0 else 0.0
            for c in CLASS_NAMES
        },
    }


def evaluate_torchvision(model_type: str, model_path: Path):
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
    model.load_state_dict(checkpoint["model_state_dict"])
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
                total_time += t1 - t0

                pred_class = CLASS_NAMES[pred_idx]

                total += 1
                class_total[true_class] += 1
                if pred_class == true_class:
                    correct += 1
                    class_correct[true_class] += 1

    acc = (correct / total) * 100 if total > 0 else 0.0
    avg_latency = (total_time / total) * 1000 if total > 0 else 0.0
    file_size_mb = model_path.stat().st_size / (1024 * 1024)

    return {
        "name": name,
        "family": family,
        "params": params,
        "accuracy": acc,
        "latency_ms": avg_latency,
        "size_mb": file_size_mb,
        "class_acc": {
            c: (class_correct[c] / class_total[c]) * 100 if class_total[c] > 0 else 0.0
            for c in CLASS_NAMES
        },
    }


def main():
    print("=" * 80)
    print("BAT DAU DANH GIA VA SO SANH KIEN TRUC YOLO26 vs LEGACY MODELS (508 ANH VAL)")
    print("=" * 80)

    results = []

    print("[1/5] Đang đánh giá YOLO26s-cls (12c -> 6 nhóm TrashNet val, 256px)...", flush=True)
    res_y26s = evaluate_yolo(
        MODELS_DIR / "yolo26s_waste_cls.pt",
        name="YOLO26s-cls (12c)",
        family="YOLO26-cls (Ultralytics)",
        params="5.46M",
        imgsz=256,
    )
    if res_y26s:
        results.append(res_y26s)

    print("[2/5] Đang đánh giá YOLO26n-cls (224px)...", flush=True)
    res_y26 = evaluate_yolo(
        MODELS_DIR / "yolo26n_waste_cls.pt",
        name="YOLO26n-cls",
        family="YOLO26-cls (Ultralytics)",
        params="1.52M",
        imgsz=224,
    )
    if res_y26:
        results.append(res_y26)

    print("[3/5] Đang đánh giá YOLOv8n-cls (Baseline cũ)...", flush=True)
    res_y8 = evaluate_yolo(
        MODELS_DIR / "best.pt",
        name="YOLOv8n-cls",
        family="YOLOv8-cls (Ultralytics)",
        params="1.44M",
        imgsz=224,
    )
    if res_y8:
        results.append(res_y8)

    print("[4/5] Đang đánh giá MobileNetV3-Small...", flush=True)
    res_mobile = evaluate_torchvision("mobilenet_v3", MODELS_DIR / "mobilenet_v3_best.pth")
    if res_mobile:
        results.append(res_mobile)

    print("[5/5] Đang đánh giá EfficientNet-B0...", flush=True)
    res_eff = evaluate_torchvision("efficientnet_b0", MODELS_DIR / "efficientnet_b0_best.pth")
    if res_eff:
        results.append(res_eff)

    print("\n" + "=" * 88)
    print("BANG KET QUA SO SANH DA MO HINH (BENCHMARK COMPARISON TABLE)")
    print("=" * 88)
    print(f"{'Tên Mô hình':<20} | {'Kiến trúc':<24} | {'Params':<8} | {'Size (MB)':<10} | {'Latency':<10} | {'Top-1 Acc':<10}")
    print("-" * 96)
    for r in results:
        print(f"{r['name']:<20} | {r['family']:<24} | {r['params']:<8} | {r['size_mb']:<10.2f} | {r['latency_ms']:<8.2f}ms | {r['accuracy']:<9.2f}%")
    print("=" * 88)

    print("\nChi tiết độ chính xác theo từng loại rác:")
    print(f"{'Loại rác':<12} | " + " | ".join([f"{r['name']:<16}" for r in results]))
    print("-" * 80)
    for c in CLASS_NAMES:
        row = f"{c:<12} | " + " | ".join([f"{r['class_acc'][c]:<15.1f}%" for r in results])
        print(row)


if __name__ == "__main__":
    main()
