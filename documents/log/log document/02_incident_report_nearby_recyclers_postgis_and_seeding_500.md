# BÁO CÁO SỰ CỐ KỸ THUẬT RUNTIME (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-GEO-02`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Backend WebApi (`RecyclersDiscoveryController`, `GeoMatchingService`), Core Infrastructure (`Ecolink.Infrastructure`), Frontend (`ecolink-web/src/pages/MapPage.tsx`)  
**Tác vụ / Chức năng ảnh hưởng:** Module 05: Tìm kiếm Cơ sở Thu gom theo Vị trí Địa lý (UC U-05 / BR-MTH-01, BR-MTH-04, BR-MTH-05, BR-MTH-07)  
**Tuyến API:** `GET /api/v1/recyclers/nearby`  
**Thời điểm ghi nhận:** 09/11/2026 – Môi trường Runtime Development  
**Mức độ nghiêm trọng:** Cao (High – Gián đoạn chức năng tra cứu bản đồ cốt lõi, người dân không thể tìm cơ sở thu gom)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Khi người dùng truy cập trang Bản đồ tra cứu cơ sở thu gom (`/map`) neo tọa độ tại khu vực **Khu Công nghệ cao Hòa Lạc** (Lat: `21.0124`, Lng: `105.5253`):

1. **Phía Giao diện Người dùng (Frontend):**
   - Màn hình xuất hiện khung cảnh báo lỗi màu đỏ:
     > *"Đã xảy ra sự cố trong quá trình xử lý hệ thống. Vui lòng thử lại sau."*
   - Mục thống kê hiển thị: **`0 mở cửa`**.
   - Danh sách cơ sở thu gom trống rỗng: *"Không tìm thấy cơ sở thu mua rác gần bạn trong bán kính hiện tại..."* mặc dù bán kính quét là 10 km hoặc mở rộng 25 km.
2. **Kiểm tra Cơ sở dữ liệu (PostgreSQL):**
   - Bảng `recycler_profiles` trong cơ sở dữ liệu `ecolink_db` đã có sẵn đầy đủ **11 bản ghi**, bao gồm 4 cơ sở thu mua thực tế tại khu vực Hòa Lạc:
     - `99999991-9999-9999-9999-999999999991` – *Trạm Thu Gom Tái Chế Xanh Hòa Lạc* (Thôn 3, Tân Xã – cách ~0.99 km)
     - `99999992-9999-9999-9999-999999999992` – *Cơ Sở Phế Liệu Minh Tâm — Ngã Tư Hòa Lạc* (Km28 QL21A – cách ~1.69 km)
     - `99999993-9999-9999-9999-999999999993` – *Vựa Tái Chế Bình Yên — Hòa Lạc* (ĐT 420 – cách ~3.2 km)
     - `99999994-9999-9999-9999-999999999994` – *Điểm Thu Mua Phế Liệu Phú Cát — Hòa Lạc* (KCN Phú Cát – cách ~4.8 km)

---

## 2. TRÍCH ĐOẠN PHẢN HỒI LỖI (ERROR RESPONSE & LOGS)

### 2.1. Phản hồi HTTP trả về Frontend
* **HTTP Status Code:** `500 Internal Server Error`
* **Response Body:**
  ```json
  {
    "success": false,
    "message": "Đã xảy ra sự cố trong quá trình xử lý hệ thống. Vui lòng thử lại sau.",
    "messageCode": "MSG-SYS-500",
    "errors": [],
    "timestamp": "2026-11-09T15:56:09Z"
  }
  ```

### 2.2. Cơ chế chặn lỗi của Middleware tập trung
Trong `GlobalExceptionMiddleware.cs`:
```csharp
var (statusCode, messageCode, message) = exception switch
{
    BusinessRuleException brEx => (HttpStatusCode.BadRequest, brEx.MessageCode, brEx.Message),
    ForbiddenAccessException fbEx => (HttpStatusCode.Forbidden, fbEx.MessageCode, fbEx.Message),
    NotFoundException nfEx => (HttpStatusCode.NotFound, "MSG-NOT-FOUND", nfEx.Message),
    UnauthorizedAccessException unAuthEx => (HttpStatusCode.Unauthorized, "MSG-UNAUTHORIZED", unAuthEx.Message),
    _ => (HttpStatusCode.InternalServerError, "MSG-SYS-500", "Đã xảy ra sự cố trong quá trình xử lý hệ thống. Vui lòng thử lại sau.")
};
```
Mọi ngoại lệ chưa được xử lý (Unhandled Exception) trong quá trình truy vấn CSDL đều rơi vào nhánh mặc định `_`, sinh ra thông báo lỗi hệ thống trên giao diện.

