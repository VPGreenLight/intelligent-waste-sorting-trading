import os
import time
import io
from pathlib import Path
from typing import List, Optional

from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from PIL import Image
from ultralytics import YOLO

# Khởi tạo App AI Service
app = FastAPI(
    title="EcoLink Dedicated AI Vision Service",
    description="Microservice thị giác máy tính nhận diện và phân loại rác thải tái chế",
    version="1.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

BASE_DIR = Path(__file__).resolve().parent
MODEL_PATH = BASE_DIR / "models" / "best.pt"
CONFIDENCE_THRESHOLD = 0.60

DISPLAY_NAMES = {
    "cardboard": "Bìa Carton (Cardboard)",
    "glass": "Thủy tinh (Glass)",
    "metal": "Kim loại & Vỏ lon (Metal)",
    "paper": "Giấy báo & Sách vở (Paper)",
    "plastic": "Nhựa tái chế (Plastic)",
    "trash": "Rác thải không tái chế (Trash)"
}

print(f"[*] Đang tải mô hình YOLOv8 từ: {MODEL_PATH}")
if MODEL_PATH.exists():
    model = YOLO(str(MODEL_PATH))
    print("[+] Tải mô hình best.pt thành công!")
else:
    print("[!] Cảnh báo: Không tìm thấy best.pt, dùng yolov8n-cls.pt dự phòng")
    model = YOLO("yolov8n-cls.pt")

class WastePrediction(BaseModel):
    label: str
    confidence: float
    display_name: str

class AiScanResponse(BaseModel):
    success: bool
    is_out_of_distribution: bool
    confidence_threshold: float
    top_prediction: Optional[WastePrediction] = None
    predictions: List[WastePrediction] = []
    inference_time_ms: float

@app.get("/health")
def health_check():
    return {
        "status": "healthy",
        "service": "EcoLink.AiService",
        "model_loaded": MODEL_PATH.exists(),
        "confidence_threshold": CONFIDENCE_THRESHOLD
    }

@app.post("/api/v1/classify", response_model=AiScanResponse)
async def classify_image(file: UploadFile = File(...)):
    if not file.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="Tập tin tải lên phải là hình ảnh (jpg, png, webp).")

    start_time = time.perf_counter()

    try:
        image_bytes = await file.read()
        pil_image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Không thể đọc file hình ảnh: {str(e)}")

    # Thực hiện suy luận (Inference)
    results = model(pil_image)
    inference_time_ms = round((time.perf_counter() - start_time) * 1000, 2)

    result = results[0]
    probs = result.probs
    names = result.names

    all_predictions = []
    for idx, conf in enumerate(probs.data.tolist()):
        lbl = names[idx].lower().strip()
        all_predictions.append(
            WastePrediction(
                label=lbl,
                confidence=round(conf, 4),
                display_name=DISPLAY_NAMES.get(lbl, lbl.capitalize())
            )
        )

    all_predictions.sort(key=lambda x: x.confidence, reverse=True)
    top_pred = all_predictions[0] if all_predictions else None

    # Kiểm tra Out-of-Distribution (OOD): Nếu confidence < 60%
    is_ood = False
    if top_pred is None or top_pred.confidence < CONFIDENCE_THRESHOLD:
        is_ood = True

    return AiScanResponse(
        success=True,
        is_out_of_distribution=is_ood,
        confidence_threshold=CONFIDENCE_THRESHOLD,
        top_prediction=top_pred,
        predictions=all_predictions[:3],
        inference_time_ms=inference_time_ms
    )

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app:app", host="0.0.0.0", port=8000, reload=True)
