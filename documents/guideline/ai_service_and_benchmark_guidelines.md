# QUY CHUẨN PHÁT TRIỂN VÀ ĐÁNH GIÁ MÔ HÌNH AI (AI SERVICE & BENCHMARK GUIDELINES)
**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** `Ecolink.AiService` (Python FastAPI + Ultralytics YOLO26 + PyTorch)  
**Phạm vi áp dụng:** Huấn luyện mô hình, kiểm thử đánh giá độ chính xác (Benchmark) và triển khai dịch vụ API nhận diện rác.  

---

## 1. MỤC ĐÍCH VÀ KIẾN TRÚC MÔ HÌNH AI

Phân hệ AI của EcoLink giải quyết bài toán cốt lõi: **Tự động nhận diện và phân loại rác thải tái chế từ ảnh chụp thực tế của người dùng**, loại bỏ nhiễu nền môi trường và hỗ trợ gợi ý quy chuẩn phân loại chính xác.

### 1.1. Kiến trúc Xử lý Hai Giai đoạn hợp nhất YOLO26 (Unified YOLO26 Two-Stage Pipeline)
Để giải quyết triệt để lỗi phân loại sai do nền ảnh phức tạp (bàn làm việc, màn hình máy tính, sàn gạch...), EcoLink hợp nhất toàn bộ pipeline suy luận quanh kiến trúc **YOLO26 End-to-End NMS-Free + YOLO26s-cls (256x256) True Batch Inference**:

```mermaid
flowchart LR
    RawImage["🖼️ Ảnh gốc từ Camera<br>(Nhiều nền gây nhiễu)"] --> Stage1["1. Object Detection<br>(YOLO26n Waste Detect 640px + COCO NMS-Free)"]
    Stage1 -->|"Square Context Crop +6% (<=288px)"| CleanCrop["✂️ Tập ảnh rác đã cô lập<br>(Batch Crops)"]
    CleanCrop --> Stage2["2. Batch Classifier Engine<br>(YOLO26s-cls 256px Core / YOLO26n-cls)"]
    Stage2 --> Output["📊 Kết quả Phân loại<br>(Danh mục, Độ tin cậy, OOD flag)"]
```

* **Giai đoạn 1 (Localization & Bounding Box):** Hệ thống thu phóng ảnh quá lớn về cạnh tối đa `960px` cho bộ dò chính thức `yolo26n_waste_detect.pt` (`5.19 MB`, `2.58M` params, `90.78% mAP@0.5` chạy thẳng ở `imgsz=640`; chỉ fallback `yolov8n_waste_detect.pt` ở `imgsz=352` nếu thiếu file) và kích hoạt có điều kiện bộ dò phụ trợ COCO NMS-Free `yolo26n.pt` (`imgsz=320`) khi cần (`0` box, `conf < 0.75`, hoặc box lớn `area_ratio > 0.25`), xếp hạng vật thể theo kích thước & độ gần tâm khung hình, và cắt vùng rác theo **Square Context Crop** (+6% padding, thu nhỏ `<= 288px` trước khi đệm viền vuông).
* **Giai đoạn 2 (Batch Fine-grained Classification):** Gộp toàn bộ các vùng ảnh cắt vào **1 lần suy luận batch (`clf(crops, imgsz=256, verbose=False)`)** bằng **`YOLO26s-cls` (`yolo26s_waste_cls.pt`, `5.46M` params, `97.10%` val / `96.17%` test 12-lớp)** để xuất xác suất 12 lớp và gộp 3 màu thủy tinh (`brown-glass` + `green-glass` + `white-glass` $\rightarrow$ `glass`) thành **10 danh mục chuẩn EcoLink** gồm: 5 nhóm phế liệu thương mại (`plastic`, `cardboard`, `metal`, `paper`, `glass` – `IsRecyclable = true`) và 5 nhóm chỉ áp dụng Policy xử lý môi trường (`battery`, `biological`, `clothes`, `shoes`, `trash` – `IsRecyclable = false`, chặn khỏi luồng thu mua đồng nát). Vẫn duy trì `YOLO26n-cls` (`yolo26n_waste_cls.pt`, `224x224`) cho nhu cầu siêu nhẹ và Torchvision (`efficientnet_b0`, `mobilenet_v3`) cho đối chiếu học thuật cùng các alias tương thích ngược (`yolov8_detect -> yolo26_detect`, `yolov8n -> yolo26n_cls`, `yolo26s -> yolo26s_cls`, `yolo26n -> yolo26n_cls`).

