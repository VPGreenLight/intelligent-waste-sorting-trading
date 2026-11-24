# BÁO CÁO SỰ CỐ LỆCH HỢP ĐỒNG KIỂM THỬ `AiCategoryNormalizerTests` GIỮA 6 LỚP CŨ VÀ 10 LỚP YOLO26 (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-TST-13`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Pure Logic Unit Tests & AI Category Normalization (`M-03`, `M-13`, `AIS`)  
**Các tệp ảnh hưởng chính:**
* **Production Code:** [`AiCategoryNormalizer.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AiCategoryNormalizer.cs#L15-L110) (`YoloClassOrder`, `LabelAliases`, `DefaultCategories`), [`model_manager.py`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.AiService/model_manager.py#L49-L73) (`CLASS_NAMES`)
* **Unit Test:** [`PureLogicTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/PureLogic/PureLogicTests.cs#L22-L85) (`AiCategoryNormalizerTests`)  
**Thời điểm ghi nhận:** 23/11/2026 (Phát hiện ngay sau khi merge nhánh `origin/add-unit-test` tại commit `27f3e9a9`, khắc phục tại commit `87b74b79`)  
**Mức độ nghiêm trọng:** Trung bình (Medium – Làm 14 test cases trong `PureLogicTests.cs` bị `Failed` khi chạy `Ecolink.UnitTests` do kỳ vọng cấu trúc 6 lớp cũ trước thời điểm nâng cấp YOLO26)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Ngay sau khi hợp nhất nhánh `origin/add-unit-test` (`27f3e9a9`) vào `develop`:

1. Kết quả chạy `dotnet test Ecolink.UnitTests.csproj` ghi nhận **`14` ca kiểm thử thất bại hàng loạt trong lớp `AiCategoryNormalizerTests`** (`1,092 passed / 15 failed`, gồm 14 ca ở `PureLogicTests.cs` và 1 ca ở `GovernanceChatValidatorTests.cs` đã tách riêng tại `INC-2026-VAL-14`):
   * **2 ca thất bại tại `NormalizeCode_MapsAliasesToCanonical`:** Mong đợi `"battery"` và `"ewaste_disposal"` quy về `"trash"`, nhưng hàm thực tế trả về `"battery"`.
   * **11 ca thất bại tại `GetYoloClassIndex_Matches_YoloOrder`:** Mong đợi `cardboard=0, glass=1, metal=2, paper=3, plastic=4, trash=5` (kèm alias `pet->4`, `carton->0`, fallback unknown/`null`/`""->5`), nhưng code thực tế trả chỉ mục theo thứ tự 10 lớp (`cardboard=2, glass=4, metal=5, paper=6, plastic=7, trash=9`).
   * **1 ca thất bại tại `YoloClassOrder_IsStable_ContractWithAiService`:** Mong đợi mảng 6 phần tử cũ thay vì mảng 10 phần tử chuẩn mới.

---

## 2. CHUỖI TRUY VẾT SỰ CỐ (BRANCH DRIFT TRACE)

```text
[1] Nhánh `origin/add-unit-test` được tách trước thời điểm merge PR nâng cấp AI (`32ea47f0`, `b8f7bf97`)
      └─► Khi đó hệ thống còn dùng danh mục 6 lớp: ["cardboard", "glass", "metal", "paper", "plastic", "trash"]
      │
      ▼
[2] Trên `develop`, hệ thống AI được nâng cấp lên YOLO26 Two-Stage (10 danh mục chuẩn):
      ├─► `model_manager.py`: CLASS_NAMES mở rộng lên 10 lớp theo thứ tự alphabet (`0..9`)
      │     ["battery", "biological", "cardboard", "clothes", "glass", "metal", "paper", "plastic", "shoes", "trash"]
      └─► `AiCategoryNormalizer.cs`: cập nhật `YoloClassOrder` (10 phần tử) & `LabelAliases`
            ("battery", "ewaste_disposal" -> "battery"; "organic", "huu_co" -> "biological"; "quan_ao" -> "clothes"; "giay_dep" -> "shoes")
      │
      ▼
[3] Merge `origin/add-unit-test` (`27f3e9a9`) vào `develop` không báo Git Conflict
      └─► Vì `PureLogicTests.cs` là file mới hoàn toàn, Git hợp nhất thẳng vào mà không phát hiện lệch hợp đồng dữ liệu
      │
      ▼
[4] Chạy `Ecolink.UnitTests`: toàn bộ 14 assertions dựa trên giả định 6 lớp (index 0..5) đều FAIL!
```

---

## 3. PHÂN TÍCH CHI TIẾT NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

1. **Độ trễ đồng bộ giữa nhánh viết test (`add-unit-test`) và nhánh nâng cấp AI (`develop`):** Bộ `PureLogicTests.cs` được xây dựng bám theo đặc tả 6 lớp (`cardboard..trash`, `"battery"` / `"ewaste_disposal"` gom vào `"trash"`). Trong thời gian chờ merge, `develop` đã tiếp nhận chuỗi commit nâng cấp YOLO26 10 lớp (`32ea47f0`, `b8f7bf97`).
2. **Không phát sinh Git Merge Conflict:** Do `PureLogicTests.cs` là tệp thêm mới từ `origin/add-unit-test`, quá trình git merge không hiển thị cảnh báo xung đột tệp với [`AiCategoryNormalizer.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AiCategoryNormalizer.cs), khiến độ lệch hợp đồng chỉ lộ diện khi thực thi xUnit.

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

| Thành phần | Mức độ ảnh hưởng | Mô tả tác động thực tế |
| :--- | :---: | :--- |
| **Mã nguồn AI & Backend (`AiCategoryNormalizer`, `model_manager.py`)** | Không lỗi | Hợp đồng 10 danh mục YOLO26 trên Production đang hoạt động chính xác. |
| **Bộ kiểm thử `Ecolink.UnitTests`** | Trung bình | Gây `14` ca `Failed` ngay sau merge `add-unit-test`, làm đỏ toàn bộ pipeline test dù mã nghiệp vụ đúng. |

---

## 5. GIẢI PHÁP KHẮC PHỤC (REMEDIATION IMPLEMENTED — COMMIT `87b74b79`)

Đã cập nhật [`PureLogicTests.cs#L24-L85`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/PureLogic/PureLogicTests.cs#L24-L85) khớp `100%` với hợp đồng 10 danh mục YOLO26 hiện hành:

1. **`YoloClassOrder_IsStable_ContractWithAiService`:** Kiểm chứng đúng mảng 10 phần tử:
   `["battery", "biological", "cardboard", "clothes", "glass", "metal", "paper", "plastic", "shoes", "trash"]`.
2. **`GetYoloClassIndex_Matches_YoloOrder`:** Cập nhật đầy đủ `[InlineData]` cho cả 10 chỉ mục `0..9`, các alias (`"pet" -> 7`, `"carton" -> 2`), và giá trị không xác định / `null` / `""` rơi về chỉ mục `"trash"` là `9`.
3. **`NormalizeCode_MapsAliasesToCanonical`:** Cập nhật `"battery" -> "battery"`, `"ewaste_disposal" -> "battery"`, và bổ sung đầy đủ ánh xạ mới `"huu_co" -> "biological"`, `"quan_ao" -> "clothes"`, `"giay_dep" -> "shoes"`.
4. **Xác minh sau sửa:** Toàn bộ `AiCategoryNormalizerTests` vượt qua 100%, góp phần đưa tổng kết `Ecolink.UnitTests` lên `1,118 / 1,118 Passed`.

---

## 6. BÀI HỌC VÀ BIỆN PHÁP PHÒNG NGỪA (LESSONS LEARNED & PREVENTIONS)

1. **Chạy full Unit Test ngay sau mọi thao tác Merge nhánh dài ngày:** Git báo *Clean Merge* không đồng nghĩa với đồng bộ hợp đồng nghiệp vụ; bắt buộc chạy `dotnet test` ngay sau khi merge bất kỳ nhánh test nào vào `develop`.
2. **Giữ một nguồn sự thật (Single Source of Truth) cho Contract AI Labels:** Đối chiếu đồng thời `AiCategoryNormalizer.YoloClassOrder`, `CLASS_NAMES` trong `model_manager.py` và các tài liệu API/Test specification mỗi khi bổ sung hoặc đổi thứ tự nhãn mô hình YOLO.
