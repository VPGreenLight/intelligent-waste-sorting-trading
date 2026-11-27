# ĐẶC TẢ KIỂM THỬ ĐƠN VỊ (UNIT TEST SPECIFICATION)
## PHÂN HỆ M-11 / FE-10: ĐÁNH GIÁ UY TÍN & GIẢI QUYẾT KHIẾU NẠI (`RATING, REVIEW & COMPLAINT RESOLUTION`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Module trong Report 5.1:** `Module 12` (`M-11` / `FE-10`)
* **Dịch vụ & Validator kiểm thử:** `ReviewService`, `ComplaintService`, `HandoverReceiptService` (`DisputeAsync`), `ReviewAndComplaintValidators`
* **Use Cases bao phủ:** `U-10` (Rate Recycler), `U-11` (Submit Complaint), `S-05` (Handle Complaints)
* **Tệp mã kiểm thử tự động:** [`ReviewComplaintTests.cs`](../../../src/Ecolink.Backend/Ecolink.UnitTests/Module11_ReviewsComplaints/ReviewComplaintTests.cs)
* **Ngày thực thi gần nhất:** `2026-11-18`
* **Kết quả thực thi:** **`Round 1`: `15/15 Passed` $\rightarrow$ `Round 2`: `15/15 Passed` (`100%` Pass Rate)**

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ ĐƠN VỊ (SUMMARY TABLE)