---

## 2. QUY CHUẨN ĐỊNH DẠNG API PHÂN HỆ AI (FASTAPI REST CONTRACT)

### 2.1. Endpoint Phân loại tổng hợp: `POST /api/v1/classify`
* **Content-Type:** `multipart/form-data` (trường `file` chứa file ảnh định dạng JPG/PNG/WEBP).
* **Form Parameters (Tùy chọn):** `model` (mặc định `"yolo26_detect"`, hỗ trợ `"yolo26s_cls"`, `"yolo26n_cls"`, `"efficientnet_b0"`, `"mobilenet_v3"`, và các alias `"yolov8_detect"`, `"yolov8n"`, `"yolo26s"`, `"yolo26n"`), `use_detection_crop` (mặc định `true`), `confidence_threshold` (mặc định `0.60` theo `rules_data`).
* **Định dạng JSON trả về thực tế (`AiScanResponse`):**
```json
{
  "success": true,
  "is_out_of_distribution": false,
  "confidence_threshold": 0.6,
  "model_used": "yolo26_detect",
  "pipeline": "yolo26_unified_two_stage",
  "inference_time_ms": 401.13,
  "top_prediction": {
    "label": "plastic",
    "confidence": 0.9081,
    "display_name": "Nhựa tái chế (Plastic)"
  },
  "detected_count": 4,
  "cropped_bbox": [1171.4, 500.3, 1693.4, 2433.4]
}
```

### 2.2. Xử lý trường hợp ảnh không phải rác (Out-of-Distribution - OOD)
* Nếu độ tin cậy cao nhất của mô hình thấp hơn ngưỡng cấu hình (**Confidence `< 0.60`** mặc định trong `Ecolink.AiService`, ngưỡng sàn kiến nghị $\ge 0.50$) hoặc khi không phát hiện được vật thể nào (`detected_count = 0`, tự động giảm hệ số tin cậy toàn ảnh `0.55x`), hệ thống kích hoạt cờ OOD:
```json
{
  "success": true,
  "is_out_of_distribution": true,
  "confidence_threshold": 0.6,
  "model_used": "yolo26_detect",
  "pipeline": "yolo26_single_stage",
  "top_prediction": {
    "label": "cardboard",
    "confidence": 0.5019,
    "display_name": "Bìa Carton (Cardboard)"
  },
  "detected_count": 0,
  "inference_time_ms": 396.91
}
```

---

## 3. TIÊU CHÍ NGHIỆM THU VÀ ĐÁNH GIÁ ĐỘ CHÍNH XÁC (AI BENCHMARK METRICS)

Để đạt điểm xuất sắc trong Báo cáo Đồ án Capstone (Mục Khảo nghiệm AI), các mô hình khi huấn luyện và bàn giao phải đạt các chỉ số tối thiểu:

| Chỉ số Đánh giá | Công thức / Ý nghĩa | Ngưỡng Nghiệm thu Tối thiểu | Mục tiêu Khuyến nghị |
| :--- | :--- | :---: | :---: |
| **mAP@0.5** (Mean Average Precision) | Độ chính xác trung bình trên ngưỡng IoU 50% | **$\ge 80.0\%$** | **$\ge 88.0\%$** |
| **Precision** (Độ chuẩn xác) | Tỷ lệ vật thể dự đoán đúng trên tổng số dự đoán | **$\ge 75.0\%$** | **$\ge 85.0\%$** |
| **Recall** (Độ bao phủ) | Tỷ lệ vật thể rác phát hiện được trên tổng số rác thật | **$\ge 75.0\%$** | **$\ge 82.0\%$** |
| **Inference Time (CPU)** | Thời gian phân tích 1 ảnh trên chip Intel Core i5/i7 | **$< 1500\text{ ms}$** | **$< 600\text{ ms}$** |
| **Inference Time (GPU)** | Thời gian phân tích 1 ảnh trên GPU T4 / RTX | **$< 300\text{ ms}$** | **$< 80\text{ ms}$** |

---

## 4. QUY CHUẨN BỘ DỮ LIỆU HUẤN LUYỆN (DATASET INTEGRITY)

