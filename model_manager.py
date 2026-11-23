import os
import io
import math
import time
import base64
import tempfile
from pathlib import Path
from typing import Dict, Any, List, Optional, Tuple
from PIL import Image


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

DEFAULT_MODEL_ID = "yolo26_detect"

MODEL_ALIASES: Dict[str, str] = {
    "yolov8_detect": "yolo26_detect",
    "yolo26": "yolo26_detect",
    "yolo26s": "yolo26s_cls",
    "yolov8n": "yolo26n_cls",
    "yolo26n": "yolo26n_cls",
}

DISPLAY_NAMES = {
    "battery": "Pin & Rác nguy hại (Battery)",
    "biological": "Rác hữu cơ & Thực phẩm (Biological)",
    "cardboard": "Bìa Carton (Cardboard)",
    "clothes": "Vải vóc & Quần áo cũ (Clothes)",
    "glass": "Thủy tinh (Glass)",
    "metal": "Kim loại & Vỏ lon (Metal)",
    "paper": "Giấy báo & Sách vở (Paper)",
    "plastic": "Nhựa tái chế (Plastic)",
    "shoes": "Giày dép cũ (Shoes)",
    "trash": "Rác thải không tái chế (Trash)",
}

CLASS_NAMES = [
    "battery",
    "biological",
    "cardboard",
    "clothes",
    "glass",
    "metal",
    "paper",
    "plastic",
    "shoes",
    "trash",
]

LEGACY_TORCH_CLASSES = ["cardboard", "glass", "metal", "paper", "plastic", "trash"]

RAW_TO_CANONICAL_CLASS = {
    "battery": "battery",
    "biological": "biological",
    "brown-glass": "glass",
    "green-glass": "glass",
    "white-glass": "glass",
    "glass": "glass",
    "cardboard": "cardboard",
    "clothes": "clothes",
    "metal": "metal",
    "paper": "paper",
    "plastic": "plastic",
    "shoes": "shoes",
    "trash": "trash",
}

# Mapping chuẩn hóa từ 12 lớp Object Detection sang 10 danh mục chuẩn EcoLink (gộp 3 màu thủy tinh -> glass)
DETECT_CLASS_MAPPING = {
    "battery": ("battery", "Pin & Rác nguy hại (Battery)"),
    "biological": ("biological", "Rác hữu cơ & Thực phẩm (Biological)"),
    "brown-glass": ("glass", "Thủy tinh (Glass)"),
    "green-glass": ("glass", "Thủy tinh (Glass)"),
    "white-glass": ("glass", "Thủy tinh (Glass)"),
    "cardboard": ("cardboard", "Bìa Carton (Cardboard)"),
    "clothes": ("clothes", "Vải vóc & Quần áo cũ (Clothes)"),
    "metal": ("metal", "Kim loại & Vỏ lon (Metal)"),
    "paper": ("paper", "Giấy báo & Sách vở (Paper)"),
    "plastic": ("plastic", "Nhựa tái chế (Plastic)"),
    "shoes": ("shoes", "Giày dép cũ (Shoes)"),
    "trash": ("trash", "Rác thải không tái chế (Trash)"),
}

# Ánh xạ nhóm vật chứa phổ quát từ YOLO26 COCO (yolo26n.pt) hỗ trợ bắt chai trong suốt / cầm tay
COCO_CONTAINER_HINTS: Dict[str, Tuple[str, List[str]]] = {
    "bottle": ("plastic", ["plastic", "glass", "metal"]),
    "cup": ("plastic", ["plastic", "paper", "glass", "metal"]),
    "wine glass": ("glass", ["glass", "plastic"]),
    "vase": ("glass", ["glass", "plastic"]),
    "bowl": ("plastic", ["plastic", "glass", "metal", "paper"]),
    "book": ("paper", ["paper", "cardboard"]),
}

# Preprocessing transform cho torchvision models (Legacy / Benchmark Comparison)
TORCH_TRANSFORMS = transforms.Compose([
    transforms.Resize(256),
    transforms.CenterCrop(224),
    transforms.ToTensor(),
    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
])


def _box_area(xyxy: List[float]) -> float:
    return max(0.0, xyxy[2] - xyxy[0]) * max(0.0, xyxy[3] - xyxy[1])


def _box_iou_and_iomin(a: List[float], b: List[float]) -> Tuple[float, float]:
    ix1 = max(a[0], b[0])
    iy1 = max(a[1], b[1])
    ix2 = min(a[2], b[2])
    iy2 = min(a[3], b[3])
    inter = max(0.0, ix2 - ix1) * max(0.0, iy2 - iy1)
    if inter <= 0.0:
        return 0.0, 0.0
    area_a = _box_area(a)
    area_b = _box_area(b)
    union = area_a + area_b - inter
    iou = inter / union if union > 0 else 0.0
    min_area = min(area_a, area_b)
    io_min = inter / min_area if min_area > 0 else 0.0
    return iou, io_min


def compute_center_weighted_score(
    conf: float,
    xyxy: List[float],
    img_w: int,
    img_h: int,
    handheld_boost: float = 1.0,
) -> float:
    """
    Chấm điểm ưu tiên vật thể gần tâm khung hình & kích thước hợp lý.
    Giúp vật thể người dùng cầm trước camera luôn được xếp #1 thay vì phông nền xung quanh.
    """
    if img_w <= 0 or img_h <= 0:
        return round(conf, 4)

    cx = (xyxy[0] + xyxy[2]) * 0.5
    cy = (xyxy[1] + xyxy[3]) * 0.5
    half_diag = math.hypot(img_w * 0.5, img_h * 0.5) or 1.0
    d_center = math.hypot(cx - img_w * 0.5, cy - img_h * 0.5) / half_diag

    area_ratio = _box_area(xyxy) / float(img_w * img_h)
    clamped_area = max(0.008, min(area_ratio, 0.18))

    score = conf * (clamped_area ** 0.15) * math.exp(-2.4 * (d_center ** 2)) * handheld_boost
    return round(float(score), 4)


