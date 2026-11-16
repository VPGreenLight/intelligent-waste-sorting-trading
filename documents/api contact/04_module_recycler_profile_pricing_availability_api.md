# TÀI LIỆU ĐẶC TẢ API – MODULE 5: QUẢN LÝ HỒ SƠ CƠ SỞ THU GOM, BẢNG GIÁ PHẾ LIỆU & TRẠNG THÁI HOẠT ĐỘNG (RECYCLER PROFILE, PRICING & AVAILABILITY)
**Mã phân hệ:** `MTH` (`Use Case R-01: Manage Accepted Waste & Pricing` & `Use Case R-02: Toggle Active Status`)  
**Controllers:** [`RecyclerController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerController.cs), [`WasteCategoryController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/WasteCategoryController.cs)  
**Service thực thi:** [`RecyclerManagementService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/RecyclerManagementService.cs) ([`IRecyclerManagementService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Interfaces/IRecyclerManagementService.cs))  
**Entities:** [`RecyclerProfile.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/RecyclerProfile.cs), [`RecyclerPrice.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/RecyclerPrice.cs), [`WasteEntities.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/WasteEntities.cs)  
**DTOs:** [`RecyclerDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/RecyclerDtos.cs)  
**Base Routes:** `/api/v1/recycler`, `/api/v1/waste-categories`

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS

| STT | Phương thức | Endpoint URI | Quyền truy cập | Use Case | Chức năng nghiệp vụ | Mã thông báo / Quy tắc |
|:---:|:---:|:---|:---:|:---:|:---|:---|
| 1 | `GET` | `/api/v1/waste-categories` | `[Authorize]` | `R-01`, `U-05` | Lấy danh sách 5 danh mục phế liệu tái chế chuẩn có thể thu mua (loại trừ rác thông thường `"trash"`). | `200 OK` |
| 2 | `GET` | `/api/v1/recycler/profile` | `[Authorize(Roles = "Recycler,Collector")]` | `R-01`, `R-02` | Xem thông tin hồ sơ cơ sở thu gom của chính đối tác đang đăng nhập kèm trạng thái duyệt và tổng số loại phế liệu đang mua. | `200 OK`, `MSG-NOT-FOUND` |
| 3 | `PUT` | `/api/v1/recycler/profile` | `[Authorize(Roles = "Recycler,Collector")]` | `R-01`, `U-17` | Cập nhật thông tin cơ sở, tọa độ GPS PostGIS (`SRID 4326`). Nếu hồ sơ đang `Rejected` sẽ tự động chuyển về `Pending` để duyệt lại (`BR-MTH-02`). | `BR-MTH-02`, `BR-MTH-03`, `MSG-MTH-06` |
| 4 | `GET` | `/api/v1/recycler/prices` | `[Authorize(Roles = "Recycler,Collector")]` | `R-01` | Xem bảng giá thu mua phế liệu của cơ sở, lọc theo từ khóa (`keyword`), trạng thái (`isActive`) và cảnh báo giá quá hạn 30 ngày (`isOutdated` – `JOB-04`). | `200 OK` |
| 5 | `POST` | `/api/v1/recycler/prices` | `[Authorize(Roles = "Recycler,Collector")]` | `R-01` | Thêm mới một loại phế liệu nhận thu mua kèm đơn giá (`VND/kg`), khối lượng tối thiểu (`kg`) và điều kiện thu gom. | `BR-MTH-01`, `BR-MTH-03`, `MSG-GEN-01` |
| 6 | `PUT` | `/api/v1/recycler/prices/{priceId:guid}` | `[Authorize(Roles = "Recycler,Collector")]` | `R-01` | Cập nhật đơn giá, khối lượng tối thiểu, điều kiện thu gom hoặc tạm ngưng/mở lại việc thu mua một loại phế liệu (`BR-MTH-07`). | `BR-MTH-03`, `BR-MTH-07`, `MSG-MTH-06` |
| 7 | `PUT` | `/api/v1/recycler/prices` | `[Authorize(Roles = "Recycler,Collector")]` | `R-01` | Lưu hàng loạt toàn bộ bảng giá (*Save price sheet* trên màn hình `SCR-RCY-01`), làm mới mốc thời gian cập nhật (`JOB-04`). | `BR-MTH-01`, `BR-MTH-03`, `MSG-MTH-06` |
| 8 | `GET` | `/api/v1/recycler/availability` | `[Authorize(Roles = "Recycler,Collector")]` | `R-02` | Xem trạng thái hoạt động (`Online` / `Offline`), lý do tạm nghỉ (`busyReason`), giờ mở cửa và lịch tuần (`weeklySchedule`). | `200 OK` |
| 9 | `PATCH` | `/api/v1/recycler/availability` | `[Authorize(Roles = "Recycler,Collector")]` | `R-02` | Bật/tắt trạng thái nhận đơn (`Online` / `Offline`) trên bản đồ tương tác kèm kiểm tra điều kiện đủ hồ sơ (`BR-MTH-03`, `BR-MTH-06`). | `BR-MTH-01`, `BR-MTH-03`, `BR-MTH-06`, `MSG-MTH-06` |
| 10 | `PUT` | `/api/v1/recycler/availability/schedule` | `[Authorize(Roles = "Recycler,Collector")]` | `R-02` | Cập nhật lịch hoạt động cố định theo từng ngày trong tuần và tự động tổng hợp chuỗi giờ mở cửa (`operatingHours`). | `BR-MTH-01`, `BR-MTH-03`, `MSG-GEN-01` |