1. **Tỷ lệ Phân chia Dữ liệu Bắt buộc:**
   * **Tập Huấn luyện (Train Set):** $70\%$ (Dùng để tối ưu trọng số mô hình).
   * **Tập Xác thực (Validation Set):** $20\%$ (Dùng để tinh chỉnh siêu tham số và early stopping).
   * **Tập Kiểm thử Độc lập (Test Set):** $10\%$ (Tập dữ liệu "mù", tuyệt đối không dùng trong quá trình train để đánh giá trung thực).
2. **Ngăn chặn Rò rỉ Dữ liệu (No Data Leakage):**
   * Nếu có nhiều góc chụp của cùng một chai nhựa, **tất cả các ảnh của chai nhựa đó phải nằm chung trong cùng 1 tập** (hoặc cùng Train, hoặc cùng Test). Tuyệt đối không để ảnh cùng 1 vật thể xuất hiện ở cả Train và Test.
3. **Cân bằng Danh mục (Class Balance):**
   * Số lượng mẫu giữa các danh mục rác chính (`plastic`, `cardboard`, `paper`, `metal`, `glass`) không được chênh lệch vượt quá tỷ lệ $1 : 2.5$.

---

## 5. BỘ KIỂM THỬ NGOẠI LỆ BẮT BUỘC (EDGE CASES & ROBUSTNESS TEST SUITE)

Mỗi phiên bản mô hình trước khi đóng gói Release phải vượt qua bộ ảnh kiểm thử đặc biệt:

| Kịch bản Kiểm thử | Loại ảnh thử nghiệm | Kết quả Kỳ vọng |
| :--- | :--- | :--- |
| **1. Vật thể rác có nền phức tạp** | Chai nhựa đặt trước màn hình laptop hiển thị hình ảnh phức tạp | Bounding box crop chính xác chai nhựa, phân loại đúng `Plastic` $\ge 85\%$. |
| **2. Nhiều vật thể trong 1 ảnh** | 4 chai nhựa nằm cạnh nhau | Báo đúng số lượng 4 vật thể, không bị sót. |
| **3. Ảnh Out-Of-Distribution (OOD)** | Bàn phím máy tính, cốc cà phê gốm, mặt người | Trả về `isOutOfDistribution: true`, từ chối phân loại sai. |
| **4. Ảnh điều kiện ánh sáng yếu** | Rác chụp trong phòng tối, ngược sáng đèn flash | Confidence score suy giảm nhưng nhãn dự đoán chính không bị đảo lộn danh mục. |

---

## 6. QUY TẮC QUẢN LÝ TỆP TRỌNG SỐ (MODEL ARTIFACT MANAGEMENT)

1. **Quy ước đặt tên tệp trọng số (`.pt`):**  
   `[architecture]_[task]_[version].pt`  
   *Các trọng số chính thức YOLO26 hiện hành:*
   * `yolo26n.pt` (COCO End-to-End NMS-Free Object Detector)
   * `yolo26s_waste_cls.pt` (YOLO26s-cls Classifier 256x256 fine-tune trên Garbage Classification 12 lớp -> ánh xạ 10 danh mục EcoLink – Engine Stage-2 mặc định)
   * `yolo26n_waste_cls.pt` (YOLO26n-cls Classifier 224x224 siêu nhẹ cho cấu hình Edge / đối chiếu)
   * `yolo26n_waste_detect.pt` (YOLO26n Waste Detector `640x640`, `5.19 MB`, `90.78% mAP@0.5`, `83.27% mAP@0.5:0.95`, `P: 84.71%`, `R: 85.28%` – Engine Stage-1 chính thức)
   * *Trọng số đối chiếu / tương thích cũ:* `yolov8n_waste_detect.pt`, `yolov8n_waste_cls.pt`, `mobilenet_v3_waste_cls.pt`, `efficientnet_b0_waste_cls.pt`
2. **Không commit trọng số thử nghiệm & tránh trùng lặp:**  
   Thư mục `runs/` (chứa hàng trăm file epoch nặng hàng GB) và weight base tiền huấn luyện (`yolo26s-cls.pt`, `yolo26n-cls.pt`) bắt buộc nằm trong `.gitignore`. Toàn bộ trọng số `best.pt` chính thức được lưu tập trung duy nhất tại thư mục `src/Ecolink.AiService/models/` (không nhân đôi bản sao ở thư mục gốc `models/`).
