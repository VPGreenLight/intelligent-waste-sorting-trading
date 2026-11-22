# ĐẶC TẢ KIỂM THỬ TÍCH HỢP (INTEGRATION TEST SPECIFICATION)
## PHÂN HỆ M-14 / FE-16: QUẢN TRỊ BÀI VIẾT BLOG & NỘI DUNG TRUYỀN THÔNG (`BLOG & CONTENT MANAGEMENT`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Sheet trong Report 5.2:** `M14_BlogContent` (`Module 14`)
* **Phạm vi API End-to-End:** `10` REST APIs (`STT 140` – `STT 149` + Canonical `/blogs*` & Alias `/blog-posts*`: `PublicBlogController`, `StaffBlogController`, `AdminBlogController`) + Tải ảnh Cloudflare R2 (`upload-media`) + Phân quyền động `MANAGE_BLOG` + Nhật ký kiểm toán `AuditLog` (`M-15`)
* **Use Cases bao phủ:** `G-01` (Browse Public Eco Blogs), `S-08` (Create Blog Post), `A-09` (Approve / Reject Blog Post)
* **Tệp mã kiểm thử HTTP E2E:** [`BlogTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/BlogTests.cs)
* **Ngày thực thi gần nhất:** `2026-11-19`
* **Kết quả thực thi:** **`16 / 16 Passed` (`100%` Pass Rate)**

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ TÍCH HỢP (MODULE SUMMARY TABLE)

| Feature | `Blog & Content Management (M14_BlogContent)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp End-to-End 3 Use Cases `G-01`, `S-08`, `A-09`: Phân quyền động `[HasPermission("MANAGE_BLOG")]` & quyền `Admin` (`BR-ADM-02`); Upload ảnh Cloudflare R2 `blog-media` (`<= 5MB`, `.jpg/.png/.webp`); Soạn nháp, sinh Slug tiếng Việt chống trùng (`ux_blog_posts_slug`), sửa & xóa nháp; Nộp duyệt (`Draft -> Pending_Approval`) & Thu hồi (`Pending_Approval -> Draft`); Admin hàng đợi (`oldest`/`newest`), duyệt xuất bản (`Approved`, `MSG-ADM-05`) & từ chối kèm góp ý bắt buộc (`Draft`, `RejectionReason`) + ghi `AuditLog` (`BR-ADM-05`); Cổng đọc công khai ẩn bài chưa duyệt, lọc nguyên thẻ tag JSON và bảo mật không rò rỉ thông tin nội bộ (`BR-ADM-08`). | | | |
| **Number of TCs** | **16** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 16 | 0 | 0 | 0 |
| *Round 2* | 0 | 0 | 16 | 0 |
| *Round 3* | 0 | 0 | 16 | 0 |

---

## 2. CHI TIẾT 16 INTEGRATION TEST CASES (`ITC-BLG-001` – `ITC-BLG-016`)

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 14.1: RBAC Guard & Cloudflare R2 Blog Media Upload (`S-08` — `STT 142`)** | | | | | | | | | |
| **ITC-BLG-001** | Kiểm soát `[HasPermission("MANAGE_BLOG")]` cho Staff/Admin và chặn Citizen/Anonymous trên toàn bộ endpoint quản trị Blog. | 1. Guest không token (`401`) và Citizen (`403`) gọi `/api/v1/staff/blogs` & `/api/v1/admin/blogs/pending`.<br>2. Staff `[MANAGE_BLOG]` (`200` trên `/staff/blogs`, `403` trên `/admin/blogs/*`); Admin (`200` cả 2). | Chặt chẽ `401/403` đúng phân quyền; Staff không tự duyệt được bài của mình (`BR-ADM-08`). | Tài khoản Citizen, Staff `MANAGE_BLOG`, Admin. | Passed | Pending | Pending | N/A | `BlogAccessTests` |
| **ITC-BLG-002** | Upload ảnh bìa lên Cloudflare R2 (`POST /api/v1/staff/blogs/upload-media`): chặn file rỗng/đuôi `.exe` và chấp nhận ảnh PNG hợp lệ. | 1. Gửi không file (`400`), gửi `malware.exe` (`400`).<br>2. Gửi ảnh PNG `32x32` hợp lệ bằng `MultipartFormDataContent`. | Tệp lỗi trả `400`; ảnh PNG trả `200 MSG-GEN-01` kèm `fileUrl` hợp lệ. | Đã đăng nhập Staff `[MANAGE_BLOG]`. | Passed | Pending | Pending | N/A | `BlogCanonicalTests` |
| **Function 14.2: Staff Authoring, Slugify, Submit, Recall & Ownership (`S-08` — `STT 143–147`)** | | | | | | | | | |
| **ITC-BLG-003** | Tạo bản nháp mới với tiêu đề tiếng Việt, kiểm định tự động sinh Slug ASCII & khử trùng lặp hậu tố `-2`. | 1. Staff gửi 2 request `POST /api/v1/staff/blogs` cùng tiêu đề tiếng Việt có dấu.<br>2. Tạo bản nháp thiếu ảnh bìa (`submitForApproval=false`). | Sinh slug không dấu, bài 2 có hậu tố `-2`; nháp lưu thành công không bắt buộc ảnh bìa. | Đã đăng nhập Staff. | Passed | Pending | Pending | N/A | `BlogStaffTests` |
| **ITC-BLG-004** | Kiểm định `FluentValidation` cho độ dài tiêu đề (`10..150`), số thẻ tag tối đa `5`, URL ảnh & điều kiện nộp duyệt. | 1. Gửi tiêu đề `9` ký tự (`400`), `6` tags (`400`), ảnh `.exe` (`400`).<br>2. Gửi `submitForApproval=true` thiếu `coverImageUrl` (`400`). | Toàn bộ trường hợp sai bị chặn `400` với thông báo chi tiết. | Đã đăng nhập Staff. | Passed | Pending | Pending | N/A | `BlogStaffTests` |
| **ITC-BLG-005** | Chỉnh sửa bản nháp (`PUT`), nộp duyệt (`POST /submit`) & chuyển cờ năng lực `CanEdit/CanSubmit/CanRecall`. | 1. Tạo nháp -> `PUT /api/v1/staff/blogs/{id}` cập nhật nội dung.<br>2. Gọi `POST /api/v1/staff/blogs/{id}/submit`. | Lưu `200`, nộp duyệt thành công trả `MSG-ADM-06`, `status="Pending_Approval"`, `canRecall=true, canEdit=false`. | Có bài `Draft`. | Passed | Pending | Pending | N/A | `BlogStaffTests` |
| **ITC-BLG-006** | Khóa sửa/xóa khi bài đang `Pending_Approval` và cho phép Thu hồi (`POST /recall`) về nháp để sửa lại. | 1. Thử `PUT` & `DELETE` lên bài `Pending_Approval` (`400`).<br>2. Gọi `POST /recall` -> `PUT` sửa lại -> `DELETE`. | `Pending_Approval` chặn sửa/xóa; sau `recall` về `Draft` sửa & xóa được bình thường. | Có bài `Pending_Approval`. | Passed | Pending | Pending | N/A | `BlogStaffTests` |
| **ITC-BLG-007** | Cô lập quyền tác giả: Staff thứ 2 (`Staff B`) không thể xem/sửa/nộp/xóa bài viết của `Staff A`. | 1. Staff A tạo bài nháp.<br>2. Admin cấp mới `Staff B` quyền `MANAGE_BLOG` và dùng token B truy cập bài của A. | Trả `404 Not Found` bảo vệ quyền riêng tư bản nháp giữa các nhân viên. | Admin có quyền tạo/phân quyền Staff. | Passed | Pending | Pending | N/A | `BlogStaffTests` |
| **ITC-BLG-008** | Lọc danh sách bài của tôi (`GET /api/v1/staff/blogs`) theo trạng thái `Draft/Pending_Approval/Published` & tìm kiếm từ khóa. | 1. Staff tạo 3 bài ở 3 trạng thái khác nhau.<br>2. Gọi `GET /staff/blogs?status=...&search=...`. | Trả phân trang chính xác bài thuộc quyền Staff đang đăng nhập. | Staff có bài ở nhiều trạng thái. | Passed | Pending | Pending | N/A | `BlogStaffTests` |
| **Function 14.3: Admin Moderation Queue, Review & Public Reading (`A-09`, `G-01` — `STT 140–141, 148–149`)** | | | | | | | | | |
| **ITC-BLG-009** | Hàng đợi duyệt của Admin (`GET /api/v1/admin/blogs/pending`) chỉ chứa bài `Pending_Approval` và sắp xếp `oldest` / `newest`. | 1. Tạo `1` bài nháp & nộp `2` bài nối tiếp.<br>2. Admin gọi `/admin/blogs/pending?sort=oldest` và `newest`. | Bài nháp không lọt vào hàng đợi; `oldest` xếp bài nộp trước lên đầu, `newest` ngược lại. | Có bài ở `Pending_Approval`. | Passed | Pending | Pending | N/A | `BlogAdminTests` |
| **ITC-BLG-010** | Admin phê duyệt xuất bản (`PATCH /api/v1/admin/blogs/{id}/review` `{ action: "Approve" }`), xuất hiện công khai & ghi `AuditLog`. | 1. Admin duyệt bài đang chờ qua endpoint thống nhất `/review`.<br>2. Kiểm tra `/blogs/{slug}` và `/admin/audit-logs`. | Trả `200 MSG-ADM-05`, `status="Approved"`, `publishedAt != null`, có log `BLOG_APPROVE`. | Có bài `Pending_Approval`. | Passed | Pending | Pending | N/A | `BlogCanonicalTests` |
| **ITC-BLG-011** | Admin từ chối bài (`action: "Reject"`) bắt buộc nhập góp ý chỉnh sửa; trả bài về `Draft` cho Staff sửa & nộp lại. | 1. Thử từ chối với `feedback` rỗng (`400`) hoặc > 2000 ký tự (`400`).<br>2. Gửi `feedback` hợp lệ -> Staff sửa `PUT` & `POST /submit`. | Góp ý rỗng bị chặn `400`; hợp lệ trả bài về `Draft` kèm `rejectionReason`, Staff sửa & nộp lại được. | Có bài `Pending_Approval`. | Passed | Pending | Pending | N/A | `BlogAdminTests` |
| **ITC-BLG-012** | Chống thao tác tranh chấp khi Admin duyệt bài đã bị Staff thu hồi hoặc đã được Admin khác duyệt trước. | 1. Staff thu hồi bài `Pending_Approval` về `Draft` -> Admin bấm duyệt (`400`).<br>2. Bài đã `Approved` -> Admin bấm từ chối (`400`). | Cả 2 trường hợp bị chặn `400 MSG-GEN-01` giữ nguyên tính nhất quán trạng thái. | Có bài ở trạng thái `Draft` / `Approved`. | Passed | Pending | Pending | N/A | `BlogAdminTests` |
| **ITC-BLG-013** | Cổng công khai (`GET /api/v1/blogs`, `/api/v1/blogs/{slug}`) ẩn bài `Draft`, `Pending_Approval` & bài bị từ chối. | 1. Tạo bài nháp, bài chờ và bài bị reject.<br>2. Guest gọi `/blogs?search=...` và `/blogs/{slug}`. | Danh sách công khai chỉ hiển thị bài `Approved`; truy cập slug bài nháp/chờ/từ chối trả `404`. | Không cần token (`AllowAnonymous`). | Passed | Pending | Pending | N/A | `BlogPublicTests` |
| **ITC-BLG-014** | Lọc danh sách công khai theo nguyên thẻ `tag` (không khớp nhầm tiền tố) & sắp xếp mới xuất bản lên đầu. | 1. Xuất bản 2 bài thẻ `tgA` và `tgAxx`.<br>2. Gọi `GET /api/v1/blogs?tag=tgA`. | Chỉ trả bài có chính xác thẻ `tgA`, loại bài `tgAxx`; bài xuất bản sau đứng trước. | Đã xuất bản bài mẫu. | Passed | Pending | Pending | N/A | `BlogPublicTests` |
| **ITC-BLG-015** | Bảo mật phản hồi công khai (`PublicBlogPostDto`): không rò rỉ `rejectionReason`, trạng thái nội bộ & kẹp `pageSize <= 50`. | 1. Gọi `GET /blogs/{slug}` bài đã xuất bản.<br>2. Gọi `GET /blogs?pageSize=9999`. | Detail không có field `rejectionReason`/`status`; list kẹp `pageSize <= 50`. | Có bài đã xuất bản. | Passed | Pending | Pending | N/A | `BlogPublicTests` |
| **ITC-BLG-016** | Xác minh toàn vẹn Alias Routes `/api/v1/blog-posts*` tương thích ngược đồng thời với `/api/v1/blogs*`. | 1. Chạy luồng Tạo -> Sửa -> Nộp -> Queue -> Approve -> Public Get qua cả `/blogs*` và `/blog-posts*`. | Cả 2 bộ đường dẫn đều định tuyến đúng Controller & trả cùng cấu trúc `ApiResponse`. | Backend đang chạy. | Passed | Pending | Pending | N/A | `BlogCanonicalTests` |
