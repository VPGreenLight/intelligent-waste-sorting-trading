# ĐẶC TẢ KIỂM THỬ ĐƠN VỊ (UNIT TEST SPECIFICATION)
## PHÂN HỆ M-14 / FE-16: QUẢN TRỊ BÀI VIẾT BLOG & NỘI DUNG TRUYỀN THÔNG (`BLOG & CONTENT MANAGEMENT`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Module trong Report 5.1:** `Module 14` (`M-14` / `FE-16`)
* **Dịch vụ & Validator kiểm thử:** `BlogPostService`, `SaveBlogPostRequestValidator`, `RejectBlogPostRequestValidator`, `ReviewBlogPostRequestValidator`
* **Use Cases bao phủ:** `G-01` (Browse Public Eco Blogs), `S-08` (Create Blog Post), `A-09` (Approve / Reject Blog Post)
* **Tệp mã kiểm thử tự động:** [`BlogContentUnitTests.cs`](../../../src/Ecolink.Backend/Ecolink.UnitTests/Module14_BlogContent/BlogContentUnitTests.cs)
* **Ngày thực thi gần nhất:** `2026-11-27`
* **Kết quả thực thi:** **`21 / 21 Passed` (`100%` Pass Rate — `10 Normal`, `7 Abnormal`, `4 Boundary`)**

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ ĐƠN VỊ (SUMMARY TABLE)

| Code Module | `BlogPostService`, `SaveBlogPostRequestValidator`, `RejectBlogPostRequestValidator`, `ReviewBlogPostRequestValidator` (`M-14`) | | |
| :--- | :---: | :---: | :---: |
| **Number of TCs** | **21** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 21 | 0 | 0 |
| *Round 2* | 0 | 0 | 21 |
| *Round 3* | 0 | 0 | 21 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count** | **10** | **7** | **4** |

---

## 2. CHI TIẾT 21 UNIT TEST CASES (`UTC001` – `UTC021`)