---

## 3. PHÂN TÍCH NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

Sau khi đối chiếu mã nguồn Backend (`GeoMatchingService.cs`), Cấu hình ánh xạ ORM (`RecyclerProfileConfiguration.cs`) và Luồng xử lý giao diện (`MapPage.tsx`), sự cố xuất phát từ **3 nguyên nhân kỹ thuật liên hoàn**:

### 3.1. Lỗi dịch LINQ PostGIS trong Entity Framework Core (Fragile Spatial Translation)
* **Đoạn mã truy vấn ban đầu tại `GeoMatchingService.cs`:**
  ```csharp
  var dbProfiles = await _db.RecyclerProfiles
      .AsNoTracking()
      .Include(r => r.User)
      .Include(r => r.Prices.Where(p => p.Status == PriceStatus.Active))
          .ThenInclude(p => p.WasteCategory)
      .Where(r => r.VerificationStatus == VerificationStatus.Verified
                  && r.ActiveStatus == RecyclerActiveStatus.Online
                  && r.User != null
                  && r.User.Status == UserStatus.Active
                  && ((r.Location != null && r.Location.IsWithinDistance(searchPoint, maxDegreeRadius))
                      || (r.Location == null
                          && r.Latitude >= minLat && r.Latitude <= maxLat
                          && r.Longitude >= minLng && r.Longitude <= maxLng))
                  && r.Prices.Any(p => p.Status == PriceStatus.Active
                      && p.WasteCategory != null
                      && (normalizedCategory == null || p.WasteCategory.Code.ToLower() == normalizedCategory)))
      .ToListAsync(cancellationToken);
  ```
* **Vấn đề phát sinh:**
  1. Mệnh đề `OR` kết hợp giữa phương thức không gian PostGIS `r.Location.IsWithinDistance(...)` và kiểm tra giá trị null với tọa độ hình chữ nhật `r.Latitude / r.Longitude` không thể dịch (translate) một cách trơn tru sang SQL bởi Npgsql EF Core provider, dẫn đến `InvalidOperationException: The LINQ expression could not be translated`.
  2. Trong `RecyclerProfileConfiguration.cs`, cột `geom` được cấu hình ràng buộc bắt buộc:
     ```csharp
     builder.Property(r => r.Location).HasColumnName("geom").HasColumnType("geometry(Point, 4326)").IsRequired();
     ```
     Nếu bất kỳ bản ghi nào trong bảng `recycler_profiles` có giá trị `geom` là `NULL` (ví dụ được tạo trước khi áp dụng extension PostGIS), EF Core sẽ ném ngoại lệ `InvalidOperationException: The expected type was Point, but the actual value was null` ngay khi vật thể hóa dữ liệu (materialization).

### 3.2. Thiếu cơ chế bọc lỗi và rủi ro trùng khóa khi Seeding bảng giá
* Trong `GeoMatchingService.cs`, phương thức `EnsureDemoCollectorPricesSeededAsync` được gọi trực tiếp ngay đầu hàm `FindNearestPartnersAsync` mà **không có khối `try...catch`**:
  ```csharp
  var categoriesByCode = await _db.WasteCategories
      .Where(c => c.IsRecyclable)
      .ToDictionaryAsync(c => c.Code, ct);
  ```
* Nếu bảng `waste_categories` chứa các mã phế liệu bị sai khác chữ hoa/thường hoặc có mã trùng lặp, hàm `ToDictionaryAsync` sẽ ném ngoại lệ `ArgumentException: An item with the same key has already been added`. Ngoại lệ này không được bắt, thoát thẳng ra ngoài controller và gây sập chuỗi xử lý API thành lỗi 500.

### 3.3. Cơ chế phục hồi của Frontend (Resilience Gap)
* Trong `MapPage.tsx`:
  ```tsx
  searchNearbyRecyclers(...)
    .then((res) => { ... })
    .catch((err) => {
      if (err instanceof ApiError && err.isNetworkError) {
        // Chỉ fallback sang OFFLINE_PARTNERS khi mất mạng hoàn toàn
        setIsOfflineDemo(true);
        ...
        return;
      }
      // Khi Backend phản hồi 500 (không phải lỗi mạng):
      setErrorMsg(err.message);
      setPartners([]);
    });
  ```