| Code Module | `ReviewService`, `ComplaintService`, `HandoverReceiptService.DisputeAsync`, `ReviewAndComplaintValidators` (`U-10`, `U-11`, `S-05`) | | |
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
| **UTC001** | Đơn `HandoverRequest` thuộc `Citizen`, `Status=Completed` | `CreateReviewAsync(5, "Cân chuẩn...")` | Lưu `TransactionReview` (`Visible`), cập nhật `RecyclerProfile`: `AverageRating=5.00`, `TotalReviews=1` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC001_CreateReviewAsync_CompletedHandoverWithin14Days_CreatesReviewAndUpdatesRecyclerAverage_Normal]` `BR-RVW-01` |
| **UTC002** | Citizen đã đăng đánh giá `3` sao | `DeleteMyReviewAsync(citizenId, reviewId)` | `Status="Deleted"`, `AverageRating=0`, `TotalReviews=0`, `/reviews/me` giảm | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC002_DeleteMyReviewAsync_AuthorSelfDeletes_SetsDeletedAndRecalculatesRating_Normal]` `BR-RVW-03` |
| **UTC003** | Có review `1` sao spam; Recycler báo cáo | `ReportAbusiveReviewAsync` $\rightarrow$ `ModerateReviewAsync("Hide")` | Chuyển `Hidden`, `AverageRating=0`, ghi `AuditLog` `MODERATE_REVIEW` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC003_ReportAndModerateReviewAsync_StaffHidesReportedReview_RecalculatesRatingAndLogsAudit_Normal]` `BR-RVW-03`, `BR-ADM-05` |
| **UTC004** | Đơn đang `AwaitingConfirmation` trong hạn `7 ngày` | `SubmitComplaintAsync("ScaleDiscrepancy", 1 ảnh)` | Mã `CMP-...`, `ComplainantRole="Citizen"`, `Status="Open"`, đơn thành `Disputed` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC004_SubmitComplaintAsync_CitizenSubmitsScaleDiscrepancy_CreatesTicketAndMarksDisputed_Normal]` `U-11` |
| **UTC005** | Biên nhận chờ Citizen xác nhận (`AwaitingConfirmation`) | `HandoverReceiptService.DisputeAsync(reason, ảnh)` | Biên nhận thành `Disputed` & tự sinh phiếu `TransactionComplaint` (`ScaleDiscrepancy`) | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC005_HandoverReceiptDisputeAsync_BridgesAutomaticallyToStaffComplaintQueue_Normal]` Hợp nhất `M-07` |
| **UTC006** | Có khiếu nại `Open`, `HandoverReceipt` và tin nhắn `ChatMessage` | `GetStaffComplaintDossierAsync(staffId, id, claim=true)` | Chuyển `Investigating`, trả `ScaleProofPhotoUrls` & toàn bộ `NegotiationMessages` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC006_GetStaffComplaintDossierAsync_ClaimsTicketAndAggregatesReceiptAndChatHistory_Normal]` Hồ sơ 360°, `BR-DSP-06` |
| **UTC007** | Đơn `Completed` (+50đ, Citizen có 120đ); phiếu `Open` | `ResolveComplaintAsync("CancelDisputedOrder", IssueWarning=true)` | Đơn `Cancelled`, điểm còn `70`, `WarningCount=1`, ghi `AccountModerationLog` & `AuditLog` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC007_ResolveComplaintAsync_CancelDisputedOrderWithWarning_RevokesPointsWarnsAndAudits_Normal]` `S-05`, `BR-GAM-01` |
| **UTC008** | Đơn đang `Accepted` (chưa hoàn tất) | `CreateReviewAsync(5)` | Ném `BusinessRuleException ("BR-RVW-01", "MSG-RVW-02")` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC008_CreateReviewAsync_NonCompletedHandover_ThrowsBusinessRuleBrRvw01_Abnormal]` Chặn review sớm |
| **UTC009** | Đơn `Completed` đã có 1 review của Citizen | `CreateReviewAsync(4)` lần 2 | Ném `ConflictException ("MSG-RVW-02")` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC009_CreateReviewAsync_DuplicateReviewSameHandover_ThrowsConflictException_Abnormal]` `1-Đơn / 1-Review` |
| **UTC010** | Người dùng ngoài đơn (`Guid.NewGuid()`) | `SubmitComplaintAsync` lên đơn người khác | Ném `ForbiddenAccessException` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC010_SubmitComplaintAsync_OutsiderNotInHandover_ThrowsForbiddenAccess_Abnormal]` Chống IDOR `U-11` |
| **UTC011** | Đơn hoàn tất cách đây `8 ngày` (`> 7 ngày`) | `SubmitComplaintAsync` | Ném `BusinessRuleException ("BR-DSP-01", "MSG-DSP-03")` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC011_SubmitComplaintAsync_Past7DaysWindow_ThrowsBusinessRuleMsgDsp03_Abnormal]` Quá hạn 7 ngày |
| **UTC012** | Đơn hoàn tất cách đây `15 ngày` (`> 14 ngày`) | `CreateReviewAsync(5)` | Ném `BusinessRuleException ("BR-RVW-01", "MSG-RVW-02")` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC012_CreateReviewAsync_Past14DaysWindow_ThrowsBusinessRuleMsgRvw02_Abnormal]` Quá hạn 14 ngày |
| **UTC013** | `SubmitComplaintRequestValidator` | Description `15` vs `14` ký tự; `5` vs `6` URL | Đúng biên `>= 15` & `<= 5` ảnh hợp lệ; `14` hoặc `6` bị chặn | Boundary | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC013_SubmitComplaintRequestValidator_Description15AndMax5EvidenceUrlsBoundaries_Boundary]` `BR-DSP-02` |
| **UTC014** | `ResolveComplaintRequestValidator` | `ResolutionNote` dài `10` vs `9` ký tự | `10` ký tự hợp lệ (`true`); `9` ký tự bị chặn (`false`) | Boundary | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC014_ResolveComplaintRequestValidator_ResolutionNote10CharBoundary_Boundary]` `BR-DSP-04` |
| **UTC015** | Citizen chỉ còn `20đ` (thấp hơn `50đ` cần thu hồi); và đơn `Disputed` chưa cộng điểm | `CancelDisputedOrder` & `ConfirmReceiptAsIs` | Kẹp sàn `CurrentGreenPoints=0` (không bao giờ âm `BR-GAM-03`); đơn `Disputed` chốt cộng `50đ` | Boundary | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC015_ResolveComplaintAsync_PointClawbackFloorZeroAndConfirmDisputedAwardsPoints_Boundary]` `BR-GAM-03` Floor 0 |