| Test Case ID | Tên Method Kiểm thử (`BlogContentUnitTests`) | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| **UTC001** | `UTC001_CreateAsync_DraftWithVietnameseTitle_ShouldSlugifyAndNormalizeUniqueTags` | Staff tồn tại | `CreateAsync(Title="  Đổi rác nhựa nhận quà xanh tại Hà Nội!  ", Tags=["Nhựa PET","nhựa pet","Kinh tế tuần hoàn"], Submit=false)` | `Status="Draft"`, `Slug="doi-rac-nhua-nhan-qua-xanh-tai-ha-noi"`, `Tags` còn `2` thẻ duy nhất, `CanEdit/Submit/Delete=true` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | `S-08` Slugify tiếng Việt |
| **UTC002** | `UTC002_CreateAsync_DuplicateTitle_ShouldAppendNumericSuffixAndClampLongSlug` | Đã có 1 bài trùng tiêu đề trong DB | Tạo bài thứ 2 cùng tiêu đề + bài tiêu đề dài `150` ký tự | Bài 2 sinh `Slug="...-2"`; bài tiêu đề `150` ký tự tạo `Slug.Length <= 200` | Boundary | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Chống trùng Slug & biên độ dài |
| **UTC003** | `UTC003_CreateAsync_WithSubmitForApprovalTrue_ShouldTransitionToPendingAndNotifyAdmins` | Staff & Admin Active tồn tại | `CreateAsync(ValidRequest(submit: true))` | `Status="Pending_Approval"`, `SubmittedAt != null`, `CanRecall=true`, tạo `Notification` cho Admin | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Nộp duyệt ngay lúc tạo |
| **UTC004** | `UTC004_CreateAsync_SubmitWithoutCoverOrContent_ShouldThrowMSGAuth02` | Staff tồn tại | `CreateAsync(submit: true, cover: null)` & `(content: "   ")` | Đều ném `BusinessRuleException ("BR-ADM-08", "MSG-AUTH-02")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Chặn nộp thiếu bìa/nội dung |
| **UTC005** | `UTC005_UpdateAsync_OnDraft_ShouldUpdateFieldsAndRegenerateSlug` | Có bài `Draft` của Staff | `UpdateAsync` đổi tiêu đề, nội dung & thẻ mới | Cập nhật dữ liệu và tái sinh `Slug` theo tiêu đề mới | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Cho phép đổi slug khi còn Draft |
| **UTC006** | `UTC006_UpdateAsync_OnPendingOrApprovedPost_ShouldThrowMSGGen01` | Có bài `PendingApproval` và sau đó `Approved` | `UpdateAsync` trên từng trạng thái | Đều ném `BusinessRuleException ("MSG-GEN-01")` bắt buộc thu hồi/cấm sửa | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | `BR-ADM-08` khóa sửa |
| **UTC007** | `UTC007_GetMineAndUpdate_ByDifferentStaffAuthor_ShouldThrowNotFoundException` | `AuthorA` đã tạo bài `Draft`; tồn tại `AuthorB` | `AuthorB` gọi `GetMineAsync` & `UpdateAsync` lên bài của `AuthorA` | Ném `NotFoundException` (cô lập tác giả) | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Chống IDOR giữa Staff |
| **UTC008** | `UTC008_SubmitAsync_ValidDraft_ShouldTransitionToPendingAndNotifyAdmins` | Có bài `Draft` đầy đủ bìa + nội dung; có Admin | `SubmitAsync(staff.Id, draft.Id)` | Chuyển `Pending_Approval`, gửi `Notification` tới Admin | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | `S-08` nộp bản nháp |
| **UTC009** | `UTC009_SubmitAsync_OnAlreadyApprovedPost_ShouldThrowMSGGen01` | Bài đã được Admin duyệt (`Approved`) | Staff gọi `SubmitAsync` lặp lại | Ném `BusinessRuleException ("MSG-GEN-01")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Chặn nộp bài đã duyệt |
| **UTC010** | `UTC010_RecallAsync_OnPendingPost_ShouldRevertToDraftAndNotifyAdmins` | Bài đang `Pending_Approval` | `RecallAsync(staff.Id, pending.Id)` | Quay về `Status="Draft"`, `SubmittedAt=null`, `CanEdit=true`, thông báo Admin | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | `S-08` thu hồi chỉnh sửa |
| **UTC011** | `UTC011_RecallAsync_OnApprovedPost_ShouldThrowMSGGen01` | Bài đã `Approved` | Staff gọi `RecallAsync` | Ném `BusinessRuleException ("MSG-GEN-01")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Không thể thu hồi bài đã đăng |
| **UTC012** | `UTC012_DeleteAsync_OnDraftPost_ShouldRemoveRecordFromDatabase` | Bài `Draft` thuộc Staff | `DeleteAsync(staff.Id, draft.Id)` | Xóa hoàn toàn bản ghi khỏi `blog_posts` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | `S-08` xóa nháp |
| **UTC013** | `UTC013_DeleteAsync_OnPendingOrApprovedPost_ShouldThrowMSGGen01` | Có bài `PendingApproval` và `Approved` | Staff gọi `DeleteAsync` trên cả 2 bài | Đều ném `BusinessRuleException ("MSG-GEN-01")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Bảo vệ bài chờ duyệt/xuất bản |
| **UTC014** | `UTC014_ListQueueAsync_ShouldFilterPendingOnlyAndSortBySubmittedAt` | Có `1` bài nháp & `2` bài nộp ở 2 thời điểm khác nhau | `ListQueueAsync(sort="oldest")` & `ListQueueAsync("kim loại", "newest")` | Loại bỏ nháp, trả đúng thứ tự cũ/mới và khớp từ khóa | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | `A-09` hàng đợi duyệt |
| **UTC015** | `UTC015_ApproveAsync_OnPendingPost_ShouldPublishNotifyAuthorAndLogAudit` | Bài đang `Pending_Approval` | `ApproveAsync(admin.Id, pending.Id)` | `Status="Approved"`, gán `PublishedAt`, thông báo Staff, ghi `AuditLog BLOG_APPROVE` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | `A-09`, `BR-ADM-05`, `MSG-ADM-05` |
| **UTC016** | `UTC016_RejectAsync_WithValidFeedback_ShouldReturnToDraftWithFeedbackNotifyAndLogAudit` | Bài đang `Pending_Approval` | `RejectAsync(admin.Id, pending.Id, "Cần bổ sung số liệu nguồn Bộ TNMT.")` | Quay về `Status="Draft"` với `RejectionReason`, gửi `Notification`, ghi `BLOG_REJECT` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | `A-09` trả về sửa ngay |
| **UTC017** | `UTC017_RejectAsync_WithBlankFeedback_ShouldThrowMSGAuth02` | Bài đang `Pending_Approval` | `RejectAsync(admin.Id, pending.Id, "   ")` | Ném `BusinessRuleException ("BR-ADM-08", "MSG-AUTH-02")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Bắt buộc góp ý khi từ chối |
| **UTC018** | `UTC018_ListPublishedAsync_ShouldMatchWholeTagOnlyAndExcludeDraftOrFuturePosts` | Có `1` Draft, `1` Approved thẻ `"nhua"`, `1` Approved thẻ `"nhua-pet"` | `ListPublishedAsync(null, "nhua", 1, 10)` | Chỉ trả về duy nhất bài mang thẻ chính xác `"nhua"`, không khớp tiền tố `"nhua-pet"` | Boundary | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Biên lọc nguyên thẻ JSON |
| **UTC019** | `UTC019_ListMineAsync_ShouldFilterByStatusSearchAndClampPageBounds` | Staff có `1` bài Draft & `1` bài Approved | `ListMineAsync(staff.Id, "Published", "carton", -1, 9999)` | Kẹp `Page=1`, `PageSize=50`, lọc đúng bài `Approved` | Boundary | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Biên phân trang `[1..50]` |
| **UTC020** | `UTC020_GetPublishedBySlugAndValidators_ShouldEnforceTitleTagsFeedbackAndNotFoundBounds` | Bài nháp vs bài đã duyệt; 3 Blog Validators | Truy cập slug bài nháp (`404`); check biên tiêu đề `9/10`, `6` tags, `Feedback 0/2001` | Ném `NotFoundException` cho bài chưa duyệt; Validators chặn đúng các biên | Boundary | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | `G-01` & FluentValidation |
| **UTC021** | `UTC021_SeedDefaultBlogPostsCoreAsync_ShouldSeedIdempotentlyWithValidSlugsCoversAndWorkflowStates` | DB sạch (`ListPublishedAsync == 0`) | Gọi `SeedDefaultBlogPostsCoreAsync()` 2 lần liên tiếp | Tạo đúng `6` bài (4 `Approved`, 1 `Pending_Approval`, 1 `Draft`) với ảnh `/assets/insights/*`, slug hợp lệ & idempotent tuyệt đối | Normal | Passed | 2026-11-27 | Hoàng Thắng Nguyễn | Seed mặc định chuẩn Demo |
