# ĐẶC TẢ HỢP ĐỒNG API CHI TIẾT (API CONTRACT SPECIFICATION)
## PHÂN HỆ M-13 / FE-15: PHẢN HỒI LỖI AI & QUẢN TRỊ BỘ DỮ LIỆU HUẤN LUYỆN (`AI MISCLASSIFICATION FEEDBACK & DATASET CURATION`)

* **Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform (`SEP490_G141`)
* **Phiên bản tài liệu:** `v2.7.0` (Cập nhật đồng bộ với mã nguồn `Ecolink.WebApi` / `.NET 10` & `Ecolink.AiService` / `FastAPI`)
* **Use Cases bao phủ:**
  * `U-04`: Report Incorrect AI Classification (*Mở rộng `/classify`, `/feedback` + bổ sung `/feedback/me`*)
  * `S-01`: Review Misclassified Images (*`ApproveAndLabel`, `ApproveToQueue`, `Reject`*)
  * `S-02`: Label Image Data (*Kho chờ OOD < 60% & gán nhãn chuẩn + hộp bao YOLO*)
  * `S-03`: Update Verified Dataset (*Sticky Stratified Split `70/20/10`, Export `.zip`, FastAPI Sync*)
* **Controllers liên quan:** `WasteScanController` (`STT 021, 024, 148`), `StaffAiFeedbackController` (`STT 149–154`), `StaffDatasetCurationController` (`STT 155–157`)
* **Phạm vi Endpoint:** `10` Core REST APIs (`STT 148` – `STT 157`), nâng cấp `2` API nền (`STT 021`, `STT 024`) và `2` AI FastAPI APIs (`STT 216` – `STT 217`) trong [00_api_inventory_and_endpoint_statistics.md](./00_api_inventory_and_endpoint_statistics.md)
* **Tiêu chuẩn phản hồi:** Chuẩn hóa theo `ApiResponse<T>` trong [00_api_overview_and_response_contract.md](./00_api_overview_and_response_contract.md) (`camelCase`, trạng thái chuỗi).

---

## 1. TỔNG QUAN KIẾN TRÚC & MA TRẬN API PHÂN HỆ `M-13`

### 1.1. Kiến trúc Khép kín Human-in-the-Loop Active Learning

1. **Lưu vết Ảnh Gốc `SHA-256` & Thu thập OOD Tự động (`STT 021`, `BR-CLS-03`):**
   * `POST /api/v1/waste-scan/classify` tính mã băm `SHA-256` (64 hex chars), tải ảnh lên `Cloudflare R2` (`ai-scans/yyyy/MM/{sha256[..16]}.{ext}`, fallback `local://` an toàn), lưu bản ghi `ai_scan_histories`, và trả `scanId`, `imageUrl` về cho UI.
   * Nếu `isOutOfDistribution == true` hoặc `confidence < confidenceThreshold` (`< 0.60`), hệ thống tự động nạp ứng viên `LowConfidenceAuto` trạng thái `Unlabeled` vào `ai_dataset_items` (khử trùng theo `uq_ai_dataset_items_sha256`).