---

## 2. LUỒNG NGHIỆP VỤ & QUY TẮC CỐT LÕI (`R-01` & `R-02`)

1. **Nguyên tắc định danh an toàn qua JWT (`Self-Service Ownership`):**
   * Toàn bộ các endpoint dưới `/api/v1/recycler/*` không nhận `recyclerId` qua URL hay Body mà trích xuất trực tiếp `UserId` từ JWT Bearer Token của đối tác đang đăng nhập (`ICurrentUserService.UserId`), đảm bảo một cơ sở thu gom không thể chỉnh sửa trái phép bảng giá hoặc trạng thái của cơ sở khác.
2. **Điều kiện tiên quyết để cấu hình bảng giá và bật hoạt động (`BR-MTH-01`):**
   * Chỉ những hồ sơ đã được `Staff/Admin` phê duyệt (`VerificationStatus == Verified`) và tài khoản không bị đình chỉ (`User.Status != Suspended`) mới được thêm/sửa bảng giá, cập nhật lịch tuần hoặc bật trạng thái `Online`.
3. **Điều kiện bắt buộc trước khi bật `Online` trên bản đồ (`BR-MTH-03` & `EF-03`):**
   * Để bật `isOnline = true`, cơ sở bắt buộc phải có **vị trí tọa độ GPS hợp lệ** (`Location != null` và khác `(0, 0)`) và **ít nhất 1 loại phế liệu đang nhận thu mua** (`ActivePriceCount >= 1`).
   * Ngược lại, khi cơ sở đang `Online`, hệ thống **chặn** việc tắt (`isActive = false`) dòng giá `Active` cuối cùng để tránh tình trạng cơ sở hiển thị trên bản đồ nhưng không nhận mua bất kỳ loại rác nào.
4. **Cơ chế tạm ngưng thu mua thay vì xóa cứng (`BR-MTH-07`) & Tắt hoạt động an toàn (`BR-MTH-06`):**
   * Khi ngưng nhận mua một loại phế liệu, bản ghi trong bảng `recycler_price_list` được chuyển sang `Status = Inactive` (`isActive = false`) để bảo toàn lịch sử đối soát cho các đơn giao dịch cũ (`BR-MTH-07`).
   * Khi chuyển sang `Offline`, cơ sở chỉ tạm ẩn khỏi kết quả tìm kiếm bản đồ mới (`U-05`), toàn bộ đơn hẹn đang xử lý (`Pending` / `Accepted`) vẫn được giữ nguyên (`BR-MTH-06`).

---

## 3. CHI TIẾT TỪNG API ENDPOINT

---

### 3.1. Lấy Danh mục Phế liệu Có thể Thu mua (`GET /api/v1/waste-categories`)

* **Mô tả:** Trả về danh sách các danh mục phế liệu tái chế chuẩn của hệ thống để hiển thị trong dropdown *"Add Scrap Type"* (`R-01`) và bộ lọc tìm kiếm điểm thu gom gần nhất (`U-05`). Hệ thống tự động loại trừ danh mục rác thải sinh hoạt không tái chế (`code == "trash"`). Nếu bảng `waste_categories` trong CSDL chưa có dữ liệu, hệ thống tự động khởi tạo 5 danh mục chuẩn (`plastic`, `paper`, `glass`, `metal`, `cardboard`).
* **Header bắt buộc:** `Authorization: Bearer <accessToken>`

#### a. Request Parameters
Không có tham số Query hay Body.

#### b. Response Parameters (`ApiResponse<IReadOnlyList<WasteCategoryDto>>`)

