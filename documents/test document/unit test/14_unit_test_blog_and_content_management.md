# ĐẶC TẢ KIỂM THỬ ĐƠN VỊ (UNIT TEST SPECIFICATION)
## PHÂN HỆ M-14 / FE-16: QUẢN TRỊ BÀI VIẾT BLOG & NỘI DUNG TRUYỀN THÔNG (`BLOG & CONTENT MANAGEMENT`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Module trong Report 5.1:** `Module 14` (`M-14` / `FE-16`)
* **Dịch vụ & Validator kiểm thử:** `BlogPostService`, `SaveBlogPostRequestValidator`, `RejectBlogPostRequestValidator`, `ReviewBlogPostRequestValidator`
* **Use Cases bao phủ:** `G-01` (Browse Public Eco Blogs), `S-08` (Create Blog Post), `A-09` (Approve / Reject Blog Post)
* **Tệp mã kiểm thử tự động:** [`BlogContentUnitTests.cs`](../../../src/Ecolink.Backend/Ecolink.UnitTests/Module14_BlogContent/BlogContentUnitTests.cs)
* **Ngày thực thi gần nhất:** `2026-11-27`
* **Kết quả thực thi:** **`Round 1`: `15/15 Passed` $\rightarrow$ `Round 2`: `15/15 Passed` (`100%` Pass Rate)**

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ ĐƠN VỊ (SUMMARY TABLE)

| Code Module | `BlogPostService`, `SaveBlogPostRequestValidator`, `RejectBlogPostRequestValidator`, `ReviewBlogPostRequestValidator` (`M-14`) | | |
| :--- | :---: | :---: | :---: |
| **Number of TCs** | **15** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 15 | 0 | 0 |
| *Round 2* | 15 | 0 | 0 |
| *Round 3* | 0 | 0 | 15 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count** | **7** | **5** | **3** |

---

