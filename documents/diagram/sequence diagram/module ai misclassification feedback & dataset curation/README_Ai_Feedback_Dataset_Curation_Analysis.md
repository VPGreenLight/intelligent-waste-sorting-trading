# PHÂN TÍCH THIẾT KẾ CLASS & SEQUENCE DIAGRAM — MODULE `M-13` (`FE-15`: AI MISCLASSIFICATION FEEDBACK & DATASET CURATION)

## 1. Phạm vi & Bố cục Thành phần

Thư mục này chứa bản đặc tả kiến trúc UML chuẩn hóa của **Module 11 (`M-13` / `FE-15`)** theo Clean Architecture (.NET 10) kết hợp Python 3.11 FastAPI AI Service (`AIS`):

- **Class Diagram:** [`module11_ai_feedback_dataset_curation_clean_architecture_class_diagram_ultra_compact.mmd`](../../../class%20diagram/module%20ai%20misclassification%20feedback%20%26%20dataset%20curation/module11_ai_feedback_dataset_curation_clean_architecture_class_diagram_ultra_compact.mmd)
- **Sequence Diagram 1 (`U-04`):** [`01_u04_scan_sha256_ood_capture_and_misclassification_feedback.mmd`](./01_u04_scan_sha256_ood_capture_and_misclassification_feedback.mmd) — Lưu vết ảnh gốc `SHA-256`, tự động thu thập ca OOD `< 60%` và gửi báo cáo sai có kiểm soát (`BR-CLS-02`, `BR-CLS-03`, `BR-CLS-05`, `BR-CLS-08`).
- **Sequence Diagram 2 (`S-01`, `S-02`):** [`02_s01_s02_staff_review_yolo_labeling_and_atomic_batch_assign.mmd`](./02_s01_s02_staff_review_yolo_labeling_and_atomic_batch_assign.mmd) — Kiểm duyệt báo sai (`ApproveAndLabel` / `ApproveToQueue` / `Reject`), gán nhãn hộp bao YOLO `[0, 1]` và gán hàng loạt nguyên tử (`BR-CLS-06`, `BR-ADM-02`, `BR-ADM-05`).
- **Sequence Diagram 3 (`S-03`):** [`03_s03_sticky_stratified_split_zip_export_and_fastapi_hot_reload.mmd`](./03_s03_sticky_stratified_split_zip_export_and_fastapi_hot_reload.mmd) — Thuật toán **Sticky Stratified Split 70/20/10** chống rò rỉ dữ liệu theo `ImageSha256`, giám sát lệch lớp `2.5x`, xuất gói `.zip` (`YOLO` / `ImageFolder`) và đồng bộ FastAPI (`BR-CLS-07`).

## 2. Điểm nhấn Kỹ thuật Cốt lõi

1. **Chống Rò rỉ Dữ liệu Tuyệt đối (`BR-CLS-07`):** Mọi bản ghi `AiDatasetItem` khi được phân vào `Validation` hoặc `Test` (hay `Train`) lần đầu sẽ khóa cứng trường `StickySplit` trên CSDL, bảo đảm mọi phiên bản Dataset sau (`v1.1.0`, `v1.2.0`...) không bao giờ đẩy ảnh kiểm thử vào lại tập huấn luyện.
2. **Bảo vệ Toàn vẹn Ngược cho Phân hệ `M-03`:** `WasteScanController` giữ nguyên tính khả dụng cho người dùng vãng lai (`[AllowAnonymous]` trên `/classify` và `/feedback`), tự động chuẩn hóa các nhãn biến thể (`plastic_bottle`, `glass_bottle`...) qua `AiCategoryNormalizer` trước khi kiểm định danh mục `Active`, và vẫn đảm bảo luồng `classify` thành công ngay cả khi dịch vụ R2 hoặc AI Sync tạm gián đoạn.
3. **Tách bạch RBAC & Dấu vết Bất biến (`BR-ADM-02`, `BR-ADM-05`):** Staff chỉ có quyền `REVIEW_AI` truy cập hàng đợi báo lỗi và gán nhãn (`STT 131–136`), còn Staff có quyền `CURATE_DATASET` thực thi đóng gói phiên bản & xuất tập huấn luyện (`STT 137–139`). Mọi thao tác đều ghi vào bảng bất biến `audit_logs`.