| Trường trong `data[]` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `id` | `Guid` | Khóa chính của danh mục phế liệu (`waste_categories.id`). |
| `code` | `string` | Mã chuẩn khớp với nhãn AI Vision (`"plastic"`, `"paper"`, `"glass"`, `"metal"`, `"cardboard"`). |
| `name` | `string` | Tên hiển thị tiếng Việt của loại phế liệu. |
| `description` | `string?` | Mô tả chi tiết các vật phẩm thuộc danh mục. |
| `standardBinColor` | `string` | Màu thùng rác tiêu chuẩn tương ứng. |
| `iconUrl` | `string?` | Đường dẫn biểu tượng hiển thị trên UI. |

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "messageCode": null,
  "message": null,
  "data": [
    {
      "id": "a1111111-1111-1111-1111-111111111111",
      "code": "cardboard",
      "name": "Bìa Carton (Cardboard)",
      "description": "Thùng carton, hộp giấy đóng gói hàng hóa.",
      "standardBinColor": "Nâu Vàng",
      "iconUrl": null
    },
    {
      "id": "a2222222-2222-2222-2222-222222222222",
      "code": "paper",
      "name": "Giấy báo & Tài liệu (Paper)",
      "description": "Giấy văn phòng, sách báo cũ, tập vở.",
      "standardBinColor": "Xanh Dương",
      "iconUrl": null
    },
    {
      "id": "a3333333-3333-3333-3333-333333333333",
      "code": "metal",
      "name": "Kim loại & Vỏ lon (Metal)",
      "description": "Vỏ lon nước ngọt bằng nhôm, sắt vụn, hộp thiếc.",
      "standardBinColor": "Xám Kim Loại",
      "iconUrl": null
    },
    {
      "id": "a4444444-4444-4444-4444-444444444444",
      "code": "plastic",
      "name": "Nhựa tái chế (Plastic)",
      "description": "Chai nhựa PET, bình nhựa HDPE sạch.",
      "standardBinColor": "Cam",
      "iconUrl": null
    },
    {
      "id": "a5555555-5555-5555-5555-555555555555",
      "code": "glass",
      "name": "Thủy tinh (Glass)",
      "description": "Chai lọ thủy tinh nguyên vẹn, không lẫn mảnh vỡ nguy hại.",
      "standardBinColor": "Trắng",
      "iconUrl": null
    }
  ],
  "errors": null,
  "timestamp": "2026-10-31T09:00:00Z"
}
```

---

### 3.2. Xem Hồ sơ Cơ sở Thu gom của Chính mình (`GET /api/v1/recycler/profile`)

* **Mô tả:** Lấy thông tin hồ sơ cơ sở thu gom (`recycler_profiles`) của tài khoản `Recycler` đang đăng nhập, kèm theo số lượng loại phế liệu đang nhận thu mua (`activePriceCount`).
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`

