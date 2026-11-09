import os
import io
import math
import time
import base64
from pathlib import Path
from typing import Dict, Any, List, Optional, Tuple
from PIL import Image

import torch
import torch.nn as nn
from torchvision import transforms, models
from ultralytics import YOLO

DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")

DISPLAY_NAMES = {
    "cardboard": "Bìa Carton (Cardboard)",
    "glass": "Thủy tinh (Glass)",
    "metal": "Kim loại & Vỏ lon (Metal)",
    "paper": "Giấy báo & Sách vở (Paper)",
    "plastic": "Nhựa tái chế (Plastic)",
    "trash": "Rác thải không tái chế (Trash)"
}

CLASS_NAMES = ["cardboard", "glass", "metal", "paper", "plastic", "trash"]

# Mapping chuẩn hóa từ 12 lớp của YOLOv8 Waste Object Detection sang nhóm rác EcoLink
DETECT_CLASS_MAPPING = {
    "battery": ("trash", "Pin & Rác nguy hại (Battery)"),
    "biological": ("trash", "Rác hữu cơ / sinh học (Biological)"),
    "brown-glass": ("glass", "Thủy tinh nâu (Brown Glass)"),
    "green-glass": ("glass", "Thủy tinh xanh (Green Glass)"),
    "white-glass": ("glass", "Thủy tinh trắng (White Glass)"),
    "cardboard": ("cardboard", "Bìa Carton (Cardboard)"),
    "clothes": ("trash", "Vải & Quần áo cũ (Clothes)"),
    "metal": ("metal", "Kim loại & Vỏ lon (Metal)"),
    "paper": ("paper", "Giấy báo & Sách vở (Paper)"),
    "plastic": ("plastic", "Nhựa tái chế (Plastic)"),
    "shoes": ("trash", "Giày dép cũ (Shoes)"),
    "trash": ("trash", "Rác thải không tái chế (Trash)")
}

# Ánh xạ nhóm vật chứa thực tế từ COCO (yolov8n.pt) hỗ trợ bắt chai trong suốt / cầm trên tay
COCO_CONTAINER_HINTS: Dict[str, Tuple[str, List[str]]] = {
    "bottle": ("plastic", ["plastic", "glass", "metal"]),
    "cup": ("plastic", ["plastic", "paper", "glass"]),
    "wine glass": ("glass", ["glass", "plastic"]),
    "book": ("paper", ["paper", "cardboard"]),
}