2. **Báo cáo Phân loại Sai & Chuẩn hóa 10 Nhãn YOLO (`STT 024`, `STT 148`, `BR-CLS-02`, `BR-CLS-05`, `BR-CLS-08`):**
   * `POST /api/v1/waste-scan/feedback` chuẩn hóa alias vật liệu qua `AiCategoryNormalizer` (`AiCategoryNormalizer.YoloClassOrder` gồm đúng **10 lớp theo chỉ mục YOLO `0..9`**: `0=battery`, `1=biological`, `2=cardboard`, `3=clothes`, `4=glass`, `5=metal`, `6=paper`, `7=plastic`, `8=shoes`, `9=trash`; tự ánh xạ các alias như `pin`/`ewaste_disposal` $\rightarrow$ `battery`, `organic`/`huu_co` $\rightarrow$ `biological`, `textile`/`quan_ao` $\rightarrow$ `clothes`, `giay_dep` $\rightarrow$ `shoes`, `brown-glass`/`green-glass`/`white-glass` $\rightarrow$ `glass`, `plastic_bottle`/`pet`/`hdpe` $\rightarrow$ `plastic`...) rồi kiểm tra danh mục tồn tại và `Status == "Active"` trong `waste_categories` (`BR-CLS-02`).
   * Nếu truyền `scanId`, chặn báo trùng lặp bằng `409 Conflict` (`BR-CLS-05`). Vẫn giữ tương thích ngược khi bỏ trống `scanId`/`imageUrl`.
   * `GET /api/v1/waste-scan/feedback/me` cô lập dữ liệu cứng theo `UserId` (`BR-CLS-08`) để công dân theo dõi kết quả duyệt (`Pending`, `Reviewed`, `Rejected`).
3. **Thẩm định Kép 2 Chế độ (`STT 149–154`, `[HasPermission("REVIEW_AI")]`):**
   * `PATCH /api/v1/staff/ai-feedback/{reportId}/review` hỗ trợ 3 quyết định (`ApproveAndLabel`, `ApproveToQueue`, `Reject` kèm `reviewNote >= 5` ký tự). Bản ghi bị `Reject` bị khóa khỏi mọi tập Dataset (`BR-CLS-06`).
   * `PUT /api/v1/staff/ai-labeling/{itemId}` và `POST /api/v1/staff/ai-labeling/batch-assign` gán nhãn chuẩn + hộp bao YOLO `[class_idx 0..9, xCenter, yCenter, width, height]` trong `[0, 1]` và giao dịch nguyên tử.
4. **Sticky Stratified Split `70/20/10` Chống Rò rỉ Dữ liệu (`STT 155–157`, `216–217`, `[HasPermission("CURATE_DATASET")]`, `BR-CLS-07`):**
   * `POST /api/v1/staff/ai-datasets/versions` chỉ lấy các mẫu `VerifiedLabeled`, gom theo `image_sha256`, giữ nguyên 100% phân tập cũ (`sticky_split`) cho mẫu lịch sử, chia phân tầng phần bù `70% Train / 20% Val / 10% Test` cho mẫu mới theo từng danh mục trong 10 lớp YOLO, tính `imbalanceRatio = maxClass / minClass` (`isBalanced = ratio <= 2.50`), và đồng bộ sang `POST /api/v1/datasets/curate` (kèm fallback `Published` + `aiSyncWarning` nếu AI Service tắt).
   * `GET /api/v1/staff/ai-datasets/versions/{id}/export?format=yolo|imagefolder` xuất trực tiếp tệp `.zip` (với `data.yaml` 10 lớp `YoloClassOrder`) kèm Header `X-EcoLink-Message-Code: MSG-ADM-02` và `X-EcoLink-Sample-Count`.

### 1.2. Bảng Quy chiếu Nhanh Endpoints

