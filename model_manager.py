import os
import io
import time
import base64
from pathlib import Path
from typing import Dict, Any, List, Optional
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

# Preprocessing transform cho torchvision models (MobileNet, EfficientNet)
TORCH_TRANSFORMS = transforms.Compose([
    transforms.Resize(256),
    transforms.CenterCrop(224),
    transforms.ToTensor(),
    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225])
])

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

        self._loaded_models = {}
        
        # Danh mục metadata các mô hình hỗ trợ
        self.registry = {
            "yolov8_detect": {
                "id": "yolov8_detect",
                "name": "YOLOv8 Waste Object Detection (SOTA)",
                "family": "Object Detection & Localization (YOLOv8)",
                "file": "yolov8n_waste_detect.pt",
                "params": "3.15M",
                "description": "Khoanh vùng Bounding Box từng món rác, đếm số lượng, loại bỏ 100% phông nền nhiễu (bàn phím, màn hình, tường)",
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
                "avg_latency_ms": 12.0,
                "recommended_for": "Ảnh thực tế có phông nền phức tạp (bàn làm việc, góc nhà), ảnh có nhiều món rác"
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

    def _get_or_load_model(self, model_id: str):
        """Lazy loading: Chỉ tải model vào RAM khi được gọi lần đầu"""
        if model_id in self._loaded_models:
            return self._loaded_models[model_id]

        info = self.registry.get(model_id)
        if not info:
            raise ValueError(f"Không hỗ trợ mô hình: '{model_id}'")

        model_path = self.models_dir / info["file"]
        
        # Dự phòng nếu chưa có model_path cụ thể
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
                m.load_state_dict(checkpoint['model_state_dict'])
                print(f"[+] Đã tải checkpoint {model_path.name} thành công (Val Acc: {checkpoint.get('val_acc', 0)*100:.2f}%)")
            else:
                print(f"[!] Cảnh báo: Không tìm thấy checkpoint {model_path}, khởi tạo model rỗng")

            m = m.to(DEVICE)
            m.eval()
            self._loaded_models[model_id] = m
            return m

    def predict(self, pil_image: Image.Image, model_id: str = "yolov8_detect", use_detection_crop: bool = True) -> Dict[str, Any]:
        """Thực hiện suy luận thống nhất cho tất cả các loại mô hình (Hỗ trợ Two-Stage: Detect -> Crop -> Classify)"""
        if model_id not in self.registry:
            model_id = "yolov8_detect"

        info = self.registry[model_id]
        start_time = time.perf_counter()

        # TH1: Nếu người dùng chọn trực tiếp mô hình Object Detection
        if info["type"] == "yolo_detect":
            model = self._get_or_load_model(model_id)
            results = model(pil_image, conf=0.25, verbose=False)[0]
            latency_ms = round((time.perf_counter() - start_time) * 1000, 2)
            
            detected_items = []
            class_scores = {c: 0.0 for c in CLASS_NAMES}
            
            for box in results.boxes:
                raw_cls = model.names[int(box.cls[0])]
                conf = float(box.conf[0])
                xyxy = [round(x, 1) for x in box.xyxy[0].tolist()]
                
                std_cls, disp_name = DETECT_CLASS_MAPPING.get(raw_cls, ("trash", raw_cls.capitalize()))
                if std_cls in class_scores:
                    class_scores[std_cls] = max(class_scores[std_cls], conf)
                
                detected_items.append({
                    "raw_class": raw_cls,
                    "label": std_cls,
                    "confidence": round(conf, 4),
                    "display_name": disp_name,
                    "bbox": xyxy
                })
            
            all_predictions = []
            for c in CLASS_NAMES:
                all_predictions.append({
                    "label": c,
                    "confidence": round(class_scores[c], 4),
                    "display_name": DISPLAY_NAMES.get(c, c.capitalize())
                })
            all_predictions.sort(key=lambda x: x["confidence"], reverse=True)
            top_pred = all_predictions[0] if all_predictions and all_predictions[0]["confidence"] > 0 else None
            
            return {
                "model_id": model_id,
                "model_name": info["name"],
                "model_family": info["family"],
                "pipeline": "object_detection",
                "top_prediction": top_pred,
                "predictions": [p for p in all_predictions if p["confidence"] > 0][:3] or all_predictions[:3],
                "all_probabilities": all_predictions,
                "detected_objects": detected_items,
                "detected_count": len(detected_items),
                "inference_time_ms": latency_ms
            }

        # TH2: Phân loại bằng MobileNetV3, EfficientNet-B0 hoặc YOLO-cls
        # Áp dụng cơ chế TWO-STAGE: Dùng Detector khoanh vùng và crop vật thể chính để loại bỏ phông nền nhiễu
        cropped_image = pil_image
        pipeline_used = "single_stage_full_image"
        cropped_bbox = None

        if use_detection_crop:
            try:
                detector = self._get_or_load_model("yolov8_detect")
                det_res = detector(pil_image, conf=0.25, verbose=False)[0]
                if len(det_res.boxes) > 0:
                    def box_score(b):
                        xy = b.xyxy[0].tolist()
                        area = (xy[2] - xy[0]) * (xy[3] - xy[1])
                        return float(b.conf[0]) * area

                    best_box = max(det_res.boxes, key=box_score)
                    xyxy = [int(x) for x in best_box.xyxy[0].tolist()]

                    w, h = xyxy[2] - xyxy[0], xyxy[3] - xyxy[1]
                    if (w * h) >= (pil_image.width * pil_image.height * 0.03):
                        pad_x, pad_y = int(w * 0.05), int(h * 0.05)
                        c_x1 = max(0, xyxy[0] - pad_x)
                        c_y1 = max(0, xyxy[1] - pad_y)
                        c_x2 = min(pil_image.width, xyxy[2] + pad_x)
                        c_y2 = min(pil_image.height, xyxy[3] + pad_y)

                        cropped_image = pil_image.crop((c_x1, c_y1, c_x2, c_y2))
                        cropped_bbox = [c_x1, c_y1, c_x2, c_y2]
                        pipeline_used = "two_stage_crop_classified"
            except Exception as e:
                print(f"[!] Warning: Two-Stage crop fallback sang ảnh gốc: {e}")

        model = self._get_or_load_model(model_id)

        if info["type"] == "yolo":
            results = model(cropped_image, verbose=False)
            latency_ms = round((time.perf_counter() - start_time) * 1000, 2)
            result = results[0]
            probs_list = result.probs.data.tolist()
            names = result.names

            all_predictions = []
            for idx, conf in enumerate(probs_list):
                lbl = names[idx].lower().strip()
                all_predictions.append({
                    "label": lbl,
                    "confidence": round(conf, 4),
                    "display_name": DISPLAY_NAMES.get(lbl, lbl.capitalize())
                })

        else:
            # PyTorch Torchvision Inference (MobileNetV3, EfficientNet-B0)
            input_tensor = TORCH_TRANSFORMS(cropped_image).unsqueeze(0).to(DEVICE)
            with torch.no_grad():
                outputs = model(input_tensor)
                probs = torch.softmax(outputs, dim=1)[0].tolist()

            latency_ms = round((time.perf_counter() - start_time) * 1000, 2)

            all_predictions = []
            for idx, conf in enumerate(probs):
                lbl = CLASS_NAMES[idx]
                all_predictions.append({
                    "label": lbl,
                    "confidence": round(conf, 4),
                    "display_name": DISPLAY_NAMES.get(lbl, lbl.capitalize())
                })

        all_predictions.sort(key=lambda x: x["confidence"], reverse=True)
        top_pred = all_predictions[0] if all_predictions else None

        return {
            "model_id": model_id,
            "model_name": info["name"],
            "model_family": info["family"],
            "pipeline": pipeline_used,
            "cropped_bbox": cropped_bbox,
            "top_prediction": top_pred,
            "predictions": all_predictions[:3],
            "all_probabilities": all_predictions,
            "inference_time_ms": latency_ms
        }

    def detect_objects(self, pil_image: Image.Image, conf_threshold: float = 0.25) -> Dict[str, Any]:
        """Chuyên biệt cho bài toán Object Detection: Vẽ Bounding box và trả ảnh Base64"""
        model = self._get_or_load_model("yolov8_detect")
        start_time = time.perf_counter()
        results = model(pil_image, conf=conf_threshold, verbose=False)[0]
        latency_ms = round((time.perf_counter() - start_time) * 1000, 2)
        
        detected_items = []
        category_counts = {}
        
        for box in results.boxes:
            raw_cls = model.names[int(box.cls[0])]
            conf = float(box.conf[0])
            xyxy = [round(x, 1) for x in box.xyxy[0].tolist()]
            
            std_cls, disp_name = DETECT_CLASS_MAPPING.get(raw_cls, ("trash", raw_cls.capitalize()))
            category_counts[std_cls] = category_counts.get(std_cls, 0) + 1
            
            detected_items.append({
                "raw_class": raw_cls,
                "category": std_cls,
                "display_name": disp_name,
                "confidence": round(conf, 4),
                "bbox": xyxy
            })
            
        # Sinh ảnh đã vẽ Bounding Box dưới dạng Base64
        annotated_bgr = results.plot()
        annotated_rgb = annotated_bgr[:, :, ::-1]
        annotated_pil = Image.fromarray(annotated_rgb)
        buf = io.BytesIO()
        annotated_pil.save(buf, format="JPEG", quality=85)
        base64_img = base64.b64encode(buf.getvalue()).decode("utf-8")
        
        return {
            "success": True,
            "total_items": len(detected_items),
            "category_summary": category_counts,
            "detected_objects": detected_items,
            "annotated_image_base64": f"data:image/jpeg;base64,{base64_img}",
            "inference_time_ms": latency_ms
        }
