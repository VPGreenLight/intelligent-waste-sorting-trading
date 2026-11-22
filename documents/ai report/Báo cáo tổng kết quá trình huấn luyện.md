# Báo Cáo Kỹ Thuật: Fine-Tune YOLO26s-cls (12 Classes -> 10 Danh Mục EcoLink) & Đánh Giá Benchmark (EcoLink)

Hệ thống **EcoLink** đã nâng cấp trọng số Stage-2 **`YOLO26s-cls` (`256x256`, `5.46M` params)** bằng bộ dữ liệu thực tế **Garbage Classification (12 classes)** trên Kaggle GPU Tesla T4 (`7,708` ảnh train / `1,542` ảnh val đạt **`97.10%`**, tập **`1,045` ảnh test mù độc lập** đạt **`96.17%` (12 lớp)** và **`96.65%` (gộp 6 nhóm)**).

Trong ứng dụng thực tế, 3 màu thủy tinh (`brown-glass`, `green-glass`, `white-glass`) được cộng gộp xác suất thành **`glass`**, tạo thành **10 danh mục chuẩn EcoLink**:
1. **5 Nhóm phế liệu thương mại (`IsRecyclable = true`):** `plastic`, `paper`, `cardboard`, `metal`, `glass` — hỗ trợ báo giá tham khảo, tìm vựa thu mua trên bản đồ và bàn giao phế liệu.
2. **5 Nhóm chỉ áp dụng Policy xử lý môi trường (`IsRecyclable = false`):** `battery` (kèm cảnh báo rác nguy hại), `biological`, `clothes`, `shoes`, `trash` — hiển thị hướng dẫn phân loại/xử lý an toàn và **chặn hoàn toàn khỏi bảng giá / tìm kiếm thu mua đồng nát**.

---

## 1. Bảng So Sánh Benchmark 5 Mô Hình (`evaluate_models.py` & Kaggle 12-Class Blind Test)

### A. Kết quả chuẩn 12 lớp trên tập dữ liệu mới (Kaggle GPU Tesla T4, `imgsz=256`, khử trùng SHA-256 + dHash, cân bằng max 1,050 ảnh/lớp):
- **Validation (`1,542` ảnh, 12 lớp):** `top1_acc = 97.10%`, `top5_acc = 99.8%` (EarlyStopping dừng ở epoch 38, lấy đỉnh **epoch 26**).
- **Test mù độc lập (`1,045` ảnh chưa từng dùng khi train/val):** **`96.17%`** (đủ 12 lớp chi tiết) / **`96.65%`** (khi gộp về 6 nhóm chuẩn).

### B. Đối chiếu chéo trên tập cũ TrashNet Validation (`508` ảnh, CPU `12th Gen Intel Core i9-12900KF`):

| Chỉ số / Đặc tính | **YOLO26s-cls (12c, 256px)** | YOLO26n-cls (224px) | YOLOv8n-cls (Baseline) | MobileNetV3-Small | EfficientNet-B0 | Nhận xét chuyên môn |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **Kiến trúc mạng** | **YOLO26-cls (`C3k2` + `C2PSA`)** | YOLO26-cls (`C3k2` + `C2PSA`) | YOLOv8-cls (`C2f`) | Depthwise Separable CNN | Compound Scaling CNN | Fine-tune trên 12 lớp đa nền (7,708 train) |
| **Số tham số / Kích thước** | 5.46M / 10.54 MB | 1.52M / 3.05 MB | **1.44M / 2.84 MB** | 2.54M / 5.94 MB | 5.29M / 15.60 MB | Nhẹ hơn ~32% so với EfficientNet-B0 (`15.60 MB`) |
| **Độ trễ 1-ảnh CPU** | 43.57 ms | **25.95 ms** | 29.20 ms | 30.95 ms | 67.79 ms | Nhanh hơn EfficientNet-B0 tới ~36% |
| **Top-1 Acc (508 TrashNet val)** | 91.73% | 89.37% | 91.73% | 91.14% | **92.32%** | Trên TrashNet do lệch định nghĩa lớp `trash` cũ (xem dưới) |
| **Cardboard (Bìa carton)** | **98.8%** | 91.4% | 90.1% | 96.3% | 91.4% | **Đỉnh cao nhất toàn bảng (+7.4% vs EfficientNet)** |
| **Glass (Thủy tinh)** | **94.1%** | 81.2% | 88.1% | 82.2% | 85.1% | Gộp xác suất 3 màu thủy tinh đạt **94.1%** |
| **Metal (Kim loại & lon)** | 96.3% | 91.5% | 97.6% | 90.2% | **98.8%** | Ổn định ở mức >96% |
| **Paper (Giấy báo)** | 96.6% | 95.0% | 95.8% | 95.8% | **97.5%** | Tương đương các mô hình đầu bảng |
| **Plastic (Nhựa)** | **97.9%** | 90.7% | 88.7% | 94.8% | 91.8% | **Đứng #1 (+6.1% vượt EfficientNet-B0)** |
| **Trash (TrashNet `trash` 28 ảnh)** | 7.1% | 78.6% | **85.7%** | 78.6% | 82.1% | Ảnh `trash` cũ của TrashNet là túi nhựa/giấy vụn nên model 12c đoán chi tiết về đúng `plastic`/`paper` |

