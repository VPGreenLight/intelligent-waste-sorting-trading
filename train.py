import argparse
import os
import shutil
import tempfile
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent


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

from ultralytics import YOLO  # noqa: E402


def sync_weight_targets(src_weight: Path, filename: str) -> None:
    target = BASE_DIR / "src" / "Ecolink.AiService" / "models" / filename
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src_weight, target)
    print(f"[+] Đã đồng bộ trọng số chuẩn tại: {target}")


def _resolve_best_weight(results: object, default_dir: Path) -> Path:
    save_dir = getattr(results, "save_dir", None)
    if save_dir:
        candidate = Path(save_dir) / "weights" / "best.pt"
        if candidate.exists():
            return candidate
    candidate = default_dir / "weights" / "best.pt"
    if candidate.exists():
        return candidate
    raise FileNotFoundError(f"Không tìm thấy checkpoint best.pt tại {candidate}")


def train_classify(
    model_variant: str = "yolo26s-cls",
    epochs: int = 35,
    batch: int = 32,
    imgsz: int = 256,
) -> None:
    base_weight = BASE_DIR / f"{model_variant}.pt"
    model = YOLO(str(base_weight if base_weight.exists() else f"{model_variant}.pt"))

    is_small = "26s" in model_variant
    run_name = "waste_yolo26s_cls" if is_small else "waste_yolo26n_cls"
    target_filename = "yolo26s_waste_cls.pt" if is_small else "yolo26n_waste_cls.pt"

    dataset_path = (BASE_DIR / "data" / "trashnet_split").resolve()
    project_dir = BASE_DIR / "runs" / "classify"
    print(f"[*] Bắt đầu huấn luyện {model_variant.upper()} (imgsz={imgsz}, epochs={epochs}) với tập dữ liệu: {dataset_path}")

    results = model.train(
        data=str(dataset_path),
        epochs=epochs,
        patience=12,
        imgsz=imgsz,
        batch=batch,
        workers=0,
        cache="ram",
        optimizer="AdamW",
        lr0=0.0005,
        lrf=0.01,
        cos_lr=True,
        dropout=0.1,
        erasing=0.1,
        hsv_s=0.3,
        hsv_v=0.3,
        degrees=15.0,
        project=str(project_dir),
        name=run_name,
        exist_ok=True,
    )

    best_path = _resolve_best_weight(results, project_dir / run_name)
    sync_weight_targets(best_path, target_filename)
    print(f"\n[+] Huấn luyện {model_variant.upper()} hoàn tất!")


def train_detect(data_yaml: str, epochs: int = 30, batch: int = 16, imgsz: int = 640) -> None:
    base_weight = BASE_DIR / "src" / "Ecolink.AiService" / "models" / "yolo26n.pt"
    model = YOLO(str(base_weight if base_weight.exists() else "yolo26n.pt"))

    project_dir = BASE_DIR / "runs" / "detect"
    run_name = "waste_yolo26n_detect"
    print(f"[*] Bắt đầu huấn luyện YOLO26 Object Detection với cấu hình: {data_yaml}")
    results = model.train(
        data=data_yaml,
        epochs=epochs,
        imgsz=imgsz,
        batch=batch,
        workers=0,
        optimizer="AdamW",
        cos_lr=True,
        project=str(project_dir),
        name=run_name,
        exist_ok=True,
    )

    best_path = _resolve_best_weight(results, project_dir / run_name)
    sync_weight_targets(best_path, "yolo26n_waste_detect.pt")
    print("\n[+] Huấn luyện YOLO26 Detection hoàn tất!")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Huấn luyện mô hình EcoLink YOLO26")
    parser.add_argument("--task", choices=["classify", "detect"], default="classify")
    parser.add_argument("--model", choices=["yolo26s-cls", "yolo26n-cls"], default="yolo26s-cls")
    parser.add_argument("--epochs", type=int, default=35)
    parser.add_argument("--batch", type=int, default=32)
    parser.add_argument("--imgsz", type=int, default=None)
    parser.add_argument("--data", type=str, default=str(BASE_DIR / "data" / "curated_datasets" / "data.yaml"))
    args = parser.parse_args()

    if args.task == "detect":
        train_detect(
            data_yaml=args.data,
            epochs=args.epochs,
            batch=args.batch,
            imgsz=args.imgsz or 640,
        )
    else:
        train_classify(
            model_variant=args.model,
            epochs=args.epochs,
            batch=args.batch,
            imgsz=args.imgsz or (256 if "26s" in args.model else 224),
        )
