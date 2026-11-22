# ĐẶC TẢ KIỂM THỬ TÍCH HỢP (INTEGRATION TEST SPECIFICATION)
## PHÂN HỆ M-13 / FE-15: PHẢN HỒI LỖI AI & QUẢN TRỊ BỘ DỮ LIỆU HUẤN LUYỆN (`AI MISCLASSIFICATION FEEDBACK & DATASET CURATION`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Sheet trong Report 5.2:** `M13_AiCuration` (`Module 11`)
* **Phạm vi API End-to-End:** `10` Core REST APIs mới (`STT 130` – `STT 139`: `WasteScanController`, `StaffAiFeedbackController`, `StaffDatasetCurationController`), nâng cấp `STT 021 & 024` và tích hợp `Ecolink.AiService` (`STT 183–184`)
* **Use Cases bao phủ:** `U-04` (Citizen), `S-01` (Staff Review), `S-02` (Ground-Truth & YOLO Labeling), `S-03` (Dataset Versioning & Export)
* **Tệp mã kiểm thử HTTP E2E:** [`AiCurationTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/AiCurationTests.cs) (`[Trait("Category", "AiCuration")]`)
* **Ngày thực thi gần nhất:** `2026-11-17`
* **Kết quả thực thi thực tế qua Kestrel + PostgreSQL 16 + FastAPI AI Service:** **`16 / 16 Passed` (`100%` Pass Rate)** *(cùng `25 / 25` bài kiểm thử `WasteScanTests` giữ vững `100%`)*

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ TÍCH HỢP (MODULE SUMMARY TABLE)

| Feature | `AI Misclassification Feedback & Dataset Curation (M13_AiCuration)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp End-to-End vòng lặp Human-in-the-Loop Active Learning: lưu vết ảnh quét `SHA-256` & tự thu thập OOD `< 60%` (`BR-CLS-03`), chặn danh mục `Inactive`/không tồn tại (`BR-CLS-02`) & trùng `ScanId` (`BR-CLS-05`), phân quyền hạt mịn `REVIEW_AI` vs `CURATE_DATASET` (`BR-ADM-02`, `MSG-ADM-04`), thẩm định 1 chạm / bác bỏ (`BR-CLS-06`), gán nhãn YOLO & hàng loạt nguyên tử, thuật toán **Sticky Stratified Split `70/20/10`** chống rò rỉ (`BR-CLS-07`), xuất `.zip` chuẩn `YOLO` & đồng bộ sang FastAPI (`STT 183–184`). | | | |
| **Number of TCs** | **16** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 16 | 0 | 0 | 0 |
| *Round 2* | 0 | 0 | 16 | 0 |
| *Round 3* | 0 | 0 | 16 | 0 |

---

## 2. CHI TIẾT 16 INTEGRATION TEST CASES (`ITC-CLS-001` – `ITC-CLS-016`)

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 11.1: Scan History Persistence & Misclassification Feedback (`U-04` — `STT 021, 024, 130`)** | | | | | | | | | |
| **ITC-CLS-001** | Quét ảnh tạo `AiScanHistory` (`scanId`, `imageUrl`) và gửi báo cáo sai vào PostgreSQL (`MSG-CLS-06`). | 1. Đăng ký `Citizen`, gửi ảnh PNG tới `POST /api/v1/waste-scan/classify`.<br>2. Lấy `scanId` gửi `POST /api/v1/waste-scan/feedback` (`userReportedLabel="plastic"`). | `classify` trả `scanId` & `imageUrl`; `feedback` trả `200 OK` (`MSG-CLS-06`), `status="Pending"`, `normalizedLabel="plastic"`. | WebApi, Postgres & FastAPI đang chạy. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS01` |
| **ITC-CLS-002** | Thu thập tự động mẫu OOD / tin cậy thấp (`< 60%`) vào hàng đợi `LowConfidenceAuto` (`Unlabeled`). | 1. Quét ảnh PNG đơn sắc `32x32`.<br>2. Gọi `GET /api/v1/staff/ai-labeling/queue?sourceType=LowConfidenceAuto`. | Khi `isOutOfDistribution=true`, tìm thấy bản ghi có `sourceScanId == scanId` & `status="Unlabeled"`. | Có quyền `Admin` đọc queue. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS02` |
| **ITC-CLS-003** | Chặn gửi báo cáo sai với nhãn danh mục không tồn tại (`BR-CLS-02`). | 1. Gửi `POST /api/v1/waste-scan/feedback` với `userReportedLabel="nuclear_waste"`. | HTTP `400 Bad Request`, `success=false`. | Đã đăng nhập `Citizen`. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS03` |
| **ITC-CLS-004** | Chặn gửi báo cáo sai trùng lặp trên cùng một `scanId` (`BR-CLS-05`). | 1. Quét lấy `scanId`.<br>2. Gửi `POST /feedback` cùng `scanId` 2 lần liên tiếp. | Lần 1 `200 OK`; lần 2 trả `409 Conflict`. | Có `scanId` hợp lệ. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS04` |
| **Function 11.2: Fine-Grained RBAC & Misclassification Review (`S-01` — `STT 131–133`)** | | | | | | | | | |
| **ITC-CLS-005** | Kiểm soát phân quyền `401` & `[HasPermission]` hạt mịn giữa `REVIEW_AI` và `CURATE_DATASET` (`MSG-ADM-04`). | 1. Gọi `feedback/me`, `staff/ai-feedback`, `staff/ai-datasets` không token (`401`) & token `Citizen` (`403`).<br>2. Tạo 3 Staff có quyền `MANAGE_BLOG`, `REVIEW_AI`, `CURATE_DATASET` và thử gọi chéo. | `REVIEW_AI` chỉ vào được `ai-feedback`; `CURATE_DATASET` chỉ vào được `ai-datasets`; thiếu quyền trả `403 MSG-ADM-04`. | Tài khoản `Admin` cấp quyền RBAC. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS05` |
| **ITC-CLS-006** | Thẩm định 1 chạm `ApproveAndLabel` thăng cấp `VerifiedLabeled` kèm hộp bao YOLO & ghi `AuditLog`. | 1. Citizen gửi báo cáo cho `paper`.<br>2. Staff `[REVIEW_AI]` gọi `PATCH /staff/ai-feedback/{id}/review` (`ApproveAndLabel`, `verifiedLabel="paper"`, 1 box).<br>3. Kiểm tra `/admin/audit-logs?action=REVIEW_AI_FEEDBACK`. | HTTP `200 MSG-CLS-07`, `status="Reviewed"`, `datasetItemStatus="VerifiedLabeled"`, `AuditLog` có bản ghi khớp `reportId`. | Staff có quyền `REVIEW_AI`. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS06` |
| **ITC-CLS-007** | Bác bỏ (`Reject`) ảnh nhiễu bắt buộc lý do `>= 5` ký tự và không tạo mẫu trong kho Dataset (`BR-CLS-06`). | 1. Thử `Reject` với `reviewNote="Mờ"` (`400`).<br>2. Gửi `Reject` với lý do hợp lệ `>= 5` ký tự. | Bước 1: `400 Bad Request`. Bước 2: `200 OK`, `status="Rejected"`, `datasetItemId=null`. | Có báo cáo `Pending`. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS07` |
| **ITC-CLS-008** | Cô lập danh sách `/waste-scan/feedback/me` (`BR-CLS-08`) và chặn duyệt lại báo cáo đã xử lý (`409`). | 1. `Citizen A` gửi báo lỗi; `Citizen B` gọi `GET /feedback/me` kiểm tra.<br>2. Admin duyệt lần 1 (`ApproveToQueue`) rồi thử duyệt lần 2. | `Citizen B` không thấy báo cáo của A; duyệt lần 2 trả `409 Conflict`. | Có 2 tài khoản `Citizen A`, `B`. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS08` |
| **Function 11.3: Ground-Truth Annotation & Atomic Batch Assign (`S-02` — `STT 134–136`)** | | | | | | | | | |
| **ITC-CLS-009** | Gán nhãn Ground-Truth & hộp bao YOLO `[0, 1]` qua `PUT /api/v1/staff/ai-labeling/{itemId}`. | 1. Đưa mẫu vào kho `Unlabeled`.<br>2. Gửi `PUT` với `groundTruthLabel="metal"` & 1 box `[0.5, 0.5, 0.3, 0.4]`. | HTTP `200 OK`, `status="VerifiedLabeled"`, `yoloAnnotations.Length=1`. | Có mẫu `Unlabeled`. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS09` |
| **ITC-CLS-010** | Chặn tọa độ YOLO ngoài biên `(0, 1]` khi gán nhãn. | 1. Gửi `PUT /ai-labeling/{itemId}` với `xCenter=1.25, width=0.0`. | HTTP `400 Bad Request`. | Có mẫu `DatasetItem`. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS10` |
| **ITC-CLS-011** | Gán nhãn hàng loạt nguyên tử (`POST /batch-assign`): thành công lô 3 ảnh & rollback khi có ID lỗi. | 1. Chuẩn bị 3 mẫu `Unlabeled`.<br>2. Gọi `/batch-assign` kèm 1 `Guid.NewGuid()` (`404`).<br>3. Kiểm tra 3 mẫu còn `Unlabeled`, sau đó gọi hợp lệ 3 ID. | Lần 1 `404 Not Found` & rollback cả 3; lần 2 `200 OK`, `updatedCount=3`. | Có 3 mẫu `Unlabeled`. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS11` |
| **Function 11.4: Sticky Stratified Split `70/20/10`, ZIP Export & FastAPI Sync (`S-03` — `STT 137–139`, `183–184`)** | | | | | | | | | |
| **ITC-CLS-012** | Đóng gói phiên bản Dataset phân tầng `70/20/10` & kiểm định tổng tỷ lệ bắt buộc `100%`. | 1. Chuẩn bị mẫu `VerifiedLabeled` đa lớp (`plastic`, `paper`, `metal`).<br>2. Thử tỷ lệ `70/20/5` (`400`) rồi tạo `70/20/10` (`201`). | Sai tổng trả `400`; hợp lệ trả `201 Created` (`MSG-CLS-08`), `trainCount > valCount >= testCount >= 1`. | Staff `[CURATE_DATASET]`. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS12` |
| **ITC-CLS-013** | Kiểm định **Sticky Split Zero Data Leakage** qua nhiều phiên bản liên tiếp (`v1` $\rightarrow$ `v2`). | 1. Tạo `v1`, xuất `manifest.json` lưu map `itemId -> split`.<br>2. Thêm 5 mẫu mới và tạo `v2`, xuất `manifest.json` đối chiếu. | `100%` mẫu có trong `v1` giữ nguyên chính xác phân tập `split` trong `v2`. | Có mẫu `VerifiedLabeled`. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS13` |
| **ITC-CLS-014** | Thống kê kho dữ liệu sạch `GET /staff/ai-datasets` & tính chỉ số cân bằng lớp (`ImbalanceRatio > 2.5x`). | 1. Bổ sung mẫu `plastic` vượt `> 2.5x` lớp `glass`.<br>2. Gọi `GET /api/v1/staff/ai-datasets`. | Trả đúng `summary.imbalanceRatio`, gắn `isBalanced=false` khi vượt ngưỡng `2.50x`. | Có ít nhất 2 lớp có mẫu. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS14` |
| **ITC-CLS-015** | Xuất gói `.zip` chuẩn `YOLO` kèm Header `X-EcoLink-Message-Code: MSG-ADM-02`. | 1. Gọi `GET /staff/ai-datasets/versions/{id}/export?format=yolo`. | `200 OK` (`application/zip`), header chứa `MSG-ADM-02`, `.zip` chứa `data.yaml` & `manifest.json`. | Có phiên bản Dataset. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS15` |
| **ITC-CLS-016** | Đồng bộ phiên bản Dataset sang FastAPI (`POST /api/v1/datasets/curate`) chuyển `SyncedToAi`. | 1. Gửi `POST /versions` với `syncToAiService=true` khi AI Service (`:8000`) đang chạy. | `201 Created`, `status="SyncedToAi"` và `syncedToAiAt != null`. | AI Service đang hoạt động. | Passed | Pending | Pending | N/A | `AiCurationTests.CLS16` |