#### a. Response Parameters (`ApiResponse<RecyclerProfileDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `recyclerId` | `Guid` | ID tài khoản của cơ sở thu gom (`recycler_profiles.user_id`). |
| `facilityName` | `string` | Tên cơ sở / vựa thu mua phế liệu. |
| `representativeName` | `string?` | Họ tên người đại diện pháp luật. |
| `businessAddress` | `string` | Địa chỉ kinh doanh thực tế. |
| `latitude` | `double` | Vĩ độ GPS (`WGS84`). |
| `longitude` | `double` | Kinh độ GPS (`WGS84`). |
| `operatingHours` | `string` | Chuỗi tóm tắt khung giờ mở cửa hoạt động. |
| `contactPhone` | `string` | Số điện thoại liên hệ thu mua. |
| `description` | `string?` | Mô tả giới thiệu cơ sở và năng lực thu mua. |
| `legalPermitUrl` | `string?` | URL giấy phép kinh doanh trên Cloudflare R2. |
| `businessPhotoUrl` | `string?` | URL ảnh chụp mặt tiền kho bãi trên Cloudflare R2. |
| `verificationStatus` | `string` | Trạng thái thẩm định: `"Pending"`, `"Verified"`, `"Rejected"`. |
| `activeStatus` | `string` | Trạng thái hoạt động: `"Online"` hoặc `"Offline"`. |
| `rejectionReason` | `string?` | Lý do từ chối của Staff (nếu `verificationStatus == "Rejected"`). |
| `busyReason` | `string?` | Ghi chú lý do tạm nghỉ khi `activeStatus == "Offline"`. |
| `activePriceCount` | `integer` | Số lượng danh mục phế liệu đang có trạng thái `Active` trong bảng giá. |

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "messageCode": null,
  "message": null,
  "data": {
    "recyclerId": "22222222-2222-2222-2222-222222222222",
    "facilityName": "Vựa phế liệu EcoGreen",
    "representativeName": "Trần Văn Thu Gom",
    "businessAddress": "123 Lê Văn Việt, Phường Hiệp Phú, TP. Thủ Đức, TP.HCM",
    "latitude": 10.8456,
    "longitude": 106.7891,
    "operatingHours": "T2-T7: 07:30 - 18:00",
    "contactPhone": "0902222222",
    "description": "Chuyên thu mua nhựa tái chế, bìa carton, giấy báo và kim loại giá cao tận nơi.",
    "legalPermitUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/legal-permits/demo_permit.pdf",
    "businessPhotoUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/facility-photos/demo_facility.jpg",
    "verificationStatus": "Verified",
    "activeStatus": "Online",
    "rejectionReason": null,
    "busyReason": null,
    "activePriceCount": 4
  },
  "errors": null,
  "timestamp": "2026-10-31T09:05:00Z"
}
```

#### b. Các Thông báo Trả về

| HTTP Status | Mã `messageCode` | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---|:---|
| **`200 OK`** | `null` | Tìm thấy hồ sơ cơ sở thu gom | Trả về `RecyclerProfileDto` trong `data`. |
| **`404 Not Found`** | `MSG-NOT-FOUND` | Tài khoản chưa có bản ghi `recycler_profiles` | `"Không tìm thấy hồ sơ cơ sở thu gom của tài khoản này."` |
| **`401 Unauthorized`** | `MSG-UNAUTHORIZED` | Thiếu hoặc hết hạn JWT Token | `"Phiên đăng nhập không hợp lệ hoặc đã hết hạn."` |
| **`403 Forbidden`** | — | Tài khoản không mang vai trò `Recycler` / `Collector` | — |

---

### 3.3. Cập nhật Hồ sơ Cơ sở Thu gom (`PUT /api/v1/recycler/profile`)

* **Mô tả:** Cập nhật thông tin liên hệ, địa chỉ, ảnh mặt tiền và tọa độ GPS (`Point(Longitude, Latitude) { SRID = 4326 }`) của cơ sở thu gom.
  * Nếu hồ sơ đang ở trạng thái `Rejected`, hệ thống tự động đặt lại `VerificationStatus = Pending`, xóa `RejectionReason = null` và chuyển `ActiveStatus = Offline` để chờ `Staff` thẩm định lại (`BR-MTH-02`).
* **Quy tắc nghiệp vụ áp dụng:** `BR-MTH-02`, `BR-MTH-03`, `BR-AUTH-05`.

#### a. Request Parameters (`application/json` – `UpdateRecyclerProfileRequest`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `facilityName` | Body | `string` | **Có** | Tối đa `150` ký tự | Tên cơ sở thu gom phế liệu. |
| `representativeName` | Body | `string?` | Không | Tối đa `100` ký tự | Họ tên người đại diện pháp luật. |
| `businessAddress` | Body | `string` | **Có** | Không được để trống | Địa chỉ kinh doanh thực tế. |
| `latitude` | Body | `double` | **Có** | `-90.0 <= lat <= 90.0` (không đồng thời bằng `0` với `longitude`) | Vĩ độ GPS của cơ sở. |
| `longitude` | Body | `double` | **Có** | `-180.0 <= lng <= 180.0` (không đồng thời bằng `0` với `latitude`) | Kinh độ GPS của cơ sở. |
| `contactPhone` | Body | `string` | **Có** | Tối đa `20` ký tự | Số điện thoại liên hệ của cơ sở. |
| `description` | Body | `string?` | Không | Tùy chọn | Mô tả giới thiệu về cơ sở. |
| `businessPhotoUrl` | Body | `string?` | Không | URL hợp lệ | Đường dẫn ảnh mặt tiền kho bãi mới (nếu thay đổi). |

##### Ví dụ Request Body:
```json
{
  "facilityName": "Vựa phế liệu EcoGreen - Chi nhánh Thủ Đức",
  "representativeName": "Trần Văn Thu Gom",
  "businessAddress": "125 Lê Văn Việt, Phường Hiệp Phú, TP. Thủ Đức, TP.HCM",
  "latitude": 10.8462,
  "longitude": 106.7895,
  "contactPhone": "0902222222",
  "description": "Nhận thu mua nhựa PET, giấy carton, vỏ lon nhôm số lượng từ 1kg trở lên.",
  "businessPhotoUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/facility-photos/updated_facility.jpg"
}
```

#### b. Các Thông báo Trả về

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `null` | `BR-MTH-02` | Cập nhật thông tin hồ sơ thành công | `"Cập nhật hồ sơ cơ sở thành công."` |
| **`400 Bad Request`** | `MSG-MTH-06` | `BR-MTH-03` | Truyền tọa độ `latitude == 0` và `longitude == 0` | `"Vị trí cơ sở không hợp lệ. Vui lòng chọn vị trí trên bản đồ hoặc nhập tọa độ chính xác."` |
| **`400 Bad Request`** | `MSG-AUTH-04` | `BR-AUTH-05` | Tài khoản đang bị đình chỉ (`Status == Suspended`) | `"Tài khoản đã bị đình chỉ hoặc vô hiệu hóa. Vui lòng liên hệ bộ phận hỗ trợ."` |
| **`404 Not Found`** | `MSG-NOT-FOUND` | — | Không tìm thấy hồ sơ cơ sở thu gom | `"Không tìm thấy hồ sơ cơ sở thu gom của tài khoản này."` |

---

### 3.4. Xem Bảng giá Phế liệu của Cơ sở (`GET /api/v1/recycler/prices`)

* **Mô tả:** Lấy danh sách các loại phế liệu trong bảng giá (`recycler_price_list`) của cơ sở thu gom hiện tại, hỗ trợ tìm kiếm theo tên/mã loại phế liệu và lọc theo trạng thái đang nhận mua (`isActive`). Tự động đánh dấu `isOutdated = true` cho những dòng giá chưa được cập nhật quá `30 ngày` (`JOB-04`).
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`

#### a. Request Parameters (Query String)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---:|:---|
| `keyword` | Query | `string?` | Không | `null` | Từ khóa tìm kiếm không phân biệt hoa thường (`ILike`) theo tên hoặc mã loại phế liệu (`plastic`, `Nhựa`, ...). |
| `isActive` | Query | `boolean?` | Không | `null` (Tất cả) | `true`: Chỉ lấy các loại đang thu mua (`Active`).<br>`false`: Chỉ lấy các loại đang tạm ngưng (`Inactive`). |

##### Ví dụ Request URL:
```http
GET /api/v1/recycler/prices?keyword=nhựa&isActive=true
Authorization: Bearer <recycler_jwt_token>
```

#### b. Response Parameters (`ApiResponse<IReadOnlyList<RecyclerPriceDto>>`)