# Preprocessing transform cho torchvision models (MobileNet, EfficientNet)
TORCH_TRANSFORMS = transforms.Compose([
    transforms.Resize(256),
    transforms.CenterCrop(224),
    transforms.ToTensor(),
    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225])
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
    Đề xuất 2: Chấm điểm ưu tiên vật thể gần tâm khung hình & kích thước hợp lý.
    Giúp vật thể người dùng cầm trước camera luôn được xếp #1 thay vì lịch/vật dụng ở rìa nền.
    """
    if img_w <= 0 or img_h <= 0:
        return round(conf, 4)

    cx = (xyxy[0] + xyxy[2]) * 0.5
    cy = (xyxy[1] + xyxy[3]) * 0.5
    half_diag = math.hypot(img_w * 0.5, img_h * 0.5) or 1.0
    d_center = math.hypot(cx - img_w * 0.5, cy - img_h * 0.5) / half_diag

    area_ratio = _box_area(xyxy) / float(img_w * img_h)
    # Giới hạn trần 0.18 để vùng tường/nền lớn không đè điểm chai/lon cầm tay (thường 3%..18% khung hình)
    clamped_area = max(0.008, min(area_ratio, 0.18))

    score = conf * (clamped_area ** 0.15) * math.exp(-2.4 * (d_center ** 2)) * handheld_boost
    return round(float(score), 4)


class ModelManager:
    def __init__(self, models_dir: Optional[Path] = None):
        if models_dir is None:
            current_dir = Path(__file__).resolve().parent
            candidates = [
                current_dir / "models",
                current_dir.parent.parent / "models",
                Path("models")
            ]
            self.models_dir = next((c for c in candidates if c.exists()), current_dir / "models")
        else:
            self.models_dir = Path(models_dir)

        self._loaded_models: Dict[str, Any] = {}

        # Danh mục metadata các mô hình hỗ trợ
        self.registry = {
            "yolov8_detect": {
                "id": "yolov8_detect",
                "name": "YOLOv8 Waste Object Detection (SOTA)",
                "family": "Two-Stage Object Detection & Ensemble",
                "file": "yolov8n_waste_detect.pt",
                "params": "3.15M",
                "description": "Định vị Bounding Box đa vật thể, ưu tiên tâm khung ngắm và hậu kiểm 2 giai đoạn với EfficientNet-B0",
                "type": "yolo_detect"
            },
            "yolov8n": {
                "id": "yolov8n",
                "name": "YOLOv8 Nano Classification",
                "family": "CNN (Ultralytics)",
                "file": "best.pt",
                "params": "1.44M",
                "description": "Siêu tốc độ (~2.5ms), chuyên dụng cho webcam và video thời gian thực",
                "type": "yolo"
            },
            "mobilenet_v3": {
                "id": "mobilenet_v3",
                "name": "MobileNetV3-Small",
                "family": "Depthwise Separable CNN",
                "file": "mobilenet_v3_best.pth",
                "params": "2.54M",
                "description": "Tối ưu hóa sâu cho thiết bị di động và điện thoại nhúng",
                "type": "torchvision"
            },
            "efficientnet_b0": {
                "id": "efficientnet_b0",
                "name": "EfficientNet-B0",
                "family": "Compound Scaling CNN",
                "file": "efficientnet_b0_best.pth",
                "params": "5.29M",
                "description": "Độ chính xác cao nhất cho ảnh tĩnh, nhận diện tốt vân bìa carton và rác biến dạng",
                "type": "torchvision"
            }
        }

    def list_available_models(self) -> List[Dict[str, Any]]:
        """Trả về danh sách các model và trạng thái sẵn sàng của file trọng số"""
        results = []
        for m_id, info in self.registry.items():
            model_path = self.models_dir / info["file"]
            exists = model_path.exists()
            file_size_mb = round(model_path.stat().st_size / (1024 * 1024), 2) if exists else 0.0
            results.append({
                "id": m_id,
                "name": info["name"],
                "family": info["family"],
                "params": info["params"],
                "description": info["description"],
                "is_ready": exists,
                "file_size_mb": file_size_mb
            })
        return results

    def get_model_details(self, model_id: str) -> Optional[Dict[str, Any]]:
        """Trả về thông tin kỹ thuật chuyên sâu và benchmark của một mô hình cụ thể"""
        if model_id not in self.registry:
            return None

        info = self.registry[model_id]
        model_path = self.models_dir / info["file"]
        exists = model_path.exists()
        file_size_mb = round(model_path.stat().st_size / (1024 * 1024), 2) if exists else 0.0

        benchmark_map = {
            "yolov8_detect": {
                "val_accuracy": "95.6% (mAP50)",
                "avg_latency_ms": 18.0,
                "recommended_for": "Ảnh thực tế có phông nền phức tạp, cầm trên tay trước camera hoặc ảnh có nhiều món rác"
            },
            "yolov8n": {
                "val_accuracy": "91.73%",
                "avg_latency_ms": 2.5,
                "recommended_for": "Webcam, realtime video stream, edge devices"
            },
            "mobilenet_v3": {
                "val_accuracy": "91.14%",
                "avg_latency_ms": 4.8,
                "recommended_for": "Thiết bị di động, app nhúng, máy tính cấu hình cơ bản"
            },
            "efficientnet_b0": {
                "val_accuracy": "92.32%",
                "avg_latency_ms": 12.08,
                "recommended_for": "Ảnh rác biến dạng, góc chụp phức tạp, độ chính xác tối đa"
            }
        }

        return {
            "id": model_id,
            "name": info["name"],
            "family": info["family"],
            "params": info["params"],
            "description": info["description"],
            "type": info["type"],
            "file": info["file"],
            "file_size_mb": file_size_mb,
            "is_ready": exists,
            "input_resolution": "640x640" if info["type"] == "yolo_detect" else "224x224",
            "classes_count": len(CLASS_NAMES),
            "supported_classes": CLASS_NAMES,
            "class_display_names": DISPLAY_NAMES,
            "hardware_device": str(DEVICE),
            "benchmark": benchmark_map.get(model_id, {})
        }

    def _get_coco_detector(self) -> Optional[YOLO]:
        """Nạp bộ dò vật thể phổ quát yolov8n.pt (nếu có sẵn trên đĩa) để bắt chai lọ trong suốt cầm trên tay & lọc người."""
        if "_coco_yolov8n" in self._loaded_models:
            return self._loaded_models["_coco_yolov8n"]

        current_dir = Path(__file__).resolve().parent
        candidates = [
            self.models_dir / "yolov8n.pt",
            current_dir / "yolov8n.pt",
            current_dir.parent.parent / "yolov8n.pt",
            Path("yolov8n.pt"),
        ]
        coco_path = next((p for p in candidates if p.exists()), None)
        if coco_path is None:
            self._loaded_models["_coco_yolov8n"] = None
            return None

        try:
            m = YOLO(str(coco_path))
            self._loaded_models["_coco_yolov8n"] = m
            return m
        except Exception as e:
            print(f"[!] Không nạp được bộ dò phụ trợ yolov8n.pt: {e}")
            self._loaded_models["_coco_yolov8n"] = None
            return None

    def _get_or_load_model(self, model_id: str):
        """Lazy loading: Chỉ tải model vào RAM khi được gọi lần đầu"""
        if model_id in self._loaded_models:
            return self._loaded_models[model_id]

        info = self.registry.get(model_id)
        if not info:
            raise ValueError(f"Không hỗ trợ mô hình: '{model_id}'")

        model_path = self.models_dir / info["file"]

        if not model_path.exists():
            fallback_root = Path("models") / info["file"]
            if fallback_root.exists():
                model_path = fallback_root

        if info["type"] in ("yolo", "yolo_detect"):
            print(f"[*] Đang nạp YOLO model ({model_id}) từ: {model_path}")
            if model_path.exists():
                m = YOLO(str(model_path))
            else:
                m = YOLO("yolov8n.pt" if info["type"] == "yolo_detect" else "best.pt")
            self._loaded_models[model_id] = m
            return m

        elif info["type"] == "torchvision":
            print(f"[*] Đang nạp PyTorch model ({model_id}) từ: {model_path}")
            if model_id == "mobilenet_v3":
                m = models.mobilenet_v3_small(weights=None)
                in_feat = m.classifier[3].in_features
                m.classifier[3] = nn.Linear(in_feat, len(CLASS_NAMES))
            elif model_id == "efficientnet_b0":
                m = models.efficientnet_b0(weights=None)
                in_feat = m.classifier[1].in_features
                m.classifier[1] = nn.Linear(in_feat, len(CLASS_NAMES))
            else:
                raise ValueError(f"Model ID không hợp lệ: {model_id}")

            if model_path.exists():
                checkpoint = torch.load(model_path, map_location=DEVICE)
                m.load_state_dict(checkpoint["model_state_dict"])
                print(f"[+] Đã tải checkpoint {model_path.name} thành công (Val Acc: {checkpoint.get('val_acc', 0)*100:.2f}%)")
            else:
                print(f"[!] Cảnh báo: Không tìm thấy checkpoint {model_path}, khởi tạo model rỗng")

            m = m.to(DEVICE)
            m.eval()
            self._loaded_models[model_id] = m
            return m

    def _crop_with_padding(self, pil_image: Image.Image, xyxy: List[float], pad_ratio: float = 0.06) -> Image.Image:
        w = max(1.0, xyxy[2] - xyxy[0])
        h = max(1.0, xyxy[3] - xyxy[1])
        pad_x = w * pad_ratio
        pad_y = h * pad_ratio
        cx1 = max(0, int(round(xyxy[0] - pad_x)))
        cy1 = max(0, int(round(xyxy[1] - pad_y)))
        cx2 = min(pil_image.width, int(round(xyxy[2] + pad_x)))
        cy2 = min(pil_image.height, int(round(xyxy[3] + pad_y)))
        if cx2 <= cx1 or cy2 <= cy1:
            return pil_image
        return pil_image.crop((cx1, cy1, cx2, cy2))

    def _classify_crops_batch(self, crops: List[Image.Image], classifier_id: str) -> List[Dict[str, float]]:
        """Chạy phân loại tinh theo batch cho toàn bộ danh sách vùng ảnh đã cắt (Đề xuất 3)."""
        if not crops:
            return []

        info = self.registry[classifier_id]
        clf = self._get_or_load_model(classifier_id)

        if info["type"] == "yolo":
            out: List[Dict[str, float]] = []
            for crop in crops:
                res = clf(crop, verbose=False)[0]
                probs_list = res.probs.data.tolist()
                names = res.names
                dist = {c: 0.0 for c in CLASS_NAMES}
                for idx, p in enumerate(probs_list):
                    lbl = names[idx].lower().strip()
                    if lbl in dist:
                        dist[lbl] = float(p)
                out.append(dist)
            return out

        batch_tensor = torch.stack([TORCH_TRANSFORMS(c) for c in crops]).to(DEVICE)
        with torch.no_grad():
            logits = clf(batch_tensor)
            probs_batch = torch.softmax(logits, dim=1).cpu().tolist()

        return [
            {CLASS_NAMES[i]: float(row[i]) for i in range(len(CLASS_NAMES))}
            for row in probs_batch
        ]

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
        stage2_classifier_id: str = "efficientnet_b0",
        strict_stage2: bool = False,
        conf_threshold: float = 0.20,
    ) -> List[Dict[str, Any]]:
        """
        Đề xuất 1 + 2 + 3:
        1. Quét đa vật thể bằng yolov8_detect (ngưỡng nhạy 0.20) kết hợp bộ dò vật chứa yolov8n.pt (COCO)
           để vừa bắt được chai lọ trong suốt cầm trên tay, vừa loại bỏ khung nhiễu toàn phòng khi có người.
        2. Cắt riêng từng Bounding Box (+6% padding) đưa qua Classifier (Batch Two-Stage Refinement).
        3. Khử khung lồng nhau và sắp xếp thứ tự ưu tiên #1..#N theo Center-Weighted Score.
        """
        img_w, img_h = pil_image.size
        img_area = float(max(1, img_w * img_h))

        detector = self._get_or_load_model("yolov8_detect")
        det_res = detector(pil_image, conf=conf_threshold, verbose=False)[0]

        # Chạy bộ dò phụ trợ COCO (nếu có) để phát hiện người cầm vật thể & chai lọ trong suốt
        coco_detector = self._get_coco_detector()
        person_boxes: List[List[float]] = []
        coco_containers: List[Dict[str, Any]] = []
        if coco_detector is not None:
            coco_res = coco_detector(pil_image, conf=0.25, verbose=False)[0]
            for cb in coco_res.boxes:
                cname = coco_detector.names[int(cb.cls[0])]
                cconf = float(cb.conf[0])
                cxyxy = [round(float(v), 1) for v in cb.xyxy[0].tolist()]
                carea_ratio = _box_area(cxyxy) / img_area
                if cname == "person" and cconf >= 0.40 and carea_ratio >= 0.08:
                    person_boxes.append(cxyxy)
                elif cname in COCO_CONTAINER_HINTS and cconf >= (0.45 if cname == "book" else 0.32) and 0.008 <= carea_ratio <= 0.85:
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
            xyxy = [round(float(v), 1) for v in box.xyxy[0].tolist()]
            area_ratio = _box_area(xyxy) / img_area
            height_ratio = (xyxy[3] - xyxy[1]) / float(max(1, img_h))

            if area_ratio < 0.006:
                continue

            # Nếu trong ảnh webcam có người đang ngồi/cầm đồ, lọc bỏ các khung nhiễu ôm người hoặc bao trùm vách tường
            if person_boxes and conf < 0.62:
                overlaps_person = any(_box_iou_and_iomin(xyxy, pb)[0] >= 0.30 for pb in person_boxes)
                is_wall_span = area_ratio > 0.28 and height_ratio > 0.85
                if area_ratio > 0.55 or overlaps_person or is_wall_span:
                    continue

            std_cls, disp_name = DETECT_CLASS_MAPPING.get(raw_cls, ("trash", DISPLAY_NAMES.get("trash", raw_cls.capitalize())))

            # Kiểm tra đồng thuận vị trí với bộ dò vật chứa COCO (VD: bottle trùng khớp plastic/glass)
            coco_boost = 0.0
            for cc in coco_containers:
                iou, _ = _box_iou_and_iomin(xyxy, cc["bbox"])
                if iou >= 0.42:
                    cc["matched"] = True
                    _, allowed = COCO_CONTAINER_HINTS[cc["coco_class"]]
                    if std_cls in allowed:
                        coco_boost = max(coco_boost, cc["confidence"])

            if coco_boost > 0.0:
                conf = 1.0 - (1.0 - conf) * (1.0 - 0.45 * coco_boost)

            raw_candidates.append({
                "raw_class": raw_cls,
                "label": std_cls,
                "display_name": DISPLAY_NAMES.get(std_cls, disp_name),
                "det_confidence": conf,
                "confidence": conf,
                "bbox": xyxy,
                "source": "yolov8_detect",
            })

        # Bổ sung các vật chứa thực tế (chai nhựa trong suốt bị che tay, cốc...) mà yolov8_detect bỏ sót
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

        # Đề xuất 3: Cắt từng vùng vật thể (+6% padding) và chạy Stage-2 Classifier theo batch
        crops = [self._crop_with_padding(pil_image, c["bbox"], pad_ratio=0.06) for c in raw_candidates]
        primary_dists = self._classify_crops_batch(crops, stage2_classifier_id)

        # Với vật thể do COCO khôi phục (chai nước cầm tay), tham khảo thêm MobileNetV3 nếu stage2 đang là EfficientNet
        aux_mob_dists: Optional[List[Dict[str, float]]] = None
        if any(c["source"] == "coco_container_assisted" for c in raw_candidates) and stage2_classifier_id != "mobilenet_v3":
            if (self.models_dir / self.registry["mobilenet_v3"]["file"]).exists():
                aux_mob_dists = self._classify_crops_batch(crops, "mobilenet_v3")

        refined: List[Dict[str, Any]] = []
        for idx, cand in enumerate(raw_candidates):
            cls_dist = primary_dists[idx]
            det_lbl = cand["label"]
            det_conf = cand["det_confidence"]

            if cand["source"] == "coco_container_assisted":
                allowed = cand.get("allowed_labels", CLASS_NAMES)
                blended = dict(cls_dist)
                if aux_mob_dists is not None:
                    mob_d = aux_mob_dists[idx]
                    blended = {k: max(cls_dist[k], mob_d[k]) for k in CLASS_NAMES}

                best_allowed = max(allowed, key=lambda k: blended.get(k, 0.0))
                allowed_prob = blended.get(best_allowed, 0.0)
                if cand["raw_class"] == "bottle" and allowed_prob < 0.42:
                    best_allowed = "plastic"
                    final_conf = min(0.90, 0.62 * det_conf + 0.34)
                else:
                    final_conf = min(0.95, 0.55 * det_conf + 0.58 * max(allowed_prob, 0.42))

                cand["label"] = best_allowed
                cand["display_name"] = DISPLAY_NAMES[best_allowed]
                cand["confidence"] = round(final_conf, 4)
                cand["prob_dist"] = blended
                cand["source"] = f"two_stage_{stage2_classifier_id}"
                refined.append(cand)
                continue

            # Nếu người dùng chủ động chọn mô hình Classifier cụ thể (EfficientNet/MobileNet/YOLOv8n-cls)
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

            # Chế độ mặc định SOTA (yolov8_detect + EfficientNet-B0 hậu kiểm)
            best_cls = max(CLASS_NAMES, key=lambda k: cls_dist[k])
            best_cls_prob = cls_dist[best_cls]
            det_cls_prob = cls_dist.get(det_lbl, 0.0)
            fiber_prob = cls_dist.get("paper", 0.0) + cls_dist.get("cardboard", 0.0)

            if det_conf >= 0.70:
                # Khắc phục nhầm lẫn bề mặt kim loại vs giấy/bìa cứng bóng sáng
                if det_cls_prob < 0.03 and fiber_prob >= 0.72 and best_cls in ("paper", "cardboard"):
                    final_lbl = best_cls
                    final_conf = min(0.95, 0.55 * fiber_prob + 0.25 * det_conf)
                elif best_cls == det_lbl:
                    final_lbl = det_lbl
                    final_conf = min(0.99, 0.82 * det_conf + 0.18 * det_cls_prob)
                else:
                    final_lbl = det_lbl
                    final_conf = det_conf
            else:
                # Với box có độ tự tin trung bình (0.20..0.70), kết hợp xác suất 2 giai đoạn
                fused = {k: 0.45 * cls_dist[k] for k in CLASS_NAMES}
                fused[det_lbl] = fused.get(det_lbl, 0.0) + 0.55 * det_conf
                # Hỗ trợ nhóm tương đồng sợi xenlulo (paper <-> cardboard) khi cả 2 cùng xác nhận là giấy/bìa
                if det_lbl in ("paper", "cardboard") and fiber_prob >= 0.70:
                    final_lbl = best_cls if best_cls in ("paper", "cardboard") else det_lbl
                    final_conf = min(0.94, 0.50 * det_conf + 0.55 * fiber_prob)
                else:
                    final_lbl = max(CLASS_NAMES, key=lambda k: fused[k])
                    final_conf = min(0.96, fused[final_lbl] + (0.12 if final_lbl == det_lbl and det_cls_prob >= 0.25 else 0.0))

                # Lọc bỏ box nhiễu yếu ở rìa (cả detector lẫn classifier đều không chắc chắn)
                if det_conf < 0.35 and best_cls_prob < 0.45 and final_conf < 0.35:
                    continue

            cand["label"] = final_lbl
            cand["display_name"] = DISPLAY_NAMES.get(final_lbl, final_lbl.capitalize())
            cand["confidence"] = round(float(final_conf), 4)
            cand["prob_dist"] = cls_dist
            cand["source"] = "two_stage_ensemble"
            refined.append(cand)

        deduped = self._deduplicate_candidates(refined)

        # Đề xuất 2: Sắp xếp vật thể theo Center-Weighted Score (ưu tiên thêm vật thể đang cầm trên tay)
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
                "source": item.get("source", "two_stage_ensemble"),
                "bbox": item["bbox"],
                "_prob_dist": item.get("prob_dist"),
            })

        return final_objects

    def predict(self, pil_image: Image.Image, model_id: str = "yolov8_detect", use_detection_crop: bool = True) -> Dict[str, Any]:
        """Thực hiện suy luận thống nhất (Hỗ trợ Multi-Object Bounding Box + Center-Weighted + Two-Stage Ensemble)"""
        if model_id not in self.registry:
            raise ValueError(f"Mô hình '{model_id}' không tồn tại hoặc chưa được hỗ trợ. Các mô hình khả dụng: {list(self.registry.keys())}")

        if model_id == "yolov8_detect":
            detect_path = self.models_dir / self.registry["yolov8_detect"]["file"]
            if not detect_path.exists():
                print(f"[!] Warning: Trọng số '{detect_path.name}' chưa có trong thư mục models. Tự động fallback sang mô hình 'yolov8n'.")
                model_id = "yolov8n"

        info = self.registry[model_id]
        img_w, img_h = pil_image.size
        start_time = time.perf_counter()

        stage2_id = "efficientnet_b0" if info["type"] == "yolo_detect" else model_id
        if stage2_id == "efficientnet_b0" and not (self.models_dir / self.registry["efficientnet_b0"]["file"]).exists():
            stage2_id = "yolov8n"

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

        # Nếu tìm thấy >= 1 vật thể đóng khung rõ ràng
        if detected_objects:
            latency_ms = round((time.perf_counter() - start_time) * 1000, 2)
            primary = detected_objects[0]
            primary_dist = primary.get("_prob_dist") or {c: 0.0 for c in CLASS_NAMES}

            # Kết hợp điểm của vật thể chính (#1) và các vật thể phụ để xây dựng bảng phân phối 6 lớp hợp lý
            class_scores: Dict[str, float] = {c: round(float(primary_dist.get(c, 0.0)) * 0.35, 4) for c in CLASS_NAMES}
            for obj in detected_objects:
                lbl = obj["label"]
                if lbl in class_scores:
                    weight = 1.0 if obj["is_primary"] else 0.75
                    class_scores[lbl] = max(class_scores[lbl], round(obj["confidence"] * weight, 4))
            class_scores[primary["label"]] = primary["confidence"]

            all_predictions = [
                {
                    "label": c,
                    "confidence": round(class_scores[c], 4),
                    "display_name": DISPLAY_NAMES.get(c, c.capitalize()),
                }
                for c in CLASS_NAMES
            ]
            # Đảm bảo vật thể #1 (ưu tiên tâm khung hình) luôn đứng đầu top_prediction
            all_predictions.sort(key=lambda x: (1 if x["label"] == primary["label"] else 0, x["confidence"]), reverse=True)

            category_summary: Dict[str, int] = {}
            public_objects: List[Dict[str, Any]] = []
            for obj in detected_objects:
                clean_obj = {k: v for k, v in obj.items() if not k.startswith("_")}
                public_objects.append(clean_obj)
                category_summary[obj["label"]] = category_summary.get(obj["label"], 0) + 1

            return {
                "model_id": model_id,
                "model_name": info["name"],
                "model_family": info["family"],
                "pipeline": "multi_object_two_stage_ensemble",
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

        # Fallback: Không phát hiện được khung rời rạc nào -> Phân loại trực tiếp trên toàn ảnh (Single-stage)
        fallback_clf_id = stage2_id if info["type"] == "yolo_detect" else model_id
        dists = self._classify_crops_batch([pil_image], fallback_clf_id)
        dist = dists[0] if dists else {c: 0.0 for c in CLASS_NAMES}
        latency_ms = round((time.perf_counter() - start_time) * 1000, 2)

        all_predictions = [
            {
                "label": c,
                "confidence": round(float(dist.get(c, 0.0)), 4),
                "display_name": DISPLAY_NAMES.get(c, c.capitalize()),
            }
            for c in CLASS_NAMES
        ]
        all_predictions.sort(key=lambda x: x["confidence"], reverse=True)
        top_pred = all_predictions[0] if all_predictions else None

        return {
            "model_id": model_id,
            "model_name": info["name"],
            "model_family": info["family"],
            "pipeline": "single_stage_full_image",
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
        """Chuyên biệt cho endpoint /api/v1/detect: Khoanh vùng đa vật thể + trả ảnh Base64"""
        start_time = time.perf_counter()
        img_w, img_h = pil_image.size
        objects = self._extract_multi_object_candidates(
            pil_image,
            stage2_classifier_id="efficientnet_b0",
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

        model = self._get_or_load_model("yolov8_detect")
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
