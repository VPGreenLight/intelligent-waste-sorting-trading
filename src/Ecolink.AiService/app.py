import os
import time
import io
from pathlib import Path
from typing import List, Optional, Dict, Any

from fastapi import FastAPI, UploadFile, File, Query, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from PIL import Image

from model_manager import ModelManager, DISPLAY_NAMES

# Khởi tạo App AI Service
app = FastAPI(
    title="EcoLink Dedicated AI Vision Service",
    description="Microservice thị giác máy tính nhận diện và phân loại rác thải tái chế đa mô hình",
    version="2.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

CONFIDENCE_THRESHOLD = 0.60
model_manager = ModelManager()

class WastePrediction(BaseModel):
    label: str
    confidence: float
    display_name: str

class AiScanResponse(BaseModel):
    success: bool
    is_out_of_distribution: bool
    confidence_threshold: float
    model_used: str
    model_name: str
    model_family: str
    top_prediction: Optional[WastePrediction] = None
    predictions: List[WastePrediction] = []
    inference_time_ms: float

@app.get("/health")
def health_check():
    return {
        "status": "healthy",
        "service": "EcoLink.AiService",
        "version": "2.0.0",
        "supported_models": ["yolov8n", "mobilenet_v3", "efficientnet_b0"],
        "confidence_threshold": CONFIDENCE_THRESHOLD
    }

@app.get("/api/v1/models")
def get_models_list():
    """Lấy danh sách các mô hình AI và trạng thái sẵn sàng"""
    return {
        "models": model_manager.list_available_models(),
        "default_model": "yolov8n"
    }

@app.post("/api/v1/classify", response_model=AiScanResponse)
async def classify_image(
    file: UploadFile = File(...),
    model: str = Query("yolov8n", description="Mô hình AI: yolov8n, mobilenet_v3, efficientnet_b0")
):
    if not file.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="Tập tin tải lên phải là hình ảnh (jpg, png, webp).")

    try:
        image_bytes = await file.read()
        pil_image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Không thể đọc file hình ảnh: {str(e)}")

    # Thực hiện suy luận bằng ModelManager
    try:
        pred_result = model_manager.predict(pil_image, model_id=model)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Lỗi khi thực hiện suy luận mô hình {model}: {str(e)}")

    top_pred_data = pred_result["top_prediction"]
    top_pred = WastePrediction(**top_pred_data) if top_pred_data else None

    # Kiểm tra Out-of-Distribution (OOD): Nếu confidence < 60%
    is_ood = False
    if top_pred is None or top_pred.confidence < CONFIDENCE_THRESHOLD:
        is_ood = True

    predictions_dto = [WastePrediction(**p) for p in pred_result["predictions"]]

    return AiScanResponse(
        success=True,
        is_out_of_distribution=is_ood,
        confidence_threshold=CONFIDENCE_THRESHOLD,
        model_used=pred_result["model_id"],
        model_name=pred_result["model_name"],
        model_family=pred_result["model_family"],
        top_prediction=top_pred,
        predictions=predictions_dto,
        inference_time_ms=pred_result["inference_time_ms"]
    )

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app:app", host="0.0.0.0", port=8000, reload=True)
