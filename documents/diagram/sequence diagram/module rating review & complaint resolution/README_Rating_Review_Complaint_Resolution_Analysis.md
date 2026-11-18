# PHÂN TÍCH THIẾT KẾ CLASS & SEQUENCE DIAGRAM — MODULE `M-11` (`FE-10`: RATING, REVIEW & COMPLAINT RESOLUTION)

## 1. Phạm vi & Bố cục Thành phần

Thư mục này chứa bản đặc tả kiến trúc UML chuẩn hóa của **Module 12 (`M-11` / `FE-10`)** theo Clean Architecture (.NET 10 + PostgreSQL 16 + Cloudflare R2):

- **Class Diagram:** [`module12_rating_review_complaint_resolution_clean_architecture_class_diagram_ultra_compact.mmd`](../../../class%20diagram/module%20rating%20review%20%26%20complaint%20resolution/module12_rating_review_complaint_resolution_clean_architecture_class_diagram_ultra_compact.mmd)
- **Sequence Diagram 1 (`U-10`):** [`01_u10_rate_recycler_delete_and_abuse_moderation.mmd`](./01_u10_rate_recycler_delete_and_abuse_moderation.mmd) — Chấm `1–5` sao sau khi đơn `Completed` trong `14 ngày`, tính `AverageRating` tức thời, tác giả tự xóa vĩnh viễn và kiểm duyệt báo cáo vi phạm (`BR-RVW-01`..`03`).
- **Sequence Diagram 2 (`U-11`):** [`02_u11_upload_r2_evidence_and_submit_bilateral_complaint.mmd`](./02_u11_upload_r2_evidence_and_submit_bilateral_complaint.mmd) — Tải bằng chứng lên Cloudflare R2, nộp khiếu nại 2 chiều (`Citizen` / `Recycler`), hợp nhất từ `HandoverReceiptService.DisputeAsync` và khóa chống trùng mức PostgreSQL (`BR-DSP-01`, `BR-DSP-02`, `BR-DSP-06`).
- **Sequence Diagram 3 (`S-05`):** [`03_s05_staff_arbitration_dossier_resolution_and_point_clawback.mmd`](./03_s05_staff_arbitration_dossier_resolution_and_point_clawback.mmd) — Tiếp nhận phiếu (`Open -> Investigating`), đọc Hồ sơ Trọng tài 360° (ảnh cân + ảnh R2 + lịch sử chat bất biến), ban hành phán quyết ràng buộc, thu hồi `GreenPoints` an toàn `>= 0` (`BR-GAM-01`/`BR-GAM-03`), cảnh cáo `AccountModerationLog` và lưu vết `AuditLog` (`BR-ADM-05`).

## 2. Điểm nhấn Kỹ thuật Cốt lõi

1. **Bộ nhớ đệm `O(1)` cho Bản đồ PostGIS (`BR-RVW-03`):** Bảng `recycler_profiles` lưu sẵn `average_rating` (`NUMERIC(3,2)`) và `total_reviews` được cập nhật nguyên tử mỗi khi tạo mới, tự xóa hoặc ẩn/khôi phục đánh giá, giúp `GeoMatchingService` sắp xếp `rating_desc` không cần `GROUP BY` toàn bảng `transaction_reviews`.
2. **Filtered Partial Unique Index Chống Race Condition (`BR-DSP-01`):** Chỉ mục `uq_transaction_complaints_active_request` chỉ cho phép duy nhất `1` bản ghi có `status IN ('Open', 'Investigating') AND is_deleted = FALSE` trên mỗi `request_id`, ngăn cả hai bên mở 2 phiếu song song cùng một tích tắc.
3. **Liên thông Trọng tài Không Escrow (`BR-DSP-04`, `BR-DSP-06`, `BR-GAM-03`):** `GetStaffComplaintDossierAsync` đọc toàn vẹn `ChatMessages` bất biến qua `TradeOrderId == RequestId`; khi `ResolveComplaintAsync` đảo ngược giao dịch (`CancelDisputedOrder`), điểm thưởng thu hồi trên `UserEcoProfiles` luôn kẹp đáy `>= 0` và gắn cờ `PointsRevoked = true`, đồng thời giữ lịch sử cảnh cáo gắn `RelatedComplaintId` trong `account_moderation_logs`.
