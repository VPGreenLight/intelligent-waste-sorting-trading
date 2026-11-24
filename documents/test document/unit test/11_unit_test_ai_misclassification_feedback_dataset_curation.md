# ĐẶC TẢ KIỂM THỬ ĐƠN VỊ (UNIT TEST SPECIFICATION)
## PHÂN HỆ M-13 / FE-15: PHẢN HỒI LỖI AI & QUẢN TRỊ BỘ DỮ LIỆU HUẤN LUYỆN (`AI MISCLASSIFICATION FEEDBACK & DATASET CURATION`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Module trong Report 5.1:** `Module 11` (`M-13` / `FE-15`)
* **Dịch vụ & Validator kiểm thử:** `AiFeedbackService`, `AiDatasetCurationService`, `AiDatasetExportBuilder`, `AiCategoryNormalizer`, `AiVisionHttpClient` (`CurateDatasetAsync`, `ReloadModelsAsync`), `AiCurationValidators`
* **Use Cases bao phủ:** `U-04`, `S-01`, `S-02`, `S-03`
* **Tệp mã kiểm thử tự động:** [`AiFeedbackCurationTests.cs`](../../../src/Ecolink.Backend/Ecolink.UnitTests/Module13_AiFeedbackCuration/AiFeedbackCurationTests.cs)
* **Ngày thực thi gần nhất:** `2026-11-17`
* **Kết quả thực thi:** **`22 / 22 Passed` (`100%` Pass Rate — `10 Normal`, `8 Abnormal`, `4 Boundary`)**

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ ĐƠN VỊ (SUMMARY TABLE)

| Code Module | `AiFeedbackService`, `AiDatasetCurationService`, `AiDatasetExportBuilder`, `AiCurationValidators` (`U-04`, `S-01`, `S-02`, `S-03`) | | |
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