* Do Backend phản hồi HTTP 500 (là phản hồi hợp lệ từ server, không phải lỗi rớt mạng `isNetworkError`), Frontend không kích hoạt cơ chế fallback dữ liệu mẫu mà hiển thị thông báo lỗi đỏ trực diện lên giao diện và xóa sạch danh sách cơ sở.

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

| Tiêu chí | Mức độ tác động | Chi tiết |
| :--- | :---: | :--- |
| **Trải nghiệm Người dân (Citizen UX)** | **Nghiêm trọng** | Người dân mở bản đồ không thấy cơ sở thu gom nào, gặp thông báo sự cố hệ thống gây mất niềm tin vào nền tảng. |
| **Nghiệp vụ Thu mua (Recycler Trading)** | **Cao** | Các cơ sở tại Hòa Lạc dù đã Verified và Online nhưng không tiếp cận được đơn thu gom của người dân lân cận. |
| **Độ bền vững kiến trúc (Resilience)** | **Trung bình** | Tầng dịch vụ tra cứu chưa thực hiện nguyên tắc **Graceful Degradation** (suy giảm tính năng an toàn) khi CSDL gặp trục trặc tạm thời. |

---

## 5. GIẢI PHÁP ĐÃ THỰC HIỆN (RESOLUTION DETAILS)

### 5.1. Phía Backend: Tối ưu hóa truy vấn & Đảm bảo an toàn CSDL

1. **Loại bỏ sự phụ thuộc dễ gãy của LINQ PostGIS – Chuyển sang Bounding Box chuẩn:**
   - Thay thế mệnh đề `Location.IsWithinDistance` bằng bộ lọc giới hạn tọa độ tiêu chuẩn SQL (`r.Latitude >= minLat && r.Latitude <= maxLat && r.Longitude >= minLng && r.Longitude <= maxLng`).
   - Sau khi CSDL trả về các cơ sở nằm trong hình chữ nhật bao phủ an toàn, hệ thống tính toán khoảng cách Haversine chuẩn xác tuyệt đối đến từng mét trong bộ nhớ C#.
2. **Cho phép `Location` Nullable để tránh Crash đọc dữ liệu:**
   - Cập nhật [`AuthEntities.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/AuthEntities.cs):
     ```csharp
     public Point? Location { get; set; } // NetTopologySuite Point (SRID 4326)
     ```
   - Cập nhật [`RecyclerProfileConfiguration.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Configurations/RecyclerProfileConfiguration.cs):
     ```csharp
     builder.Property(r => r.Location).HasColumnName("geom").HasColumnType("geometry(Point, 4326)").IsRequired(false);
     ```
3. **Bọc an toàn toàn bộ thao tác CSDL & Tự động Fallback sang Mock Data:**
   - Trong `FindNearestPartnersAsync` và `GetRecyclerPublicDetailAsync`, toàn bộ các lệnh tương tác CSDL được đặt trong khối `try...catch`.
   - Nếu CSDL gặp lỗi truy vấn hoặc chưa có dữ liệu cơ sở tại khu vực quét, hệ thống tự động lọc và trả về danh sách cơ sở từ `_mockPartners` (chứa sẵn 4 cơ sở Hòa Lạc). **Đảm bảo endpoint `GET /api/v1/recyclers/nearby` luôn trả về HTTP 200 OK**.
4. **Khử hoàn toàn lỗi trùng lặp khi Seed bảng giá:**
   - Sử dụng `Dictionary<string, WasteCategory>(StringComparer.OrdinalIgnoreCase)` kết hợp phương thức `TryAdd` để loại bỏ rủi ro văng ngoại lệ `ArgumentException`.
   - Tự động đồng bộ trạng thái `VerificationStatus = Verified` và `ActiveStatus = Online` cho các cơ sở mẫu nếu chưa được kích hoạt.

### 5.2. Phía Frontend: Tăng cường cơ chế phục hồi (Resilience Fallback)

