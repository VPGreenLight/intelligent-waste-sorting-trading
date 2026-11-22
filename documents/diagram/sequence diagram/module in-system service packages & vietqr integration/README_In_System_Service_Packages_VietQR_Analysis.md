# PHÂN TÍCH LUỒNG THIẾT KẾ TUẦN TỰ – PHÂN HỆ M-12 / FE-14: GÓI DỊCH VỤ NỘI BỘ & TÍCH HỢP VIETQR

Tài liệu này tổng hợp 4 biểu đồ tuần tự chi tiết cho phân hệ **Gói dịch vụ nội bộ & Tích hợp VietQR (`M-12` / `FE-14`)**, bao phủ 4 Use Cases chính thức (`U-16`, `R-07`, `A-10`, `A-11`) và toàn bộ 15 REST APIs (`STT 115` – `STT 129`).

---

## 1. Danh mục Biểu đồ Tuần tự

1. **[01_u16_r07_browse_tiers_create_dynamic_vietqr_kyb_guard_and_cancel_order.mmd](./01_u16_r07_browse_tiers_create_dynamic_vietqr_kyb_guard_and_cancel_order.mmd)**
   * **Use Cases:** `U-16` (Citizen), `R-07` (Recycler) | **APIs:** `STT 115, 116, 120, 122`
   * **Trọng tâm kỹ thuật:** Lấy danh sách 3 gói (`BASIC`, `PREMIUM`, `PRO`), đánh giá quyền đăng ký `EvaluateEligibility`, chốt giá trị từ DB (`BR-PAY-01`), chặn gói `BASIC` & yêu cầu `Verified` KYB cho `PRO` (`BR-PAY-03`), tạo VietQR động qua PayOS SDK hoặc fallback NAPAS 24/7 EMVCo (`15 phút`), và cho phép hủy đơn `Pending` (`409 Conflict` nếu đã `Paid`).

2. **[02_u16_r07_webhook_signature_idempotency_duration_stacking_and_five_anomalies.mmd](./02_u16_r07_webhook_signature_idempotency_duration_stacking_and_five_anomalies.mmd)**
   * **Use Cases:** `U-16`, `R-07` | **APIs:** `STT 118, 119, 121`
   * **Trọng tâm kỹ thuật:** Kiểm tra chữ ký HMAC / Header Secret (`VerifyWebhookSignature`), ghi nhận audit nếu sai (`MSG-PAY-05`), tính khóa `IdempotencyKey` (`SHA256`), xử lý idempotent `DuplicateIgnored` (`BR-PAY-02`), phân luồng 5 trạng thái bất thường (`UnmatchedReference`, `LateTransfer`, `Underpaid`, `Overpaid`, `Matched`), kích hoạt thuê bao theo nguyên tắc **Cộng dồn nối tiếp vs Nâng bậc tức thì** (`BR-PAY-04`), và xóa cache `IMemoryCache` tức thì.

3. **[03_a10_r07_admin_package_pricing_grandfathering_and_pro_map_priority_boost.mmd](./03_a10_r07_admin_package_pricing_grandfathering_and_pro_map_priority_boost.mmd)**
   * **Use Cases:** `A-10` (Admin), `R-07` (Map Priority) | **APIs:** `STT 123, 124, 125, 126` & `GeoMatchingService`
   * **Trọng tâm kỹ thuật:** Quản trị viên tạo gói mới, cập nhật giá với cơ chế **Grandfathering** bảo toàn snapshot cho thuê bao cũ, khóa thao tác sửa giá/ngừng bán gói mặc định `BASIC` (`BR-PAY-05`), ghi `AuditLog`, và cơ chế `GeoMatchingService` gọi `ISubscriptionEntitlementService.GetBatchEntitlementsAsync` để gắn `hasVerifiedBadge` + `isFeatured` đưa cơ sở gói `PRO` lên đầu danh sách tìm kiếm bản đồ.

4. **[04_a11_admin_reconciliation_ledger_manual_match_and_anonymized_report_export.mmd](./04_a11_admin_reconciliation_ledger_manual_match_and_anonymized_report_export.mmd)**
   * **Use Case:** `A-11` (Admin) | **APIs:** `STT 127, 128, 129`
   * **Trọng tâm kỹ thuật:** Tổng hợp sổ cái Unified Ledger kết hợp giữa `ServiceOrder` và webhook mồ côi (`WH-...`), đối soát thủ công một lần (`ApproveAndActivate`, `ManualMatchOrder`, `MarkRefunded`, `MarkExcessRefunded`, `CloseAnomaly`) kèm khóa chống đối soát lặp (`409 Conflict` theo `BR-PAY-06`), và xuất file CSV/XLSX ẩn danh PII email/SĐT qua `PiiMaskingHelper` (`BR-ADM-06`) kèm Header `X-EcoLink-Message-Code: MSG-ADM-02`.

---

## 2. Tài liệu liên kết đồng bộ
* **Hợp đồng API (`15` Endpoints):** [10_module_in_system_service_packages_vietqr_api.md](../../../api%20contact/10_module_in_system_service_packages_vietqr_api.md)
* **Biểu đồ Lớp (Clean Architecture):** [module_in_system_service_packages_vietqr_clean_architecture_class_diagram_ultra_compact.mmd](../../class%20diagram/module%20in-system%20service%20packages%20%26%20vietqr%20integration/module_in_system_service_packages_vietqr_clean_architecture_class_diagram_ultra_compact.mmd)
* **Đặc tả Unit Test & Integration Test:** [10_unit_test_in_system_service_packages_vietqr.md](../../../test%20document/unit%20test/10_unit_test_in_system_service_packages_vietqr.md) & [10_integration_test_in_system_service_packages_vietqr.md](../../../test%20document/integration%20test/10_integration_test_in_system_service_packages_vietqr.md)