| Test Case ID | Tên Method Kiểm thử (`AiFeedbackCurationTests`) | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| **UTC001** | `UTC001_RecordScanHistoryAsync_HighConfidenceScan_PersistsHistoryWithSha256WithoutAutoOodItem_Normal` | Tài khoản `Citizen` hợp lệ, ảnh JPEG `0.91` `plastic` | `RecordScanHistoryAsync` (`IsOutOfDistribution=false`, `Confidence=0.91`) | Lưu `AiScanHistory` có `ImageSha256` 64 ký tự, `ScanStatus="Completed"`, không tạo `LowConfidenceAuto` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `U-04` lưu vết |
| **UTC002** | `UTC002_RecordScanHistoryAsync_LowConfidenceOodScan_AutoQueuesLowConfidenceItemAndDeduplicatesSha256_Normal` | 1 tệp ảnh độ tin cậy `0.42 < 0.60` quét lặp 2 lần | `RecordScanHistoryAsync` (`IsOutOfDistribution=true`) 2 lần liên tiếp | Tạo `2` `AiScanHistories` nhưng chỉ nạp `1` `AiDatasetItem` (`LowConfidenceAuto`, `Unlabeled`) khử trùng `SHA-256` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-CLS-03` |
| **UTC003** | `UTC003_SubmitFeedbackAsync_ValidScanWithAliasLabel_SavesPendingLogAndSyncsAi_Normal` | Có `ScanId` và bảng `waste_categories` `Active` | `SubmitFeedbackAsync` (`ScanId`, `UserReportedLabel="plastic_bottle"`) | Chuẩn hóa `"plastic"`, tạo `AIFeedbackLog` (`Status=Pending`), đồng bộ `IAiVisionClient` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-CLS-05` |
| **UTC004** | `UTC004_GetMyFeedbackReportsAsync_IsolatesReportsByOwnerUserId_Normal` | Có báo cáo của `Citizen A` (đã duyệt `plastic`) và `Citizen B` | `GetMyFeedbackReportsAsync(CitizenA.Id, status="Reviewed")` | Chỉ trả báo cáo của `Citizen A`, không rò rỉ dữ liệu của `Citizen B` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-CLS-08` |
| **UTC005** | `UTC005_ReviewFeedbackAsync_ApproveAndLabelOneClick_CreatesVerifiedDatasetItemAndAuditLog_Normal` | Báo cáo đang `Pending` | `ReviewFeedbackAsync("ApproveAndLabel", "paper", 1 YOLO box)` | Cập nhật `Reviewed`, tạo `AiDatasetItem` (`VerifiedLabeled`) kèm hộp bao và `AuditLog` `REVIEW_AI_FEEDBACK` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `S-01` 1 chạm |
| **UTC006** | `UTC006_ReviewApproveToQueueThenAssignGroundTruthLabelWithYolo_PromotesToVerifiedLabeled_Normal` | Báo cáo đang `Pending` | `ApproveToQueue` $\rightarrow$ `AssignGroundTruthLabelAsync("metal", YOLO box)` | Chuyển từ hàng đợi `Unlabeled` sang `VerifiedLabeled`, ghi `ASSIGN_GROUND_TRUTH_LABEL` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `S-01` $\rightarrow$ `S-02` |
| **UTC007** | `UTC007_BatchAssignLabelsAsync_MultipleValidUnlabeledItems_UpdatesAtomicallyAndLogsAudit_Normal` | Có `3` `AiDatasetItems` trạng thái `Unlabeled` | `BatchAssignLabelsAsync(3 IDs, "cardboard")` | Cập nhật đủ `3` mục sang `VerifiedLabeled` và ghi `BATCH_ASSIGN_AI_LABELS` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `S-02` Batch |
| **UTC008** | `UTC008_CreateDatasetVersionAsync_ThirtyVerifiedSamples_Splits702010AndSyncsToAi_Normal` | Có `30` mẫu `VerifiedLabeled` (10 mỗi loại `plastic/paper/metal`) | `CreateDatasetVersionAsync("v1.0.0", 70/20/10, sync=true)` | `Train=21`, `Val=6`, `Test=3` (`7/2/1` từng lớp), `IsBalanced=true`, `Status="SyncedToAi"` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `S-03`, `BR-CLS-07` |
| **UTC009** | `UTC009_CreateDatasetVersionAsync_StickyStratifiedSplit_PreventsTrainTestLeakageAcrossVersions_Normal` | Đã phát hành `v1.0.0` (10 mẫu); thêm 10 mẫu mới | `CreateDatasetVersionAsync("v1.1.0")` | Mọi mẫu `Test`/`Val` của `v1.0.0` giữ nguyên `100%` phân tập cũ, giao `Train(v1.1) ∩ Test(v1.0) == ∅` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Zero Data Leakage |
| **UTC010** | `UTC010_ExportDatasetVersionAsync_YoloAndImageFolder_ProducesValidZipArchivesWithManifest_Normal` | Phiên bản `v1.0.0` có `2` mẫu `metal` | `ExportDatasetVersionAsync("yolo")` & `("imagefolder")` | Tạo `.zip` hợp lệ chứa `data.yaml`, `labels/train/*.txt`, `manifest.json`; ghi `2` `AuditLog` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `STT 157`, `BR-ADM-05` |
| **UTC011** | `UTC011_SubmitFeedbackAsync_UnknownOrInactiveCategory_ThrowsBusinessRuleBrCls02_Abnormal` | Có danh mục `inactive_foam` (`Status="Inactive"`) | `SubmitFeedbackAsync` với `"nuclear_waste"` & `"inactive_foam"` | Cả 2 trường hợp đều ném `BusinessRuleException ("BR-CLS-02")` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-CLS-02` |
| **UTC012** | `UTC012_SubmitFeedbackAsync_DuplicateScanId_ThrowsConflictException_Abnormal` | `ScanId` đã có `1` báo cáo sai trước đó | `SubmitFeedbackAsync` lần 2 với cùng `ScanId` | Ném `ConflictException` (`BR-CLS-05`) | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Chặn spam trùng |
| **UTC013** | `UTC013_SubmitFeedbackAsync_MissingScanId_ThrowsNotFoundException_Abnormal` | Truyền `ScanId = Guid.NewGuid()` | `SubmitFeedbackAsync` | Ném `NotFoundException` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Khóa toàn vẹn FK |
| **UTC014** | `UTC014_ReviewFeedbackAsync_RejectRequiresNoteMin5AndExcludesAutoCandidate_Abnormal` | Ca quét OOD `< 60%` đã tự tạo ứng viên `Unlabeled` | `ReviewFeedbackAsync("Reject")` với note `< 5` ký tự rồi note hợp lệ | Note ngắn ném `BusinessRuleException`; note hợp lệ đặt `Rejected` & `Excluded` mẫu khỏi kho `S-03` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-CLS-06` |
| **UTC015** | `UTC015_ReviewFeedbackAsync_AlreadyReviewedReport_ThrowsConflictException_Abnormal` | Báo cáo đã được `ApproveAndLabel` | Gọi `ReviewFeedbackAsync("Reject")` lần 2 | Ném `ConflictException` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Khóa tái thẩm định |
| **UTC016** | `UTC016_BatchAssignLabelsAsync_ContainsUnknownItemId_ThrowsNotFoundAndRollsBackAll_Abnormal` | Có 1 mẫu `Unlabeled` hợp lệ & 1 `Guid.NewGuid()` lạ | `BatchAssignLabelsAsync` | Ném `NotFoundException` và hoàn tác nguyên tử (mẫu hợp lệ giữ nguyên `Unlabeled`) | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | ACID Rollback |
| **UTC017** | `UTC017_CreateDatasetVersionAsync_EmptyVerifiedPoolOrDuplicateVersionCode_ThrowsExpectedErrors_Abnormal` | Kho chỉ có `Unlabeled`; sau đó tạo trùng `v1.0.0` | `CreateDatasetVersionAsync` | Kho rỗng ném `BusinessRuleException ("BR-CLS-06")`; trùng `VersionCode` ném `ConflictException` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-CLS-06`, `BR-CLS-07` |
| **UTC018** | `UTC018_AiServiceOfflineOnCurateSyncAndReload_SavesPublishedWithWarningWithoutCrashing_Abnormal` | `IAiVisionClient` trả thất bại / mất kết nối | `CreateDatasetVersionAsync(sync=true)` & `AiVisionHttpClient.ReloadModelsAsync()` | Dataset vẫn lưu an toàn `Published` kèm `AiSyncWarning`; `ReloadModelsAsync()` trả `Success=false` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Resilient Sync |
| **UTC019** | `UTC019_AssignGroundTruthLabelRequestValidator_YoloCoordinateBoundaries_ValidatesStrictUnitInterval_Boundary` | `AssignGroundTruthLabelRequestValidator` | Biên `[0.0, 1.0, 1.0, 0.001]` vs `width=0` & `xCenter=1.01` | Biên trong `[0, 1]` hợp lệ; ngoài biên hoặc `width <= 0` trả `IsValid=false` | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Biên tọa độ YOLO |
| **UTC020** | `UTC020_CreateDatasetVersionRequestValidator_RatiosAndSemverBoundaries_Enforces100PercentSum_Boundary` | `CreateDatasetVersionRequestValidator` | `70/20/10`, `100/0/0` vs tổng `95%` & semver `"1.0"` | Chấp nhận tổng `= 100%` & `^v\d+\.\d+\.\d+$`; từ chối tổng sai hoặc sai SemVer | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Biên tỷ lệ chia tập |
| **UTC021** | `UTC021_DatasetOverview_ImbalanceRatioThreshold250_FlagsImbalanceAndWeakestClass_Boundary` | Tỷ lệ `5 plastic : 2 glass` (`2.50x`) rồi `12 : 2` (`6.00x`) | `GetDatasetOverviewAsync()` | `2.50x` -> `IsBalanced=true`; `6.00x` -> `IsBalanced=false` & gắn `NeedsMoreSamples=true` cho `"glass"` | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Ngưỡng `2.50x` |
| **UTC022** | `UTC022_BatchAssignLabelsRequestValidator_ItemCountBoundaries1And200_EnforcesLimit_Boundary` | `BatchAssignLabelsRequestValidator` | Biên `1`, `200` phần tử vs `0`, `201` phần tử | `1` và `200` hợp lệ (`true`); `0` và `201` bị chặn (`false`) | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Giới hạn Batch `1..200` |
