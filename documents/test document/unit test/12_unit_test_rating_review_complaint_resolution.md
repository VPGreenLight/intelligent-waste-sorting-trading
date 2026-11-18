# ĐẶC TẢ KIỂM THỬ ĐƠN VỊ (UNIT TEST SPECIFICATION)
## PHÂN HỆ M-11 / FE-10: ĐÁNH GIÁ UY TÍN & GIẢI QUYẾT KHIẾU NẠI (`RATING, REVIEW & COMPLAINT RESOLUTION`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Module trong Report 5.1:** `Module 12` (`M-11` / `FE-10`)
* **Dịch vụ & Validator kiểm thử:** `ReviewService`, `ComplaintService`, `HandoverReceiptService` (`DisputeAsync`), `ReviewAndComplaintValidators`
* **Use Cases bao phủ:** `U-10` (Rate Recycler), `U-11` (Submit Complaint), `S-05` (Handle Complaints)
* **Tệp mã kiểm thử tự động:** [`ReviewComplaintTests.cs`](../../../src/Ecolink.Backend/Ecolink.UnitTests/Module11_ReviewsComplaints/ReviewComplaintTests.cs)
* **Ngày thực thi gần nhất:** `2026-11-18`
* **Kết quả thực thi:** **`22 / 22 Passed` (`100%` Pass Rate — `10 Normal`, `8 Abnormal`, `4 Boundary`)**

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ ĐƠN VỊ (SUMMARY TABLE)

| Code Module | `ReviewService`, `ComplaintService`, `HandoverReceiptService.DisputeAsync`, `ReviewAndComplaintValidators` (`U-10`, `U-11`, `S-05`) | | |
| :--- | :---: | :---: | :---: |
| **Number of TCs** | **22** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 22 | 0 | 0 |
| *Round 2* | 0 | 0 | 22 |
| *Round 3* | 0 | 0 | 22 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count** | **10** | **8** | **4** |

---

## 2. CHI TIẾT 22 UNIT TEST CASES (`UTC001` – `UTC022`)