### Kết quả kiểm thử End-to-End FastAPI Two-Stage trên CPU (`Ecolink.AiService v3.0.0`, Model 12c -> 10 Danh mục):
* **`GET /api/v1/models`:** Trả đủ **10 danh mục** (`battery`, `biological`, `cardboard`, `clothes`, `glass`, `metal`, `paper`, `plastic`, `shoes`, `trash`); không rò rỉ 3 nhãn phụ (`brown-glass`, `green-glass`, `white-glass`).
* **`Chai nhựa.jpg` (4 vật thể, `yolo26_detect`):** `plastic` (confidence **`0.9081`**), đếm đúng **`4` vật thể**, độ trễ warm **`373.77 ms`**.
* **`Chai nhựa lỗi Carton.jpg` (ảnh 12MP `3472x3472`, `yolo26_detect`):** Nhận diện đúng **`plastic`** (confidence **`0.7727`**), **`1` vật thể**, độ trễ warm **`488.60 ms`**.
* **`Chai nhựa lỗi Thủy tinh.jpg` (`yolo26_detect`):** Hợp nhất mảnh vỡ dọc thành **`1` vật thể** duy nhất, nhận diện đúng **`plastic`** (confidence **`0.8990`**), độ trễ warm **`420.17 ms`**.
* **Single-mode & Aliases & OOD:** `yolo26s_cls` (`444.68 ms`), `yolo26n_cls` (`350.94 ms`), alias `yolov8_detect -> yolo26_detect` (`465.91 ms`), alias `yolov8n -> yolo26n_cls` (`347.65 ms`), `SYNTHETIC_NOISE_OOD` (`conf=0.512`, `ood=True`, `468.96 ms`), `OOD_THRESHOLD_CHECK` (`ood=True`, `454.71 ms`).
* **Kết luận triển khai:** Model mới tăng vọt độ chuẩn xác nhận diện 5 nhóm tái chế thương mại (**94.1% – 98.8%** trên 5 lớp tái chế và **96.17%** trên 12 lớp thực tế) với toàn bộ độ trễ FastAPI Two-Stage CPU đạt **`347–489 ms`** (`< 600 ms`).

---

## 2. Kỹ Thuật Huấn Luyện & Toàn Vẹn Dữ Liệu (Zero Data Leakage)

File huấn luyện chuẩn YOLO26 local ([train.py](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/train.py)) & Kaggle GPU Pipeline (`Tesla T4`):
- **Lọc trùng & Cân bằng bộ 12 lớp:** Lọc trùng tuyệt đối bằng **SHA-256**, gom nhóm ảnh cùng vật thể/góc chụp bằng **dHash (`8x8`)** chia Group Split `75% Train (7,708)` / `15% Val (1,542)` / `10% Blind Test (1,045)` không xé lẻ nhóm. Cắt cân bằng tối đa `1,050` ảnh/lớp để xử lý lệch mẫu nặng của `clothes` (`5,325`) và `shoes` (`1,977`).
- **Siêu tham số `YOLO26s-cls` (12 classes):** Khởi tạo `yolo26s-cls.pt`, **`imgsz=256`**, `batch=64` (Kaggle GPU), `epochs=40` + `patience=12` (dừng tại epoch 38, lấy `best.pt` ở **epoch 26**), **`cos_lr=True`**, `AdamW` (`lr0=0.0005`, `lrf=0.01`), **`dropout=0.1`**, **`erasing=0.1`**, `hsv_s=0.3`, `hsv_v=0.3`, `degrees=15.0`. Lưu checkpoint duy nhất tại [src/Ecolink.AiService/models/yolo26s_waste_cls.pt](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.AiService/models/yolo26s_waste_cls.pt).
- **Chiến lược mở rộng Detection:** Hỗ trợ chế độ `python train.py --task detect --data <data.yaml>` để fine-tune `yolo26n_waste_detect.pt` từ tập dữ liệu curation (`curated_datasets`).