| Trường trong `data[]` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `id` | `Guid` | Khóa chính của dòng giá (`recycler_price_list.id`). |
| `wasteCategoryId` | `Guid` | ID danh mục phế liệu (`waste_categories.id`). |
| `wasteCategoryCode` | `string` | Mã loại phế liệu (`"plastic"`, `"paper"`, `"metal"`, `"cardboard"`, `"glass"`). |
| `wasteCategoryName` | `string` | Tên hiển thị của loại phế liệu. |
| `pricePerKg` | `decimal` | Đơn giá thu mua niêm yết (`VND/kg`, số nguyên dương). |
| `minQuantityKg` | `decimal` | Khối lượng tối thiểu nhận thu gom (`kg`). |
| `collectionConditions` | `string?` | Điều kiện thu gom (VD: `"Đã làm sạch, tháo nhãn, ép dẹp"`). |
| `isActive` | `boolean` | `true` nếu đang nhận thu mua (`Active`), `false` nếu tạm ngưng (`Inactive`). |
| `effectiveFrom` | `string (ISO 8601)` | Thời điểm bắt đầu áp dụng đơn giá. |
| `effectiveTo` | `string? (ISO 8601)` | Thời điểm kết thúc hiệu lực (nếu có). |
| `lastUpdatedAt` | `string (ISO 8601)` | Thời điểm cập nhật dòng giá gần nhất (`UpdatedAt`). |
| `isOutdated` | `boolean` | `true` nếu `(DateTime.UtcNow - lastUpdatedAt).TotalDays > 30` (cảnh báo giá cũ cần xác nhận lại theo `JOB-04`). |

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "messageCode": null,
  "message": null,
  "data": [
    {
      "id": "b1111111-1111-1111-1111-111111111111",
      "wasteCategoryId": "a4444444-4444-4444-4444-444444444444",
      "wasteCategoryCode": "plastic",
      "wasteCategoryName": "Nhựa tái chế (Plastic)",
      "pricePerKg": 7000,
      "minQuantityKg": 1.0,
      "collectionConditions": "Chai PET trong suốt, đã đổ hết nước và bóp dẹp",
      "isActive": true,
      "effectiveFrom": "2026-10-30T08:00:00Z",
      "effectiveTo": null,
      "lastUpdatedAt": "2026-10-31T08:30:00Z",
      "isOutdated": false
    }
  ],
  "errors": null,
  "timestamp": "2026-10-31T09:10:00Z"
}
```

---

### 3.5. Thêm Mới Một Loại Phế liệu vào Bảng giá (`POST /api/v1/recycler/prices`)

* **Mô tả:** Thêm một danh mục phế liệu mới vào bảng giá thu mua của cơ sở (`Use Case R-01`). Yêu cầu hồ sơ cơ sở đã được phê duyệt (`VerificationStatus == Verified`).
* **Quy tắc nghiệp vụ áp dụng:** `BR-MTH-01`, `BR-MTH-03`, `BR-AUTH-05`.

#### a. Request Parameters (`application/json` – `CreateRecyclerPriceRequest`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `wasteCategoryId` | Body | `Guid` | **Có** | Khác `"trash"` và chưa tồn tại trong bảng giá của cơ sở | ID loại phế liệu cần thêm vào bảng giá. |
| `pricePerKg` | Body | `decimal` | **Có** | Số nguyên dương (`1 <= price <= 1,000,000,000`) | Đơn giá thu mua tính theo `VND/kg`. |
| `minQuantityKg` | Body | `decimal` | Không | Mặc định `1.0`, `0.001 <= minQuantityKg <= 100,000` | Khối lượng tối thiểu nhận thu gom (`kg`). |
| `collectionConditions` | Body | `string?` | Không | Tối đa `200` ký tự | Ghi chú điều kiện chất lượng phế liệu khi bàn giao. |

##### Ví dụ Request Body:
```json
{
  "wasteCategoryId": "a5555555-5555-5555-5555-555555555555",
  "pricePerKg": 2500,
  "minQuantityKg": 5.0,
  "collectionConditions": "Chai lọ thủy tinh nguyên vẹn, đã súc sạch, không lẫn mảnh vỡ"
}
```

#### b. Các Thông báo Trả về

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`201 Created`** | `null` | `BR-MTH-03` | Thêm dòng giá mới thành công | `"Đã thêm loại phế liệu vào bảng giá."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-MTH-03` | Loại phế liệu đã có sẵn trong bảng giá của cơ sở | `"Loại phế liệu này đã có trong bảng giá. Vui lòng chỉnh sửa dòng hiện có."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-MTH-03` | Chọn danh mục rác không tái chế (`code == "trash"`) | `"Rác thải thông thường không thể đưa vào danh sách phế liệu nhận mua."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-MTH-03` | `pricePerKg <= 0` hoặc có phần thập phân lẻ; `minQuantityKg <= 0`; `collectionConditions > 200` ký tự | `"Giá thu mua phải là số nguyên dương (VND/kg)."` / `"Khối lượng tối thiểu phải lớn hơn 0."` |
| **`400 Bad Request`** | `MSG-MTH-03` | `BR-MTH-01` | Hồ sơ cơ sở đang ở trạng thái `Pending` | `"Hồ sơ cơ sở đang chờ Staff phê duyệt. Bạn chỉ có thể thao tác sau khi được duyệt."` |
| **`400 Bad Request`** | `MSG-MTH-05` | `BR-MTH-01` | Hồ sơ cơ sở đang ở trạng thái `Rejected` | `"Hồ sơ cơ sở chưa được phê duyệt: {rejectionReason}. Vui lòng cập nhật hồ sơ và gửi lại."` |
| **`404 Not Found`** | `MSG-NOT-FOUND` | — | `wasteCategoryId` không tồn tại trong hệ thống | `"Loại phế liệu không tồn tại."` |

---

### 3.6. Cập nhật / Tạm ngưng Một Dòng Giá (`PUT /api/v1/recycler/prices/{priceId:guid}`)

* **Mô tả:** Cho phép chỉnh sửa đơn giá, khối lượng tối thiểu, điều kiện thu gom hoặc bật/tắt trạng thái thu mua (`isActive`) của một dòng giá cụ thể (`BR-MTH-07`: ngưng mua thay vì xóa cứng).
* **Kiểm tra an toàn khi đang `Online` (`EF-03` / `BR-MTH-03`):** Nếu cơ sở đang ở trạng thái `Online` và người dùng truyền `isActive = false` cho dòng giá `Active` cuối cùng, hệ thống sẽ từ chối và yêu cầu giữ ít nhất 1 loại phế liệu đang nhận mua.

#### a. Request Parameters

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `priceId` | Path (Route) | `Guid` | **Có** | `Guid` hợp lệ thuộc sở hữu của Recycler | ID của dòng giá cần cập nhật. |
| `pricePerKg` | Body | `decimal?` | Không | Số nguyên dương (`1 .. 1,000,000,000`) | Đơn giá thu mua mới (`VND/kg`). |
| `minQuantityKg` | Body | `decimal?` | Không | `> 0` (`0.001 .. 100,000`) | Khối lượng tối thiểu mới (`kg`). |
| `collectionConditions` | Body | `string?` | Không | Tối đa `200` ký tự | Điều kiện thu gom mới. |
| `isActive` | Body | `boolean?` | Không | `true` hoặc `false` | `true`: Mở nhận mua (`Active`).<br>`false`: Tạm ngưng nhận mua (`Inactive` – `BR-MTH-07`). |

##### Ví dụ Request Body:
```json
{
  "pricePerKg": 7500,
  "minQuantityKg": 1.5,
  "collectionConditions": "Chai nhựa PET sạch, đã tháo nắp và nhãn",
  "isActive": true
}
```

#### b. Các Thông báo Trả về

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `null` | `BR-MTH-07` | Cập nhật dòng giá thành công | `"Cập nhật bảng giá thành công."` |
| **`400 Bad Request`** | `MSG-MTH-06` | `BR-MTH-03` | Tắt (`isActive = false`) dòng giá `Active` cuối cùng khi cơ sở đang `Online` | `"Vui lòng thêm ít nhất một loại phế liệu nhận mua và một vị trí hợp lệ trước khi hoạt động."` |
| **`404 Not Found`** | `MSG-NOT-FOUND` | — | Không tìm thấy `priceId` thuộc cơ sở thu gom hiện tại | `"Không tìm thấy dòng giá cần cập nhật."` |

---

### 3.7. Lưu Hàng loạt Bảng giá (`PUT /api/v1/recycler/prices`)

* **Mô tả:** Phục vụ nút *"Save price sheet"* trên màn hình quản lý bảng giá (`SCR-RCY-01`). Cho phép gửi danh sách nhiều loại phế liệu cùng lúc:
  * Nếu loại phế liệu chưa có trong bảng giá và `isActive = true`: Tự động tạo mới dòng giá.
  * Nếu loại phế liệu đã có: Cập nhật `pricePerKg`, `minQuantityKg`, `collectionConditions`, `status` và làm mới `UpdatedAt = DateTime.UtcNow` (để xác nhận lại bảng giá, xóa cờ `isOutdated` của `JOB-04`).
  * Các dòng giá của loại phế liệu không nằm trong danh sách gửi lên sẽ được giữ nguyên.

#### a. Request Parameters (`application/json` – `BulkSavePricesRequest`)

| Tên tham số | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---|:---|
| `items` | `BulkPriceItemRequest[]` | **Có** | Tối thiểu `1` phần tử, không trùng `wasteCategoryId` | Danh sách các dòng giá cần lưu đồng loạt. |
| `items[].wasteCategoryId` | `Guid` | **Có** | Tồn tại trong `waste_categories` và khác `"trash"` | ID loại phế liệu. |
| `items[].isActive` | `boolean` | Không | Mặc định `true` | Bật/tắt nhận thu mua loại phế liệu này. |
| `items[].pricePerKg` | `decimal` | **Có** | Số nguyên dương (`VND/kg`) | Đơn giá thu mua niêm yết. |
| `items[].minQuantityKg` | `decimal` | Không | Mặc định `1.0`, `> 0` | Khối lượng tối thiểu nhận mua (`kg`). |
| `items[].collectionConditions` | `string?` | Không | Tối đa `200` ký tự | Điều kiện thu gom. |

##### Ví dụ Request Body:
```json
{
  "items": [
    {
      "wasteCategoryId": "a4444444-4444-4444-4444-444444444444",
      "isActive": true,
      "pricePerKg": 7200,
      "minQuantityKg": 1.0,
      "collectionConditions": "Chai nhựa PET sạch, đã ép dẹp"
    },
    {
      "wasteCategoryId": "a1111111-1111-1111-1111-111111111111",
      "isActive": true,
      "pricePerKg": 4000,
      "minQuantityKg": 2.0,
      "collectionConditions": "Bìa carton khô ráo, đã gấp phẳng"
    }
  ]
}
```

#### b. Các Thông báo Trả về

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `null` | `BR-MTH-03` | Lưu và công bố toàn bộ bảng giá thành công | `"Đã lưu và công bố bảng giá."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-MTH-03` | Mảng `items` có 2 phần tử trùng `wasteCategoryId` | `"Danh sách có loại phế liệu bị trùng lặp."` |
| **`400 Bad Request`** | `MSG-MTH-06` | `BR-MTH-03` | Cơ sở đang `Online` nhưng sau khi lưu không còn dòng giá nào ở trạng thái `Active` | `"Vui lòng thêm ít nhất một loại phế liệu nhận mua và một vị trí hợp lệ trước khi hoạt động."` |
| **`404 Not Found`** | `MSG-NOT-FOUND` | — | Có `wasteCategoryId` không tồn tại trong hệ thống | `"Có loại phế liệu không tồn tại trong danh mục hệ thống."` |

---

### 3.8. Xem Trạng thái Hoạt động & Lịch tuần (`GET /api/v1/recycler/availability`)

* **Mô tả:** Lấy trạng thái sẵn sàng nhận đơn (`isOnline`, `activeStatus`), lý do tạm nghỉ (`busyReason`), chuỗi tóm tắt giờ mở cửa (`operatingHours`), mảng lịch hoạt động theo tuần (`weeklySchedule`) và số lượng loại phế liệu đang nhận thu mua (`activePriceCount`).
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`

