# ĐẶC TẢ KIỂM THỬ ĐƠN VỊ (UNIT TEST SPECIFICATION)
## PHÂN HỆ M-13 / FE-15: PHẢN HỒI LỖI AI & QUẢN TRỊ BỘ DỮ LIỆU HUẤN LUYỆN (`AI MISCLASSIFICATION FEEDBACK & DATASET CURATION`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Module trong Report 5.1:** `Module 11` (`M-13` / `FE-15`)
* **Dịch vụ & Validator kiểm thử:** `AiFeedbackService`, `AiDatasetCurationService`, `AiDatasetExportBuilder`, `AiCategoryNormalizer`, `AiVisionHttpClient` (`CurateDatasetAsync`, `ReloadModelsAsync`), `AiCurationValidators`
* **Use Cases bao phủ:** `U-04`, `S-01`, `S-02`, `S-03`
* **Tệp mã kiểm thử tự động:** [`AiFeedbackCurationTests.cs`](../../../src/Ecolink.Backend/Ecolink.UnitTests/Module13_AiFeedbackCuration/AiFeedbackCurationTests.cs)
* **Ngày thực thi gần nhất:** `2026-11-17`
* **Kết quả thực thi:** **`Round 1`: `15/16 Passed`, `1 Failed` (`INC-2026-TST-13`) $\rightarrow$ `Round 2`: `16/16 Passed` (`100%` Pass Rate)**

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ ĐƠN VỊ (SUMMARY TABLE)

| Code Module | `AiFeedbackService`, `AiDatasetCurationService`, `AiDatasetExportBuilder`, `AiCurationValidators` (`U-04`, `S-01`, `S-02`, `S-03`) | | |
| :--- | :---: | :---: | :---: |
| **Number of TCs** | **16** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 15 | 1 | 0 |
| *Round 2* | 16 | 0 | 0 |
| *Round 3* | 0 | 0 | 16 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count** | **8** | **5** | **3** |

---

