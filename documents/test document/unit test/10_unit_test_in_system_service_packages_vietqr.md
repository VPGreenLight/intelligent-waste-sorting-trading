# ĐẶC TẢ KIỂM THỬ ĐƠN VỊ (UNIT TEST SPECIFICATION)
## PHÂN HỆ M-12 / FE-14: GÓI DỊCH VỤ NỘI BỘ & TÍCH HỢP VIETQR (`IN-SYSTEM SERVICE PACKAGES & VIETQR`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Module trong Report 5.1:** `Module 10` (`M-12` / `FE-14`)
* **Dịch vụ & Validator kiểm thử:** `PaymentService`, `ServicePackageService`, `SubscriptionEntitlementService`, `AdminReconciliationService`, `GeoMatchingService`, `UpsertServicePackageRequestValidator`, `ResolvePaymentAnomalyRequestValidator`, `AuditLogImmutabilityInterceptor`
* **Use Cases bao phủ:** `U-16`, `R-07`, `A-10`, `A-11`
* **Tệp mã kiểm thử tự động:** [`ServicePackagePaymentTests.cs`](../../../src/Ecolink.Backend/Ecolink.UnitTests/Module12_ServicePackages/ServicePackagePaymentTests.cs)
* **Ngày thực thi gần nhất:** `2026-11-17`
* **Kết quả thực thi:** **`22 / 22 Passed` (`100%` Pass Rate — `10 Normal`, `8 Abnormal`, `4 Boundary`)**

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ ĐƠN VỊ (SUMMARY TABLE)

| Code Module | `PaymentService`, `ServicePackageService`, `SubscriptionEntitlementService`, `AdminReconciliationService`, `ServicePackageValidators` (`U-16`, `R-07`, `A-10`, `A-11`) | | |
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