| STT | Phương thức | Tuyến đường Endpoint | Controller / Action | Phân quyền (`RBAC`) |
|:---:|:---:|:---|:---|:---|
| **021** | `POST` | `/api/v1/waste-scan/classify` *(Nâng cấp)* | `WasteScanController.ClassifyImage` | `Authorize` |
| **024** | `POST` | `/api/v1/waste-scan/feedback` *(Nâng cấp)* | `WasteScanController.SubmitFeedback` | `Authorize` |
| **148** | `GET` | `/api/v1/waste-scan/feedback/me` | `WasteScanController.GetMyFeedbackReports` | `Authorize` (Chính chủ) |
| **149** | `GET` | `/api/v1/staff/ai-feedback` | `StaffAiFeedbackController.GetFeedbackQueue` | `Staff [REVIEW_AI]`, `Admin` |
| **150** | `GET` | `/api/v1/staff/ai-feedback/{reportId:guid}` | `StaffAiFeedbackController.GetFeedbackDetail` | `Staff [REVIEW_AI]`, `Admin` |
| **151** | `PATCH` | `/api/v1/staff/ai-feedback/{reportId:guid}/review` | `StaffAiFeedbackController.ReviewFeedback` | `Staff [REVIEW_AI]`, `Admin` |
| **152** | `GET` | `/api/v1/staff/ai-labeling/queue` | `StaffAiFeedbackController.GetLabelingQueue` | `Staff [REVIEW_AI]`, `Admin` |
| **153** | `PUT` | `/api/v1/staff/ai-labeling/{itemId:guid}` | `StaffAiFeedbackController.AssignGroundTruthLabel` | `Staff [REVIEW_AI]`, `Admin` |
| **154** | `POST` | `/api/v1/staff/ai-labeling/batch-assign` | `StaffAiFeedbackController.BatchAssignLabels` | `Staff [REVIEW_AI]`, `Admin` |
| **155** | `GET` | `/api/v1/staff/ai-datasets` | `StaffDatasetCurationController.GetDatasetDashboard` | `Staff [CURATE_DATASET]`, `Admin` |
| **156** | `POST` | `/api/v1/staff/ai-datasets/versions` | `StaffDatasetCurationController.CreateDatasetVersion` | `Staff [CURATE_DATASET]`, `Admin` |
| **157** | `GET` | `/api/v1/staff/ai-datasets/versions/{versionId:guid}/export` | `StaffDatasetCurationController.ExportDatasetVersion` | `Staff [CURATE_DATASET]`, `Admin` |
| **216** | `POST` | `/api/v1/datasets/curate` *(AI Service)* | `app.curate_dataset_version` | Internal Service |
| **217** | `POST` | `/api/v1/models/reload` *(AI Service)* | `app.reload_ai_models` | Internal Service |

---

## 2. ĐẶC TẢ CHI TIẾT CÁC ENDPOINTS (`STT 148` – `STT 157`, `021`, `024`, `216`, `217`)

### 2.1. Nhóm Citizen Quét & Báo cáo AI Sai (`WasteScanController`)

#### [STT 021 Nâng cấp] Quét phân loại rác & lưu vết phiên quét `SHA-256`
* **Endpoint:** `POST /api/v1/waste-scan/classify` (`multipart/form-data`: `file`, `model`)
* **Response `200 OK` (`MSG-CLS-01` hoặc `MSG-CLS-02` nếu OOD `< 60%`):** Bổ sung `scanId` (`uuid`) và `imageUrl` (`string`) bên cạnh `aiResult`, `sortingGuide`, `message`.

#### [STT 024 Nâng cấp] Gửi báo cáo AI nhận diện sai (`U-04`)
* **Endpoint:** `POST /api/v1/waste-scan/feedback`
* **Request Body (`SubmitFeedbackDto`):**
  ```json
  {
    "scanId": "a7b12c34-1111-4222-9333-445566778899",
    "imageUrl": "https://cdn.ecolink.test/ai-scans/2026/11/a1b2c3.jpg",
    "predictedLabel": "glass",
    "confidenceScore": 0.54,
    "userReportedLabel": "plastic",
    "note": "Chai nhựa PET trong bị chói sáng."
  }
  ```
* **Response `200 OK` (`MSG-CLS-06`):**
  ```json
  {
    "success": true,
    "messageCode": "MSG-CLS-06",
    "message": "Cảm ơn bạn đã phản hồi! Dữ liệu đã được chuyển tới Nhân viên vận hành để kiểm chứng và cải thiện mô hình AI.",
    "data": {
      "reportId": "d1000001-0000-0000-0000-000000000001",
      "scanId": "a7b12c34-1111-4222-9333-445566778899",
      "status": "Pending",
      "normalizedLabel": "plastic",
      "syncedToAi": true
    }
  }
  ```