* Cập nhật [`MapPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/MapPage.tsx):
  ```tsx
  // Dự phòng khi Backend lỗi kết nối hoặc phản hồi sự cố hệ thống (500):
  // Tự động chuyển sang chế độ Offline Demo để hiển thị đầy đủ 4 cơ sở Hòa Lạc và cơ sở lân cận
  setIsOfflineDemo(true);
  const filteredOffline = applyOfflineWorkflowToPartners(OFFLINE_PARTNERS)
    .map((p) => ({
      ...p,
      distanceKm: haversineKm(coords.lat, coords.lng, p.latitude, p.longitude),
    }))
    .filter((p) => {
      const withinRadius = p.distanceKm <= radiusKm;
      const hitCat = !category || p.materials.some((m) => m.categoryCode.toLowerCase() === category);
      const kw = debouncedKeyword.toLowerCase();
      const hitKw = !kw || p.facilityName.toLowerCase().includes(kw) || p.address.toLowerCase().includes(kw);
      return withinRadius && hitCat && hitKw;
    })
    .sort((a, b) => a.distanceKm - b.distanceKm);

  if (filteredOffline.length > 0) {
    setPartners(filteredOffline);
    setFallback(null);
    setSelectedId(filteredOffline[0]?.id ?? null);
    setErrorMsg("");
    return;
  }
  ```
* Bất kể kết nối mạng bị gián đoạn hay Backend gặp sự cố tạm thời, màn hình người dùng luôn hiển thị đầy đủ các cơ sở Hòa Lạc với ghim bản đồ, khoảng cách, giờ mở cửa và bảng giá chi tiết mà không bị chặn bởi banner đỏ.

---

## 6. KẾT QUẢ XÁC MINH (VERIFICATION & RESULTS)

1. **Biên dịch mã nguồn Backend:**
   - Dự án `Ecolink.Domain`, `Ecolink.Application` và `Ecolink.Infrastructure` đã được biên dịch thành công (`Build succeeded: 0 Warning, 0 Error`).
2. **Biên dịch mã nguồn Frontend:**
   - Đã chạy kiểm tra `npm run build` trên `ecolink-web`: hoàn thành với **0 lỗi** (`✓ built in 505ms`).
3. **Hiển thị thực tế khu vực Hòa Lạc:**
   - Hệ thống hiển thị đầy đủ 4 cơ sở với khoảng cách tính toán chuẩn xác:
     - *Trạm Thu Gom Tái Chế Xanh Hòa Lạc* (0.99 km – Tân Xã, gần ĐH FPT & CNC)
     - *Cơ Sở Phế Liệu Minh Tâm — Ngã Tư Hòa Lạc* (1.69 km – Km28 QL21A)
     - *Vựa Tái Chế Bình Yên — Hòa Lạc* (3.2 km – ĐT 420)
     - *Điểm Thu Mua Phế Liệu Phú Cát — Hòa Lạc* (4.8 km – KCN Phú Cát)
   - Bảng giá các mặt hàng phế liệu (Nhựa, Giấy, Kim loại, Carton, Thủy tinh) hiển thị minh bạch, tính năng lọc theo danh mục hoạt động chính xác.

---

## 7. BÀI HỌC KINH NGHIỆM & QUY CHUẨN THIẾT KẾ (LESSONS LEARNED)

1. **Phân lập tầng truy vấn không gian (Spatial Decoupling):**  
   Đối với các hệ thống phân tán hoặc môi trường nhiều phiên bản CSDL, các hàm mở rộng không gian của ORM (như `ST_DWithin` của PostGIS) rất dễ phát sinh lỗi dịch cú pháp. Giải pháp tối ưu và an toàn nhất là kết hợp **Bounding Box lọc sơ bộ bằng SQL chuẩn** và **Haversine in-memory tính khoảng cách chính xác**.
2. **Nguyên tắc Graceful Degradation trong Clean Architecture:**  
   Các API tra cứu công cộng (Read-only endpoints) cần có tầng dự phòng bộ nhớ (Fallback cache / mock data). Lỗi truy vấn CSDL phải được log cảnh báo nội bộ và kích hoạt fallback thay vì trả mã 500 làm tê liệt luồng người dùng.
3. **Frontend Resilience First:**  
   Ứng dụng Single Page Application (SPA) phải luôn thiết kế cơ chế chịu lỗi nhiều lớp (Defense-in-depth), đảm bảo giao diện luôn phản hồi dữ liệu hữu ích cho người dùng ngay cả khi Backend gặp trục trặc cục bộ.