| Test Case ID | Tên Method Kiểm thử (`ServicePackagePaymentTests`) | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| **UTC001** | `UTC001_CreatePayInOrder_ValidPremiumPackage_AndExactWebhookActivates30DayAdFree_Normal` | Tài khoản Citizen `Active`, DB có 3 gói chuẩn `BASIC/PREMIUM/PRO` | `PackageCode="PREMIUM"`, sau đó `SimulateTransfer("Exact")` | `Amount=59000, Duration=30, Status="PAID"`, `ExpiresAt = +15m`, `CurrentTier="Premium", IsAdFree=true` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-PAY-01`, `U-16` |
| **UTC002** | `UTC002_EarlyRenewalSamePackage_StacksRemainingDaysFromOldExpiry_Normal` | Citizen đã có thuê bao `PREMIUM` còn `10` ngày hiệu lực | Mua mới `PREMIUM` (`30d`) + `SimulateTransfer("Exact")` | `max(ExpiresAt) ≈ initialExpiry + 30 days` (`~40 ngày`) | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-PAY-04` (Cộng dồn) |
| **UTC003** | `UTC003_UpgradeFromActivePremiumToPro_VerifiedRecycler_ActivatesProImmediately_Normal` | Recycler `Verified` KYB đang còn hiệu lực `PREMIUM` | `CreatePayInOrderAsync("PRO")` + `SimulateTransfer("Exact")` | `PRO` (`1,000,000đ / 365d`) chạy ngay tại `utcNow`, `HasVerifiedBlueBadge=true, MapPriorityBoost=100` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-PAY-04`, `R-07` |
| **UTC004** | `UTC004_ProSubscriptionEntitlement_GrantsVerifiedBadgeAndFeaturedGeoSort_Normal` | 2 cơ sở `Online` bán `"plastic"`: `Basic` gần `1km`, `PRO` xa `3km` | `GeoMatchingService.FindNearestPartnersAsync(21.0124, 105.5253)` | Cơ sở `PRO` đứng vị trí `[0]` (`IsFeatured=true, HasVerifiedBadge=true`) trước cơ sở `Basic` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `R-07`, `U-05` Geo Sort |
| **UTC005** | `UTC005_DuplicateWebhookDelivery_ActivatesSubscriptionExactlyOnce_Normal` | Đơn `PREMIUM` `Pending` | Gửi 3 lần cùng `VietQrWebhookPayload` (`FT_IDEMPOTENT_999`) | Lần 1 `"PAID"`, lần 2 & 3 `"ALREADY_PROCESSED"`, chỉ tạo `1` `UserSubscription` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-PAY-02` Idempotency |
| **UTC006** | `UTC006_GetAvailablePackagesAndCancelPendingOrder_MarksOrderCancelled_Normal` | Recycler `Verified` KYB tạo đơn `PRO` đang `Pending` | `GetAvailablePackagesAsync`, `CancelPendingOrderAsync`, `GetUserOrdersAsync("Cancelled")` | Catalog trả `BASIC, PREMIUM, PRO` (`PRO.IsEligible=true`); hủy đơn trả `Status="CANCELLED"` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `STT 116, 117, 120, 122` |
| **UTC007** | `UTC007_AdminCreateAndUpdateServicePackage_GrandfathersExistingSubscriptions_Normal` | Citizen đã mua `PREMIUM` giá cũ `59,000đ / 30d` | `CreatePackageAsync("RECYCLER_VIP")` & `UpdatePackageAsync(PremiumId, 79000đ, 45d)` | Đơn cũ giữ `59,000đ / 30d`; đơn mới áp `79,000đ / 45d`; ghi `2` log `CREATE/UPDATE_SERVICE_PACKAGE` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-PAY-05`, `A-10` |
| **UTC008** | `UTC008_AdminResolveUnderpaidOrderWithActivateOrder_ActivatesPackageAndLogsAudit_Normal` | Đơn `PREMIUM` bị `Underpaid` (`40,000đ / 59,000đ`) | `GetPaymentLedgerAsync(OnlyAnomalies=true)` + `ResolvePaymentAnomalyAsync("ApproveAndActivate")` | `OrderStatus="Paid", ReconciliationStatus="Resolved"`, kích hoạt `Premium`, ghi `AuditLog` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-PAY-06`, `A-11` |
| **UTC009** | `UTC009_AdminManualMatchOrphanWebhook_MatchesPendingOrderAndActivatesSubscription_Normal` | Có 1 đơn `PRO` `Pending` (`1tr`) và 1 webhook mồ côi `WH-...` (`1tr`) | `ResolvePaymentAnomalyAsync(whCode, Action="ManualMatchOrder", TargetOrderCode)` | `OrderStatus="Paid", ReconciliationStatus="Resolved"`, kích hoạt gói `Pro` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-PAY-06` Orphan Match |
| **UTC010** | `UTC010_AdminExportReconciliationReport_CsvAndXlsx_MasksPiiAndProtectsAuditLog_Normal` | Có giao dịch của User `nguyen.secret.pii@gmail.com`, SĐT `0987654321` | `ExportReconciliationReportAsync("csv"/"xlsx")` + thử xóa `AuditLog` | CSV che `"ng***@gmail.com"`, `"098****321"`; XLSX chứa `"xl/worksheets/sheet1.xml"`; xóa log ném `InvalidOperationException` | Normal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-ADM-05`, `BR-ADM-06` |
| **UTC011** | `UTC011_CreatePayInOrder_TamperedClientAmount_ThrowsBrPay01_Abnormal` | Tài khoản Citizen `Active` | `CreatePayInOrderAsync` (`PackageCode="PREMIUM", Amount=1000m`) | Ném `BusinessRuleException` (`RuleCode="BR-PAY-01"`) | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Chống giả mạo giá |
| **UTC012** | `UTC012_OtherCitizenViewOrCancelOrder_ThrowsForbiddenIdor_Abnormal` | `User A` tạo đơn `PREMIUM`, `User B` khác sở hữu | `User B` gọi `GetUserOrdersAsync`, `GetOrderForUserAsync`, `CancelPendingOrderAsync` | Danh sách của `User B` rỗng; xem và hủy đơn của A ném `ForbiddenAccessException` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Chống IDOR (`BR-AUTH-08`) |
| **UTC013** | `UTC013_PurchaseFreeBasicOrCitizenBuyPro_Rejected_Abnormal` | Tài khoản `Role = User` (`Citizen`) | Gọi `CreatePayInOrderAsync` với `"BASIC"` và `"PRO"` | Mua `BASIC` ném `BusinessRuleException ("BR-PAY-01")`; mua `PRO` ném `ForbiddenAccessException` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | `BR-PAY-01`, `BR-PAY-03` |
| **UTC014** | `UTC014_UnverifiedKybRecyclerBuyPro_ThrowsMsgPay06_Abnormal` | Recycler có hồ sơ KYB `VerificationStatus = Pending` | `GetAvailablePackagesAsync` & `CreatePayInOrderAsync("PRO")` | `PRO.IsEligible = false`; tạo đơn ném `BusinessRuleException ("MSG-PAY-06")` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Khóa KYB gói PRO |
| **UTC015** | `UTC015_UnderpaidWebhook_KeepsPendingAndFlagsAnomalyWithoutActivation_Abnormal` | Đơn `PREMIUM` (`59,000đ`) đang `Pending` | `SimulateTransferAsync("Underpaid", 50_000m)` | `Status="PENDING", ReconciliationStatus="Underpaid"`, không kích hoạt gói (`CurrentTier="Basic"`) | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Kịch bản `Underpaid` |
| **UTC016** | `UTC016_InvalidWebhookSignature_RejectedWithMsgPay05AndLogsAudit_Abnormal` | Cấu hình `VietQr:WebhookSecret = "ecolink-super-secret"` | `ProcessWebhookAsync(Signature="BAD_SIGNATURE", headerSecret="wrong-header")` | Trả về `Success=false, Status="INVALID_SIGNATURE"`, ghi log `InvalidSignature` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Bảo mật chữ ký (`MSG-PAY-05`) |
| **UTC017** | `UTC017_DisableBasicPackageAndCancelPaidOrder_ThrowExpectedRules_Abnormal` | Hệ thống có gói `BASIC` & đơn `PREMIUM` đã thanh toán `Paid` | `TogglePackageStatusAsync(BasicId, false)` & `CancelPendingOrderAsync(paidOrder)` | Tắt `BASIC` ném `BusinessRuleException ("BR-PAY-05")`; hủy đơn `Paid` ném `ConflictException ("BR-PAY-01")` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Khóa gói nền & đơn đã thu tiền |
| **UTC018** | `UTC018_AdminDoubleResolveOrUnderfundedManualMatch_Rejected_Abnormal` | Đơn `PRO` (`1tr`) và webhook mồ côi `500k` | `ManualMatchOrder` thiếu tiền, `CloseAnomaly` rồi thử `MarkRefunded` lần 2 | Khớp thiếu tiền ném `BusinessRuleException ("BR-PAY-06")`; xử lý lần 2 ném `ConflictException ("BR-PAY-06")` | Abnormal | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Chống đối soát sai / lặp |
| **UTC019** | `UTC019_OverpaidWebhook_ActivatesPackageImmediatelyAndFlagsOverpaid_Boundary` | Đơn `PREMIUM` (`59,000đ`) trong hạn QR | `SimulateTransferAsync("Overpaid", 65_000m)` | `Status="PAID", ReconciliationStatus="Overpaid", PaidAmount=65000`, kích hoạt ngay `Premium` | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Biên dư tiền (`Overpaid`) |
| **UTC020** | `UTC020_LateTransferOnExpiredQr_MarksExpiredAndFlagsLateTransfer_Boundary` | Đơn `PRO` (`1tr`) vừa quá hạn QR 15 phút | `SimulateTransferAsync("LateTransfer")` | `Status="EXPIRED", ReconciliationStatus="LateTransfer"`, ghi log `LateTransfer` không tự kích hoạt | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Biên quá hạn 15 phút |
| **UTC021** | `UTC021_EntitlementCacheHitAndInvalidation_UpdatesImmediatelyAfterActivation_Boundary` | Citizen lần đầu đọc cache (`Basic`), sau đó thanh toán `PREMIUM` | `GetUserEntitlementsAsync`, `SimulateTransferAsync`, `GetBatchEntitlementsAsync([])` | Cache ban đầu trả `Basic`; sau `SimulateTransfer` tự xóa cache trả `Premium`; tập ID rỗng trả từ điển rỗng | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | Vòng đời `IMemoryCache` |
| **UTC022** | `UTC022_ServicePackageValidators_EnforceBoundaryPricesDurationsAndResolutionNote_Boundary` | Hai bộ `UpsertServicePackageRequestValidator` & `ResolvePaymentAnomalyRequestValidator` | Biên giá `2,000đ` vs `1,500đ`; chu kỳ `1`/`730` vs `0`/`731`; note `10` vs `9` ký tự; thiếu `TargetOrderCode`/`BankRef` | Giá `< 2000`, chu kỳ ngoài `[1..730]`, note `< 10` ký tự, hoặc thiếu trường bắt buộc trả `IsValid=false` | Boundary | Passed | 2026-11-17 | Hoàng Thắng Nguyễn | FluentValidation Boundaries |
