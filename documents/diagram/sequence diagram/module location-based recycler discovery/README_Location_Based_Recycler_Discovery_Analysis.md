# PHÂN TÍCH SƠ ĐỒ THIẾT KẾ MODULE: LOCATION-BASED RECYCLER DISCOVERY (`U-05`)

**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** `MTH` – Tìm kiếm & Kết nối Cơ sở Thu gom theo Vị trí Địa lý  
**Use Case trọng tâm:** `U-05: Find Nearby Recyclers (GPS)`  

---

## 1. Danh mục Sơ đồ Thiết kế trong Module

| STT | Loại sơ đồ | Đường dẫn File | Mô tả nội dung |
|:---:|:---:|:---|:---|
| **01** | **Class Diagram** | [`module_location_based_recycler_discovery_clean_architecture_class_diagram_ultra_compact.mmd`](../../class%20diagram/module%20location-based%20recycler%20discovery/module_location_based_recycler_discovery_clean_architecture_class_diagram_ultra_compact.mmd) | Sơ đồ lớp kiến trúc 4 tầng Clean Architecture (`RecyclersDiscoveryController`, `TradeController`, `IGeoMatchingService`, `NearbyRecyclerFilterValidator`, `GeoMatchingService`, `WasteRuleEngineService`, `RecyclerProfile`, `RecyclerPrice`, `WasteCategory`). |
| **02** | **Sequence Diagram 01** | [`01_u05_search_nearby_recyclers_and_fallback_suggestion.mmd`](./01_u05_search_nearby_recyclers_and_fallback_suggestion.mmd) | Luồng tìm kiếm cơ sở thu gom trong bán kính GPS (`GET /api/v1/recyclers/nearby`), kiểm tra tọa độ (`MSG-MTH-02`), lọc kết hợp `Verified` + `Online` + `Active Prices`, sắp xếp khoảng cách (`BR-MTH-04`) và gợi ý dự phòng từ Rule Engine khi `TotalFound == 0` (`MSG-MTH-01`). |
| **03** | **Sequence Diagram 02** | [`02_u05_view_recycler_public_detail_and_active_price_sheet.mmd`](./02_u05_view_recycler_public_detail_and_active_price_sheet.mmd) | Luồng xem chi tiết hồ sơ công khai, giờ mở cửa và bảng giá niêm yết đang áp dụng của một cơ sở trên bản đồ (`GET /api/v1/recyclers/{recyclerId}`). |

---

## 2. Bảng Đối chiếu Quy tắc Nghiệp vụ (`BR-*`) & Mã Thông báo (`MSG-*`)

| Mã Quy tắc / Thông báo | Nội dung Đặc tả | Thành phần Thực thi trong Sơ đồ |
|:---:|:---|:---|
| **`BR-MTH-01`** | Cơ sở thu gom (`Recycler`) bắt buộc phải được `Staff` phê duyệt (`VerificationStatus == Verified`) trước khi xuất hiện trên bản đồ tìm kiếm. | `GeoMatchingService.FindNearestPartnersAsync` & `GetRecyclerPublicDetailAsync` |
| **`BR-MTH-03`** | Hồ sơ cơ sở chỉ được công khai khi có ít nhất 1 loại phế liệu đang nhận thu mua (`PriceStatus.Active`) và vị trí hợp lệ. | Điều kiện lọc `r.Prices.Any(p => p.Status == PriceStatus.Active)` trong `GeoMatchingService` |
| **`BR-MTH-04`** | Kết quả tìm kiếm chỉ gồm các cơ sở đã duyệt, đang `Online`, trong bán kính `radiusKm` và nhận đúng loại rác, mặc định sắp xếp theo khoảng cách gần nhất (`DistanceKm ASC`). | `GeoMatchingService.FindNearestPartnersAsync` & `SearchNearbyRecyclersAsync` |
| **`BR-MTH-05`** | Tọa độ GPS (`lat`, `lng`) của người dân chỉ dùng tạm thời trong bộ nhớ để tính khoảng cách `DistanceKm`, không lưu xuống CSDL và không hiển thị cho `Recycler`. | `GeoMatchingService.CalculateDistanceKm` |
| **`BR-MTH-06`** | Cơ sở chuyển sang `Offline` (tại `R-02`) hoặc tài khoản bị đình chỉ/khóa (`User.Status != Active`) bị ẩn khỏi kết quả tìm kiếm bản đồ ngay lập tức. | Điều kiện `r.ActiveStatus == RecyclerActiveStatus.Online && r.User.Status == UserStatus.Active` |
| **`BR-MTH-07`** | Các dòng giá ở trạng thái tạm ngưng (`PriceStatus.Inactive`) không xuất hiện trong bảng giá công khai và bộ lọc tìm kiếm. | `Include(r => r.Prices.Where(p => p.Status == PriceStatus.Active))` |
| **`MSG-MTH-01`** | *"Không tìm thấy cơ sở thu mua rác gần bạn."* (Trả về kèm `DiscoveryFallbackSuggestionDto` khi `TotalFound == 0`). | `RecyclersDiscoveryController.SearchNearby` & `GeoMatchingService.SearchNearbyRecyclersAsync` |
| **`MSG-MTH-02`** | *"Cần quyền truy cập vị trí để tìm cơ sở thu mua gần bạn. Vui lòng bật trong phần cài đặt."* (Khi `lat = 0` hoặc `lng = 0`). | `NearbyRecyclerFilterValidator` & `RecyclersDiscoveryController.SearchNearby` |