#### a. Response Parameters (`ApiResponse<AvailabilityDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `isOnline` | `boolean` | `true` nếu `activeStatus == "Online"`, `false` nếu `"Offline"`. |
| `activeStatus` | `string` | `"Online"` hoặc `"Offline"`. |
| `verificationStatus` | `string` | `"Pending"`, `"Verified"`, hoặc `"Rejected"`. |
| `busyReason` | `string?` | Ghi chú lý do tạm nghỉ khi `isOnline == false`. |
| `operatingHours` | `string` | Chuỗi tóm tắt giờ mở cửa (VD: `"T2-T6: 08:00 - 17:30; T7: 08:00 - 12:00"`). |
| `weeklySchedule` | `ScheduleDayDto[]` | Mảng lịch hoạt động chi tiết từng ngày trong tuần (`dayOfWeek`: `0=CN` .. `6=T7`, `isClosed`, `openTime`, `closeTime`). |
| `activePriceCount` | `integer` | Số lượng loại phế liệu đang có trạng thái `Active`. |

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "messageCode": null,
  "message": null,
  "data": {
    "isOnline": true,
    "activeStatus": "Online",
    "verificationStatus": "Verified",
    "busyReason": null,
    "operatingHours": "T2-T6: 08:00 - 17:30; T7: 08:00 - 12:00",
    "weeklySchedule": [
      { "dayOfWeek": 1, "isClosed": false, "openTime": "08:00", "closeTime": "17:30" },
      { "dayOfWeek": 2, "isClosed": false, "openTime": "08:00", "closeTime": "17:30" },
      { "dayOfWeek": 3, "isClosed": false, "openTime": "08:00", "closeTime": "17:30" },
      { "dayOfWeek": 4, "isClosed": false, "openTime": "08:00", "closeTime": "17:30" },
      { "dayOfWeek": 5, "isClosed": false, "openTime": "08:00", "closeTime": "17:30" },
      { "dayOfWeek": 6, "isClosed": false, "openTime": "08:00", "closeTime": "12:00" },
      { "dayOfWeek": 0, "isClosed": true, "openTime": null, "closeTime": null }
    ],
    "activePriceCount": 4
  },
  "errors": null,
  "timestamp": "2026-10-31T09:15:00Z"
}
```

---

### 3.9. Bật / Tắt Trạng thái Hoạt động Online/Offline (`PATCH /api/v1/recycler/availability`)

* **Mô tả:** Thực hiện `Use Case R-02` (*Toggle Active Status*):
  * **Khi `isOnline = true`:** Kiểm tra hồ sơ đã `Verified` (`BR-MTH-01`), có tọa độ GPS hợp lệ và có ít nhất 1 loại phế liệu đang `Active` (`BR-MTH-03`). Đặt `ActiveStatus = Online`, xóa `BusyReason = null` để cơ sở hiển thị ngay trên bản đồ tìm kiếm của người dân (`U-05`).
  * **Khi `isOnline = false`:** Luôn cho phép chuyển sang `ActiveStatus = Offline` kèm ghi chú `busyReason` (nếu có). Theo `BR-MTH-06`, cơ sở chỉ ẩn khỏi tìm kiếm mới trên bản đồ, các đơn hẹn đang xử lý không bị hủy.

#### a. Request Parameters (`application/json` – `UpdateAvailabilityRequest`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `isOnline` | Body | `boolean` | **Có** | `true` hoặc `false` | `true`: Bật hoạt động (`Online`).<br>`false`: Tạm nghỉ (`Offline`). |
| `busyReason` | Body | `string?` | Không | Tối đa `100` ký tự | Lý do tạm nghỉ hiển thị khi `isOnline = false` (VD: `"Kho đã đầy, tạm ngưng nhận đến 14:00"`). |

##### Ví dụ 1 – Request Body Bật `Online`:
```json
{
  "isOnline": true,
  "busyReason": null
}
```

##### Ví dụ 2 – Request Body Tắt `Offline` kèm ghi chú bận:
```json
{
  "isOnline": false,
  "busyReason": "Kho bãi đang bảo trì cân điện tử đến 15:00 chiều nay"
}
```

#### b. Các Thông báo Trả về

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `null` | `BR-MTH-03` | `isOnline == true` thành công | `"Cơ sở đã chuyển sang trạng thái hoạt động."` |
| **`200 OK`** | `null` | `BR-MTH-06` | `isOnline == false` thành công | `"Cơ sở đã chuyển sang trạng thái tạm nghỉ."` |
| **`400 Bad Request`** | `MSG-MTH-06` | `BR-MTH-03` | Bật `isOnline == true` khi `activePriceCount == 0` hoặc chưa có tọa độ GPS hợp lệ | `"Vui lòng thêm ít nhất một loại phế liệu nhận mua và một vị trí hợp lệ trước khi hoạt động."` |
| **`400 Bad Request`** | `MSG-MTH-03` / `MSG-MTH-05` | `BR-MTH-01` | Bật `isOnline == true` khi hồ sơ đang `Pending` hoặc `Rejected` | `"Hồ sơ cơ sở đang chờ Staff phê duyệt..."` / `"Hồ sơ cơ sở chưa được phê duyệt..."` |

---

### 3.10. Cập nhật Lịch Hoạt động Cố định theo Tuần (`PUT /api/v1/recycler/availability/schedule`)

* **Mô tả:** Cập nhật lịch đóng/mở cửa theo từng ngày trong tuần (`0 = Chủ nhật`, `1 = Thứ hai`, ..., `6 = Thứ bảy`). Hệ thống tự động chuẩn hóa thứ tự từ Thứ hai đến Chủ nhật, lưu JSON vào `weekly_schedule` và sinh chuỗi tóm tắt thông minh vào `operating_hours` (gộp từ 3 ngày liên tiếp có cùng khung giờ thành dạng `"T2-T6: 08:00 - 17:30"`, tối đa 100 ký tự).

#### a. Request Parameters (`application/json` – `UpdateScheduleRequest`)

| Tên tham số | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---|:---|
| `days` | `ScheduleDayDto[]` | **Có** | `1 .. 7` phần tử, không trùng `dayOfWeek` | Danh sách lịch hoạt động các ngày trong tuần. |
| `days[].dayOfWeek` | `integer` | **Có** | `0 <= dayOfWeek <= 6` | `0`: Chủ nhật (`CN`), `1`: Thứ hai (`T2`), ..., `6`: Thứ bảy (`T7`). |
| `days[].isClosed` | `boolean` | **Có** | `true` hoặc `false` | `true` nếu ngày đó đóng cửa nghỉ. |
| `days[].openTime` | `string?` | Bắt buộc nếu `isClosed == false` | Định dạng `"HH:mm"` (24h, VD: `"08:00"`) | Giờ mở cửa. |
| `days[].closeTime` | `string?` | Bắt buộc nếu `isClosed == false` | Định dạng `"HH:mm"` và phải lớn hơn `openTime` | Giờ đóng cửa. |

##### Ví dụ Request Body:
```json
{
  "days": [
    { "dayOfWeek": 1, "isClosed": false, "openTime": "08:00", "closeTime": "17:30" },
    { "dayOfWeek": 2, "isClosed": false, "openTime": "08:00", "closeTime": "17:30" },
    { "dayOfWeek": 3, "isClosed": false, "openTime": "08:00", "closeTime": "17:30" },
    { "dayOfWeek": 4, "isClosed": false, "openTime": "08:00", "closeTime": "17:30" },
    { "dayOfWeek": 5, "isClosed": false, "openTime": "08:00", "closeTime": "17:30" },
    { "dayOfWeek": 6, "isClosed": false, "openTime": "08:00", "closeTime": "12:00" },
    { "dayOfWeek": 0, "isClosed": true, "openTime": null, "closeTime": null }
  ]
}
```

#### b. Các Thông báo Trả về

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `null` | `BR-MTH-03` | Lưu lịch tuần và cập nhật `operatingHours` thành công | `"Đã cập nhật lịch hoạt động."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-MTH-03` | Khai báo trùng `dayOfWeek` trong mảng `days` | `"Mỗi ngày trong tuần chỉ được khai báo một lần."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-MTH-03` | `openTime` hoặc `closeTime` sai định dạng `"HH:mm"` | `"Giờ mở/đóng cửa của {Thứ} phải có dạng HH:mm (ví dụ 08:00)."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-MTH-03` | `closeTime <= openTime` | `"Giờ đóng cửa của {Thứ} phải sau giờ mở cửa."` |