## 2. CHI TIẾT 15 UNIT TEST CASES (`UTC001` – `UTC015`)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- | :---: | :---: | :---: | :--- |
| **UTC001** | Staff tồn tại | `CreateAsync(Title="  Đổi rác nhựa nhận quà xanh tại Hà Nội!  ", Tags=["Nhựa PET","nhựa pet","Kinh tế tuần hoàn"], Submit=false)` | `Status="Draft"`, `Slug="doi-rac-nhua-nhan-qua-xanh-tai-ha-noi"`, `Tags` còn `2` thẻ duy nhất, `CanEdit/Submit/Delete=true` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC001_CreateAsync_DraftWithVietnameseTitle_ShouldSlugifyAndNormalizeUniqueTags]` `S-08` Slugify tiếng Việt |
| **UTC002** | Đã có 1 bài trùng tiêu đề trong DB | Tạo bài thứ 2 cùng tiêu đề + bài tiêu đề dài `150` ký tự | Bài 2 sinh `Slug="...-2"`; bài tiêu đề `150` ký tự tạo `Slug.Length <= 200` | Boundary | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC002_CreateAsync_DuplicateTitle_ShouldAppendNumericSuffixAndClampLongSlug]` Chống trùng Slug & biên độ dài |
| **UTC003** | Staff & Admin Active tồn tại | `CreateAsync(ValidRequest(submit: true))` | `Status="Pending_Approval"`, `SubmittedAt != null`, `CanRecall=true`, tạo `Notification` cho Admin | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC003_CreateAsync_WithSubmitForApprovalTrue_ShouldTransitionToPendingAndNotifyAdmins]` Nộp duyệt ngay lúc tạo |
| **UTC004** | Staff tồn tại | `CreateAsync(submit: true, cover: null)` & `(content: "   ")` | Đều ném `BusinessRuleException ("BR-ADM-08", "MSG-AUTH-02")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC004_CreateAsync_SubmitWithoutCoverOrContent_ShouldThrowMSGAuth02]` Chặn nộp thiếu bìa/nội dung |
| **UTC005** | Có bài `Draft` của Staff | `UpdateAsync` đổi tiêu đề, nội dung & thẻ mới | Cập nhật dữ liệu và tái sinh `Slug` theo tiêu đề mới | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC005_UpdateAsync_OnDraft_ShouldUpdateFieldsAndRegenerateSlug]` Cho phép đổi slug khi còn Draft |
| **UTC006** | Có bài `PendingApproval` và sau đó `Approved` | `UpdateAsync` trên từng trạng thái | Đều ném `BusinessRuleException ("MSG-GEN-01")` bắt buộc thu hồi/cấm sửa | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC006_UpdateAsync_OnPendingOrApprovedPost_ShouldThrowMSGGen01]` `BR-ADM-08` khóa sửa |
| **UTC007** | `AuthorA` đã tạo bài `Draft`; tồn tại `AuthorB` | `AuthorB` gọi `GetMineAsync` & `UpdateAsync` lên bài của `AuthorA` | Ném `NotFoundException` (cô lập tác giả) | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC007_GetMineAndUpdate_ByDifferentStaffAuthor_ShouldThrowNotFoundException]` Chống IDOR giữa Staff |
| **UTC008** | Có bài `Draft` đầy đủ bìa + nội dung; có Admin | `SubmitAsync(staff.Id, draft.Id)` | Chuyển `Pending_Approval`, gửi `Notification` tới Admin | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC008_SubmitAsync_ValidDraft_ShouldTransitionToPendingAndNotifyAdmins]` `S-08` nộp bản nháp |
| **UTC009** | Bài đã được Admin duyệt (`Approved`) | Staff gọi `SubmitAsync` lặp lại | Ném `BusinessRuleException ("MSG-GEN-01")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC009_SubmitAsync_OnAlreadyApprovedPost_ShouldThrowMSGGen01]` Chặn nộp bài đã duyệt |
| **UTC010** | Bài đang `Pending_Approval` | `RecallAsync(staff.Id, pending.Id)` | Quay về `Status="Draft"`, `SubmittedAt=null`, `CanEdit=true`, thông báo Admin | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC010_RecallAsync_OnPendingPost_ShouldRevertToDraftAndNotifyAdmins]` `S-08` thu hồi chỉnh sửa |
| **UTC011** | Bài `Draft` thuộc Staff | `DeleteAsync(staff.Id, draft.Id)` | Xóa hoàn toàn bản ghi khỏi `blog_posts` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC011_DeleteAsync_OnDraftPost_ShouldRemoveRecordFromDatabase]` `S-08` xóa nháp |
| **UTC012** | Bài đang `Pending_Approval` | `ApproveAsync(admin.Id, pending.Id)` | `Status="Approved"`, gán `PublishedAt`, thông báo Staff, ghi `AuditLog BLOG_APPROVE` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC012_ApproveAsync_OnPendingPost_ShouldPublishNotifyAuthorAndLogAudit]` `A-09`, `BR-ADM-05`, `MSG-ADM-05` |
| **UTC013** | Bài đang `Pending_Approval` | `RejectAsync(admin.Id, pending.Id, "   ")` | Ném `BusinessRuleException ("BR-ADM-08", "MSG-AUTH-02")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC013_RejectAsync_WithBlankFeedback_ShouldThrowMSGAuth02]` Bắt buộc góp ý khi từ chối |
| **UTC014** | Có `1` Draft, `1` Approved thẻ `"nhua"`, `1` Approved thẻ `"nhua-pet"` | `ListPublishedAsync(null, "nhua", 1, 10)` | Chỉ trả về duy nhất bài mang thẻ chính xác `"nhua"`, không khớp tiền tố `"nhua-pet"` | Boundary | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC014_ListPublishedAsync_ShouldMatchWholeTagOnlyAndExcludeDraftOrFuturePosts]` Biên lọc nguyên thẻ JSON |
| **UTC015** | Staff có `1` bài Draft & `1` bài Approved | `ListMineAsync(staff.Id, "Published", "carton", -1, 9999)` | Kẹp `Page=1`, `PageSize=50`, lọc đúng bài `Approved` | Boundary | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC015_ListMineAsync_ShouldFilterByStatusSearchAndClampPageBounds]` Biên phân trang `[1..50]` |