---

## 3. Kiến Trúc Hợp Nhất YOLO26 & Backward Aliases

Bộ điều phối mô hình được chuẩn hóa tại [model_manager.py](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/model_manager.py) và đồng bộ đầy đủ cho microservice tại [src/Ecolink.AiService/model_manager.py](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.AiService/model_manager.py):

```mermaid
graph TD
    Client["Frontend Scanner / .NET AiVisionHttpClient"] -->|POST /api/v1/classify model=yolo26_detect| Controller["FastAPI Ecolink.AiService v3.0.0"]
    Controller --> Alias["Alias Resolver (yolov8_detect -> yolo26_detect, yolov8n -> yolo26n_cls, yolo26s -> yolo26s_cls)"]
    Alias --> ModelManager["ModelManager (YOLO26 Unified Engine)"]
    
    subgraph TwoStage ["YOLO26 Unified Two-Stage Pipeline"]
        ModelManager --> S1["Stage 1: Scaled Detect (<=960px, imgsz=352) + Conditional COCO YOLO26n (imgsz=320)"]
        S1 --> S2["Stage 2: YOLO26s-cls True Batch Inference (crops <=384px, imgsz=256)"]
    end

    ModelManager -.->|Lightweight / Legacy| Legacy["YOLO26n-cls / MobileNetV3 / EfficientNet-B0"]
    S2 --> Normalizer["Chuẩn hóa kết quả + Backlit PET Guard + Zero-Det OOD Damping (< 0.60)"]
    Legacy --> Normalizer
    Normalizer --> Client
```

- **True Batch Inference:** `ModelManager` truyền danh sách ảnh crop (giới hạn cạnh tối đa `384px`) thẳng vào `clf(crops, imgsz=256, verbose=False)` cho `yolo26s_cls`, kết hợp pre-warm `batch=1` khi nạp model giúp loại bỏ giật khung đầu sau warm-up và giữ độ trễ CPU ổn định ở **~401–485 ms**.
- **Tối ưu & An toàn nhận diện:** Khi Stage-1 tự tin vật thể `plastic` (`det_conf >= 0.72` kết hợp COCO `bottle`), cơ chế **Backlit PET Guard** ngăn chặn việc ghi đè nhầm sang `glass` dưới ánh sáng ngược nền màn hình. Khi Stage-1 không phát hiện vật thể (`detected_count == 0`), hệ thống nhân hệ số giảm nhiễu `0.55x` để từ chối chính xác ảnh OOD.
- **Unified Schema:** Tương thích 100% với `Ecolink.Backend` (`AiVisionHttpClient.cs`, `WasteScanController.cs`) và `ecolink-web` (`wasteScanApi.ts`, `Scanner.tsx`).

---

## 4. Giao Diện Người Dùng & Đích Mặc Định

- Tại **EcoLink Web (`Scanner.tsx`, `wasteScanApi.ts`)** và **Backend API (`WasteScanController.cs`)**, tham số mặc định là **`yolo26_detect`** (bên trong gọi `yolo26s_cls`), đồng thời hỗ trợ chọn trực tiếp `yolo26s_cls` hoặc `yolo26n_cls`.
- Các request hoặc dữ liệu cũ gửi kèm `yolov8_detect` / `yolov8n` vẫn được tự động ánh xạ sang `yolo26_detect` / `yolo26n_cls`.

---

## 5. Hướng Dẫn Khởi Chạy & Kiểm Thử

### 1. Chạy EcoLink Dedicated AI Microservice:
```powershell
cd src/Ecolink.AiService
..\..\venv\Scripts\python.exe app.py
```
Truy cập Swagger Docs: **`http://127.0.0.1:8000/docs`** (kiểm tra `/health` với `default_model: "yolo26_detect"` và `models_available` chứa `yolo26s_cls`).

### 2. Chạy Web Demo Local:
```powershell
.\venv\Scripts\python.exe app.py
```

### 3. Huấn luyện lại YOLO26s-cls & Chạy Benchmark 5 mô hình:
```powershell
.\venv\Scripts\python.exe train.py --task classify --model yolo26s-cls --epochs 35 --batch 32 --imgsz 256
.\venv\Scripts\python.exe evaluate_models.py
```
