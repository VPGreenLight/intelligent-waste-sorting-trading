Dưới đây là phần phân tích chi tiết **Lý do lựa chọn PostgreSQL & PostGIS** (để bạn tự tin đưa vào báo cáo và trả lời phản biện của Hội đồng) cùng với **các câu lệnh thực tế** thường dùng nhất trong dự án của bạn:

---

## PHẦN 1: TẠI SAO LẠI CHỌN POSTGRESQL VÀ EXTENSION POSTGIS?

Trong một Khóa luận tốt nghiệp về hệ thống thông minh kết nối thu gom rác, việc lựa chọn **PostgreSQL + PostGIS** là một quyết định kiến trúc chuẩn mực và cực kỳ thuyết phục vì các lý do sau:

### 1. Lý do chọn PostgreSQL làm Cơ sở dữ liệu chính
* **Chuẩn ACID & Toàn vẹn giao dịch tuyệt đối:** Hệ thống của bạn có các nghiệp vụ tài chính/điểm thưởng nhạy cảm (bàn giao rác qua QR, cân ký, tích Điểm Xanh, trừ điểm đổi Voucher). Cơ chế khóa và kiểm soát đồng thời (MVCC) của PostgreSQL đảm bảo không xảy ra xung đột dữ liệu hay lỗi cộng/trừ điểm trùng lặp.
* **Hỗ trợ kiểu dữ liệu lai (Hybrid Relational + JSONB):** PostgreSQL hỗ trợ lưu trữ dữ liệu dạng `JSONB` có đánh index. Điều này cực kỳ hữu ích để lưu:
  * Kết quả suy luận của AI (mảng xác suất chi tiết của các lớp rác).
  * Quy tắc rác động hoặc khung giờ mở cửa linh hoạt của các cơ sở thu mua mà không cần phải tách ra quá nhiều bảng phụ.
* **Tương thích hoàn hảo với ASP.NET Core:** Thư viện `Npgsql.EntityFrameworkCore.PostgreSQL` là một trong những bộ ORM mạnh mẽ nhất, cho phép map trực tiếp các Entity C# sang PostgreSQL.
* **Chuẩn Cloud-Native & Mã nguồn mở:** Hoàn toàn miễn phí, không tốn chi phí bản quyền đắt đỏ như Oracle hay SQL Server Enterprise, dễ dàng đóng gói vào Docker container để mang đi demo/deploy.

---

### 2. Lý do PostGIS là "Vũ khí bí mật" không thể thay thế (Usecase 5)
PostGIS là một Extension (tiện ích mở rộng) biến PostgreSQL thành một **Hệ quản trị cơ sở dữ liệu không gian địa lý (Spatial Database)** chuyên nghiệp số 1 thế giới.

* **Bài toán thực tế:** Người dùng sau khi chụp ảnh chai nhựa muốn: *"Tìm các vựa ve chai hoặc trạm tái chế gần tôi nhất trong bán kính 3km – 5km"*.
* **Nếu KHÔNG dùng PostGIS (Cách làm thủ công yếu kém):**
  * Bạn phải `SELECT` toàn bộ hàng ngàn điểm thu gom về RAM của backend C#.
  * Dùng vòng lặp chạy công thức lượng giác phức tạp (Haversine formula) tính khoảng cách từng điểm với tọa độ GPS của User, rồi mới sắp xếp lại.
  * ❌ *Hậu quả:* Tốn CPU backend, độ trễ cao, khi có nhiều người dùng cùng tìm kiếm thì server sẽ bị quá tải (bottleneck).
* **Khi CÓ PostGIS (Giải pháp chuẩn kỹ sư):**
  * Tọa độ được lưu dưới dạng chuẩn không gian `GEOGRAPHY(Point, 4326)` (hệ tọa độ WGS84 toàn cầu).
  * Sử dụng chỉ mục không gian chuyên dụng **GiST Index (Generalized Search Tree / R-Tree)**.
  * Truy vấn tìm các trạm trong bán kính $R$ mét diễn ra ngay trong Database chỉ mất **1 – 2 mili-giây**!
* **Tích hợp sâu với Entity Framework Core qua `NetTopologySuite`:**
  * Bạn có thể viết truy vấn khoảng cách bằng LINQ thuần trong C#, EF Core sẽ tự dịch sang lệnh PostGIS tối ưu:
    ```csharp
    var nearby = await _context.Partners
        .Where(p => p.Location.Distance(userLocation) <= 5000) // Bán kính 5000m
        .OrderBy(p => p.Location.Distance(userLocation))
        .ToListAsync();
    ```

---

## PHẦN 2: GIỚI THIỆU CÁC CÂU LỆNH POSTGRESQL & POSTGIS THƯỜNG DÙNG

### 1. Lệnh quản trị trong giao diện dòng lệnh `psql` (Meta-commands)
Khi bạn truy cập vào PostgreSQL terminal:

| Lệnh | Ý nghĩa |
| :--- | :--- |
| `\l` | Liệt kê danh sách tất cả các Database hiện có |
| `\c ecolink_db` | Kết nối (chuyển sang làm việc) với database `ecolink_db` |
| `\dt` | Hiển thị danh sách các bảng (Tables) trong database hiện tại |
| `\d TenBang` | Xem chi tiết cấu trúc cột, khóa chính, khóa ngoại của bảng `TenBang` |
| `\dx` | Xem danh sách các Extension đã được cài đặt (để kiểm tra xem có `postgis` chưa) |
| `\q` | Thoát khỏi môi trường psql |

---

### 2. Nhóm lệnh SQL cơ bản kết hợp PostGIS

#### A. Kích hoạt Extension PostGIS (Chỉ cần chạy 1 lần duy nhất)
```sql
-- Bật tính năng không gian địa lý cho database
CREATE EXTENSION IF NOT EXISTS postgis;
```

#### B. Tạo bảng có tọa độ GPS và cột JSONB (Ví dụ bảng Cơ sở thu mua)
```sql
CREATE TABLE partners (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    address TEXT NOT NULL,
    phone VARCHAR(20),
    -- Cột lưu vị trí GPS theo hệ tọa độ WGS84 (SRID 4326)
    location GEOGRAPHY(Point, 4326) NOT NULL,
    -- Cột JSON lưu giờ mở cửa linh hoạt
    operating_hours JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
```

#### C. Tạo chỉ mục không gian GiST (Cực kỳ quan trọng để tăng tốc tìm kiếm)
```sql
-- Tạo Spatial Index trên cột location
CREATE INDEX idx_partners_location ON partners USING GIST (location);
```

#### D. Thêm dữ liệu (Lưu ý: Kinh độ - Longitude trước, Vĩ độ - Latitude sau)
```sql
-- Thêm một cơ sở thu mua rác tại Hà Nội (Kinh độ: 105.8542, Vĩ độ: 21.0285)
INSERT INTO partners (name, address, location, operating_hours)
VALUES (
    'Trạm Thu Gom Phế Liệu Xanh Hoàn Kiếm',
    'Số 12 Tràng Thi, Hoàn Kiếm, Hà Nội',
    ST_SetSRID(ST_MakePoint(105.8542, 21.0285), 4326),
    '{"open": "08:00", "close": "17:30", "days": ["T2", "T3", "T4", "T5", "T6", "T7"]}'::jsonb
);
```

---

### 3. Các hàm không gian "đắt giá" nhất của PostGIS cho Đề tài

#### 📍 Lệnh 1: `ST_DWithin` — Tìm các trạm thu gom trong bán kính $R$ mét (Usecase 5)
```sql
-- Giả sử vị trí người dùng là (105.8500, 21.0300)
-- Tìm các cơ sở thu mua cách người dùng không quá 3000 mét (3km)
SELECT 
    name, 
    address,
    -- Tính luôn khoảng cách chính xác theo đơn vị mét
    ROUND(ST_Distance(location, ST_SetSRID(ST_MakePoint(105.8500, 21.0300), 4326))::numeric, 1) AS distance_meters
FROM partners
WHERE ST_DWithin(
    location, 
    ST_SetSRID(ST_MakePoint(105.8500, 21.0300), 4326), 
    3000 -- Bán kính 3000m
)
ORDER BY distance_meters ASC;
```

#### 📍 Lệnh 2: `ST_Distance` — Đo khoảng cách thực tế giữa 2 điểm GPS
Trả về khoảng cách thực địa tính bằng mét trên mặt cầu Trái Đất (không cần tự viết thuật toán):
```sql
SELECT ST_Distance(
    ST_SetSRID(ST_MakePoint(105.8542, 21.0285), 4326), -- Điểm A
    ST_SetSRID(ST_MakePoint(105.8000, 21.0100), 4326)  -- Điểm B
) AS distance_in_meters;
```

#### 📍 Lệnh 3: `ST_AsGeoJSON` — Xuất tọa độ ra định dạng GeoJSON cho Frontend
Frontend (React / Leaflet / Mapbox / Google Maps) thường cần dữ liệu GeoJSON để vẽ ghim (Marker) lên bản đồ:
```sql
SELECT 
    id, 
    name, 
    ST_AsGeoJSON(location) AS geojson_geometry
FROM partners;
```
*Kết quả trả về dạng:* `{"type":"Point","coordinates":[105.8542,21.0285]}` để Frontend nạp thẳng vào bản đồ hiển thị ngay lập tức.

---

### 💡 Tóm lại điểm mấu chốt để báo cáo:
Khi Thầy Cô hỏi: *"Tại sao không dùng MySQL hay SQL Server cho quen thuộc mà lại chọn PostgreSQL?"*  
Bạn trả lời:
> *"Thưa Thầy Cô, hệ thống của em có tính năng cốt lõi là **kết nối người gom rác với các điểm thu mua lân cận theo vị trí thời gian thực (GPS)**. **PostgreSQL kết hợp Extension PostGIS** là tiêu chuẩn công nghiệp mạnh mẽ nhất hiện nay về xử lý dữ liệu không gian địa lý. Nó cho phép đánh chỉ mục GiST Index và tính toán bán kính trong vài mili-giây mà không làm nghẽn CPU Backend, đồng thời tích hợp mượt mà với **NetTopologySuite** trong ASP.NET Core ạ."*