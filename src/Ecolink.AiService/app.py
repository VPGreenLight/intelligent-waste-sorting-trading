import os
import io
import time
import json
from pathlib import Path
from typing import List, Optional, Dict, Any

from fastapi import FastAPI, UploadFile, File, Form, Query, HTTPException, status
from fastapi.responses import JSONResponse, FileResponse, RedirectResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field
from PIL import Image

from model_manager import ModelManager, DISPLAY_NAMES, CLASS_NAMES, DEVICE

START_TIME = time.time()

# Khởi tạo App AI Service với Metadata OpenAPI chi tiết
app = FastAPI(
    title="EcoLink Dedicated AI Vision Service",
    description="""
    Microservice Thị giác Máy tính Phân loại Rác thải Tái chế Đa Mô hình (EcoLink AI Vision Service).
    
    Hỗ trợ 3 mô hình học sâu đã tối ưu hóa:
    - **YOLOv8 Nano**: Tốc độ siêu nhanh (~2.5ms), phù hợp cho video và camera thời gian thực.
    - **MobileNetV3-Small**: Rất nhẹ (2.54M params, Val Acc 91.14%), tối ưu cho thiết bị di động/nhúng.
    - **EfficientNet-B0**: Độ chính xác cao nhất (Val Acc 92.32%), xử lý ảnh rác biến dạng và góc chụp khó.
    
    Tích hợp cơ chế an toàn AI (Safe AI):
    - **Out-of-Distribution (OOD)**: Tự động phát hiện và cảnh báo ảnh có độ tin cậy < 60% hoặc không thuộc 6 nhóm rác.
    - **Knowledge Base Integration**: Tự động liên kết nhãn nhận diện với Quy tắc phân loại rác tĩnh (thùng rác, các bước xử lý).
    """,
    version="2.0.0",
    docs_url="/docs",
    redoc_url="/redoc"
)

# Cấu hình CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Định vị các thư mục tài nguyên
SERVICE_DIR = Path(__file__).resolve().parent
ROOT_DIR = SERVICE_DIR.parent.parent

# Tìm thư mục data và models
DATA_DIR = ROOT_DIR / "data" if (ROOT_DIR / "data").exists() else SERVICE_DIR / "data"
MODELS_DIR = SERVICE_DIR / "models" if (SERVICE_DIR / "models").exists() else ROOT_DIR / "models"
SAMPLES_DIR = DATA_DIR / "trashnet_split" / "val"

RULES_FILE = DATA_DIR / "rules.json"
FEEDBACK_FILE = DATA_DIR / "feedback.json"
STATS_FILE = DATA_DIR / "stats.json"

DATA_DIR.mkdir(parents=True, exist_ok=True)
MODELS_DIR.mkdir(parents=True, exist_ok=True)

# Khởi tạo ModelManager
model_manager = ModelManager(MODELS_DIR)
DEFAULT_THRESHOLD = 0.60

# --- Helper Functions cho Rules, Feedback và Stats ---

