# Tài Liệu Đặc Tả: Luồng Xử Lý AI & Cơ Chế Huấn Luyện Lại (Retrain)

> **Dự án:** EcoLink – Hệ thống hỗ trợ phân loại và kết nối thu mua rác thải tái chế thông minh  
> **Phiên bản:** 1.0  
> **Ngày cập nhật:** 2026-10-20  
> **Mục đích:** Đáp ứng yêu cầu buổi review hội đồng – Trình bày rõ ràng luồng xử lý AI, cơ chế retrain, thông số kỹ thuật model và quy trình vòng đời dữ liệu.

---

## Mục Lục

1. [Tổng Quan Kiến Trúc AI](#1-tổng-quan-kiến-trúc-ai)
2. [Luồng Xử Lý Phân Loại Rác (AI Classification Workflow)](#2-luồng-xử-lý-phân-loại-rác-ai-classification-workflow)
3. [Luồng Xử Lý Ngoại Lệ (Exception Handling)](#3-luồng-xử-lý-ngoại-lệ-exception-handling)
4. [Vòng Đời Dữ Liệu & Data Loop (Data Lifecycle)](#4-vòng-đời-dữ-liệu--data-loop-data-lifecycle)
5. [Cơ Chế Huấn Luyện Lại (Retraining Mechanism)](#5-cơ-chế-huấn-luyện-lại-retraining-mechanism)
6. [Thông Số Kỹ Thuật Mô Hình (Model Technical Specifications)](#6-thông-số-kỹ-thuật-mô-hình-model-technical-specifications)
7. [Thông Số Sau Mỗi Lần Retrain (Post-Retrain Metrics)](#7-thông-số-sau-mỗi-lần-retrain-post-retrain-metrics)
8. [Use Case Liên Quan Đến AI Lifecycle](#8-use-case-liên-quan-đến-ai-lifecycle)
9. [Kịch Bản Dự Phòng (Backup & Fallback Plan)](#9-kịch-bản-dự-phòng-backup--fallback-plan)
10. [Ma Trận Trách Nhiệm (RACI Matrix)](#10-ma-trận-trách-nhiệm-raci-matrix)

---

## 1. Tổng Quan Kiến Trúc AI

### 1.1 Nguyên Tắc Thiết Kế

EcoLink tách biệt rõ ràng hai bài toán:

| Thành phần | Trách nhiệm | Đặc điểm |
|:---|:---|:---|
| **AI Vision Model** | `Image → Waste Category` (nhận diện vật thể) | Thay đổi qua retrain |
| **Rule Engine (Database)** | `Waste Category → Sorting Instruction` (hướng dẫn phân loại) | Quản lý bởi Admin, ổn định |

> **Tại sao không để AI sinh hướng dẫn?**  
> Quy định phân loại rác thay đổi theo địa phương, đơn vị xử lý và chính sách. Rule Engine cho phép cập nhật hướng dẫn mà không cần retrain model.

### 1.2 Kiến Trúc Đa Mô Hình (Multi-Model Architecture)

```
┌─────────────────────────────────────────────────────────┐
│                    API Gateway (.NET 8)                  │
│                                                         │
│  POST /api/classifications                              │
│  ┌──────────────┐    ┌──────────────────────────────┐   │
│  │ Image Upload │───→│ Forward to AI Microservice    │   │
│  │ Validation   │    │ (FastAPI Python)              │   │
│  └──────────────┘    └──────────────────────────────┘   │
└───────────────────────────┬─────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│              AI Microservice (FastAPI)                   │
│                                                         │
│  ┌────────────────────────────────────────────────────┐  │
│  │          ModelManager (Singleton/Registry)          │  │
│  │                                                    │  │
│  │  ┌──────────┐  ┌──────────────┐  ┌──────────────┐ │  │
│  │  │ YOLOv8n  │  │ MobileNetV3  │  │EfficientNet  │ │  │
│  │  │  (Nano)  │  │   (Small)    │  │    (B0)      │ │  │
│  │  └──────────┘  └──────────────┘  └──────────────┘ │  │
│  └────────────────────────────────────────────────────┘  │
│                         │                                │
│                         ▼                                │
│  ┌────────────────────────────────────────────────────┐  │
│  │        Unified Response Schema (JSON)              │  │
│  │  { model_used, top_prediction, confidence,         │  │
│  │    predictions[top-3], inference_time_ms }          │  │
│  └────────────────────────────────────────────────────┘  │
└───────────────────────────┬─────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│            Backend .NET 8 (Rule Engine)                  │
│                                                         │
│  AI Label ──→ waste_categories ──→ sorting_rules        │
│            ──→ sorting_instructions (ordered steps)      │
│            ──→ Response to Frontend                      │
└─────────────────────────────────────────────────────────┘
```

---

## 2. Luồng Xử Lý Phân Loại Rác (AI Classification Workflow)

### 2.1 Sơ Đồ Tổng Quan (End-to-End Flow)

```
┌──────┐    ┌───────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌───────────┐
│ User │───→│  Upload   │───→│  Image   │───→│   AI     │───→│  Rule    │───→│  Display  │
│      │    │  Image    │    │ Validate │    │ Classify │    │ Engine   │    │  Result   │
└──────┘    └───────────┘    └──────────┘    └──────────┘    └──────────┘    └───────────┘
                                  │               │                               │
                                  ▼               ▼                               ▼
                             Reject if       Confidence        Hướng dẫn       User xác nhận
                             invalid          Check           phân loại        hoặc báo sai
```

### 2.2 Chi Tiết Từng Bước

| Bước | Tên bước | Actor | Input | Output | Thời gian ước tính |
|:---:|:---|:---|:---|:---|:---:|
| 1 | Upload/Chụp ảnh | User | Ảnh rác (JPG/PNG, ≤ 5MB) | File ảnh hợp lệ | < 1s |
| 2 | Validation ảnh đầu vào | Hệ thống | File ảnh | Ảnh đã qua kiểm tra định dạng, kích thước | < 100ms |
| 3 | Tiền xử lý ảnh (Preprocessing) | AI Service | Ảnh gốc | Ảnh 224×224px, chuẩn hóa ImageNet | < 50ms |
| 4 | Suy luận mô hình (Inference) | AI Service | Ảnh đã tiền xử lý | Label + Confidence Score + Top-3 | 11–27ms |
| 5 | Kiểm tra ngưỡng tin cậy | Hệ thống | Confidence Score | Phân nhánh: ≥ 60% hoặc < 60% | < 10ms |
| 6 | Ánh xạ Rule Engine | Backend | AI Label | Sorting Instructions (bước xử lý có thứ tự) | < 50ms |
| 7 | Hiển thị kết quả | Frontend | Instructions + Confidence | Giao diện hướng dẫn phân loại | < 100ms |
| 8 | Xác nhận/Phản hồi | User | Kết quả hiển thị | Xác nhận đúng HOẶC Báo cáo sai | — |

**Tổng thời gian xử lý end-to-end:** < 500ms (không tính upload)

### 2.3 Sơ Đồ Quyết Định Theo Confidence

```
                    ┌───────────────┐
                    │ AI Inference  │
                    │ Confidence: X%│
                    └───────┬───────┘
                            │
                   ┌────────┴────────┐
                   │  X ≥ 60% ?      │
                   └────────┬────────┘
                      Yes   │   No
                   ┌────────┴────────┐
                   ▼                 ▼
          ┌────────────────┐  ┌─────────────────────┐
          │ Hiển thị kết   │  │ Gắn cờ "Không chắc  │
          │ quả + Hướng    │  │ chắn" + Yêu cầu     │
          │ dẫn phân loại  │  │ User xác nhận thủ    │
          └────────┬───────┘  │ công hoặc chụp lại   │
                   │          └──────────┬────────────┘
                   ▼                     ▼
          ┌────────────────┐   ┌─────────────────────┐
          │ User xác nhận  │   │ User chọn danh mục  │
          │ hoặc báo sai   │   │ bằng tay từ danh    │
          └────────────────┘   │ sách gợi ý          │
                               └─────────────────────┘
```

---

## 3. Luồng Xử Lý Ngoại Lệ (Exception Handling)

### 3.1 Bảng Kịch Bản Ngoại Lệ

| # | Kịch bản | Nguyên nhân | Xử lý | Phản hồi cho User |
|:---:|:---|:---|:---|:---|
| E1 | Ảnh mờ/tối/góc xấu | Chất lượng ảnh kém | Confidence thấp → Flow thủ công | "Vui lòng chụp lại ảnh rõ hơn hoặc chọn danh mục bên dưới" |
| E2 | Vật thể lạ (không phải rác) | Out-of-Distribution | Confidence thấp → Flow thủ công | "Hệ thống không nhận diện được. Vui lòng chụp lại hoặc chọn thủ công" |
| E3 | Nhiều vật thể trong ảnh | Object Detection limitation | Lấy prediction cao nhất | "Hệ thống phát hiện nhiều vật thể. Kết quả dựa trên vật thể chính" |
| E4 | AI Service không phản hồi | Service down/timeout | Fallback sang model backup | "Đang xử lý, vui lòng thử lại sau ít giây" |
| E5 | User báo kết quả sai | Misclassification | Tạo MisclassificationReport → Queue cho Staff | "Cảm ơn phản hồi! Đội ngũ sẽ xem xét để cải thiện" |

### 3.2 Sơ Đồ Xử Lý Ngoại Lệ

```
┌───────────┐     ┌──────────────┐     ┌──────────────────┐
│ AI Result │────→│ Confidence   │────→│ ≥ 60%: Normal    │
│           │     │ Check        │     │ < 60%: Exception │
└───────────┘     └──────────────┘     └────────┬─────────┘
                                                │
                                    ┌───────────┴───────────┐
                                    ▼                       ▼
                           ┌──────────────┐        ┌──────────────────┐
                           │ Normal Flow  │        │ Exception Flow   │
                           │ Show result  │        │ 1. Suggest retry │
                           │ + Instruction│        │ 2. Manual select │
                           └──────┬───────┘        │ 3. Skip          │
                                  │                └──────────────────┘
                                  ▼
                        ┌──────────────────┐
                        │ User confirms    │
                        │ or reports wrong │
                        └──────────────────┘
```

---

## 4. Vòng Đời Dữ Liệu & Data Loop (Data Lifecycle)

### 4.1 Tổng Quan Data Loop

Đây là luồng nghiệp vụ **khép kín** (Closed-Loop), đầu ra của luồng phân loại trở thành đầu vào của luồng cải thiện model:

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        DATA LOOP (Vòng lặp dữ liệu)                    │
│                                                                          │
│  ┌─────────┐    ┌──────────┐    ┌──────────┐    ┌──────────────────┐    │
│  │  User   │───→│ AI phân  │───→│ User xác │───→│ Báo cáo phân    │    │
│  │ upload  │    │ loại ảnh │    │ nhận/báo │    │ loại sai         │    │
│  │ ảnh rác │    │          │    │ sai      │    │ (MisclassReport) │    │
│  └─────────┘    └──────────┘    └──────────┘    └────────┬─────────┘    │
│                                                          │              │
│                                                          ▼              │
│  ┌──────────────────┐    ┌──────────────┐    ┌───────────────────────┐  │
│  │ Developer/Staff  │◀───│ Staff duyệt  │◀───│ Hàng đợi duyệt      │  │
│  │ retrain model    │    │ + gán nhãn   │    │ (Misclass. Queue)    │  │
│  │ với data mới     │    │ chính xác    │    │                       │  │
│  └────────┬─────────┘    └──────────────┘    └───────────────────────┘  │
│           │                                                              │
│           ▼                                                              │
│  ┌──────────────────┐    ┌──────────────┐                               │
│  │ Deploy model     │───→│ Model mới    │──── Quay lại đầu vòng ──→    │
│  │ mới lên server   │    │ phục vụ User │                               │
│  └──────────────────┘    └──────────────┘                               │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### 4.2 Chi Tiết Các Giai Đoạn

| Giai đoạn | Actor chịu trách nhiệm | Mô tả | Loại thao tác |
|:---|:---|:---|:---|
| **Thu thập dữ liệu** | User (gián tiếp) | User upload ảnh rác và sử dụng hệ thống phân loại hàng ngày | Tự động |
| **Phát hiện lỗi** | User | Báo cáo khi AI phân loại sai qua nút "Report Incorrect" | Thủ công |
| **Xếp hàng đợi duyệt** | Hệ thống | Tạo bản ghi MisclassificationReport, xếp vào hàng đợi Staff | Tự động |
| **Duyệt & Gán nhãn** | Staff | Xem ảnh gốc, so sánh nhãn AI vs nhãn User, gán nhãn chính xác | Thủ công |
| **Phân chia tập dữ liệu** | Hệ thống + Staff | Ảnh đã duyệt được phân vào Train/Validation (80/20), đảm bảo không trùng lặp | Bán tự động |
| **Kích hoạt retrain** | Staff (trigger) + Hệ thống (thực thi) | Staff quyết định thời điểm retrain khi đủ dữ liệu mới | Bán tự động |
| **Huấn luyện lại** | Developer/Hệ thống | Chạy pipeline retrain trên server training | Tự động (script) |
| **Đánh giá model mới** | Developer/Staff | So sánh metrics giữa model cũ và model mới | Thủ công |
| **Triển khai** | Developer | Thay thế checkpoint trên AI Microservice | Thủ công |

---

## 5. Cơ Chế Huấn Luyện Lại (Retraining Mechanism)

### 5.1 Phương Thức: Huấn Luyện Theo Đợt (Batch Retraining) – Bán Tự Động

EcoLink sử dụng cơ chế **Batch Retraining (huấn luyện theo đợt)** thay vì Auto-Retraining vì:

| Tiêu chí | Auto-Retraining | Batch Retraining (EcoLink ✓) |
|:---|:---|:---|
| Kiểm soát chất lượng dữ liệu | Rủi ro nhãn nhiễu | Staff duyệt trước khi train |
| Tài nguyên server | Cần GPU thường trực | Chỉ cần GPU khi retrain |
| Phù hợp quy mô | Hệ thống lớn (>1M ảnh/ngày) | Hệ thống vừa (đồ án, startup) |
| Khả năng kiểm soát | Khó roll-back | Có version control rõ ràng |

### 5.2 Điều Kiện Kích Hoạt Retrain

| Điều kiện | Ngưỡng | Ghi chú |
|:---|:---|:---|
| Số ảnh mới đã duyệt + gán nhãn | ≥ 200 ảnh | Đủ ý nghĩa thống kê cho fine-tuning |
| Tỷ lệ báo cáo sai tích lũy | ≥ 10% tổng lượt phân loại trong 30 ngày | Dấu hiệu model degradation |
| Thêm danh mục rác mới | Khi Admin tạo danh mục mới | Bắt buộc retrain để nhận diện danh mục mới |
| Chu kỳ định kỳ | Tối thiểu 1 lần / quý (3 tháng) | Maintenance retrain |

### 5.3 Quy Trình Retrain Chi Tiết

```
 ┌────────────────────────────────────────────────────────────────────┐
 │              QUY TRÌNH HUẤN LUYỆN LẠI (RETRAIN PIPELINE)         │
 │                                                                    │
 │  Phase 1: CHUẨN BỊ DỮ LIỆU (Data Preparation)                   │
 │  ┌─────────────────────────────────────────────────────────────┐   │
 │  │ 1. Staff xuất danh sách ảnh đã duyệt + nhãn chính xác     │   │
 │  │ 2. Hệ thống merge với dataset gốc (TrashNet baseline)      │   │
 │  │ 3. Phân chia: Train 80% / Validation 20%                   │   │
 │  │ 4. Kiểm tra: Không có ảnh trùng giữa Train & Validation   │   │
 │  │ 5. Áp dụng Data Augmentation (rotate, flip, color jitter)  │   │
 │  └─────────────────────────────────────────────────────────────┘   │
 │                              │                                     │
 │                              ▼                                     │
 │  Phase 2: HUẤN LUYỆN (Training)                                   │
 │  ┌─────────────────────────────────────────────────────────────┐   │
 │  │ 1. Load checkpoint model hiện tại (Transfer Learning)      │   │
 │  │ 2. Phase 1: Freeze backbone, train classifier head (5 ep)  │   │
 │  │ 3. Phase 2: Unfreeze, differential LR, full fine-tune (15) │   │
 │  │ 4. Optimizer: AdamW (weight decay 1e-4)                    │   │
 │  │ 5. Scheduler: CosineAnnealingLR (T_max = 15)              │   │
 │  │ 6. Loss: CrossEntropyLoss (label_smoothing = 0.1)          │   │
 │  │ 7. Early Stopping: patience = 5 epochs                     │   │
 │  └─────────────────────────────────────────────────────────────┘   │
 │                              │                                     │
 │                              ▼                                     │
 │  Phase 3: ĐÁNH GIÁ (Evaluation)                                   │
 │  ┌─────────────────────────────────────────────────────────────┐   │
 │  │ 1. Chạy Benchmark trên Validation Set (508+ ảnh)           │   │
 │  │ 2. So sánh: Accuracy, Precision, Recall, F1-score          │   │
 │  │ 3. Xuất Confusion Matrix để phát hiện nhầm lẫn             │   │
 │  │ 4. So sánh Inference Latency (ms) model cũ vs mới          │   │
 │  │ 5. Tiêu chí PASS: Accuracy mới ≥ Accuracy cũ              │   │
 │  │                  VÀ không có class nào giảm > 3%            │   │
 │  └─────────────────────────────────────────────────────────────┘   │
 │                              │                                     │
 │                              ▼                                     │
 │  Phase 4: TRIỂN KHAI (Deployment)                                  │
 │  ┌─────────────────────────────────────────────────────────────┐   │
 │  │ 1. Lưu checkpoint model mới (versioned: v1.1, v1.2...)     │   │
 │  │ 2. Ghi log retrain vào bảng ai_model_versions              │   │
 │  │ 3. Hot-swap: Upload checkpoint mới lên AI Service          │   │
 │  │ 4. ModelManager reload model (Lazy Loading)                │   │
 │  │ 5. Smoke test: Chạy 10 ảnh mẫu, verify kết quả            │   │
 │  │ 6. Nếu FAIL → rollback về checkpoint cũ                   │   │
 │  └─────────────────────────────────────────────────────────────┘   │
 │                                                                    │
 └────────────────────────────────────────────────────────────────────┘
```

### 5.4 Ước Tính Thời Gian Retrain

| Giai đoạn | Thời gian ước tính | Điều kiện |
|:---|:---|:---|
| Chuẩn bị dữ liệu | 30–60 phút | Staff xuất + script merge |
| Huấn luyện (20 epochs, GPU) | 15–30 phút | NVIDIA T4/RTX 3060, batch_size=32 |
| Huấn luyện (20 epochs, CPU) | 2–4 giờ | Intel i7, 16GB RAM |
| Đánh giá Benchmark | 5–10 phút | 508+ ảnh validation |
| Upload + Hot-swap model | 5–10 phút | Upload checkpoint, restart service |
| **Tổng (GPU)** | **~1–2 giờ** | Bao gồm cả kiểm tra |
| **Tổng (CPU)** | **~3–5 giờ** | Bao gồm cả kiểm tra |

---

## 6. Thông Số Kỹ Thuật Mô Hình (Model Technical Specifications)

### 6.1 Thông Tin Chung

| Thông số | Giá trị |
|:---|:---|
| **Framework** | PyTorch 2.x + Ultralytics (YOLOv8) |
| **Serving** | FastAPI (Python), giao tiếp qua REST API |
| **Số lượng model** | 3 (Multi-Model Architecture) |
| **Số nhãn (classes)** | 6: Cardboard, Glass, Metal, Paper, Plastic, Trash |
| **Input size** | 224 × 224 px (RGB) |
| **Chuẩn hóa** | ImageNet normalization (mean, std) |
| **Dataset gốc** | TrashNet (2,527 ảnh), chia train/val |
| **Validation Set** | 508 ảnh |

### 6.2 Chi Tiết Từng Model

| Đặc tính | YOLOv8n-cls | MobileNetV3-Small | EfficientNet-B0 |
|:---|:---:|:---:|:---:|
| **Kiến trúc** | Ultralytics CNN | Depthwise Separable CNN | Compound Scaling CNN |
| **Số tham số** | **1.44M** | 2.54M | 5.29M |
| **Kích thước checkpoint** | **2.84 MB** | 5.94 MB | 15.60 MB |
| **Suy luận CPU (ms)** | **11.19** | 13.22 | 26.66 |
| **Pretrained weights** | Ultralytics Hub | ImageNet (Torchvision) | ImageNet (Torchvision) |
| **Khuyến nghị sử dụng** | Camera/Webcam realtime | Mobile app | Server audit, kiểm định |

### 6.3 Siêu Tham Số Huấn Luyện (Hyperparameters)

| Tham số | Giá trị | Ghi chú |
|:---|:---|:---|
| **Optimizer** | AdamW | Kết hợp weight decay tốt hơn Adam |
| **Weight Decay** | 1 × 10⁻⁴ | Regularization chống overfitting |
| **Learning Rate (init)** | 1 × 10⁻³ (head), 1 × 10⁻⁴ (backbone) | Differential LR cho fine-tuning |
| **LR Scheduler** | CosineAnnealingLR | T_max = 15 epochs |
| **Loss Function** | CrossEntropyLoss | label_smoothing = 0.1 |
| **Batch Size** | 32 | Cân bằng giữa GPU memory và convergence |
| **Epochs** | 15–20 | Với Early Stopping patience = 5 |
| **Input Resolution** | 224 × 224 | Chuẩn ImageNet |

### 6.4 Data Augmentation

| Kỹ thuật | Tham số | Mục đích |
|:---|:---|:---|
| RandomResizedCrop | 224, scale=(0.8, 1.0) | Mô phỏng khoảng cách chụp khác nhau |
| RandomHorizontalFlip | p=0.5 | Rác không có hướng cố định |
| RandomRotation | degrees=15 | Mô phỏng góc chụp xiên |
| ColorJitter | brightness=0.2, contrast=0.2, saturation=0.2 | Mô phỏng điều kiện ánh sáng khác nhau |

---

## 7. Thông Số Sau Mỗi Lần Retrain (Post-Retrain Metrics)

### 7.1 Baseline Benchmark (Model Hiện Tại – v1.0)

**Đánh giá trên 508 ảnh Validation Set:**

| Chỉ số | YOLOv8n-cls | MobileNetV3-Small | EfficientNet-B0 |
|:---|:---:|:---:|:---:|
| **Top-1 Accuracy** | 91.73% | 91.14% | **92.32%** |
| **Cardboard** | 90.1% | **96.3%** | 91.4% |
| **Glass** | **88.1%** | 82.2% | 85.1% |
| **Metal** | 97.6% | 90.2% | **98.8%** |
| **Paper** | 95.8% | 95.8% | **97.5%** |
| **Plastic** | 88.7% | **94.8%** | 91.8% |
| **Trash** | **85.7%** | 78.6% | 82.1% |
| **Inference (CPU)** | **11.19ms** | 13.22ms | 26.66ms |

### 7.2 Mẫu Báo Cáo Sau Retrain (Template)

Sau mỗi lần retrain, hệ thống phải ghi nhận đầy đủ các thông số sau:

| Mục | Nội dung ghi nhận |
|:---|:---|
| **Version** | v1.x (tăng dần) |
| **Ngày retrain** | YYYY-MM-DD |
| **Số ảnh train mới thêm** | N ảnh |
| **Tổng số ảnh training set** | N ảnh |
| **Tổng số ảnh validation set** | N ảnh |
| **Model được retrain** | YOLOv8n / MobileNetV3 / EfficientNet-B0 |
| **Accuracy trước retrain** | X.XX% |
| **Accuracy sau retrain** | Y.YY% (Δ = +/-Z.ZZ%) |
| **Accuracy từng class** | Bảng chi tiết 6 class |
| **Inference time** | X.XX ms |
| **Confusion Matrix** | Ma trận 6×6 |
| **Có PASS tiêu chí?** | ✅ Có / ❌ Không + lý do |
| **Người thực hiện** | Developer name |
| **Checkpoint path** | models/v1.x/model_name.pth |

### 7.3 Tiêu Chí Chấp Nhận Model Mới (Acceptance Criteria)

| # | Tiêu chí | Ngưỡng | Hành động khi FAIL |
|:---:|:---|:---|:---|
| 1 | Overall Accuracy | ≥ Accuracy model cũ | Rollback, kiểm tra data |
| 2 | Accuracy từng class | Không class nào giảm > 3% so với model cũ | Cân bằng lại dataset, retrain |
| 3 | Inference Latency | ≤ 1.5× latency model cũ | Kiểm tra model size, optimize |
| 4 | Smoke Test | 10/10 ảnh mẫu đúng | Rollback ngay lập tức |

---

## 8. Use Case Liên Quan Đến AI Lifecycle

### UC-CLS-01: Phân Loại Rác Bằng Hình Ảnh

| Mục | Nội dung |
|:---|:---|
| **Actor** | User |
| **Mô tả** | User upload/chụp ảnh rác, hệ thống AI nhận diện và hiển thị kết quả + hướng dẫn phân loại |
| **Tiền điều kiện** | User đã đăng nhập, có ảnh rác |
| **Luồng chính** | 1. User chụp/upload ảnh → 2. Hệ thống validate → 3. AI phân loại → 4. Kiểm tra confidence → 5. Ánh xạ Rule Engine → 6. Hiển thị kết quả + hướng dẫn → 7. User xác nhận |
| **Luồng ngoại lệ** | Confidence < 60% → Yêu cầu chụp lại hoặc chọn thủ công |
| **Hậu điều kiện** | Kết quả phân loại được lưu vào lịch sử |

### UC-CLS-02: Báo Cáo Kết Quả Phân Loại Sai

| Mục | Nội dung |
|:---|:---|
| **Actor** | User |
| **Mô tả** | User báo cáo khi AI phân loại sai, đề xuất danh mục đúng |
| **Tiền điều kiện** | User đã nhận kết quả phân loại |
| **Luồng chính** | 1. User nhấn "Báo cáo sai" → 2. Chọn danh mục đúng từ danh sách → 3. Hệ thống tạo MisclassificationReport liên kết ảnh gốc + nhãn AI + nhãn User → 4. Xếp vào hàng đợi Staff |
| **Hậu điều kiện** | Báo cáo ở trạng thái Pending, chờ Staff xử lý |

### UC-CLS-03: Duyệt Báo Cáo Phân Loại Sai & Gán Nhãn Dữ Liệu

| Mục | Nội dung |
|:---|:---|
| **Actor** | Staff |
| **Mô tả** | Staff xem xét ảnh được báo cáo sai, xác nhận nhãn chính xác, đưa ảnh vào tập dữ liệu huấn luyện |
| **Tiền điều kiện** | Có báo cáo ở trạng thái Pending |
| **Luồng chính** | 1. Staff xem danh sách báo cáo → 2. Mở từng báo cáo: xem ảnh gốc, nhãn AI, nhãn User đề xuất → 3. Staff gán nhãn chính xác (có thể khác cả AI và User) → 4. Approve → 5. Ảnh được đánh dấu "Approved for Training" |
| **Luồng phụ** | Staff có thể Reject (ảnh không đủ chất lượng, spam) |
| **Hậu điều kiện** | Ảnh đã gán nhãn được thêm vào Training Dataset Pool |

### UC-CLS-04: Kích Hoạt & Thực Hiện Retrain Model AI

| Mục | Nội dung |
|:---|:---|
| **Actor** | Staff (trigger), Developer (thực thi) |
| **Mô tả** | Khi đủ điều kiện (≥ 200 ảnh mới hoặc tỷ lệ sai ≥ 10%), Staff kích hoạt quy trình retrain. Developer chạy pipeline huấn luyện, đánh giá và triển khai model mới |
| **Tiền điều kiện** | Đủ ảnh đã duyệt, điều kiện retrain đạt ngưỡng |
| **Luồng chính** | 1. Hệ thống thông báo Staff đã đủ điều kiện retrain → 2. Staff xác nhận kích hoạt → 3. Hệ thống export dataset (Train 80% / Val 20%) → 4. Developer chạy script retrain → 5. So sánh metrics model cũ vs mới → 6. Nếu PASS → deploy model mới → 7. Ghi log retrain |
| **Luồng ngoại lệ** | Model mới FAIL tiêu chí → Rollback, giữ model cũ |
| **Hậu điều kiện** | Model mới phục vụ user, version tăng, log ghi nhận |

### UC-CLS-05: Xem Benchmark & So Sánh Hiệu Năng Model

| Mục | Nội dung |
|:---|:---|
| **Actor** | Staff, Admin |
| **Mô tả** | Xem bảng so sánh hiệu năng giữa các model và giữa các version |
| **Tiền điều kiện** | Có ít nhất 1 model đã benchmark |
| **Luồng chính** | 1. Staff/Admin truy cập trang Model Management → 2. Xem bảng benchmark 3 model → 3. Xem lịch sử retrain + metrics từng version → 4. Xem Confusion Matrix |
| **Hậu điều kiện** | Không thay đổi dữ liệu, chỉ xem |

---

## 9. Kịch Bản Dự Phòng (Backup & Fallback Plan)

### 9.1 Fallback Khi AI Service Gặp Sự Cố

| Tình huống | Giải pháp | Chi tiết |
|:---|:---|:---|
| AI Service timeout (> 5s) | Retry 1 lần, sau đó fallback | User được thông báo và chuyển sang chọn thủ công |
| AI Service down hoàn toàn | Manual Classification Mode | Tắt chức năng AI, User chọn danh mục rác bằng tay từ danh sách |
| Model checkpoint bị lỗi | Auto-rollback | ModelManager load checkpoint version trước đó |
| Demo offline (mất mạng) | Offline Model Bundle | Model ONNX/TF.js nhúng sẵn, chạy trên browser |

### 9.2 Versioning & Rollback

```
models/
├── v1.0/
│   ├── yolov8n_cls_v1.0.pt
│   ├── mobilenetv3_small_v1.0.pth
│   └── efficientnet_b0_v1.0.pth
├── v1.1/
│   ├── yolov8n_cls_v1.1.pt
│   ├── mobilenetv3_small_v1.1.pth
│   └── efficientnet_b0_v1.1.pth
└── current/ → symlink to latest stable version
```

**Quy tắc rollback:** Nếu model mới gặp vấn đề trong 24h đầu (dựa trên tỷ lệ báo cáo sai tăng đột biến), Developer thực hiện rollback bằng cách trỏ `current/` về version trước.

---

## 10. Ma Trận Trách Nhiệm (RACI Matrix)

| Hoạt động | User | Staff | Developer | Admin |
|:---|:---:|:---:|:---:|:---:|
| Upload ảnh phân loại | **R** | – | – | – |
| Báo cáo phân loại sai | **R** | – | – | – |
| Duyệt báo cáo + gán nhãn | – | **R/A** | – | I |
| Quyết định kích hoạt retrain | – | **R** | C | A |
| Chạy pipeline retrain | – | I | **R/A** | I |
| Đánh giá metrics model mới | – | C | **R** | I |
| Triển khai model mới | – | I | **R/A** | I |
| Rollback model | – | I | **R/A** | I |
| Quản lý Rule Engine | – | – | – | **R/A** |
| Quản lý danh mục rác | – | – | – | **R/A** |
| Xem Dashboard AI metrics | I | **R** | **R** | **R** |

> **R** = Responsible (Thực hiện) | **A** = Accountable (Chịu trách nhiệm) | **C** = Consulted (Được tham vấn) | **I** = Informed (Được thông báo)

---

## Phụ Lục: Mapping Yêu Cầu Buổi Review → Tài Liệu

| Yêu cầu từ Hội đồng | Phần đáp ứng trong tài liệu |
|:---|:---|
| Mô hình hóa Pipeline AI rõ ràng hơn | §1, §2 (Architecture + Classification Workflow) |
| Thông số huấn luyện + kết quả testing | §6, §7 (Technical Specs + Benchmark Table) |
| Bổ sung luồng retrain vào sơ đồ nghiệp vụ | §4, §5 (Data Loop + Retraining Mechanism) |
| Actor chịu trách nhiệm retrain theo đợt | §5.1, §10 (Batch Retraining + RACI Matrix) |
| Thiết kế quy trình thay vì liệt kê | Toàn bộ tài liệu dùng sơ đồ + bảng thay vì gạch đầu dòng |
| Use Case / Luồng Retrain AI | §8 (UC-CLS-03, UC-CLS-04) |
| Cơ chế retrain: auto hay batch/manual? | §5.1 (Batch Retraining – Bán tự động, có lý giải) |
| Thông số kỹ thuật cụ thể (train time, deploy time) | §5.4, §6.3 (Ước tính thời gian + Hyperparameters) |
| Kịch bản dự phòng khi ảnh mờ/tối/vật lạ | §3, §9 (Exception Handling + Fallback Plan) |
| Flow Confidence < 60% | §2.3, §3 (Sơ đồ quyết định + Exception Flow) |