* **Lỗi nghiệp vụ:** `400 Bad Request` (`BR-CLS-02` nhãn không hợp lệ/ngừng hoạt động), `404 Not Found` (`scanId` không tồn tại), `409 Conflict` (`BR-CLS-05` trùng `scanId`).

#### [STT 148] Theo dõi lịch sử báo cáo AI sai của bản thân (`U-04`)
* **Endpoint:** `GET /api/v1/waste-scan/feedback/me?status=Reviewed&pageNumber=1&pageSize=20`
* **RBAC:** `[Authorize]`
* **Response `200 OK` (`MSG-GEN-01`):** Trả về `PagedResult<MyAiFeedbackReportDto>` gồm `id`, `scanId`, `imageUrl`, `predictedLabel`, `userReportedLabel`, `adminVerifiedLabel`, `status` (`Pending|Reviewed|Rejected`), `reviewNote`, `createdAt`, `reviewedAt`.

---

### 2.2. Nhóm Staff Thẩm định & Gán nhãn Dữ liệu (`StaffAiFeedbackController` — `[HasPermission(REVIEW_AI)]`)

#### [STT 149] Hàng đợi báo cáo AI sai kèm bộ đếm tồn đọng (`S-01`)
* **Endpoint:** `GET /api/v1/staff/ai-feedback?status=Pending&predictedLabel=&userReportedLabel=&maxConfidence=&search=&pageNumber=1&pageSize=20`
* **Response `200 OK` (`MSG-GEN-01`):** Trả `StaffAiFeedbackQueueDto` chứa `items`, `totalRecords`, `pendingCount`, `reviewedCount`, `rejectedCount`.

#### [STT 150] Chi tiết ảnh gốc & hộp bao lúc quét (`S-01`)
* **Endpoint:** `GET /api/v1/staff/ai-feedback/{reportId:guid}`
* **Response `200 OK` (`MSG-GEN-01`):** Trả `StaffAiFeedbackDetailDto` (`imageUrl`, `imageSha256`, `imageWidth`, `imageHeight`, `detectedObjectsSnapshot`, `existingYoloAnnotations`, `reporterName`, `reporterRole`, `datasetItemId`, `datasetItemStatus`).

#### [STT 151] Thẩm định báo cáo 1 chạm (`ApproveAndLabel`, `ApproveToQueue`, `Reject`)
* **Endpoint:** `PATCH /api/v1/staff/ai-feedback/{reportId:guid}/review`
* **Request Body (`ReviewAiFeedbackRequest`):**
  ```json
  {
    "decision": "ApproveAndLabel",
    "verifiedLabel": "plastic",
    "reviewNote": "Xác nhận chai nhựa PET có nhãn phản quang.",
    "yoloAnnotations": [
      {
        "categoryCode": "plastic",
        "xCenter": 0.51,
        "yCenter": 0.49,
        "width": 0.34,
        "height": 0.62
      }
    ]
  }
  ```
* **Response `200 OK` (`MSG-CLS-07`):** Trả `StaffAiFeedbackDetailDto` đã cập nhật. Trả `409 Conflict` nếu báo cáo không còn ở `Pending`; trả `400 Bad Request` nếu `Reject` có `reviewNote < 5` ký tự (`BR-CLS-06`).

#### [STT 152] Hàng đợi gán nhãn Ground-Truth & mẫu OOD `< 60%` (`S-02`)
* **Endpoint:** `GET /api/v1/staff/ai-labeling/queue?status=Unlabeled&sourceType=&groundTruthLabel=&predictedLabel=&unlabeledOnly=&pageNumber=1&pageSize=20`
* **Response `200 OK` (`MSG-GEN-01`):** Trả `PagedResult<AiDatasetItemDto>`.

