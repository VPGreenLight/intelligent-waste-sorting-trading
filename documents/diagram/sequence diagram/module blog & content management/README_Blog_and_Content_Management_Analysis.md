# PHÂN TÍCH THIẾT KẾ CLASS & SEQUENCE DIAGRAM — MODULE `M-14` (`FE-16`: BLOG & CONTENT MANAGEMENT)

## 1. Phạm vi & Bố cục Thành phần

Thư mục này đặc tả kiến trúc UML của **Module 14 (`M-14` / `FE-16`: Blog & Content Management)** trên `.NET 10 + PostgreSQL 16 + Cloudflare R2` (`10` REST Endpoints: `STT 140` – `STT 149`, bao phủ `3` Use Cases `G-01`, `S-08`, `A-09`):

* **Class Diagram:** [`module14_blog_and_content_management_clean_architecture_class_diagram_ultra_compact.mmd`](../../../class%20diagram/module%20blog%20%26%20content%20management/module14_blog_and_content_management_clean_architecture_class_diagram_ultra_compact.mmd)
* **Sequence Diagram 1 (`S-08`):** [`01_s08_staff_upload_r2_media_create_draft_slugify_submit_and_recall.mmd`](./01_s08_staff_upload_r2_media_create_draft_slugify_submit_and_recall.mmd) — Staff `[MANAGE_BLOG]` tải ảnh lên R2 (`/api/v1/staff/blogs/upload-media`), soạn nháp, sinh slug tiếng Việt chống trùng (`ux_blog_posts_slug`), nộp duyệt (`Draft -> Pending_Approval`, `MSG-ADM-06`) hoặc thu hồi về nháp.
* **Sequence Diagram 2 (`A-09`):** [`02_a09_admin_pending_queue_approve_publish_or_reject_with_mandatory_feedback.mmd`](./02_a09_admin_pending_queue_approve_publish_or_reject_with_mandatory_feedback.mmd) — Admin xem hàng đợi chờ duyệt, xuất bản (`Approved`, `MSG-ADM-05`) hoặc trả về bản nháp (`Draft`) kèm `RejectionReason` bắt buộc, thông báo cho tác giả và ghi `AuditLog` (`BLOG_APPROVE`, `BLOG_REJECT`).
* **Sequence Diagram 3 (`G-01`):** [`03_g01_guest_browse_published_blogs_filter_by_tag_and_read_by_slug.mmd`](./03_g01_guest_browse_published_blogs_filter_by_tag_and_read_by_slug.mmd) — Khách vãng lai và Người dân duyệt bài đã xuất bản (`AllowAnonymous`), lọc nguyên thẻ JSON `"tag"` và xem chi tiết theo slug không rò rỉ thông tin kiểm duyệt nội bộ.

## 2. Điểm nhấn Kỹ thuật Cốt lõi

1. **Máy trạng thái Hai bước Tách quyền Soạn thảo & Xuất bản (`BR-ADM-08`):** `Staff` chỉ có quyền đưa bài giữa `Draft` $\leftrightarrow$ `Pending_Approval` và chỉ sửa/xóa được bài `Draft` của chính mình; mọi chuyển trạng thái sang `Approved` đều khóa cứng cho `Admin` (`[Authorize(Roles = "Admin")]`), còn khi từ chối bài được đưa về `Draft` kèm `RejectionReason` để `Staff` có thể chỉnh sửa và nộp lại ngay (`CanEdit = true`).
2. **Bảo vệ Race Condition Khi Duyệt & Thu hồi:** Các thao tác `Submit`, `Recall`, `Approve`, `Reject` trên Relational DB đều thực thi bằng `ExecuteUpdateAsync` có điều kiện trạng thái hiện tại (`WHERE status = ...`), đồng thời có nhánh `!_db.Database.IsRelational()` giúp toàn bộ 20 Unit Tests chạy thông suốt trên EF Core InMemory.
3. **Upload Ảnh Cloudflare R2 & Chuẩn hóa Route (`M-14` + Alias):** Toàn bộ Controller hỗ trợ cả tuyến chuẩn `STT 140–149` (`/api/v1/blogs`, `/api/v1/staff/blogs/*`, `/api/v1/admin/blogs/pending`, `/api/v1/admin/blogs/{id}/review`) lẫn alias `/api/v1/*blog-posts*` để bảo đảm tương thích 100% giữa Frontend mới và bộ kiểm thử tích hợp hiện có.
