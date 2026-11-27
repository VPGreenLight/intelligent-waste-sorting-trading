# REPORT 5.3 – SYSTEM TEST: MODULE 14: BLOG AUTHORING, EDITORIAL APPROVAL WORKFLOW & PUBLIC PORTAL (G-01, S-08, A-09)

## 1. Workflow Summary Header (Theo mẫu Sheet `M14_BlogContent` – `SEP490_G141`)

| Workflow | `Module 14: Blog Authoring, Editorial Approval Workflow & Public Portal (G-01, S-08, A-09)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử End-to-End phân hệ Truyền thông Blog trên /blog, /blog/:slug, /staff/blogs và /admin/blog: Staff (MANAGE_BLOG) tải ảnh bìa R2, tạo bài nháp sinh slug tiếng Việt chống trùng, gửi duyệt & thu hồi bài chờ duyệt, Admin duyệt xuất bản (Approved) hoặc từ chối kèm góp ý bắt buộc + AuditLog, và người dân/khách vãng lai đọc bài công khai & lọc đúng nguyên thẻ. | | | |
| **Number of TCs** | **10** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 9 | 1 | 0 | 0 |
| *Round 2* | 10 | 0 | 0 | 0 |
| *Round 3* | 0 | 0 | 10 | 0 |

---

## 2. System Test Case Specification (`STC-BLG-001` $\rightarrow$ `STC-BLG-010`)

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Scenario 14.1: Staff Soạn thảo, Tải Ảnh Bìa R2, Gửi Duyệt & Thu hồi (/staff/blogs - S-08)** | | | | | | | | | |
| **STC-BLG-001** | Staff tải ảnh bìa R2, soạn bài nháp tiếng Việt và tự động sinh slug chuẩn (chống trùng hậu tố -2) | 1. Đăng nhập staff -> mở /staff/blogs.<br>2. Bấm 'Tạo bài viết', tải ảnh bìa .jpg (<= 5MB), nhập tiêu đề tiếng Việt 'Phân loại nhựa tại nguồn Hòa Lạc', thêm thẻ tag và bấm 'Lưu nháp'. | - Ảnh bìa tải lên R2 thành công.<br>- Tạo bài trạng thái Draft với slug không dấu 'phan-loai-nhua-tai-nguon-hoa-lac'. Nếu tạo bài thứ 2 trùng tên, tự động nối hậu tố '-2'. | Staff có quyền MANAGE_BLOG. | Passed | Passed | Pending | N/A | S-08 Draft & Slugify |
| **STC-BLG-002** | Chặn Staff nộp duyệt bài viết khi thiếu ảnh bìa hoặc nội dung trống (BR-ADM-08) | 1. Tạo bài viết bỏ trống ảnh bìa hoặc nội dung.<br>2. Bấm 'Gửi duyệt ngay'. | - Hệ thống chặn nộp duyệt với lỗi MSG-AUTH-02: bắt buộc đủ tiêu đề >= 10 ký tự, ảnh bìa và nội dung. | Bài viết chưa đủ điều kiện xuất bản. | Passed | Passed | Pending | N/A | BR-ADM-08 Validation |
| **STC-BLG-003** | Staff gửi duyệt bài nháp -> Trạng thái Pending_Approval & thông báo tới Admin | 1. Mở bài nháp hợp lệ ở STC-BLG-001.<br>2. Bấm 'Gửi duyệt (Submit)'. | - Bài chuyển sang Pending_Approval, hiển thị trong hàng đợi Admin (/admin/blog) và gửi thông báo cho Admin. | Bài nháp đầy đủ ảnh bìa & nội dung. | Failed | Passed | Pending | **INC-2026-GAM-07** | Submit for Approval \| Round 1 Failed (INC-2026-GAM-07): xung đột migration blog_posts & quyền MANAGE_BLOG -> đã xử lý Round 2 Passed |
| **STC-BLG-004** | Staff thu hồi bài đang chờ duyệt (Pending_Approval -> Draft) và chặn sửa/xóa bài đã Approved | 1. Bấm 'Thu hồi về nháp (Recall)' với bài đang Pending_Approval.<br>2. Mở bài viết đã xuất bản (Approved) và kiểm tra nút Sửa/Thu hồi/Xóa. | - Thu hồi bài Pending thành công về Draft.<br>- Bài đã Approved khóa hoàn toàn thao tác Sửa/Thu hồi/Xóa (BR-ADM-08). | Có 1 bài Pending_Approval và 1 bài Approved. | Passed | Passed | Pending | N/A | Recall & Approved Lock |
| **Scenario 14.2: Admin Hàng đợi Kiểm duyệt Xuất bản / Từ chối (/admin/blog - A-09)** | | | | | | | | | |
| **STC-BLG-005** | Admin xem hàng đợi bài chờ duyệt sắp xếp FIFO/mới nhất (/admin/blog) | 1. Đăng nhập admin -> mở /admin/blog.<br>2. Thử sắp xếp theo 'Cũ nhất' (oldest) / 'Mới nhất' và tìm theo từ khóa. | - Chỉ liệt kê các bài đang Pending_Approval (không lẫn bài nháp của Staff), sắp xếp chuẩn theo thời gian nộp. | Có bài Pending_Approval trong DB. | Passed | Passed | Pending | N/A | A-09 Moderation Queue |
| **STC-BLG-006** | Admin bấm Từ chối kèm góp ý bắt buộc -> Bài quay về Draft của tác giả kèm lý do chỉnh sửa | 1. Bấm 'Từ chối' một bài chờ duyệt, thử bỏ trống ô góp ý.<br>2. Nhập góp ý 'Cần bổ sung số liệu trích dẫn Nghị định mới' và xác nhận. | - Bỏ trống góp ý bị chặn (MSG-AUTH-02).<br>- Gửi kèm góp ý chuyển bài về Draft với RejectionReason hiển thị rõ ở trang /staff/blogs của tác giả, gửi thông báo & ghi AuditLog BLOG_REJECT. | Có bài Pending_Approval. | Passed | Passed | Pending | N/A | A-09 Reject with Feedback |
| **STC-BLG-007** | Cô lập bài nháp giữa các Staff tác giả khác nhau (Chống IDOR) và chặn Citizen vào /staff/blogs, /admin/blog | 1. Thử truy cập ID bài nháp của Staff A bằng phiên Staff B.<br>2. Dùng Citizen vào /staff/blogs hoặc /admin/blog. | - Trả về 404 NotFound (chống IDOR giữa Staff) và 403 Forbidden đối với Citizen. | Có Staff khác hoặc Citizen. | Passed | Passed | Pending | N/A | Blog RBAC & IDOR |
| **Scenario 14.3: Cổng Đọc Blog Công khai (/blog, /blog/:slug - G-01)** | | | | | | | | | |
| **STC-BLG-008** | Khách vãng lai xem danh sách bài viết đã xuất bản trên /blog (ẩn hoàn toàn bài nháp và chờ duyệt) | 1. Mở trang /blog ở trạng thái chưa đăng nhập.<br>2. Kiểm tra số lượng bài, ảnh minh họa (/assets/insights/*) và tên tác giả. | - Hiển thị chính xác 4 bài đã Approved từ DemoDataSeeder với ảnh bìa đầy đủ; bài Draft và Pending_Approval không lộ ra cổng công khai. | Đã seed 6 bài viết mặc định. | Passed | Passed | Pending | N/A | G-01 Public Listing |
| **STC-BLG-009** | Lọc bài viết theo đúng nguyên thẻ (exact whole-tag match) và tìm theo từ khóa | 1. Nhấn chọn một thẻ chủ đề (VD: 'nhua') hoặc gõ từ khóa vào thanh tìm kiếm trên /blog. | - Chỉ trả về bài mang đúng thẻ 'nhua', không khớp nhầm thẻ con như 'nhua-pet'. | Có bài gắn thẻ trên /blog. | Passed | Passed | Pending | N/A | Exact Tag Filtering |
| **STC-BLG-010** | Mở xem chi tiết bài viết qua URL /blog/:slug và chặn truy cập trực tiếp slug của bài nháp | 1. Nhấn vào bài viết nổi bật trên /blog để mở /blog/:slug.<br>2. Thử gõ thẳng trên thanh địa chỉ slug của một bài viết đang Draft. | - Bài Approved hiển thị đầy đủ nội dung chuẩn Markdown/HTML và metadata tác giả.<br>- Slug bài Draft trả về trang 404 Not Found. | Có bài Approved và bài Draft. | Passed | Passed | Pending | N/A | Slug Detail & Draft Protection |
