import os
import json
import time
import shutil
from pathlib import Path
from typing import Optional

from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import JSONResponse, FileResponse
from fastapi.staticfiles import StaticFiles
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from PIL import Image
import io
from ultralytics import YOLO

# Khởi tạo App
app = FastAPI(title="Waste Classification Local AI Demo", version="1.0.0")

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

BASE_DIR = Path(__file__).resolve().parent
DATA_DIR = BASE_DIR / "data"
MODELS_DIR = BASE_DIR / "models"
STATIC_DIR = BASE_DIR / "static"
RULES_FILE = DATA_DIR / "rules.json"
FEEDBACK_FILE = DATA_DIR / "feedback.json"
STATS_FILE = DATA_DIR / "stats.json"
VAL_DIR = DATA_DIR / "trashnet_split" / "val"

# Đảm bảo các thư mục tồn tại
STATIC_DIR.mkdir(parents=True, exist_ok=True)
DATA_DIR.mkdir(parents=True, exist_ok=True)
MODELS_DIR.mkdir(parents=True, exist_ok=True)

# Khởi tạo ModelManager hỗ trợ đa mô hình (YOLOv8, MobileNetV3, EfficientNet-B0)
from model_manager import ModelManager
model_manager = ModelManager(MODELS_DIR)
print("[+] ModelManager đa mô hình đã sẵn sàng!")