## 2. CHI TIẾT 16 UNIT TEST CASES (`UTC001` – `UTC016`)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- | :---: | :---: | :---: | :--- |
| **UTC001** | Tài khoản `Citizen` hợp lệ, ảnh JPEG `0.91` `plastic` | `RecordScanHistoryAsync` (`IsOutOfDistribution=false`, `Confidence=0.91`) | Lưu `AiScanHistory` có `ImageSha256` 64 ký tự, `ScanStatus="Completed"`, không tạo `LowConfidenceAuto` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC001_RecordScanHistoryAsync_HighConfidenceScan_PersistsHistoryWithSha256WithoutAutoOodItem_Normal]` `U-04` lưu vết |
| **UTC002** | 1 tệp ảnh độ tin cậy `0.42 < 0.60` quét lặp 2 lần | `RecordScanHistoryAsync` (`IsOutOfDistribution=true`) 2 lần liên tiếp | Tạo `2` `AiScanHistories` nhưng chỉ nạp `1` `AiDatasetItem` (`LowConfidenceAuto`, `Unlabeled`) khử trùng `SHA-256` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC002_RecordScanHistoryAsync_LowConfidenceOodScan_AutoQueuesLowConfidenceItemAndDeduplicatesSha256_Normal]` `BR-CLS-03` |
| **UTC003** | Có `ScanId` và bảng `waste_categories` `Active` | `SubmitFeedbackAsync` (`ScanId`, `UserReportedLabel="plastic_bottle"`) | Chuẩn hóa `"plastic"`, tạo `AIFeedbackLog` (`Status=Pending`), đồng bộ `IAiVisionClient` | Normal | Failed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | **INC-2026-TST-13** | `[UTC003_SubmitFeedbackAsync_ValidScanWithAliasLabel_SavesPendingLogAndSyncsAi_Normal]` `BR-CLS-05` — Round 1 Failed (INC-2026-TST-13): ánh xạ 10 lớp YOLO26 trong AiCategoryNormalizer -> đã đồng bộ Round 2 Passed |
| **UTC004** | Có báo cáo của `Citizen A` (đã duyệt `plastic`) và `Citizen B` | `GetMyFeedbackReportsAsync(CitizenA.Id, status="Reviewed")` | Chỉ trả báo cáo của `Citizen A`, không rò rỉ dữ liệu của `Citizen B` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC004_GetMyFeedbackReportsAsync_IsolatesReportsByOwnerUserId_Normal]` `BR-CLS-08` |
| **UTC005** | Báo cáo đang `Pending` | `ReviewFeedbackAsync("ApproveAndLabel", "paper", 1 YOLO box)` | Cập nhật `Reviewed`, tạo `AiDatasetItem` (`VerifiedLabeled`) kèm hộp bao và `AuditLog` `REVIEW_AI_FEEDBACK` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC005_ReviewFeedbackAsync_ApproveAndLabelOneClick_CreatesVerifiedDatasetItemAndAuditLog_Normal]` `S-01` 1 chạm |
| **UTC006** | Có `30` mẫu `VerifiedLabeled` (10 mỗi loại `plastic/paper/metal`) | `CreateDatasetVersionAsync("v1.0.0", 70/20/10, sync=true)` | `Train=21`, `Val=6`, `Test=3` (`7/2/1` từng lớp), `IsBalanced=true`, `Status="SyncedToAi"` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC006_CreateDatasetVersionAsync_ThirtyVerifiedSamples_Splits702010AndSyncsToAi_Normal]` `S-03`, `BR-CLS-07` |
| **UTC007** | Đã phát hành `v1.0.0` (10 mẫu); thêm 10 mẫu mới | `CreateDatasetVersionAsync("v1.1.0")` | Mọi mẫu `Test`/`Val` của `v1.0.0` giữ nguyên `100%` phân tập cũ, giao `Train(v1.1) ∩ Test(v1.0) == ∅` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC007_CreateDatasetVersionAsync_StickyStratifiedSplit_PreventsTrainTestLeakageAcrossVersions_Normal]` Zero Data Leakage |
| **UTC008** | Phiên bản `v1.0.0` có `2` mẫu `metal` | `ExportDatasetVersionAsync("yolo")` & `("imagefolder")` | Tạo `.zip` hợp lệ chứa `data.yaml`, `labels/train/*.txt`, `manifest.json`; ghi `2` `AuditLog` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC008_ExportDatasetVersionAsync_YoloAndImageFolder_ProducesValidZipArchivesWithManifest_Normal]` `STT 157`, `BR-ADM-05` |
| **UTC009** | Có danh mục `inactive_foam` (`Status="Inactive"`) | `SubmitFeedbackAsync` với `"nuclear_waste"` & `"inactive_foam"` | Cả 2 trường hợp đều ném `BusinessRuleException ("BR-CLS-02")` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC009_SubmitFeedbackAsync_UnknownOrInactiveCategory_ThrowsBusinessRuleBrCls02_Abnormal]` `BR-CLS-02` |
| **UTC010** | `ScanId` đã có `1` báo cáo sai trước đó | `SubmitFeedbackAsync` lần 2 với cùng `ScanId` | Ném `ConflictException` (`BR-CLS-05`) | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC010_SubmitFeedbackAsync_DuplicateScanId_ThrowsConflictException_Abnormal]` Chặn spam trùng |
| **UTC011** | Truyền `ScanId = Guid.NewGuid()` | `SubmitFeedbackAsync` | Ném `NotFoundException` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC011_SubmitFeedbackAsync_MissingScanId_ThrowsNotFoundException_Abnormal]` Khóa toàn vẹn FK |
| **UTC012** | Ca quét OOD `< 60%` đã tự tạo ứng viên `Unlabeled` | `ReviewFeedbackAsync("Reject")` với note `< 5` ký tự rồi note hợp lệ | Note ngắn ném `BusinessRuleException`; note hợp lệ đặt `Rejected` & `Excluded` mẫu khỏi kho `S-03` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC012_ReviewFeedbackAsync_RejectRequiresNoteMin5AndExcludesAutoCandidate_Abnormal]` `BR-CLS-06` |
| **UTC013** | Kho chỉ có `Unlabeled`; sau đó tạo trùng `v1.0.0` | `CreateDatasetVersionAsync` | Kho rỗng ném `BusinessRuleException ("BR-CLS-06")`; trùng `VersionCode` ném `ConflictException` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC013_CreateDatasetVersionAsync_EmptyVerifiedPoolOrDuplicateVersionCode_ThrowsExpectedErrors_Abnormal]` `BR-CLS-06`, `BR-CLS-07` |
| **UTC014** | `AssignGroundTruthLabelRequestValidator` | Biên `[0.0, 1.0, 1.0, 0.001]` vs `width=0` & `xCenter=1.01` | Biên trong `[0, 1]` hợp lệ; ngoài biên hoặc `width <= 0` trả `IsValid=false` | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC014_AssignGroundTruthLabelRequestValidator_YoloCoordinateBoundaries_ValidatesStrictUnitInterval_Boundary]` Biên tọa độ YOLO |
| **UTC015** | `CreateDatasetVersionRequestValidator` | `70/20/10`, `100/0/0` vs tổng `95%` & semver `"1.0"` | Chấp nhận tổng `= 100%` & `^v\d+\.\d+\.\d+$`; từ chối tổng sai hoặc sai SemVer | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC015_CreateDatasetVersionRequestValidator_RatiosAndSemverBoundaries_Enforces100PercentSum_Boundary]` Biên tỷ lệ chia tập |
| **UTC016** | Tỷ lệ `5 plastic : 2 glass` (`2.50x`) rồi `12 : 2` (`6.00x`) | `GetDatasetOverviewAsync()` | `2.50x` -> `IsBalanced=true`; `6.00x` -> `IsBalanced=false` & gắn `NeedsMoreSamples=true` cho `"glass"` | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC016_DatasetOverview_ImbalanceRatioThreshold250_FlagsImbalanceAndWeakestClass_Boundary]` Ngưỡng `2.50x` |