#### [STT 153] Gán nhãn chuẩn đơn lẻ & hộp bao YOLO `[0, 1]` (`S-02`)
* **Endpoint:** `PUT /api/v1/staff/ai-labeling/{itemId:guid}`
* **Request Body (`AssignGroundTruthLabelRequest`):** `{ groundTruthLabel, excludeFromDataset, qualityNote, yoloAnnotations }`
* **Response `200 OK` (`MSG-CLS-07`):** Trả `AiDatasetItemDto` (`status = "VerifiedLabeled"` hoặc `"Excluded"`). Trả `400` nếu hộp bao ngoài `(0, 1]`.

#### [STT 154] Gán nhãn hàng loạt nguyên tử (`S-02`)
* **Endpoint:** `POST /api/v1/staff/ai-labeling/batch-assign`
* **Request Body (`BatchAssignLabelsRequest`):** `{ "itemIds": ["..."], "groundTruthLabel": "cardboard", "qualityNote": "..." }` (`1..200` phần tử).
* **Response `200 OK` (`MSG-CLS-07`):** Trả `{ updatedCount, groundTruthLabel, updatedItemIds }`. Trả `404 Not Found` và rollback toàn bộ giao dịch nếu có bất kỳ ID không tồn tại.

---

### 2.3. Nhóm Staff Đóng gói Phiên bản Dataset & FastAPI Sync (`StaffDatasetCurationController` — `[HasPermission(CURATE_DATASET)]`)

#### [STT 155] Tổng quan kho dữ liệu sạch, tỷ lệ lệch lớp & lịch sử phiên bản (`S-03`)
* **Endpoint:** `GET /api/v1/staff/ai-datasets`
* **Response `200 OK` (`MSG-GEN-01`):** Trả `AiDatasetDashboardDto` gồm `summary` (`totalPoolItems`, `verifiedLabeledCount`, `unlabeledCount`, `excludedCount`, `imbalanceRatio`, `isBalanced`, `weakestCategory`, `empiricalAccuracyPercent`), `byCategory[]`, `versions[]`.

#### [STT 156] Đóng gói phiên bản Dataset Sticky Stratified Split `70/20/10` (`S-03`)
* **Endpoint:** `POST /api/v1/staff/ai-datasets/versions`
* **Request Body (`CreateDatasetVersionRequest`):**
  ```json
  {
    "versionCode": "v1.1.0",
    "versionName": "Dataset T11/2026 chuẩn hóa",
    "description": "Tổng hợp mẫu OOD & UserFeedback đã kiểm định",
    "trainRatio": 70,
    "valRatio": 20,
    "testRatio": 10,
    "syncToAiService": true
  }
  ```
* **Response `201 Created` (`MSG-CLS-08`):** Trả `AiDatasetVersionDto`. Trả `409 Conflict` nếu trùng `versionCode`; `400` nếu thiếu `VerifiedLabeled` hoặc tổng tỷ lệ `!= 100`. Nếu AI Service ngoại tuyến, vẫn lưu `Published` kèm `aiSyncWarning`.

#### [STT 157] Xuất gói `.zip` huấn luyện (`yolo` hoặc `imagefolder`) (`S-03`)
* **Endpoint:** `GET /api/v1/staff/ai-datasets/versions/{versionId:guid}/export?format=yolo`
* **Response `200 OK` (`application/zip`):** Trả file `ecolink-dataset-{versionCode}-{format}.zip` kèm headers `X-EcoLink-Message-Code: MSG-ADM-02` và `X-EcoLink-Sample-Count`.

#### [STT 216 & 217] Nội bộ Python FastAPI (`src/Ecolink.AiService/app.py`)
* `POST /api/v1/datasets/curate`: Kiểm định không giao tập `train ∩ test` hay `train ∩ val` theo `image_sha256` (trả `400` nếu rò rỉ), lưu `data/curated_datasets/{version_code}/manifest.json`.
* `POST /api/v1/models/reload`: Xóa cache RAM `ModelManager._loaded_models` và nạp lại danh sách mô hình mới nhất từ `models/`.