| Test Case ID | Tên Method Kiểm thử (`ReviewComplaintTests`) | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| **UTC001** | `UTC001_CreateReviewAsync_CompletedHandoverWithin14Days_CreatesReviewAndUpdatesRecyclerAverage_Normal` | Đơn `HandoverRequest` thuộc `Citizen`, `Status=Completed` | `CreateReviewAsync(5, "Cân chuẩn...")` | Lưu `TransactionReview` (`Visible`), cập nhật `RecyclerProfile`: `AverageRating=5.00`, `TotalReviews=1` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | `BR-RVW-01` |
| **UTC002** | `UTC002_CreateReviewAsync_MultipleReviews_CalculatesExactAverageAndStarDistribution_Normal` | Đã có 1 đánh giá `5` sao trên vựa; thêm Citizen thứ 2 | `CreateReviewAsync(4)` $\rightarrow$ `GetRecyclerReviewsAsync` | `AverageRating=4.50`, `TotalReviews=2`, phân bố `FiveStars=1`, `FourStars=1` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Tính TB nguyên tử |
| **UTC003** | `UTC003_DeleteMyReviewAsync_AuthorSelfDeletes_SetsDeletedAndRecalculatesRating_Normal` | Citizen đã đăng đánh giá `3` sao | `DeleteMyReviewAsync(citizenId, reviewId)` | `Status="Deleted"`, `AverageRating=0`, `TotalReviews=0`, `/reviews/me` giảm | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | `BR-RVW-03` |
| **UTC004** | `UTC004_ReportAndModerateReviewAsync_StaffHidesReportedReview_RecalculatesRatingAndLogsAudit_Normal` | Có review `1` sao spam; Recycler báo cáo | `ReportAbusiveReviewAsync` $\rightarrow$ `ModerateReviewAsync("Hide")` | Chuyển `Hidden`, `AverageRating=0`, ghi `AuditLog` `MODERATE_REVIEW` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | `BR-RVW-03`, `BR-ADM-05` |
| **UTC005** | `UTC005_ModerateReviewAsync_StaffRestoresHiddenReview_IncludesBackInAverage_Normal` | Review `5` sao đang bị `Hidden` | `ModerateReviewAsync("Restore", lý do)` | Phục hồi `Visible`, `AverageRating=5.00`, `TotalReviews=1` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | `S-05` khôi phục |
| **UTC006** | `UTC006_SubmitComplaintAsync_CitizenSubmitsScaleDiscrepancy_CreatesTicketAndMarksDisputed_Normal` | Đơn đang `AwaitingConfirmation` trong hạn `7 ngày` | `SubmitComplaintAsync("ScaleDiscrepancy", 1 ảnh)` | Mã `CMP-...`, `ComplainantRole="Citizen"`, `Status="Open"`, đơn thành `Disputed` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | `U-11` |
| **UTC007** | `UTC007_SubmitComplaintAsync_BilateralRecyclerSubmitsNoShow_SetsComplainantRoleRecycler_Normal` | Đơn `Accepted` chưa bàn giao | Recycler gọi `SubmitComplaintAsync("NoShow")` | `ComplainantRole="Recycler"`, `RespondentId=Citizen.Id`, `Status="Open"` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Khiếu nại 2 chiều |
| **UTC008** | `UTC008_HandoverReceiptDisputeAsync_BridgesAutomaticallyToStaffComplaintQueue_Normal` | Biên nhận chờ Citizen xác nhận (`AwaitingConfirmation`) | `HandoverReceiptService.DisputeAsync(reason, ảnh)` | Biên nhận thành `Disputed` & tự sinh phiếu `TransactionComplaint` (`ScaleDiscrepancy`) | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Hợp nhất `M-07` |
| **UTC009** | `UTC009_GetStaffComplaintDossierAsync_ClaimsTicketAndAggregatesReceiptAndChatHistory_Normal` | Có khiếu nại `Open`, `HandoverReceipt` và tin nhắn `ChatMessage` | `GetStaffComplaintDossierAsync(staffId, id, claim=true)` | Chuyển `Investigating`, trả `ScaleProofPhotoUrls` & toàn bộ `NegotiationMessages` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Hồ sơ 360°, `BR-DSP-06` |
| **UTC010** | `UTC010_ResolveComplaintAsync_CancelDisputedOrderWithWarning_RevokesPointsWarnsAndAudits_Normal` | Đơn `Completed` (+50đ, Citizen có 120đ); phiếu `Open` | `ResolveComplaintAsync("CancelDisputedOrder", IssueWarning=true)` | Đơn `Cancelled`, điểm còn `70`, `WarningCount=1`, ghi `AccountModerationLog` & `AuditLog` | Normal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | `S-05`, `BR-GAM-01` |
| **UTC011** | `UTC011_CreateReviewAsync_NonCompletedHandover_ThrowsBusinessRuleBrRvw01_Abnormal` | Đơn đang `Accepted` (chưa hoàn tất) | `CreateReviewAsync(5)` | Ném `BusinessRuleException ("BR-RVW-01", "MSG-RVW-02")` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Chặn review sớm |
| **UTC012** | `UTC012_CreateReviewAsync_DuplicateReviewSameHandover_ThrowsConflictException_Abnormal` | Đơn `Completed` đã có 1 review của Citizen | `CreateReviewAsync(4)` lần 2 | Ném `ConflictException ("MSG-RVW-02")` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | `1-Đơn / 1-Review` |
| **UTC013** | `UTC013_DeleteMyReviewAsync_CalledByRatedRecycler_ThrowsForbiddenAccess_Abnormal` | Có review của Citizen trên vựa | Recycler gọi `DeleteMyReviewAsync` | Ném `ForbiddenAccessException` (`403`) | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Cấm vựa tự xóa |
| **UTC014** | `UTC014_SubmitComplaintAsync_OutsiderNotInHandover_ThrowsForbiddenAccess_Abnormal` | Người dùng ngoài đơn (`Guid.NewGuid()`) | `SubmitComplaintAsync` lên đơn người khác | Ném `ForbiddenAccessException` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Chống IDOR `U-11` |
| **UTC015** | `UTC015_SubmitComplaintAsync_SecondActiveComplaintSameRequest_ThrowsConflictMsgDsp04_Abnormal` | Đơn đã có phiếu `Open` do Citizen gửi | Recycler mở thêm phiếu thứ 2 cùng đơn | Ném `ConflictException ("MSG-DSP-04")` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Tối đa 1 phiếu mở |
| **UTC016** | `UTC016_SubmitComplaintAsync_Past7DaysWindow_ThrowsBusinessRuleMsgDsp03_Abnormal` | Đơn hoàn tất cách đây `8 ngày` (`> 7 ngày`) | `SubmitComplaintAsync` | Ném `BusinessRuleException ("BR-DSP-01", "MSG-DSP-03")` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Quá hạn 7 ngày |
| **UTC017** | `UTC017_ResolveComplaintAsync_AlreadyResolvedComplaint_ThrowsConflictException_Abnormal` | Phiếu đã chốt `Dismissed` | Staff gọi `ResolveComplaintAsync` lần 2 | Ném `ConflictException` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Khóa tái phân xử |
| **UTC018** | `UTC018_CreateReviewAsync_Past14DaysWindow_ThrowsBusinessRuleMsgRvw02_Abnormal` | Đơn hoàn tất cách đây `15 ngày` (`> 14 ngày`) | `CreateReviewAsync(5)` | Ném `BusinessRuleException ("BR-RVW-01", "MSG-RVW-02")` | Abnormal | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Quá hạn 14 ngày |
| **UTC019** | `UTC019_CreateReviewRequestValidator_RatingStarsAndCommentLengthBoundaries_Boundary` | `CreateReviewRequestValidator` | Stars `1`, `5`, `0`, `6`; comment `1000` vs `1001` ký tự | `1..5` & `<= 1000` hợp lệ; `0`, `6` và `1001` trả `IsValid=false` | Boundary | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | Biên đánh giá |
| **UTC020** | `UTC020_SubmitComplaintRequestValidator_Description15AndMax5EvidenceUrlsBoundaries_Boundary` | `SubmitComplaintRequestValidator` | Description `15` vs `14` ký tự; `5` vs `6` URL | Đúng biên `>= 15` & `<= 5` ảnh hợp lệ; `14` hoặc `6` bị chặn | Boundary | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | `BR-DSP-02` |
| **UTC021** | `UTC021_ResolveComplaintRequestValidator_ResolutionNote10CharBoundary_Boundary` | `ResolveComplaintRequestValidator` | `ResolutionNote` dài `10` vs `9` ký tự | `10` ký tự hợp lệ (`true`); `9` ký tự bị chặn (`false`) | Boundary | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | `BR-DSP-04` |
| **UTC022** | `UTC022_ResolveComplaintAsync_PointClawbackFloorZeroAndConfirmDisputedAwardsPoints_Boundary` | Citizen chỉ còn `20đ` (thấp hơn `50đ` cần thu hồi); và đơn `Disputed` chưa cộng điểm | `CancelDisputedOrder` & `ConfirmReceiptAsIs` | Kẹp sàn `CurrentGreenPoints=0` (không bao giờ âm `BR-GAM-03`); đơn `Disputed` chốt cộng `50đ` | Boundary | Passed | 2026-11-18 | Hoàng Thắng Nguyễn | `BR-GAM-03` Floor 0 |