def load_rules():
    if RULES_FILE.exists():
        with open(RULES_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    return {"rules": {}, "confidence_threshold": 0.60}

def save_rules(data):
    with open(RULES_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

def load_feedback():
    if FEEDBACK_FILE.exists():
        with open(FEEDBACK_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    return []

def save_feedback(items):
    with open(FEEDBACK_FILE, "w", encoding="utf-8") as f:
        json.dump(items, f, ensure_ascii=False, indent=2)

def log_scan_stat(category: str, confidence: float, is_ood: bool):
    stats = {
        "total_scans": 0,
        "categories": {},
        "ood_count": 0,
        "recent": []
    }
    if STATS_FILE.exists():
        try:
            with open(STATS_FILE, "r", encoding="utf-8") as f:
                stats = json.load(f)
        except Exception:
            pass

    stats["total_scans"] += 1
    if is_ood:
        stats["ood_count"] += 1
    else:
        stats["categories"][category] = stats["categories"].get(category, 0) + 1

    stats["recent"].insert(0, {
        "category": category,
        "confidence": round(confidence * 100, 1),
        "is_ood": is_ood,
        "timestamp": time.strftime("%H:%M:%S")
    })
    stats["recent"] = stats["recent"][:15]

    with open(STATS_FILE, "w", encoding="utf-8") as f:
        json.dump(stats, f, ensure_ascii=False, indent=2)

# API: Phân loại rác (Hỗ trợ upload ảnh hoặc gửi đường dẫn ảnh mẫu)
# API: Lấy danh sách các mô hình AI và trạng thái
@app.get("/api/models")
async def get_available_models():
    return {
        "models": model_manager.list_available_models(),
        "default": "yolov8n"
    }

# API: Phân loại rác đa mô hình (Hỗ trợ upload ảnh hoặc gửi đường dẫn ảnh mẫu)
@app.post("/api/classify")
async def classify_waste(
    file: Optional[UploadFile] = File(None),
    sample_path: Optional[str] = Form(None),
    model: str = Form("yolov8n")
):
    try:
        if file is not None:
            contents = await file.read()
            image = Image.open(io.BytesIO(contents)).convert("RGB")
        elif sample_path:
            full_path = (BASE_DIR / sample_path).resolve()
            if not full_path.exists() or not str(full_path).startswith(str(BASE_DIR)):
                raise HTTPException(status_code=400, detail="Ảnh mẫu không hợp lệ")
            image = Image.open(full_path).convert("RGB")
        else:
            raise HTTPException(status_code=400, detail="Cần cung cấp ảnh tải lên hoặc ảnh mẫu")

        # Chạy suy luận qua ModelManager với model được chọn
        pred_data = model_manager.predict(image, model_id=model)
        top1 = pred_data["top_prediction"]
        top1_name = top1["label"] if top1 else "trash"
        confidence = top1["confidence"] if top1 else 0.0

        rules_data = load_rules()
        threshold = rules_data.get("confidence_threshold", 0.60)
        is_ood = (confidence / 100.0) < threshold

        rule_info = rules_data.get("rules", {}).get(top1_name.lower(), {})

        # Ghi log thống kê
        log_scan_stat(top1_name, confidence / 100.0, is_ood)

        return {
            "success": True,
            "prediction": {
                "label": top1_name,
                "confidence": confidence,
                "confidence_ratio": confidence / 100.0,
                "is_out_of_distribution": is_ood,
                "threshold": round(threshold * 100, 1),
                "model_used": pred_data["model_id"],
                "model_name": pred_data["model_name"],
                "model_family": pred_data["model_family"],
                "all_probabilities": pred_data["all_probabilities"],
                "inference_time_ms": pred_data["inference_time_ms"],
                "rule": rule_info
            }
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

# API: Lấy danh sách ảnh mẫu đại diện để demo nhanh
@app.get("/api/sample-images")
async def get_sample_images():
    samples = []
    if VAL_DIR.exists():
        for cat_dir in sorted(VAL_DIR.iterdir()):
            if cat_dir.is_dir():
                imgs = list(cat_dir.glob("*.jpg")) + list(cat_dir.glob("*.png"))
                if imgs:
                    # Lấy 2 ảnh đầu mỗi loại rác
                    for img in imgs[:2]:
                        rel_path = img.relative_to(BASE_DIR).as_posix()
                        samples.append({
                            "category": cat_dir.name,
                            "filename": img.name,
                            "path": rel_path,
                            "url": f"/dataset-view/{rel_path}"
                        })
    return {"samples": samples}

# API: Lấy và cập nhật bảng quy tắc (Admin Usecase 9)
@app.get("/api/rules")
async def get_rules():
    return load_rules()

class RuleUpdateRequest(BaseModel):
    category: str
    bin_color_name: Optional[str] = None
    instructions: Optional[list[str]] = None
    confidence_threshold: Optional[float] = None

@app.post("/api/rules")
async def update_rules(req: RuleUpdateRequest):
    data = load_rules()
    if req.confidence_threshold is not None:
        data["confidence_threshold"] = req.confidence_threshold

    if req.category in data.get("rules", {}):
        if req.bin_color_name:
            data["rules"][req.category]["bin_color_name"] = req.bin_color_name
        if req.instructions:
            data["rules"][req.category]["instructions"] = req.instructions

    save_rules(data)
    return {"success": True, "message": "Cập nhật quy tắc thành công"}

# API: Báo cáo nhận diện sai (Feedback Usecase 4 & 11)
class FeedbackRequest(BaseModel):
    predicted_label: str
    corrected_label: str
    confidence: float
    note: Optional[str] = ""

@app.post("/api/feedback")
async def submit_feedback(req: FeedbackRequest):
    items = load_feedback()
    new_item = {
        "id": len(items) + 1,
        "predicted_label": req.predicted_label,
        "corrected_label": req.corrected_label,
        "confidence": req.confidence,
        "note": req.note,
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S")
    }
    items.insert(0, new_item)
    save_feedback(items[:100])
    return {"success": True, "message": "Ghi nhận phản hồi thành công!", "feedback": new_item}

@app.get("/api/feedback")
async def get_all_feedback():
    return load_feedback()

# API: Thống kê Dashboard (Usecase 6 & 10)
@app.get("/api/stats")
async def get_stats():
    stats = {
        "total_scans": 0,
        "categories": {},
        "ood_count": 0,
        "recent": []
    }
    if STATS_FILE.exists():
        try:
            with open(STATS_FILE, "r", encoding="utf-8") as f:
                stats = json.load(f)
        except Exception:
            pass
    return stats

# Mount dataset path để load ảnh mẫu trực tiếp lên Web
if DATA_DIR.exists():
    app.mount("/dataset-view/data", StaticFiles(directory=str(DATA_DIR)), name="data_view")

# Mount Static Files (Frontend UI)
app.mount("/static", StaticFiles(directory=str(STATIC_DIR)), name="static")

@app.get("/")
async def root():
    return FileResponse(str(STATIC_DIR / "index.html"))

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app:app", host="127.0.0.1", port=8000, reload=False)