def load_rules_data() -> Dict[str, Any]:
    if RULES_FILE.exists():
        try:
            with open(RULES_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass
    return {"rules": {}, "confidence_threshold": DEFAULT_THRESHOLD}

def save_rules_data(data: Dict[str, Any]) -> None:
    with open(RULES_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

def load_feedback_data() -> List[Dict[str, Any]]:
    if FEEDBACK_FILE.exists():
        try:
            with open(FEEDBACK_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass
    return []

def save_feedback_data(items: List[Dict[str, Any]]) -> None:
    with open(FEEDBACK_FILE, "w", encoding="utf-8") as f:
        json.dump(items, f, ensure_ascii=False, indent=2)

def record_scan_stat(category: str, confidence: float, is_ood: bool, latency_ms: float, model_used: str):
    stats = {
        "total_scans": 0,
        "categories": {},
        "ood_count": 0,
        "avg_latency_ms": 0.0,
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

    # Tính độ trễ trung bình trượt
    prev_avg = stats.get("avg_latency_ms", 0.0)
    total = stats["total_scans"]
    stats["avg_latency_ms"] = round(((prev_avg * (total - 1)) + latency_ms) / total, 2)

    stats["recent"].insert(0, {
        "category": category,
        "confidence": round(confidence * 100, 1),
        "is_ood": is_ood,
        "model": model_used,
        "latency_ms": latency_ms,
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S")
    })
    stats["recent"] = stats["recent"][:25]

    try:
        with open(STATS_FILE, "w", encoding="utf-8") as f:
            json.dump(stats, f, ensure_ascii=False, indent=2)
    except Exception:
        pass


# --- Pydantic Data Transfer Objects (Schemas) ---

class WastePrediction(BaseModel):
    label: str = Field(..., example="plastic", description="Mã nhãn rác phân loại")
    confidence: float = Field(..., example=0.945, description="Độ tin cậy từ 0.0 đến 1.0")
    display_name: str = Field(..., example="Nhựa tái chế (Plastic)", description="Tên hiển thị tiếng Việt")

class AiScanResponse(BaseModel):
    success: bool = Field(True, description="Trạng thái thực thi yêu cầu")
    is_out_of_distribution: bool = Field(False, description="Cờ cảnh báo vật thể ngoại lai hoặc độ tin cậy thấp")
    confidence_threshold: float = Field(0.60, description="Ngưỡng tin cậy áp dụng")
    model_used: str = Field(..., example="yolov8n", description="Mã định danh mô hình đã sử dụng")
    model_name: str = Field(..., example="YOLOv8 Nano (Default)", description="Tên mô hình")
    model_family: str = Field(..., example="CNN (Ultralytics)", description="Kiến trúc mạng")
    top_prediction: Optional[WastePrediction] = None
    predictions: List[WastePrediction] = Field(default=[], description="Top-3 kết quả có độ tin cậy cao nhất")
    all_probabilities: List[WastePrediction] = Field(default=[], description="Phân phối xác suất toàn bộ 6 lớp rác")
    sorting_rule: Optional[Dict[str, Any]] = Field(None, description="Quy tắc phân loại và hướng dẫn xử lý tĩnh")
    inference_time_ms: float = Field(..., example=12.4, description="Thời gian suy luận mô hình (mili-giây)")

class BatchScanItem(BaseModel):
    filename: str
    success: bool
    is_out_of_distribution: bool
    top_prediction: Optional[WastePrediction] = None
    inference_time_ms: float

class BatchScanResponse(BaseModel):
    success: bool
    total_files: int
    model_used: str
    total_inference_time_ms: float
    avg_inference_time_ms: float
    results: List[BatchScanItem]

class FeedbackCreateRequest(BaseModel):
    predicted_label: str = Field(..., example="plastic")
    user_reported_label: str = Field(..., example="paper")
    confidence_score: float = Field(..., example=0.62)
    note: Optional[str] = Field("", example="Chai nhựa có dán nhãn giấy lớn")
    image_url: Optional[str] = Field(None, example="")

class RuleUpdateRequest(BaseModel):
    category: Optional[str] = Field(None, example="plastic")
    bin_color_name: Optional[str] = Field(None, example="Thùng Màu Vàng")
    instructions: Optional[List[str]] = Field(None, example=["Tráng sạch", "Bóp dẹp chai"])
    confidence_threshold: Optional[float] = Field(None, example=0.65)


# --- API Routes ---

@app.get("/", include_in_schema=False)
def root():
    """Chuyển hướng về trang tài liệu Swagger OpenAPI"""
    return RedirectResponse(url="/docs")


@app.get("/health", tags=["1. System & Health"])
def health_check():
    """Kiểm tra tình trạng sức khỏe của dịch vụ, cấu hình phần cứng và model"""
    rules = load_rules_data()
    return {
        "status": "healthy",
        "service": "EcoLink.AiService",
        "version": "2.0.0",
        "uptime_seconds": round(time.time() - START_TIME, 1),
        "hardware_device": str(DEVICE),
        "cuda_available": DEVICE.type == "cuda",
        "models_available": [m["id"] for m in model_manager.list_available_models() if m["is_ready"]],
        "default_model": "yolov8n",
        "confidence_threshold": rules.get("confidence_threshold", DEFAULT_THRESHOLD)
    }


@app.get("/api/v1/models", tags=["2. Model Management"])
def list_models():
    """Lấy danh sách các mô hình AI có sẵn và trạng thái file trọng số"""
    return {
        "models": model_manager.list_available_models(),
        "default_model": "yolov8n",
        "supported_classes": CLASS_NAMES,
        "classes_count": len(CLASS_NAMES)
    }


@app.get("/api/v1/models/{model_id}", tags=["2. Model Management"])
def get_model_details(model_id: str):
    """Lấy thông số kỹ thuật, cấu trúc mạng và chỉ số benchmark của mô hình cụ thể"""
    details = model_manager.get_model_details(model_id)
    if not details:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy mô hình với id '{model_id}'")
    return details


@app.post("/api/v1/classify", response_model=AiScanResponse, tags=["3. Waste Classification"])
async def classify_waste_image(
    file: UploadFile = File(..., description="Tập tin hình ảnh rác cần phân loại (jpg, png, webp)"),
    model: str = Form("yolov8n", description="Mô hình AI: yolov8n, mobilenet_v3, efficientnet_b0"),
    confidence_threshold: Optional[float] = Form(None, description="Ngưỡng tin cậy OOD (Mặc định 0.60)")
):
    """
    Nhận diện và phân loại rác thải qua ảnh (Single Image Classification):
    - Thực hiện suy luận bằng mô hình được chỉ định (`yolov8n`, `mobilenet_v3`, `efficientnet_b0`).
    - Kiểm tra ngoại lai (OOD): Nếu độ tin cậy < ngưỡng (0.60) thì đánh dấu `is_out_of_distribution = true`.
    - Tự động liên kết bảng quy tắc xử lý rác tĩnh (thùng rác, các bước chuẩn bị).
    """
    if not file.content_type or not file.content_type.startswith("image/"):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Tập tin tải lên phải là hình ảnh hợp lệ (jpg, png, webp)."
        )

    try:
        contents = await file.read()
        pil_image = Image.open(io.BytesIO(contents)).convert("RGB")
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Không thể đọc file hình ảnh: {str(e)}"
        )

    # Đọc quy tắc & ngưỡng tin cậy
    rules_data = load_rules_data()
    threshold = confidence_threshold if confidence_threshold is not None else rules_data.get("confidence_threshold", DEFAULT_THRESHOLD)

    # Thực hiện suy luận
    try:
        pred_result = model_manager.predict(pil_image, model_id=model)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Lỗi trong quá trình suy luận mô hình '{model}': {str(e)}"
        )

    top_pred_data = pred_result.get("top_prediction")
    top_pred = WastePrediction(**top_pred_data) if top_pred_data else None

    # Kiểm tra OOD
    is_ood = False
    if top_pred is None or top_pred.confidence < threshold:
        is_ood = True

    # Lấy quy tắc tương ứng với nhãn
    rule_info = None
    if top_pred and not is_ood:
        rule_info = rules_data.get("rules", {}).get(top_pred.label.lower())

    top_label = top_pred.label if top_pred else "unknown"
    top_conf = top_pred.confidence if top_pred else 0.0

    # Ghi nhận thống kê
    record_scan_stat(top_label, top_conf, is_ood, pred_result["inference_time_ms"], model)

    predictions_dto = [WastePrediction(**p) for p in pred_result["predictions"]]
    all_probs_dto = [WastePrediction(**p) for p in pred_result["all_probabilities"]]

    return AiScanResponse(
        success=True,
        is_out_of_distribution=is_ood,
        confidence_threshold=round(threshold, 2),
        model_used=pred_result["model_id"],
        model_name=pred_result["model_name"],
        model_family=pred_result["model_family"],
        top_prediction=top_pred,
        predictions=predictions_dto,
        all_probabilities=all_probs_dto,
        sorting_rule=rule_info,
        inference_time_ms=pred_result["inference_time_ms"]
    )


@app.post("/api/v1/classify/batch", response_model=BatchScanResponse, tags=["3. Waste Classification"])
async def classify_waste_batch(
    files: List[UploadFile] = File(..., description="Danh sách các file ảnh cần phân loại"),
    model: str = Form("yolov8n", description="Mô hình AI sử dụng"),
    confidence_threshold: Optional[float] = Form(None, description="Ngưỡng tin cậy OOD")
):
    """Phân loại rác theo lô nhiều ảnh (Batch Classification) để kiểm thử hiệu năng và độ chính xác"""
    if not files:
        raise HTTPException(status_code=400, detail="Cần cung cấp ít nhất một file ảnh.")

    rules_data = load_rules_data()
    threshold = confidence_threshold if confidence_threshold is not None else rules_data.get("confidence_threshold", DEFAULT_THRESHOLD)

    results = []
    total_start_time = time.perf_counter()

    for f in files:
        try:
            content = await f.read()
            img = Image.open(io.BytesIO(content)).convert("RGB")
            pred_data = model_manager.predict(img, model_id=model)
            top_p = pred_data.get("top_prediction")
            top_pred_dto = WastePrediction(**top_p) if top_p else None
            is_ood = top_pred_dto is None or top_pred_dto.confidence < threshold

            results.append(BatchScanItem(
                filename=f.filename,
                success=True,
                is_out_of_distribution=is_ood,
                top_prediction=top_pred_dto,
                inference_time_ms=pred_data["inference_time_ms"]
            ))
        except Exception:
            results.append(BatchScanItem(
                filename=f.filename,
                success=False,
                is_out_of_distribution=True,
                top_prediction=None,
                inference_time_ms=0.0
            ))

    total_time_ms = round((time.perf_counter() - total_start_time) * 1000, 2)
    avg_time_ms = round(total_time_ms / len(files), 2) if files else 0.0

    return BatchScanResponse(
        success=True,
        total_files=len(files),
        model_used=model,
        total_inference_time_ms=total_time_ms,
        avg_inference_time_ms=avg_time_ms,
        results=results
    )


@app.get("/api/v1/samples", tags=["4. Testing Data & Samples"])
def get_sample_images_list():
    """Liệt kê danh sách các ảnh mẫu trong tập test theo từng danh mục rác kèm đường dẫn tải về"""
    samples_by_category: Dict[str, List[Dict[str, Any]]] = {}

    if SAMPLES_DIR.exists():
        for cat_dir in sorted(SAMPLES_DIR.iterdir()):
            if cat_dir.is_dir():
                cat_name = cat_dir.name
                imgs = sorted(list(cat_dir.glob("*.jpg")) + list(cat_dir.glob("*.png")))
                samples_by_category[cat_name] = []
                for img in imgs[:5]:  # Lấy 5 ảnh mẫu mỗi loại
                    samples_by_category[cat_name].append({
                        "category": cat_name,
                        "filename": img.name,
                        "file_size_bytes": img.stat().st_size,
                        "download_url": f"/api/v1/samples/{cat_name}/{img.name}"
                    })

    total_samples = sum(len(v) for v in samples_by_category.values())
    return {
        "total_samples": total_samples,
        "categories": list(samples_by_category.keys()),
        "samples": samples_by_category
    }


@app.get("/api/v1/samples/{category}/{filename}", tags=["4. Testing Data & Samples"])
def download_sample_image(category: str, filename: str):
    """Xem hoặc tải về file ảnh mẫu cụ thể để dùng trong Postman test case"""
    file_path = SAMPLES_DIR / category / filename
    if not file_path.exists() or not file_path.is_file():
        raise HTTPException(status_code=404, detail="Không tìm thấy file ảnh mẫu")
    return FileResponse(path=str(file_path), media_type="image/jpeg", filename=filename)


@app.get("/api/v1/rules", tags=["5. Rules & Knowledge Base"])
def get_all_rules():
    """Tra cứu toàn bộ cơ sở tri thức quy tắc phân loại rác tĩnh (thùng rác, các bước xử lý)"""
    return load_rules_data()


@app.put("/api/v1/rules", tags=["5. Rules & Knowledge Base"])
def update_rules(req: RuleUpdateRequest):
    """Cập nhật ngưỡng tin cậy OOD hoặc cập nhật hướng dẫn phân loại cho danh mục rác"""
    data = load_rules_data()

    if req.confidence_threshold is not None:
        data["confidence_threshold"] = req.confidence_threshold

    if req.category and req.category in data.get("rules", {}):
        if req.bin_color_name:
            data["rules"][req.category]["bin_color_name"] = req.bin_color_name
        if req.instructions:
            data["rules"][req.category]["instructions"] = req.instructions

    save_rules_data(data)
    return {
        "success": True,
        "message": "Cập nhật quy tắc phân loại thành công",
        "confidence_threshold": data.get("confidence_threshold", DEFAULT_THRESHOLD)
    }


@app.post("/api/v1/feedback", tags=["6. Feedback & Retrain Loop"])
def submit_misclassification_feedback(req: FeedbackCreateRequest):
    """Báo cáo kết quả AI nhận diện sai để người quản trị kiểm chứng và đưa vào tập dữ liệu retrain"""
    items = load_feedback_data()
    new_item = {
        "id": len(items) + 1,
        "predicted_label": req.predicted_label,
        "user_reported_label": req.user_reported_label,
        "confidence_score": req.confidence_score,
        "note": req.note or "",
        "image_url": req.image_url or "",
        "status": "pending_verification",
        "created_at": time.strftime("%Y-%m-%d %H:%M:%S")
    }
    items.insert(0, new_item)
    save_feedback_data(items[:200])

    return {
        "success": True,
        "message": "Ghi nhận phản hồi thành công! Dữ liệu đã được lưu trữ cho vòng lặp huấn luyện lại.",
        "feedback": new_item
    }


@app.get("/api/v1/feedback", tags=["6. Feedback & Retrain Loop"])
def get_all_feedback_logs():
    """Lấy danh sách các phản hồi báo sai từ người dùng"""
    return {
        "total": len(load_feedback_data()),
        "feedback_logs": load_feedback_data()
    }


@app.get("/api/v1/stats", tags=["7. Analytics & Stats"])
def get_inference_stats():
    """Xem thống kê sử dụng AI: tổng lượt quét, phân bổ danh mục rác, tỷ lệ OOD và lịch sử gần nhất"""
    stats = {
        "total_scans": 0,
        "categories": {},
        "ood_count": 0,
        "avg_latency_ms": 0.0,
        "recent": []
    }
    if STATS_FILE.exists():
        try:
            with open(STATS_FILE, "r", encoding="utf-8") as f:
                stats = json.load(f)
        except Exception:
            pass

    ood_rate = round((stats["ood_count"] / stats["total_scans"] * 100), 1) if stats["total_scans"] > 0 else 0.0
    return {
        **stats,
        "ood_rate_percent": ood_rate,
        "active_models_count": len([m for m in model_manager.list_available_models() if m["is_ready"]])
    }


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app:app", host="0.0.0.0", port=8000, reload=False)

