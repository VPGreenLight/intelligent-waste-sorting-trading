# BÁO CÁO SỰ CỐ FLAKY UNIT TEST `UTC004` DO CỜ TĨNH `_demoSeedChecked` TRONG `GeoMatchingService` (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-TST-12`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Location-Based Recycler Discovery (`M-05`) & Service Packages Geo Boost (`M-12`)  
**Các tệp ảnh hưởng chính:**
* **Service:** [`src/Ecolink.Backend/Ecolink.Infrastructure/Services/GeoMatchingService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GeoMatchingService.cs#L21-L22) (`private static bool _demoSeedChecked`, `EnsureDemoRecyclerPricesSeededAsync`)
* **Unit Test:** [`src/Ecolink.Backend/Ecolink.UnitTests/Module12_ServicePackages/ServicePackagePaymentTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Module12_ServicePackages/ServicePackagePaymentTests.cs#L252-L317) (`UTC004_ProSubscriptionEntitlement_GrantsVerifiedBadgeAndFeaturedGeoSort_Normal`)  
**Thời điểm ghi nhận:** 23/11/2026 (Phát hiện khi chạy cô lập class/method `ServicePackagePaymentTests` so với chạy toàn bộ `Ecolink.UnitTests`)  
**Mức độ nghiêm trọng:** Trung bình – Cao (High Test Reliability – Tạo hiện tượng *Flaky Test* phụ thuộc thứ tự chạy trong cùng `AppDomain`: chạy cả suite thì `Passed`, chạy riêng lẻ một bài test lại `Failed`)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Ca kiểm thử đơn vị [`UTC004_ProSubscriptionEntitlement_GrantsVerifiedBadgeAndFeaturedGeoSort_Normal`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Module12_ServicePackages/ServicePackagePaymentTests.cs#L252-L317) có mục tiêu tạo `2` cơ sở thu gom (1 `Basic` ở gần, 1 `Pro` ở xa hơn trong bán kính `10 km`) để xác minh gói `PRO` đẩy cơ sở ở xa lên vị trí đầu tiên trên bản đồ tìm kiếm:

1. **Khi chạy toàn bộ `Ecolink.UnitTests`:** Ca test `UTC004` trả về kết quả **`Passed`** (`partners.Count == 2`).
2. **Khi chạy cô lập riêng `ServicePackagePaymentTests` (ví dụ qua `--filter FullyQualifiedName~UTC004`):** Ca test **`Failed`** tại dòng `partners.Should().HaveCount(2)` với lỗi thực tế nhận được **`6` cơ sở** (vì xuất hiện thêm `4` cơ sở demo tại khu vực Hòa Lạc dù DbContext InMemory mới khởi tạo chỉ thêm đúng `2` cơ sở).

---

## 2. CHUỖI TRUY VẾT SỰ CỐ (TEST-ORDER DEPENDENCY TRACE)

```text
[Trường hợp A: Chạy toàn bộ suite Ecolink.UnitTests]
  1. `GeoMatchingUnitTests` (Module 05) chạy trước -> khởi tạo GeoMatchingService
  2. `EnsureDemoRecyclerPricesSeededAsync()` chạy lần đầu -> tự động chèn 4 cơ sở demo Hòa Lạc vào DB riêng của Module 05
  3. Đặt biến `private static bool _demoSeedChecked = true` (chia sẻ cấp Process/AppDomain!)
  4. Đến lượt `ServicePackagePaymentTests.UTC004` tạo DB InMemory mới hoàn toàn:
       -> `if (_db == null || _demoSeedChecked) return;` -> BỎ QUA không seed 4 cơ sở Hòa Lạc!
       -> Kết quả truy vấn quanh Hòa Lạc chỉ có đúng 2 cơ sở vừa tạo -> PASS!

[Trường hợp B: Chạy cô lập ServicePackagePaymentTests / UTC004]
  1. `GeoMatchingUnitTests` KHÔNG chạy trước -> biến tĩnh `_demoSeedChecked` vẫn là `false`!
  2. `UTC004` tạo DB InMemory mới và gọi `geoSvc.FindNearestPartnersAsync(21.0124, 105.5253, "plastic", 10.0)`
  3. `EnsureDemoRecyclerPricesSeededAsync()` thấy `_demoSeedChecked == false` -> thực thi seeding!
  4. DB InMemory riêng của `UTC004` bị chèn thêm 4 cơ sở mẫu quanh Hòa Lạc (Lat ~20.99..21.03, Lon ~105.51..105.55)
  5. Tổng cộng có 2 + 4 = 6 cơ sở trong bán kính 10 km -> `partners.Should().HaveCount(2)` THẤT BẠI (FAIL)!
```

---

## 3. PHÂN TÍCH CHI TIẾT NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

1. **Trạng thái tĩnh toàn cục rò rỉ giữa các Unit Test độc lập:** Trong [`GeoMatchingService.cs#L22`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GeoMatchingService.cs#L22), biến `private static bool _demoSeedChecked` nằm ở cấp lớp (`static`), nghĩa là nó tồn tại suốt vòng đời tiến trình xUnit và **bị chia sẻ qua tất cả các `ApplicationDbContext` InMemory** vốn được thiết kế để cô lập từng test case qua `TestDbContextFactory.Create()`.
2. **Va chạm vùng địa lý với dữ liệu demo tự động (Geo Overlap):** `UTC004` trước đây đặt 2 vựa test và tâm tìm kiếm tại khu vực **Hòa Lạc (`21.0124, 105.5253`)** — trùng đúng phạm vi `10 km` của 4 cơ sở demo do `EnsureDemoRecyclerPricesSeededAsync()` tự chèn vào cơ sở dữ liệu khi `_demoSeedChecked == false`.
3. **Hiệu ứng che khuất lỗi khi chạy gộp:** Vì các test của Module 05 thường chạy trước Module 12 theo thứ tự tên namespace/thời điểm nạp assembly, lỗi phụ thuộc thứ tự này không bộc lộ khi chạy cả suite mà chỉ nổ khi lọc chạy đơn lẻ trên IDE Test Explorer hoặc CI shard.

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

| Thành phần | Mức độ ảnh hưởng | Mô tả tác động thực tế |
| :--- | :---: | :--- |
| **Độ tin cậy bộ kiểm thử (`Ecolink.UnitTests`)** | Cao | Gây hiện tượng test chạy lúc Pass lúc Fail (*Heisenbug / Flaky test*) tùy bộ lọc chạy hoặc chiến lược song song hóa của xUnit. |
| **Logic xếp hạng gói PRO trên Production** | Không ảnh hưởng | Trên PostgreSQL thực tế, 4 cơ sở demo Hòa Lạc là dữ liệu khởi tạo chủ đích một lần ở lần truy vấn đầu tiên. |

---

## 5. GIẢI PHÁP KHẮC PHỤC (REMEDIATION IMPLEMENTED)

Đã xử lý trực tiếp tại [`ServicePackagePaymentTests.cs#L274-L310`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Module12_ServicePackages/ServicePackagePaymentTests.cs#L274-L310) nhằm bảo đảm **100% tính cô lập vị trí địa lý** cho `UTC004` trong mọi chế độ chạy:

1. **Di chuyển tọa độ test của `UTC004` sang khu vực Đà Nẵng (cách Hòa Lạc > 600 km):**
   * **Vựa gần (`Basic`):** `Latitude = 16.0620m`, `Longitude = 108.2022m`
   * **Vựa xa hơn (`PRO`):** `Latitude = 16.0800m`, `Longitude = 108.2022m`
   * **Tâm tìm kiếm (`FindNearestPartnersAsync`):** `(16.0544, 108.2022)`, bán kính `10.0 km`
2. **Kết quả:** Ngay cả khi `_demoSeedChecked` còn `false` và `EnsureDemoRecyclerPricesSeededAsync()` chèn các vựa demo tại Hòa Lạc/Hà Nội vào InMemory DB, truy vấn bán kính `10 km` quanh tâm `(16.0544, 108.2022)` tại Đà Nẵng luôn lọc ra **chính xác duy nhất 2 cơ sở của bài test**, trong đó cơ sở `PRO` ở `16.0800` đứng hạng 1 (`IsFeatured = true`, `HasVerifiedBadge = true`) dù `DistanceKm` lớn hơn cơ sở `Basic` ở `16.0620`.
3. **Xác minh:** Chạy cô lập `--filter FullyQualifiedName~UTC004` và chạy toàn bộ `Ecolink.UnitTests` (`1,118 / 1,118 Passed`) đều đạt `100% Passed`.

---

## 6. BÀI HỌC VÀ BIỆN PHÁP PHÒNG NGỪA (LESSONS LEARNED & PREVENTIONS)

1. **Tránh trạng thái `static` khả biến (Mutable Static State) trong Service khi dùng chung với InMemory DB:** Với các cờ khởi tạo một lần như `_demoSeedChecked`, cân nhắc tiêm cấu hình tắt auto-seed demo trong môi trường test hoặc kiểm tra theo từng instance/DB name thay vì biến `static` cấp process.
2. **Cô lập tọa độ kiểm thử GPS khỏi vùng seed mặc định:** Các bài unit test về GeoMatching không trực tiếp kiểm thử dữ liệu mẫu Hòa Lạc nên sử dụng cụm tọa độ độc lập (như Đà Nẵng / TP.HCM) để không bao giờ bị nhiễu bởi dữ liệu mẫu.
3. **Kiểm chứng test mới theo chế độ chạy đơn lẻ:** Sau khi viết unit test mới, luôn thử chạy riêng từng method/class (`--filter`) bên cạnh chạy toàn bộ suite để phát hiện sớm phụ thuộc thứ tự.
