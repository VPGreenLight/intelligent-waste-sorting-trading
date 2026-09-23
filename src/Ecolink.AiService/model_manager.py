import os
import time
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
            # Tìm thư mục models ở cấp service hoặc cấp root
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
            "yolov8n": {
                "id": "yolov8n",
                "name": "YOLOv8 Nano (Default)",
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
                "description": "Độ chính xác cao nhất, xử lý tốt các góc chụp khó và rác biến dạng",
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

        if info["type"] == "yolo":
            print(f"[*] Đang nạp YOLO model từ: {model_path}")
            if model_path.exists():
                m = YOLO(str(model_path))
            else:
                m = YOLO("yolov8n-cls.pt")
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

    def predict(self, pil_image: Image.Image, model_id: str = "yolov8n") -> Dict[str, Any]:
        """Thực hiện suy luận thống nhất cho tất cả các loại mô hình"""
        if model_id not in self.registry:
            model_id = "yolov8n"

        info = self.registry[model_id]
        model = self._get_or_load_model(model_id)

        start_time = time.perf_counter()

        if info["type"] == "yolo":
            results = model(pil_image)
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
            # PyTorch Torchvision Inference
            input_tensor = TORCH_TRANSFORMS(pil_image).unsqueeze(0).to(DEVICE)
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
            "top_prediction": top_pred,
            "predictions": all_predictions[:3],
            "all_probabilities": all_predictions,
            "inference_time_ms": latency_ms
        }