class ModelManager:
    def __init__(self, models_dir: Optional[Path] = None):
        current_dir = Path(__file__).resolve().parent
        ai_service_models = current_dir / "src" / "Ecolink.AiService" / "models"
        if models_dir is None:
            candidates = [
                current_dir / "models",
                ai_service_models,
                current_dir.parent.parent / "models",
                Path("src/Ecolink.AiService/models"),
                Path("models"),
            ]
            self.models_dir = next((c for c in candidates if c.exists()), current_dir / "models")
        else:
            self.models_dir = Path(models_dir)

        self._fallback_models_dirs = [
            d for d in (
                current_dir / "models",
                ai_service_models,
                current_dir.parent.parent / "models",
                Path("src/Ecolink.AiService/models"),
            )
            if d.exists()
        ]
        self._loaded_models: Dict[str, Any] = {}
        self._loaded_weight_names: Dict[str, str] = {}

        # Kiến trúc hợp nhất tập trung vào Ultralytics YOLO26 (Two-Stage End-to-End NMS-Free)
        self.registry = {
            "yolo26_detect": {
                "id": "yolo26_detect",
                "name": "YOLO26 Waste Object Detection (End-to-End NMS-Free)",
                "family": "YOLO26 End-to-End NMS-Free (Two-Stage)",
                "file": "yolo26n_waste_detect.pt",
                "fallback_files": ["yolov8n_waste_detect.pt", "yolo26n.pt"],
                "params": "2.58M",
                "description": "Định vị đa vật thể End-to-End NMS-Free (bỏ DFL), ưu tiên tâm khung ngắm kết hợp phân loại tinh YOLO26s-cls (256x256) theo batch",
                "type": "yolo_detect",
                "is_legacy": False,
            },
            "yolo26s_cls": {
                "id": "yolo26s_cls",
                "name": "YOLO26 Small Classification (12-Class Fine-Tuned)",
                "family": "YOLO26-cls (Ultralytics)",
                "file": "yolo26s_waste_cls.pt",
                "fallback_files": ["yolo26n_waste_cls.pt", "best.pt"],
                "params": "5.46M",
                "imgsz": 256,
                "description": "Phân loại tinh 12 lớp rác Garbage Classification (gộp 3 màu thủy tinh thành 10 danh mục EcoLink, 256x256, C3k2 + C2PSA)",
                "type": "yolo",
                "is_legacy": False,
            },
            "yolo26n_cls": {
                "id": "yolo26n_cls",
                "name": "YOLO26 Nano Classification",
                "family": "YOLO26-cls (Ultralytics)",
                "file": "yolo26n_waste_cls.pt",
                "fallback_files": ["best.pt"],
                "params": "1.52M",
                "imgsz": 224,
                "description": "Phân loại 6 nhóm rác chuẩn EcoLink siêu tốc trên kiến trúc YOLO26 không DFL",
                "type": "yolo",
                "is_legacy": False,
            },
            "efficientnet_b0": {
                "id": "efficientnet_b0",
                "name": "EfficientNet-B0 (Legacy Benchmark)",
                "family": "Compound Scaling CNN",
                "file": "efficientnet_b0_best.pth",
                "fallback_files": [],
                "params": "5.29M",
                "description": "Mô hình đối chiếu Torchvision dùng cho kiểm thử benchmark và góc chụp biến dạng",
                "type": "torchvision",
                "is_legacy": True,
            },
            "mobilenet_v3": {
                "id": "mobilenet_v3",
                "name": "MobileNetV3-Small (Legacy Benchmark)",
                "family": "Depthwise Separable CNN",
                "file": "mobilenet_v3_best.pth",
                "fallback_files": [],
                "params": "2.54M",
                "description": "Mô hình đối chiếu Torchvision tối ưu cho thiết bị nhúng cấu hình thấp",
                "type": "torchvision",
                "is_legacy": True,
            },
        }

    def resolve_model_id(self, model_id: Optional[str]) -> str:
        """Chuẩn hóa model ID mới (yolo26_*) và ánh xạ tương thích ngược alias YOLOv8 cũ."""
        if not model_id or not str(model_id).strip():
            return DEFAULT_MODEL_ID
        normalized = str(model_id).strip().lower()
        canonical = MODEL_ALIASES.get(normalized, normalized)
        if canonical not in self.registry:
            raise ValueError(
                f"Mô hình '{model_id}' không tồn tại hoặc chưa được hỗ trợ. Các mô hình khả dụng: {list(self.registry.keys())}"
            )
        return canonical

    def _resolve_weight_path(self, info: Dict[str, Any]) -> Tuple[Optional[Path], bool]:
        """Tìm file trọng số chính hoặc file dự phòng trên các thư mục models."""
        search_dirs = [self.models_dir] + self._fallback_models_dirs
        candidates = [info["file"]] + list(info.get("fallback_files") or [])

        for idx, filename in enumerate(candidates):
            for base in search_dirs:
                p = base / filename
                if p.exists() and p.is_file():
                    return p, idx == 0
        return None, False

    def list_available_models(self) -> List[Dict[str, Any]]:
        """Trả về danh sách các model và trạng thái sẵn sàng của file trọng số."""
        results = []
        for m_id, info in self.registry.items():
            weight_path, _ = self._resolve_weight_path(info)
            exists = weight_path is not None
            file_size_mb = round(weight_path.stat().st_size / (1024 * 1024), 2) if exists else 0.0
            results.append({
                "id": m_id,
                "name": info["name"],
                "family": info["family"],
                "params": info["params"],
                "description": info["description"],
                "is_ready": exists,
                "is_legacy": bool(info.get("is_legacy", False)),
                "active_weight": weight_path.name if weight_path else info["file"],
                "file_size_mb": file_size_mb,
            })
        return results

    def get_model_details(self, model_id: str) -> Optional[Dict[str, Any]]:
        """Trả về thông tin kỹ thuật chuyên sâu và benchmark của một mô hình cụ thể."""
        try:
            canonical_id = self.resolve_model_id(model_id)
        except ValueError:
            return None

        info = self.registry[canonical_id]
        weight_path, _ = self._resolve_weight_path(info)
        exists = weight_path is not None
        file_size_mb = round(weight_path.stat().st_size / (1024 * 1024), 2) if exists else 0.0

        benchmark_map = {
            "yolo26_detect": {
                "pipeline": "YOLO26n Waste Detect (640x640, NMS-Free) + YOLO26s-cls Batch Refinement (256x256)",
                "val_map50": "90.78% mAP@0.5 / 83.27% mAP@0.5:0.95 (P: 84.71%, R: 85.28%)",
                "target_cpu_latency_ms": "< 300 ms",
                "recommended_for": "Ảnh thực tế có phông nền phức tạp, cầm trên tay trước camera hoặc ảnh đa vật thể",
            },
            "yolo26s_cls": {
                "pipeline": "YOLO26s-cls 12-Class Fine-Tuned -> 10 EcoLink Categories (256x256)",
                "val_accuracy": "97.10% (val) / 96.17% (blind test 12c)",
                "target_cpu_latency_ms": "< 80 ms",
                "recommended_for": "Phân loại độ chính xác cao các vật liệu khó (thủy tinh, pin, rác hữu cơ, nhựa trong suốt)",
            },
            "yolo26n_cls": {
                "pipeline": "YOLO26-cls End-to-End DFL-Free",
                "target_cpu_latency_ms": "< 30 ms",
                "recommended_for": "Webcam, video thời gian thực, xử lý theo lô tốc độ cao",
            },
            "mobilenet_v3": {
                "val_accuracy": "91.14%",
                "recommended_for": "Đối chiếu benchmark thiết bị di động / nhúng",
            },
            "efficientnet_b0": {
                "val_accuracy": "92.32%",
                "recommended_for": "Đối chiếu benchmark phân loại ảnh tĩnh phức tạp",
            },
        }

        return {
            "id": canonical_id,
            "name": info["name"],
            "family": info["family"],
            "params": info["params"],
            "description": info["description"],
            "type": info["type"],
            "file": weight_path.name if weight_path else info["file"],
            "file_size_mb": file_size_mb,
            "is_ready": exists,
            "input_resolution": "640x640" if info["type"] == "yolo_detect" else f"{info.get('imgsz', 224)}x{info.get('imgsz', 224)}",
            "classes_count": len(CLASS_NAMES),
            "supported_classes": CLASS_NAMES,
            "class_display_names": DISPLAY_NAMES,
            "hardware_device": str(DEVICE),
            "benchmark": benchmark_map.get(canonical_id, {}),
        }

    def _get_coco_detector(self) -> Optional[YOLO]:
        """Nạp bộ dò phổ quát YOLO26n COCO (yolo26n.pt, NMS-Free) để lọc người & bắt chai lọ trong suốt."""
        if "_coco_yolo26n" in self._loaded_models:
            return self._loaded_models["_coco_yolo26n"]

        search_dirs = [self.models_dir] + self._fallback_models_dirs
        coco_path = None
        for filename in ("yolo26n.pt",):
            for d in search_dirs:
                p = d / filename
                if p.exists() and p.is_file():
                    coco_path = p
                    break
            if coco_path is not None:
                break

        if coco_path is None:
            self._loaded_models["_coco_yolo26n"] = None
            return None

        try:
            m = YOLO(str(coco_path))
            m(Image.new("RGB", (320, 320)), imgsz=320, verbose=False)
            self._loaded_models["_coco_yolo26n"] = m
            self._loaded_weight_names["_coco_yolo26n"] = coco_path.name
            print(f"[+] Đã nạp COCO detector YOLO26: {coco_path.name}")
            return m
        except Exception as e:
            print(f"[!] Không nạp được bộ dò phụ trợ {coco_path.name}: {e}")
            self._loaded_models["_coco_yolo26n"] = None
            return None

    def _get_or_load_model(self, model_id: str):
        """Lazy loading: Chỉ tải model vào RAM khi được gọi lần đầu."""
        canonical_id = self.resolve_model_id(model_id)
        if canonical_id in self._loaded_models:
            return self._loaded_models[canonical_id]

        info = self.registry[canonical_id]
        model_path, _ = self._resolve_weight_path(info)

        if info["type"] in ("yolo", "yolo_detect"):
            target = str(model_path) if model_path is not None else info["file"]
            print(f"[*] Đang nạp YOLO26 model ({canonical_id}) từ: {target}")
            m = YOLO(target)
            warm_sz = 352 if (info["type"] == "yolo_detect" and "yolov8n_waste_detect" in (model_path.name if model_path else "")) else int(info.get("imgsz", 640 if info["type"] == "yolo_detect" else 224))
            m([Image.new("RGB", (warm_sz, warm_sz))], imgsz=warm_sz, verbose=False)
            self._loaded_models[canonical_id] = m
            self._loaded_weight_names[canonical_id] = model_path.name if model_path else info["file"]
            return m

        elif info["type"] == "torchvision":
            print(f"[*] Đang nạp PyTorch benchmark model ({canonical_id}) từ: {model_path}")
            if canonical_id == "mobilenet_v3":
                m = models.mobilenet_v3_small(weights=None)
                in_feat = m.classifier[3].in_features
                m.classifier[3] = nn.Linear(in_feat, len(LEGACY_TORCH_CLASSES))
            elif canonical_id == "efficientnet_b0":
                m = models.efficientnet_b0(weights=None)
                in_feat = m.classifier[1].in_features
                m.classifier[1] = nn.Linear(in_feat, len(LEGACY_TORCH_CLASSES))
            else:
                raise ValueError(f"Model ID không hợp lệ: {canonical_id}")

            if model_path is not None and model_path.exists():
                checkpoint = torch.load(model_path, map_location=DEVICE)
                m.load_state_dict(checkpoint["model_state_dict"])
                print(f"[+] Đã tải checkpoint {model_path.name} thành công (Val Acc: {checkpoint.get('val_acc', 0)*100:.2f}%)")
            else:
                print(f"[!] Cảnh báo: Không tìm thấy checkpoint {info['file']}, khởi tạo model rỗng")

            m = m.to(DEVICE)
            m.eval()
            self._loaded_models[canonical_id] = m
            return m

        raise ValueError(f"Loại mô hình không hỗ trợ: {info['type']}")

    def _letterbox_to_square(self, img: Image.Image) -> Image.Image:
        """Đệm viền trung tính theo màu góc ảnh để giữ nguyên 100% biên dạng 4 góc khi qua CenterCrop của YOLO-cls."""
        if img.width == img.height or img.width <= 0 or img.height <= 0:
            return img
        side = max(img.width, img.height)
        corners = [
            img.getpixel((0, 0)),
            img.getpixel((img.width - 1, 0)),
            img.getpixel((0, img.height - 1)),
            img.getpixel((img.width - 1, img.height - 1)),
        ]
        bg = tuple(sorted(p[ch] for p in corners)[1] for ch in range(3))
        square = Image.new("RGB", (side, side), bg)
        square.paste(img, ((side - img.width) // 2, (side - img.height) // 2))
        return square

    def _resize_max_side(self, img: Image.Image, max_side: int = 288) -> Image.Image:
        if max(img.width, img.height) > max_side:
            scale = max_side / float(max(img.width, img.height))
            return img.resize(
                (max(1, int(round(img.width * scale))), max(1, int(round(img.height * scale)))),
                Image.BILINEAR,
            )
        return img

    def _crop_with_padding(
        self,
        pil_image: Image.Image,
        xyxy: List[float],
        pad_ratio: float = 0.06,
        other_boxes: Optional[List[List[float]]] = None,
        fast_source: Optional[Image.Image] = None,
        fast_scale: float = 1.0,
    ) -> Image.Image:
        """
        Cắt khung vuông đồng tâm (Square Context Crop) để ngăn CenterCrop(256) của YOLO-cls
        xén mất nắp/đáy chai hoặc các góc nhọn của xấp giấy/bìa cứng.
        Ưu tiên cắt trên bản thu phóng `fast_source` nếu vùng cắt vẫn đủ >= 288px để tối ưu tốc độ CPU,
        và thu nhỏ về <= 288px trước khi đệm viền vuông (letterbox).
        """
        w = max(1.0, xyxy[2] - xyxy[0])
        h = max(1.0, xyxy[3] - xyxy[1])
        pad_x = w * pad_ratio
        pad_y = h * pad_ratio
        tx1 = max(0.0, xyxy[0] - pad_x)
        ty1 = max(0.0, xyxy[1] - pad_y)
        tx2 = min(float(pil_image.width), xyxy[2] + pad_x)
        ty2 = min(float(pil_image.height), xyxy[3] + pad_y)
        if tx2 <= tx1 + 1.0 or ty2 <= ty1 + 1.0:
            return self._letterbox_to_square(self._resize_max_side(pil_image, 288))

        tight_box = [tx1, ty1, tx2, ty2]
        side = max(w, h) * (1.0 + 2.0 * pad_ratio)
        cx = (xyxy[0] + xyxy[2]) * 0.5
        cy = (xyxy[1] + xyxy[3]) * 0.5
        sx1 = max(0.0, cx - side * 0.5)
        sy1 = max(0.0, cy - side * 0.5)
        sx2 = min(float(pil_image.width), cx + side * 0.5)
        sy2 = min(float(pil_image.height), cy + side * 0.5)
        sq_box = [sx1, sy1, sx2, sy2]

        neighbor_intrusion = False
        if other_boxes:
            for ob in other_boxes:
                if _box_iou_and_iomin(sq_box, ob)[1] > 0.25 and _box_iou_and_iomin(tight_box, ob)[1] <= 0.25:
                    neighbor_intrusion = True
                    break

        active_box = tight_box if neighbor_intrusion else sq_box
        src_img = pil_image
        box_on_src = active_box
        if (
            fast_source is not None
            and 0.0 < fast_scale < 1.0
            and max(active_box[2] - active_box[0], active_box[3] - active_box[1]) * fast_scale >= 288.0
        ):
            src_img = fast_source
            box_on_src = [v * fast_scale for v in active_box]

        ix1 = max(0, int(round(box_on_src[0])))
        iy1 = max(0, int(round(box_on_src[1])))
        ix2 = min(src_img.width, int(round(box_on_src[2])))
        iy2 = min(src_img.height, int(round(box_on_src[3])))
        crop = src_img.crop((ix1, iy1, ix2, iy2)) if (ix2 > ix1 and iy2 > iy1) else src_img
        crop = self._resize_max_side(crop, 288)
        if neighbor_intrusion or abs(crop.width - crop.height) > max(4, int(0.08 * max(crop.width, crop.height))):
            crop = self._letterbox_to_square(crop)

        return crop

    def _classify_crops_batch(self, crops: List[Image.Image], classifier_id: str) -> List[Dict[str, float]]:
        """Chạy phân loại tinh theo batch thực sự (True Batch Inference) cho toàn bộ danh sách vùng ảnh cắt."""
        if not crops:
            return []

        canonical_clf = self.resolve_model_id(classifier_id)
        info = self.registry[canonical_clf]
        clf = self._get_or_load_model(canonical_clf)

        if info["type"] == "yolo":
            clf_imgsz = int(info.get("imgsz", 224))
            results = clf(crops, imgsz=clf_imgsz, verbose=False)
            out: List[Dict[str, float]] = []
            for res in results:
                probs_list = res.probs.data.tolist()
                names = res.names
                dist = {c: 0.0 for c in CLASS_NAMES}
                for idx, p in enumerate(probs_list):
                    raw_lbl = names[idx].lower().strip()
                    canon = RAW_TO_CANONICAL_CLASS.get(raw_lbl, raw_lbl)
                    if canon in dist:
                        dist[canon] += float(p)
                out.append(dist)
            return out

        batch_tensor = torch.stack([TORCH_TRANSFORMS(c) for c in crops]).to(DEVICE)
        with torch.no_grad():
            logits = clf(batch_tensor)
            probs_batch = torch.softmax(logits, dim=1).cpu().tolist()

        out_torch: List[Dict[str, float]] = []
        for row in probs_batch:
            dist = {c: 0.0 for c in CLASS_NAMES}
            for i, legacy_lbl in enumerate(LEGACY_TORCH_CLASSES):
                dist[legacy_lbl] = float(row[i])
            out_torch.append(dist)
        return out_torch

    def _merge_vertically_split_boxes(
        self, items: List[Dict[str, Any]], img_h: int
    ) -> List[Dict[str, Any]]:
        """Hợp nhất các khung vật chứa bị cắt đôi theo trục dọc do tương phản nền (ví dụ chai trong suốt qua mép bàn)."""
        if len(items) <= 1:
            return items

        container_labels = {"plastic", "glass", "metal"}
        merged_list = [dict(item) for item in items]
        changed = True
        while changed and len(merged_list) > 1:
            changed = False
            next_list: List[Dict[str, Any]] = []
            used = [False] * len(merged_list)
            for i in range(len(merged_list)):
                if used[i]:
                    continue
                curr = merged_list[i]
                for j in range(i + 1, len(merged_list)):
                    if used[j]:
                        continue
                    other = merged_list[j]
                    same_group = (
                        curr["label"] == other["label"]
                        or (curr["label"] in container_labels and other["label"] in container_labels)
                    )
                    if not same_group:
                        continue

                    ax1, ay1, ax2, ay2 = curr["bbox"]
                    bx1, by1, bx2, by2 = other["bbox"]
                    wa, wb = max(1.0, ax2 - ax1), max(1.0, bx2 - bx1)
                    ha, hb = max(1.0, ay2 - ay1), max(1.0, by2 - by1)

                    x_inter = max(0.0, min(ax2, bx2) - max(ax1, bx1))
                    x_union = max(1.0, max(ax2, bx2) - min(ax1, bx1))
                    if (x_inter / x_union) < 0.80 or (min(wa, wb) / max(wa, wb)) < 0.78:
                        continue

                    acy, bcy = (ay1 + ay2) * 0.5, (by1 + by2) * 0.5
                    upper_y2, lower_y1 = (ay2, by1) if acy <= bcy else (by2, ay1)
                    y_gap = lower_y1 - upper_y2
                    if not (-0.15 * min(ha, hb) <= y_gap <= max(12.0, 0.03 * img_h)):
                        continue

                    mx1, my1 = min(ax1, bx1), min(ay1, by1)
                    mx2, my2 = max(ax2, bx2), max(ay2, by2)
                    if ((my2 - my1) / max(1.0, mx2 - mx1)) > 4.8:
                        continue

                    primary = curr if curr.get("det_confidence", 0.0) >= other.get("det_confidence", 0.0) else other
                    combined = dict(primary)
                    max_det_conf = max(curr.get("det_confidence", 0.0), other.get("det_confidence", 0.0))
                    max_conf = max(curr.get("confidence", 0.0), other.get("confidence", 0.0))
                    combined["det_confidence"] = max_det_conf
                    combined["confidence"] = max_conf
                    combined["bbox"] = [round(mx1, 1), round(my1, 1), round(mx2, 1), round(my2, 1)]
                    curr = combined
                    used[j] = True
                    changed = True

                used[i] = True
                next_list.append(curr)
            merged_list = next_list
        return merged_list

    def _deduplicate_candidates(self, items: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Khử các khung lồng nhau hoặc trùng vị trí (Nested / Duplicate Box Suppression)."""
        sorted_items = sorted(items, key=lambda x: x["confidence"], reverse=True)
        kept: List[Dict[str, Any]] = []
        for cand in sorted_items:
            suppress = False
            for existing in kept:
                iou, io_min = _box_iou_and_iomin(cand["bbox"], existing["bbox"])
                if iou > 0.60:
                    suppress = True
                    break
                if cand["label"] == existing["label"] and (iou > 0.42 or io_min > 0.76):
                    suppress = True
                    break
            if not suppress:
                kept.append(cand)
            if len(kept) >= 8:
                break
        return kept

    def _extract_multi_object_candidates(
        self,
        pil_image: Image.Image,
        stage2_classifier_id: str = "yolo26s_cls",
        strict_stage2: bool = False,
        conf_threshold: float = 0.20,
    ) -> List[Dict[str, Any]]:
        """
        Kiến trúc YOLO26 Two-Stage Engine:
        1. Quét đa vật thể bằng yolo26_detect + bộ dò phụ trợ YOLO26n COCO (End-to-End NMS-Free)
           để lọc người, bắt chai lọ trong suốt cầm trên tay và giữ vùng rác.
        2. Cắt từng Bounding Box (+6% padding) đưa qua YOLO26n-cls bằng True Batch Inference.
        3. Khử khung lồng nhau và sắp xếp thứ tự #1..#N theo Center-Weighted Score.
        """
        img_w, img_h = pil_image.size
        img_area = float(max(1, img_w * img_h))

        max_det_side = 960
        if max(img_w, img_h) > max_det_side:
            det_scale = max_det_side / float(max(img_w, img_h))
            det_w = max(1, int(round(img_w * det_scale)))
            det_h = max(1, int(round(img_h * det_scale)))
            det_input = pil_image.resize((det_w, det_h), Image.BILINEAR)
            inv_scale = 1.0 / det_scale
        else:
            det_scale = 1.0
            det_input = pil_image
            inv_scale = 1.0

        detector = self._get_or_load_model("yolo26_detect")
        det_weight_name = self._loaded_weight_names.get("yolo26_detect", "")
        # Tối ưu tốc độ CPU nếu đang fallback bằng checkpoint yolov8n_waste_detect (YOLO11l 51MB)
        det_imgsz = 352 if "yolov8n_waste_detect" in det_weight_name else 640
        det_res = detector(det_input, imgsz=det_imgsz, conf=conf_threshold, verbose=False)[0]

        coco_detector = self._get_coco_detector()
        person_boxes: List[List[float]] = []
        coco_containers: List[Dict[str, Any]] = []
        need_coco = (
            len(det_res.boxes) == 0
            or any(
                float(b.conf[0]) < 0.75
                or (_box_area([float(v) * inv_scale for v in b.xyxy[0].tolist()]) / img_area) > 0.25
                for b in det_res.boxes
            )
        )
        if coco_detector is not None and need_coco:
            coco_res = coco_detector(det_input, imgsz=320, conf=0.25, verbose=False)[0]
            for cb in coco_res.boxes:
                cname = coco_detector.names[int(cb.cls[0])]
                cconf = float(cb.conf[0])
                cxyxy = [round(float(v) * inv_scale, 1) for v in cb.xyxy[0].tolist()]
                carea_ratio = _box_area(cxyxy) / img_area
                if cname == "person" and cconf >= 0.40 and carea_ratio >= 0.08:
                    person_boxes.append(cxyxy)
                elif cname in COCO_CONTAINER_HINTS and cconf >= (0.45 if cname == "book" else 0.30) and 0.008 <= carea_ratio <= 0.85:
                    coco_containers.append({
                        "coco_class": cname,
                        "confidence": cconf,
                        "bbox": cxyxy,
                        "matched": False,
                    })

        raw_candidates: List[Dict[str, Any]] = []
        for box in det_res.boxes:
            raw_cls = detector.names[int(box.cls[0])]
            conf = float(box.conf[0])
            xyxy = [round(float(v) * inv_scale, 1) for v in box.xyxy[0].tolist()]
            area_ratio = _box_area(xyxy) / img_area
            height_ratio = (xyxy[3] - xyxy[1]) / float(max(1, img_h))

            if area_ratio < 0.006:
                continue

            if raw_cls == "person":
                if conf >= 0.40 and area_ratio >= 0.08:
                    person_boxes.append(xyxy)
                continue

            if person_boxes and conf < 0.62:
                overlaps_person = any(_box_iou_and_iomin(xyxy, pb)[0] >= 0.30 for pb in person_boxes)
                is_wall_span = area_ratio > 0.28 and height_ratio > 0.85
                if area_ratio > 0.55 or overlaps_person or is_wall_span:
                    continue

            if raw_cls in COCO_CONTAINER_HINTS:
                default_lbl, allowed_lbls = COCO_CONTAINER_HINTS[raw_cls]
                raw_candidates.append({
                    "raw_class": raw_cls,
                    "label": default_lbl,
                    "display_name": DISPLAY_NAMES[default_lbl],
                    "det_confidence": conf,
                    "confidence": conf,
                    "bbox": xyxy,
                    "allowed_labels": allowed_lbls,
                    "source": "coco_container_assisted",
                })
                continue

            std_cls, disp_name = DETECT_CLASS_MAPPING.get(raw_cls, ("trash", DISPLAY_NAMES.get("trash", raw_cls.capitalize())))

            coco_boost = 0.0
            upgraded_by_coco: Optional[Dict[str, Any]] = None
            for cc in coco_containers:
                iou, io_min = _box_iou_and_iomin(xyxy, cc["bbox"])
                if iou >= 0.42 or io_min >= 0.65:
                    default_lbl, allowed = COCO_CONTAINER_HINTS[cc["coco_class"]]
                    if std_cls in allowed:
                        cc["matched"] = True
                        coco_boost = max(coco_boost, cc["confidence"])
                    elif cc["confidence"] >= conf + 0.15 and conf < 0.70:
                        cc["matched"] = True
                        upgraded_by_coco = {
                            "raw_class": cc["coco_class"],
                            "label": default_lbl,
                            "display_name": DISPLAY_NAMES[default_lbl],
                            "det_confidence": cc["confidence"],
                            "confidence": cc["confidence"],
                            "bbox": cc["bbox"],
                            "allowed_labels": allowed,
                            "source": "coco_container_assisted",
                        }
                        break

            if upgraded_by_coco is not None:
                raw_candidates.append(upgraded_by_coco)
                continue

            if coco_boost > 0.0:
                conf = 1.0 - (1.0 - conf) * (1.0 - 0.45 * coco_boost)

            raw_candidates.append({
                "raw_class": raw_cls,
                "label": std_cls,
                "display_name": DISPLAY_NAMES.get(std_cls, disp_name),
                "det_confidence": conf,
                "confidence": conf,
                "bbox": xyxy,
                "source": "yolo26_detect",
            })

        # Bổ sung các vật chứa thực tế (chai nhựa trong suốt bị che tay, cốc...) mà Stage-1 bỏ sót
        for cc in coco_containers:
            if cc["matched"]:
                continue
            overlaps_existing = any(_box_iou_and_iomin(cc["bbox"], rc["bbox"])[0] >= 0.30 for rc in raw_candidates)
            if overlaps_existing:
                continue
            default_lbl, allowed_lbls = COCO_CONTAINER_HINTS[cc["coco_class"]]
            raw_candidates.append({
                "raw_class": cc["coco_class"],
                "label": default_lbl,
                "display_name": DISPLAY_NAMES[default_lbl],
                "det_confidence": cc["confidence"],
                "confidence": cc["confidence"],
                "bbox": cc["bbox"],
                "allowed_labels": allowed_lbls,
                "source": "coco_container_assisted",
            })

        if not raw_candidates:
            return []

        raw_candidates = self._merge_vertically_split_boxes(raw_candidates, img_h)

        # Stage 2: Cắt từng vùng vật thể (+6% padding) và chạy YOLO26s-cls True Batch Inference
        crops = [
            self._crop_with_padding(
                pil_image,
                c["bbox"],
                pad_ratio=0.06,
                other_boxes=[
                    o["bbox"]
                    for j, o in enumerate(raw_candidates)
                    if j != i and o["label"] != c["label"]
                ],
                fast_source=det_input,
                fast_scale=det_scale,
            )
            for i, c in enumerate(raw_candidates)
        ]
        stage2_dists = self._classify_crops_batch(crops, stage2_classifier_id)

        # Đối chiếu chéo toàn khung (Full-frame sanity check) khi chỉ có 1 vật thể cận cảnh lớn (>=30%)
        # nhưng Stage-1 và Stage-2 mâu thuẫn nhãn (ví dụ bìa trắng phẳng bị Stage-1 bắt nhầm metal)
        if len(raw_candidates) == 1 and raw_candidates[0].get("source") != "coco_container_assisted":
            c0 = raw_candidates[0]
            d0 = stage2_dists[0]
            c0_area_ratio = _box_area(c0["bbox"]) / img_area
            top_s2 = max(CLASS_NAMES, key=lambda k: d0[k])
            if c0_area_ratio >= 0.30 and top_s2 != c0["label"]:
                full_sq = self._letterbox_to_square(self._resize_max_side(det_input, 288))
                full_dist = self._classify_crops_batch([full_sq], stage2_classifier_id)[0]
                stage2_dists[0] = {k: 0.65 * d0[k] + 0.35 * full_dist[k] for k in CLASS_NAMES}

        refined: List[Dict[str, Any]] = []
        for idx, cand in enumerate(raw_candidates):
            cls_dist = stage2_dists[idx]
            det_lbl = cand["label"]
            det_conf = cand["det_confidence"]

            if cand["source"] == "coco_container_assisted":
                allowed = cand.get("allowed_labels", CLASS_NAMES)
                best_allowed = max(allowed, key=lambda k: cls_dist.get(k, 0.0))
                allowed_prob = cls_dist.get(best_allowed, 0.0)
                if cand["raw_class"] == "bottle" and allowed_prob < 0.42:
                    best_allowed = "plastic"
                    final_conf = min(0.92, 0.62 * det_conf + 0.36)
                else:
                    final_conf = min(0.96, 0.52 * det_conf + 0.58 * max(allowed_prob, 0.44))

                cand["label"] = best_allowed
                cand["display_name"] = DISPLAY_NAMES[best_allowed]
                cand["confidence"] = round(final_conf, 4)
                cand["prob_dist"] = cls_dist
                cand["source"] = f"yolo26_coco_assisted_{stage2_classifier_id}"
                refined.append(cand)
                continue

            cand_area_ratio = _box_area(cand["bbox"]) / img_area
            fiber_prob_raw = cls_dist.get("paper", 0.0) + cls_dist.get("cardboard", 0.0)
            related_fiber = det_lbl in ("paper", "cardboard") and fiber_prob_raw >= 0.50
            if cand_area_ratio >= 0.88 and cls_dist.get(det_lbl, 0.0) < 0.03 and not related_fiber:
                continue

            if strict_stage2:
                best_cls = max(CLASS_NAMES, key=lambda k: cls_dist[k])
                best_cls_prob = cls_dist[best_cls]
                if best_cls == det_lbl:
                    final_lbl = best_cls
                    final_conf = 1.0 - (1.0 - det_conf) * (1.0 - 0.55 * best_cls_prob)
                elif best_cls_prob >= 0.52 and det_conf < 0.80:
                    final_lbl = best_cls
                    final_conf = 0.65 * best_cls_prob + 0.35 * det_conf
                else:
                    final_lbl = det_lbl if det_conf >= best_cls_prob else best_cls
                    final_conf = max(det_conf * 0.85, best_cls_prob)
                cand["label"] = final_lbl
                cand["display_name"] = DISPLAY_NAMES.get(final_lbl, final_lbl.capitalize())
                cand["confidence"] = round(min(0.99, final_conf), 4)
                cand["prob_dist"] = cls_dist
                cand["source"] = f"two_stage_{stage2_classifier_id}"
                refined.append(cand)
                continue

            # Chế độ mặc định Unified YOLO26 (yolo26_detect + yolo26s_cls)
            best_cls = max(CLASS_NAMES, key=lambda k: cls_dist[k])
            best_cls_prob = cls_dist[best_cls]
            det_cls_prob = cls_dist.get(det_lbl, 0.0)
            fiber_prob = cls_dist.get("paper", 0.0) + cls_dist.get("cardboard", 0.0)

            is_backlit_pet = det_lbl == "plastic" and best_cls == "glass" and det_conf >= 0.72
            is_false_hazard_battery = (
                det_lbl != "battery"
                and best_cls == "battery"
                and (best_cls_prob < 0.88 or fiber_prob >= 0.15)
            )
            is_ruled_paper_clothes = (
                det_lbl in ("paper", "cardboard")
                and best_cls == "clothes"
                and (det_conf >= 0.70 or fiber_prob >= 0.12)
            )

            if det_conf >= 0.70:
                if det_cls_prob < 0.05 and fiber_prob >= 0.72 and best_cls in ("paper", "cardboard"):
                    final_lbl = best_cls
                    final_conf = min(0.95, 0.62 * fiber_prob + 0.25 * det_conf)
                elif (
                    det_cls_prob < 0.06
                    and best_cls_prob >= 0.78
                    and not is_backlit_pet
                    and not is_false_hazard_battery
                    and not is_ruled_paper_clothes
                ):
                    final_lbl = best_cls
                    final_conf = min(0.95, 0.65 * best_cls_prob + 0.25 * det_conf)
                elif best_cls == det_lbl:
                    final_lbl = det_lbl
                    final_conf = min(0.99, 0.80 * det_conf + 0.20 * det_cls_prob)
                else:
                    final_lbl = det_lbl
                    final_conf = det_conf
            else:
                if (
                    det_cls_prob < 0.08
                    and best_cls_prob >= 0.80
                    and not is_backlit_pet
                    and not is_false_hazard_battery
                    and not is_ruled_paper_clothes
                ):
                    final_lbl = best_cls
                    final_conf = min(0.92, 0.68 * best_cls_prob + 0.22 * det_conf)
                elif det_lbl in ("paper", "cardboard") and fiber_prob >= 0.70:
                    final_lbl = best_cls if best_cls in ("paper", "cardboard") else det_lbl
                    final_conf = min(0.94, 0.50 * det_conf + 0.55 * fiber_prob)
                else:
                    fused = {k: 0.45 * cls_dist[k] for k in CLASS_NAMES}
                    fused[det_lbl] = fused.get(det_lbl, 0.0) + 0.55 * det_conf
                    final_lbl = max(CLASS_NAMES, key=lambda k: fused[k])
                    final_conf = min(0.96, fused[final_lbl] + (0.12 if final_lbl == det_lbl and det_cls_prob >= 0.25 else 0.0))

                if det_conf < 0.35 and best_cls_prob < 0.45 and final_conf < 0.35:
                    continue

            cand["label"] = final_lbl
            cand["display_name"] = DISPLAY_NAMES.get(final_lbl, final_lbl.capitalize())
            cand["confidence"] = round(float(final_conf), 4)
            cand["prob_dist"] = cls_dist
            cand["source"] = "yolo26_unified_two_stage"
            refined.append(cand)

        deduped = self._deduplicate_candidates(refined)

        for item in deduped:
            item_area_ratio = _box_area(item["bbox"]) / img_area
            is_handheld = bool(
                person_boxes
                and item_area_ratio < 0.25
                and any(_box_iou_and_iomin(item["bbox"], pb)[1] >= 0.40 for pb in person_boxes)
            )
            item["center_score"] = compute_center_weighted_score(
                item["confidence"],
                item["bbox"],
                img_w,
                img_h,
                handheld_boost=1.35 if is_handheld else 1.0,
            )

        deduped.sort(key=lambda x: x["center_score"], reverse=True)

        final_objects: List[Dict[str, Any]] = []
        for rank, item in enumerate(deduped, start=1):
            final_objects.append({
                "id": rank,
                "raw_class": item["raw_class"],
                "label": item["label"],
                "display_name": item["display_name"],
                "confidence": round(float(item["confidence"]), 4),
                "center_score": item["center_score"],
                "is_primary": rank == 1,
                "source": item.get("source", "yolo26_unified_two_stage"),
                "bbox": item["bbox"],
                "_prob_dist": item.get("prob_dist"),
            })

        return final_objects

    def predict(
        self,
        pil_image: Image.Image,
        model_id: str = DEFAULT_MODEL_ID,
        use_detection_crop: bool = True,
    ) -> Dict[str, Any]:
        """Thực hiện suy luận thống nhất xoay quanh YOLO26 (End-to-End Two-Stage + Center-Weighted)."""
        canonical_id = self.resolve_model_id(model_id)
        info = self.registry[canonical_id]
        img_w, img_h = pil_image.size
        start_time = time.perf_counter()

        stage2_id = "yolo26s_cls" if info["type"] == "yolo_detect" else canonical_id

        detected_objects: List[Dict[str, Any]] = []
        if info["type"] == "yolo_detect" or use_detection_crop:
            try:
                detected_objects = self._extract_multi_object_candidates(
                    pil_image,
                    stage2_classifier_id=stage2_id,
                    strict_stage2=(info["type"] != "yolo_detect"),
                    conf_threshold=0.20,
                )
            except Exception as e:
                print(f"[!] Warning: Multi-object detection fallback sang phân loại toàn ảnh: {e}")
                detected_objects = []

        if detected_objects:
            latency_ms = round((time.perf_counter() - start_time) * 1000, 2)
            primary = detected_objects[0]
            primary_dist = primary.get("_prob_dist") or {c: 0.0 for c in CLASS_NAMES}

            class_scores: Dict[str, float] = {c: round(float(primary_dist.get(c, 0.0)) * 0.35, 4) for c in CLASS_NAMES}
            for obj in detected_objects:
                lbl = obj["label"]
                if lbl in class_scores:
                    weight = 1.0 if obj["is_primary"] else 0.75
                    class_scores[lbl] = max(class_scores[lbl], round(obj["confidence"] * weight, 4))
            class_scores[primary["label"]] = primary["confidence"]

            total_score = sum(class_scores.values())
            if total_score > 0:
                norm_scores = {c: round(class_scores[c] / total_score, 4) for c in CLASS_NAMES}
            else:
                norm_scores = {c: round(1.0 / len(CLASS_NAMES), 4) for c in CLASS_NAMES}

            all_predictions = [
                {
                    "label": c,
                    "confidence": norm_scores[c],
                    "display_name": DISPLAY_NAMES.get(c, c.capitalize()),
                }
                for c in CLASS_NAMES
            ]
            all_predictions.sort(key=lambda x: (1 if x["label"] == primary["label"] else 0, x["confidence"]), reverse=True)

            category_summary: Dict[str, int] = {}
            public_objects: List[Dict[str, Any]] = []
            for obj in detected_objects:
                clean_obj = {k: v for k, v in obj.items() if not k.startswith("_")}
                public_objects.append(clean_obj)
                category_summary[obj["label"]] = category_summary.get(obj["label"], 0) + 1

            return {
                "model_id": canonical_id,
                "model_name": info["name"],
                "model_family": info["family"],
                "pipeline": "yolo26_unified_two_stage",
                "image_width": img_w,
                "image_height": img_h,
                "cropped_bbox": primary["bbox"],
                "top_prediction": {
                    "label": primary["label"],
                    "confidence": primary["confidence"],
                    "display_name": primary["display_name"],
                },
                "predictions": all_predictions[:3],
                "all_probabilities": all_predictions,
                "detected_objects": public_objects,
                "detected_count": len(public_objects),
                "category_summary": category_summary,
                "inference_time_ms": latency_ms,
            }

        fallback_clf_id = stage2_id if info["type"] == "yolo_detect" else canonical_id
        dists = self._classify_crops_batch([pil_image], fallback_clf_id)
        dist = dists[0] if dists else {c: 0.0 for c in CLASS_NAMES}
        zero_det_factor = 0.55 if (info["type"] == "yolo_detect" or use_detection_crop) else 1.0
        latency_ms = round((time.perf_counter() - start_time) * 1000, 2)

        all_predictions = [
            {
                "label": c,
                "confidence": round(float(dist.get(c, 0.0)) * zero_det_factor, 4),
                "display_name": DISPLAY_NAMES.get(c, c.capitalize()),
            }
            for c in CLASS_NAMES
        ]
        all_predictions.sort(key=lambda x: x["confidence"], reverse=True)
        top_pred = all_predictions[0] if all_predictions else None

        return {
            "model_id": canonical_id,
            "model_name": info["name"],
            "model_family": info["family"],
            "pipeline": "yolo26_single_stage",
            "image_width": img_w,
            "image_height": img_h,
            "cropped_bbox": None,
            "top_prediction": top_pred,
            "predictions": all_predictions[:3],
            "all_probabilities": all_predictions,
            "detected_objects": [],
            "detected_count": 0,
            "category_summary": {},
            "inference_time_ms": latency_ms,
        }

    def detect_objects(self, pil_image: Image.Image, conf_threshold: float = 0.20) -> Dict[str, Any]:
        """Chuyên biệt cho endpoint /api/v1/detect: Khoanh vùng đa vật thể YOLO26 + trả ảnh Base64."""
        start_time = time.perf_counter()
        img_w, img_h = pil_image.size
        objects = self._extract_multi_object_candidates(
            pil_image,
            stage2_classifier_id="yolo26s_cls",
            strict_stage2=False,
            conf_threshold=conf_threshold,
        )
        latency_ms = round((time.perf_counter() - start_time) * 1000, 2)

        detected_items = []
        category_counts: Dict[str, int] = {}
        for obj in objects:
            lbl = obj["label"]
            category_counts[lbl] = category_counts.get(lbl, 0) + 1
            detected_items.append({
                "id": obj["id"],
                "raw_class": obj["raw_class"],
                "category": lbl,
                "display_name": obj["display_name"],
                "confidence": obj["confidence"],
                "center_score": obj["center_score"],
                "is_primary": obj["is_primary"],
                "bbox": obj["bbox"],
            })

        model = self._get_or_load_model("yolo26_detect")
        raw_res = model(pil_image, conf=conf_threshold, verbose=False)[0]
        annotated_bgr = raw_res.plot()
        annotated_rgb = annotated_bgr[:, :, ::-1]
        annotated_pil = Image.fromarray(annotated_rgb)
        buf = io.BytesIO()
        annotated_pil.save(buf, format="JPEG", quality=85)
        base64_img = base64.b64encode(buf.getvalue()).decode("utf-8")

        return {
            "success": True,
            "image_width": img_w,
            "image_height": img_h,
            "total_items": len(detected_items),
            "category_summary": category_counts,
            "detected_objects": detected_items,
            "annotated_image_base64": f"data:image/jpeg;base64,{base64_img}",
            "inference_time_ms": latency_ms,
        }

    def reload_models(self) -> Dict[str, Any]:
        """Xóa bộ nhớ đệm trọng số trên RAM và quét nạp lại danh sách mô hình mới từ đĩa."""
        self._loaded_models.clear()
        self._loaded_weight_names.clear()
        available = self.list_available_models()
        ready_count = len([m for m in available if m.get("is_ready")])
        return {
            "success": True,
            "reloaded_at": time.strftime("%Y-%m-%d %H:%M:%S"),
            "ready_count": ready_count,
            "models": available,
        }
