# TÀI LIỆU ĐẶC TẢ API – MODULE 2: ĐĂNG KÝ NÂNG CẤP & THẨM ĐỊNH CƠ SỞ THU GOM (RECYCLER ONBOARDING & VERIFICATION)
**Mã phân hệ:** `MTH` (`Use Case U-17` & `Use Case S-04`)  
**Controller:** [`RecyclerApplicationsController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerApplicationsController.cs)  
**Services thực thi:** [`RecyclerOnboardingService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/RecyclerOnboardingService.cs), [`CloudflareR2StorageService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/CloudflareR2StorageService.cs)  
**Validators:** [`RecyclerOnboardingValidators.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/RecyclerOnboardingValidators.cs)  
**DTOs:** [`RecyclerOnboardingDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/RecyclerOnboardingDtos.cs)  
**Base Route:** `/api/v1/recycler-applications`

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS

| STT | Phương thức | Endpoint URI | Quyền truy cập | Use Case | Chức năng nghiệp vụ | Mã thông báo chính |
|:---:|:---:|:---|:---:|:---:|:---|:---|
| 1 | `POST` | `/api/v1/recycler-applications/upload-document` | `[Authorize]` (`Citizen`) | `U-17` | Tải tệp Giấy phép kinh doanh hoặc Ảnh mặt tiền kho bãi lên **Cloudflare R2 Object Storage** (AWS SigV4). | `MSG-GEN-01` |
| 2 | `POST` | `/api/v1/recycler-applications` | `[Authorize]` (`Citizen`) | `U-17` | Nộp mới hoặc nộp lại hồ sơ đăng ký nâng cấp từ `Citizen` lên `Recycler` kèm tọa độ GPS PostGIS (`SRID 4326`). | `MSG-MTH-03`, `MSG-MTH-04`, `MSG-GEN-01`, `MSG-AUTH-04` |
| 3 | `GET` | `/api/v1/recycler-applications/me` | `[Authorize]` (`Citizen`/`Recycler`) | `U-17` | Xem trạng thái hồ sơ đăng ký nâng cấp của chính mình (`Pending`, `Verified`, `Rejected` kèm lý do). | `MSG-GEN-01`, `MSG-NOT-FOUND` |
| 4 | `GET` | `/api/v1/recycler-applications` | `[Authorize(Roles = "Staff,Admin")]` | `S-04` | Lấy danh sách phân trang các hồ sơ đăng ký `Recycler` (lọc theo `status`, `keyword`, sắp xếp ưu tiên đơn nộp trước FIFO). | `200 OK` |
| 5 | `GET` | `/api/v1/recycler-applications/{userId:guid}` | `[Authorize(Roles = "Staff,Admin")]` | `S-04` | Xem chi tiết hồ sơ, giấy phép pháp lý, ảnh cơ sở và tọa độ GPS của một ứng viên cụ thể. | `200 OK`, `MSG-NOT-FOUND` |
| 6 | `PUT` | `/api/v1/recycler-applications/{userId:guid}/verification` | `[Authorize(Roles = "Staff,Admin")]` | `S-04` | Phê duyệt (`Approve` $\rightarrow$ nâng `User.Role = Recycler`) hoặc Từ chối (`Reject` bắt buộc kèm lý do `BR-MTH-02`). | `MSG-MTH-04`, `MSG-MTH-05`, `MSG-GEN-01` |

---

## 2. LUỒNG NGHIỆP VỤ TỔNG THỂ (`U-17` $\rightarrow$ `S-04`)

1. **Bước 1 (`U-17` – Upload chứng từ):** Người dùng đã đăng nhập với vai trò `Citizen` gọi `POST /upload-document` 2 lần để tải lên:
   * Giấy phép kinh doanh / CCCD hộ kinh doanh (`documentType = "permit"`) $\rightarrow$ nhận về `legalPermitUrl`.
   * Ảnh chụp thực tế mặt tiền cơ sở / kho bãi (`documentType = "facility-photo"`) $\rightarrow$ nhận về `businessPhotoUrl`.
2. **Bước 2 (`U-17` – Nộp đơn nâng cấp):** Gọi `POST /api/v1/recycler-applications` kèm tọa độ GPS (`latitude`, `longitude`) và 2 URL chứng từ ở Bước 1. Hệ thống lưu bản ghi `RecyclerProfile` với `VerificationStatus = Pending`, `ActiveStatus = Hidden` (`BR-MTH-01`: chưa xuất hiện trên bản đồ tìm kiếm).
3. **Bước 3 (`S-04` – Staff thẩm định):** Nhân viên vận hành (`Staff` / `Admin`) gọi `GET /api/v1/recycler-applications?status=Pending` để xem hàng đợi và gọi `PUT /{userId}/verification`:
   * **Nếu Phê duyệt (`isApproved = true`):** Hệ thống chuyển `VerificationStatus = Verified`, `ActiveStatus = Available`, nâng cấp **`User.Role = Recycler`**, và gửi email chúc mừng (`MSG-MTH-04`).
   * **Nếu Từ chối (`isApproved = false`):** Bắt buộc nhập `rejectionReason` ($\ge 10$ ký tự, `BR-MTH-02`), hệ thống chuyển `VerificationStatus = Rejected`, giữ nguyên `User.Role = Citizen`, và gửi email thông báo lý do từ chối để người dùng sửa và nộp lại (`MSG-MTH-05`).

---

## 3. CHI TIẾT TỪNG API ENDPOINT

---

### 3.1. Tải chứng từ / Ảnh cơ sở lên Cloudflare R2 (`POST /api/v1/recycler-applications/upload-document`)

* **Mô tả:** Nhận tệp tin qua `multipart/form-data`, kiểm tra định dạng và dung lượng, ký xác thực chuẩn **AWS Signature Version 4 (`HMAC-SHA256`)** và tải trực tiếp lên bucket `ecolink` trên Cloudflare R2 Object Storage.
* **Header bắt buộc:** `Authorization: Bearer <accessToken>`

#### a. Request Parameters (`multipart/form-data`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc / Giá trị hợp lệ | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `file` | Form-Data | `IFormFile` | **Có** | Định dạng: `.jpg`, `.jpeg`, `.png`, `.webp`, `.pdf`<br>Dung lượng: `> 0 bytes` và `<= 5 MB` (`5,242,880 bytes`) | Tệp tin hình ảnh hoặc tài liệu PDF cần tải lên. |
| `documentType` | Form-Data | `string?` | Không | `"permit"` *(mặc định)* hoặc `"facility-photo"` | Phân loại thư mục lưu trữ trên R2:<br>• `"permit"` $\rightarrow$ `recycler-kyb/legal-permits/`<br>• `"facility-photo"` $\rightarrow$ `recycler-kyb/facility-photos/` |

#### b. Response Parameters (`ApiResponse<RecyclerDocumentUploadResponseDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `fileUrl` | `string` | Đường dẫn URL công khai (Public URL) của tệp trên Cloudflare R2 để truyền vào API nộp hồ sơ. |
| `objectKey` | `string` | Khóa định danh của đối tượng trong R2 bucket (VD: `"recycler-kyb/legal-permits/20261030_a1b2c3d4.pdf"`). |
| `contentType` | `string` | Kiểu MIME của tệp (VD: `"image/jpeg"`, `"application/pdf"`). |
| `sizeBytes` | `long` | Kích thước tệp tính bằng byte. |
| `uploadedAt` | `string (ISO 8601)` | Thời điểm tải lên thành công (UTC). |

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "messageCode": "MSG-GEN-01",
  "message": "Tải chứng từ lên Cloudflare R2 thành công.",
  "data": {
    "fileUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/legal-permits/20261030_8f9a1b2c.pdf",
    "objectKey": "recycler-kyb/legal-permits/20261030_8f9a1b2c.pdf",
    "contentType": "application/pdf",
    "sizeBytes": 412850,
    "uploadedAt": "2026-10-30T08:40:12Z"
  },
  "errors": null,
  "timestamp": "2026-10-30T08:40:12Z"
}
```

#### c. Các Thông báo Trả về (Response Messages & Error Codes)

| HTTP Status | Mã `messageCode` | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-GEN-01` | Tải tệp lên Cloudflare R2 thành công | `"Tải chứng từ lên Cloudflare R2 thành công."` |
| **`400 Bad Request`** | `MSG-GEN-01` | Không đính kèm `file` hoặc `file.Length == 0` | `"Vui lòng chọn tệp chứng từ hoặc ảnh cơ sở để tải lên."` |
| **`400 Bad Request`** | `MSG-GEN-01` | Tệp vượt quá dung lượng tối đa 5 MB | `"Dung lượng tệp vượt quá giới hạn cho phép (Tối đa 5 MB)."` |
| **`400 Bad Request`** | `MSG-GEN-01` | Đuôi file không thuộc `.jpg`, `.jpeg`, `.png`, `.webp`, `.pdf` | `"Định dạng tệp không được hỗ trợ. Chỉ chấp nhận .jpg, .jpeg, .png, .webp hoặc .pdf."` |
| **`401 Unauthorized`** | `MSG-UNAUTHORIZED` | Chưa đăng nhập hoặc thiếu Bearer Token | — |

---

### 3.2. Nộp / Nộp lại Hồ sơ Đăng ký Nâng cấp Recycler (`POST /api/v1/recycler-applications`)

* **Mô tả:** Cho phép người dùng (`Citizen`) gửi hồ sơ đăng ký cơ sở thu gom phế liệu (`U-17`).
  * Nếu nộp lần đầu: Tạo mới `RecyclerProfile` với tọa độ không gian PostGIS `Point(Longitude, Latitude) { SRID = 4326 }`, đặt `VerificationStatus = Pending` và `ActiveStatus = Hidden`.
  * Nếu hồ sơ trước đó bị từ chối (`VerificationStatus == Rejected`): Cho phép cập nhật thông tin/chứng từ mới, xóa `RejectionReason` cũ và chuyển trạng thái về lại `Pending` (`BR-MTH-02`).
* **Quy tắc nghiệp vụ áp dụng:** `BR-MTH-01`, `BR-MTH-02`, `BR-AUTH-05`.

#### a. Request Parameters (`application/json` – `ApplyRecyclerUpgradeRequest`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc (FluentValidation) | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `facilityName` | Body | `string` | **Có** | `3 - 150` ký tự | Tên cơ sở / vựa thu mua phế liệu. |
| `representativeName` | Body | `string` | **Có** | Tối đa `100` ký tự | Họ và tên người đại diện pháp luật / chủ cơ sở. |
| `businessAddress` | Body | `string` | **Có** | Tối đa `255` ký tự | Địa chỉ kinh doanh thực tế của kho bãi / cơ sở. |
| `latitude` | Body | `double` | **Có** | `-90.0 <= lat <= 90.0` (khác `0`) | Vĩ độ GPS của cơ sở thu gom. |
| `longitude` | Body | `double` | **Có** | `-180.0 <= lng <= 180.0` (khác `0`) | Kinh độ GPS của cơ sở thu gom. |
| `contactPhone` | Body | `string` | **Có** | Regex `^(0|\+84)[0-9]{8,10}$` | Số điện thoại liên hệ thu mua của cơ sở. |
| `operatingHours` | Body | `string?` | Không | Mặc định `"08:00 - 18:00"` | Khung giờ mở cửa hoạt động trong ngày. |
| `description` | Body | `string?` | Không | Tùy chọn | Mô tả giới thiệu về cơ sở, năng lực thu gom, loại phế liệu nhận mua. |
| `legalPermitUrl` | Body | `string` | **Có** | URL hợp lệ (`http://` hoặc `https://`) | URL ảnh/PDF giấy phép đăng ký kinh doanh (từ API `upload-document`). |
| `businessPhotoUrl` | Body | `string` | **Có** | URL hợp lệ (`http://` hoặc `https://`) | URL ảnh chụp thực tế mặt tiền cơ sở/kho bãi (từ API `upload-document`). |

##### Ví dụ Request Body:
```json
{
  "facilityName": "Vựa Phế Liệu Xanh Thủ Đức",
  "representativeName": "Trần Văn Thu Gom",
  "businessAddress": "245 Võ Văn Ngân, Phường Linh Chiểu, TP. Thủ Đức, TP.HCM",
  "latitude": 10.8505,
  "longitude": 106.7717,
  "contactPhone": "0909123456",
  "operatingHours": "07:30 - 18:30",
  "description": "Chuyên thu mua nhựa PET, bìa carton, giấy báo và vỏ lon nhôm số lượng lớn tận nơi.",
  "legalPermitUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/legal-permits/20261030_8f9a1b2c.pdf",
  "businessPhotoUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/facility-photos/20261030_9d8e7f6a.jpg"
}
```

#### b. Response Parameters (`ApiResponse<RecyclerApplicationDetailDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `userId` | `Guid` | ID tài khoản người nộp đơn (`recycler_profiles.user_id`). |
| `username` | `string` | Tên đăng nhập của người nộp đơn. |
| `email` | `string` | Địa chỉ email của người nộp đơn. |
| `applicantFullName` | `string` | Họ và tên chủ tài khoản. |
| `currentRole` | `string` | Vai trò hiện tại của tài khoản (`"Citizen"` khi đang chờ duyệt, `"Recycler"` sau khi được duyệt). |
| `facilityName` | `string` | Tên cơ sở thu gom. |
| `representativeName` | `string?` | Tên người đại diện pháp luật. |
| `businessAddress` | `string` | Địa chỉ kinh doanh. |
| `latitude` | `decimal` | Vĩ độ GPS (làm tròn 6 chữ số thập phân). |
| `longitude` | `decimal` | Kinh độ GPS (làm tròn 6 chữ số thập phân). |
| `operatingHours` | `string` | Giờ mở cửa hoạt động. |
| `contactPhone` | `string` | Số điện thoại liên hệ của cơ sở. |
| `description` | `string?` | Mô tả cơ sở. |
| `legalPermitUrl` | `string?` | Đường dẫn chứng từ pháp lý trên Cloudflare R2. |
| `businessPhotoUrl` | `string?` | Đường dẫn ảnh mặt tiền kho bãi trên Cloudflare R2. |
| `verificationStatus` | `string` | Trạng thái thẩm định: `"Pending"`, `"Verified"`, hoặc `"Rejected"`. |
| `activeStatus` | `string` | Trạng thái hiển thị trên bản đồ: `"Hidden"`, `"Available"`, `"Busy"`, `"TemporarilyClosed"`. |
| `rejectionReason` | `string?` | Lý do từ chối của Staff (nếu `verificationStatus == "Rejected"`). |
| `submittedAt` | `string (ISO 8601)` | Thời điểm nộp hồ sơ lần đầu (`CreatedAt`). |
| `reviewedAt` | `string? (ISO 8601)` | Thời điểm Staff duyệt/từ chối gần nhất (`UpdatedAt`). |
| `reviewedBy` | `string?` | Tên đăng nhập của Staff/Admin đã thẩm định (`UpdatedBy`). |

##### Ví dụ Response Thành công (`201 Created`):
```json
{
  "success": true,
  "messageCode": "MSG-MTH-03",
  "message": "Hồ sơ cơ sở của bạn đang được xem xét và sẽ xuất hiện trong tìm kiếm sau khi được duyệt.",
  "data": {
    "userId": "10000000-0000-0000-0000-000000000001",
    "username": "citizen",
    "email": "citizen@ecolink.vn",
    "applicantFullName": "Nguyễn Văn Xanh (Người dân)",
    "currentRole": "Citizen",
    "facilityName": "Vựa Phế Liệu Xanh Thủ Đức",
    "representativeName": "Trần Văn Thu Gom",
    "businessAddress": "245 Võ Văn Ngân, Phường Linh Chiểu, TP. Thủ Đức, TP.HCM",
    "latitude": 10.8505,
    "longitude": 106.7717,
    "operatingHours": "07:30 - 18:30",
    "contactPhone": "0909123456",
    "description": "Chuyên thu mua nhựa PET, bìa carton, giấy báo và vỏ lon nhôm số lượng lớn tận nơi.",
    "legalPermitUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/legal-permits/20261030_8f9a1b2c.pdf",
    "businessPhotoUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/facility-photos/20261030_9d8e7f6a.jpg",
    "verificationStatus": "Pending",
    "activeStatus": "Hidden",
    "rejectionReason": null,
    "submittedAt": "2026-10-30T08:42:00Z",
    "reviewedAt": null,
    "reviewedBy": null
  },
  "errors": null,
  "timestamp": "2026-10-30T08:42:00Z"
}
```

#### c. Các Thông báo Trả về (Response Messages & Error Codes)

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`201 Created`** | `MSG-MTH-03` | `BR-MTH-01` | Nộp hồ sơ mới hoặc nộp lại hồ sơ sau khi bị từ chối thành công | `"Hồ sơ cơ sở của bạn đang được xem xét và sẽ xuất hiện trong tìm kiếm sau khi được duyệt."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-MTH-01` | Thiếu tên cơ sở, tọa độ GPS ngoài khoảng `[-90..90]` / `[-180..180]`, SĐT sai định dạng, hoặc thiếu URL chứng từ | `"Dữ liệu hồ sơ đăng ký không hợp lệ."` *(kèm danh sách lỗi chi tiết trong `errors`)* |
| **`400 Bad Request`** | `MSG-MTH-03` | `BR-MTH-01` | Người dùng đã có một hồ sơ đang ở trạng thái chờ duyệt (`VerificationStatus == Pending`) | `"Hồ sơ cơ sở của bạn đang được xem xét và sẽ xuất hiện trong tìm kiếm sau khi được duyệt."` |
| **`400 Bad Request`** | `MSG-MTH-04` | — | Tài khoản đã là `Recycler` hoặc hồ sơ đã được phê duyệt (`VerificationStatus == Verified`) | `"Hồ sơ cơ sở của bạn đã được duyệt và hiện hiển thị với người dùng."` |
| **`400 Bad Request`** | `MSG-AUTH-04` | `BR-AUTH-05` | Tài khoản người nộp đơn đang bị `Suspended` hoặc `Deactivated` | `"Tài khoản này hiện không còn hoạt động. Vui lòng liên hệ bộ phận hỗ trợ."` |

---

### 3.3. Xem trạng thái Hồ sơ của Chính mình (`GET /api/v1/recycler-applications/me`)

* **Mô tả:** Người dùng (`Citizen` hoặc `Recycler`) kiểm tra tiến độ xét duyệt hồ sơ nâng cấp của mình (`Pending`, `Verified`, hoặc `Rejected` kèm `rejectionReason` để chỉnh sửa và nộp lại).
* **Header bắt buộc:** `Authorization: Bearer <accessToken>`

#### a. Request Parameters
Không có tham số Query hay Body (Định danh `userId` được trích xuất tự động từ claim `sub` / `NameIdentifier` trong JWT Bearer Token).

#### b. Response Parameters
Trả về `ApiResponse<RecyclerApplicationDetailDto>` (cấu trúc giống mục 3.2.b).

#### c. Các Thông báo Trả về

| HTTP Status | Mã `messageCode` | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-GEN-01` | Tìm thấy hồ sơ đăng ký của người dùng hiện tại | `"Lấy thông tin hồ sơ thành công."` |
| **`404 Not Found`** | `MSG-NOT-FOUND` | Người dùng chưa từng gửi hồ sơ đăng ký nâng cấp lên `Recycler` | `"Bạn chưa gửi hồ sơ đăng ký nâng cấp cơ sở thu gom (Recycler)."` |
| **`401 Unauthorized`** | `MSG-UNAUTHORIZED` | Không truyền hoặc truyền JWT Token hết hạn | — |

---

### 3.4. [Staff/Admin] Lấy Danh sách Hồ sơ Đăng ký Recycler (`GET /api/v1/recycler-applications`)

* **Mô tả:** Phục vụ Use Case `S-04`, cho phép `Staff` và `Admin` truy vấn danh sách các hồ sơ đăng ký `Recycler` có phân trang, lọc theo trạng thái thẩm định (`Pending`, `Verified`, `Rejected`) và tìm kiếm từ khóa. Mặc định sắp xếp tăng dần theo thời gian nộp (`sortOrder = "asc"`) để ưu tiên duyệt đơn nộp trước (FIFO).
* **Phân quyền:** `[Authorize(Roles = "Staff,Admin")]`

#### a. Request Parameters (Query String – `RecyclerApplicationFilterRequest`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---:|:---|
| `status` | Query | `string?` | Không | `null` (Tất cả) | Lọc theo trạng thái thẩm định: `"Pending"`, `"Verified"`, hoặc `"Rejected"`. |
| `keyword` | Query | `string?` | Không | `null` | Từ khóa tìm kiếm theo tên cơ sở, địa chỉ, tên người đại diện, SĐT, email hoặc username. |
| `pageNumber` | Query | `integer` | Không | `1` | Số trang hiện tại ($\ge 1$). |
| `pageSize` | Query | `integer` | Không | `10` | Số bản ghi mỗi trang ($1 \le \text{pageSize} \le 100$). |
| `sortBy` | Query | `string` | Không | `"createdAt"` | Trường sắp xếp (`"createdAt"` hoặc `"facilityName"`). |
| `sortOrder` | Query | `string` | Không | `"asc"` | Thứ tự sắp xếp: `"asc"` (đơn cũ lên trước - FIFO) hoặc `"desc"` (đơn mới lên trước). |

##### Ví dụ Request URL:
```http
GET /api/v1/recycler-applications?status=Pending&pageNumber=1&pageSize=10&sortBy=createdAt&sortOrder=asc
Authorization: Bearer <staff_or_admin_jwt_token>
```

#### b. Response Parameters (`ApiResponse<PagedResult<RecyclerApplicationDetailDto>>`)

```json
{
  "success": true,
  "messageCode": null,
  "message": null,
  "data": {
    "items": [
      {
        "userId": "10000000-0000-0000-0000-000000000001",
        "username": "citizen",
        "email": "citizen@ecolink.vn",
        "applicantFullName": "Nguyễn Văn Xanh (Người dân)",
        "currentRole": "Citizen",
        "facilityName": "Vựa Phế Liệu Xanh Thủ Đức",
        "representativeName": "Trần Văn Thu Gom",
        "businessAddress": "245 Võ Văn Ngân, Phường Linh Chiểu, TP. Thủ Đức, TP.HCM",
        "latitude": 10.8505,
        "longitude": 106.7717,
        "operatingHours": "07:30 - 18:30",
        "contactPhone": "0909123456",
        "description": "Chuyên thu mua nhựa PET, bìa carton...",
        "legalPermitUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/legal-permits/20261030_8f9a1b2c.pdf",
        "businessPhotoUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/facility-photos/20261030_9d8e7f6a.jpg",
        "verificationStatus": "Pending",
        "activeStatus": "Hidden",
        "rejectionReason": null,
        "submittedAt": "2026-10-30T08:42:00Z",
        "reviewedAt": null,
        "reviewedBy": null
      }
    ],
    "pageNumber": 1,
    "pageSize": 10,
    "totalRecords": 1,
    "totalPages": 1,
    "hasPreviousPage": false,
    "hasNextPage": false
  },
  "errors": null,
  "timestamp": "2026-10-30T08:45:00Z"
}
```

#### c. Các Thông báo Trả về

| HTTP Status | Điều kiện kích hoạt | Nội dung phản hồi |
|:---:|:---|:---|
| **`200 OK`** | Truy vấn danh sách thành công | `ApiResponse<PagedResult<RecyclerApplicationDetailDto>>` |
| **`401 Unauthorized`** | Chưa đăng nhập | — |
| **`403 Forbidden`** | Tài khoản đăng nhập có vai trò `Citizen` hoặc `Recycler` (không phải `Staff` / `Admin`) | — |

---

### 3.5. [Staff/Admin] Xem Chi tiết Một Hồ sơ theo `userId` (`GET /api/v1/recycler-applications/{userId:guid}`)

* **Mô tả:** Cho phép `Staff` / `Admin` xem toàn bộ thông tin chi tiết, chứng từ pháp lý (`legalPermitUrl`), ảnh mặt tiền (`businessPhotoUrl`) và tọa độ GPS của một hồ sơ cụ thể để đối chiếu trước khi ra quyết định thẩm định.
* **Phân quyền:** `[Authorize(Roles = "Staff,Admin")]`

#### a. Request Parameters

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|
| `userId` | Path (Route) | `Guid` | **Có** | Mã định danh `UserId` của người nộp đơn cần xem hồ sơ. |

#### b. Response Parameters & Thông báo Trả về

| HTTP Status | Mã `messageCode` | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---|:---|
| **`200 OK`** | `null` | Tìm thấy hồ sơ theo `userId` | Trả về `RecyclerApplicationDetailDto` trong `data`. |
| **`404 Not Found`** | `MSG-NOT-FOUND` | Không tồn tại hồ sơ `RecyclerProfile` ứng với `userId` | `"Không tìm thấy hồ sơ đăng ký cơ sở thu gom cho người dùng '{userId}'."` |
| **`403 Forbidden`** | — | Người gọi không có quyền `Staff` hoặc `Admin` | — |

---

### 3.6. [Staff/Admin] Phê duyệt hoặc Từ chối Hồ sơ Recycler (`PUT /api/v1/recycler-applications/{userId:guid}/verification`)

* **Mô tả:** Thực hiện quyết định thẩm định của `Staff` / `Admin` (`Use Case S-04`):
  * **Khi `isApproved = true` (Phê duyệt):**
    1. Chuyển `RecyclerProfile.VerificationStatus = Verified`, `ActiveStatus = Available`, xóa `RejectionReason = null`.
    2. Nâng cấp vai trò tài khoản ứng viên từ `Citizen` $\rightarrow$ **`User.Role = Recycler`** (`User.Status = Active`).
    3. Ghi vết kiểm toán (`UpdatedAt`, `UpdatedBy` = username của Staff/Admin theo `BR-ADM-05`).
    4. Gửi email thông báo phê duyệt tới ứng viên (`BR-NTF-02`).
  * **Khi `isApproved = false` (Từ chối):**
    1. Bắt buộc kiểm tra `rejectionReason` không được để trống và tối thiểu 10 ký tự (`BR-MTH-02`).
    2. Chuyển `RecyclerProfile.VerificationStatus = Rejected`, `ActiveStatus = Hidden`, lưu `RejectionReason`.
    3. Giữ nguyên `User.Role = Citizen` để người dùng có thể chỉnh sửa và nộp lại qua `POST /api/v1/recycler-applications`.
    4. Gửi email thông báo kèm lý do từ chối tới ứng viên.
* **Phân quyền:** `[Authorize(Roles = "Staff,Admin")]`

#### a. Request Parameters

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc (FluentValidation) | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `userId` | Path (Route) | `Guid` | **Có** | `Guid` hợp lệ | ID tài khoản người nộp đơn cần thẩm định. |
| `isApproved` | Body JSON | `boolean` | **Có** | `true` hoặc `false` | `true`: Phê duyệt nâng cấp lên `Recycler`.<br>`false`: Từ chối hồ sơ. |
| `rejectionReason` | Body JSON | `string?` | **Bắt buộc nếu `isApproved == false`** | Từ `10` đến `500` ký tự khi từ chối (`BR-MTH-02`) | Lý do từ chối cụ thể để người nộp đơn khắc phục và nộp lại. |

##### Ví dụ 1 – Request Body Phê duyệt (`Approve`):
```json
{
  "isApproved": true,
  "rejectionReason": null
}
```

##### Ví dụ 2 – Request Body Từ chối (`Reject`):
```json
{
  "isApproved": false,
  "rejectionReason": "Ảnh chụp giấy phép đăng ký hộ kinh doanh bị mờ dấu mộc đỏ và địa chỉ trên giấy phép không khớp với địa chỉ kho bãi."
}
```

#### b. Response Parameters (`ApiResponse<RecyclerApplicationDetailDto>`)

##### Ví dụ 1 – Response khi Phê duyệt (`200 OK` – `MSG-MTH-04`):
```json
{
  "success": true,
  "messageCode": "MSG-MTH-04",
  "message": "Hồ sơ cơ sở của bạn đã được duyệt và hiện hiển thị với người dùng.",
  "data": {
    "userId": "10000000-0000-0000-0000-000000000001",
    "username": "citizen",
    "email": "citizen@ecolink.vn",
    "applicantFullName": "Nguyễn Văn Xanh (Người dân)",
    "currentRole": "Recycler",
    "facilityName": "Vựa Phế Liệu Xanh Thủ Đức",
    "representativeName": "Trần Văn Thu Gom",
    "businessAddress": "245 Võ Văn Ngân, Phường Linh Chiểu, TP. Thủ Đức, TP.HCM",
    "latitude": 10.8505,
    "longitude": 106.7717,
    "operatingHours": "07:30 - 18:30",
    "contactPhone": "0909123456",
    "description": "Chuyên thu mua nhựa PET, bìa carton...",
    "legalPermitUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/legal-permits/20261030_8f9a1b2c.pdf",
    "businessPhotoUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/recycler-kyb/facility-photos/20261030_9d8e7f6a.jpg",
    "verificationStatus": "Verified",
    "activeStatus": "Available",
    "rejectionReason": null,
    "submittedAt": "2026-10-30T08:42:00Z",
    "reviewedAt": "2026-10-30T09:00:00Z",
    "reviewedBy": "admin"
  },
  "errors": null,
  "timestamp": "2026-10-30T09:00:00Z"
}
```

##### Ví dụ 2 – Response khi Từ chối (`200 OK` – `MSG-MTH-05`):
```json
{
  "success": true,
  "messageCode": "MSG-MTH-05",
  "message": "Hồ sơ cơ sở của bạn chưa được duyệt: Ảnh chụp giấy phép đăng ký hộ kinh doanh bị mờ dấu mộc đỏ. Vui lòng cập nhật và gửi lại.",
  "data": {
    "userId": "10000000-0000-0000-0000-000000000001",
    "username": "citizen",
    "email": "citizen@ecolink.vn",
    "applicantFullName": "Nguyễn Văn Xanh (Người dân)",
    "currentRole": "Citizen",
    "facilityName": "Vựa Phế Liệu Xanh Thủ Đức",
    "verificationStatus": "Rejected",
    "activeStatus": "Hidden",
    "rejectionReason": "Ảnh chụp giấy phép đăng ký hộ kinh doanh bị mờ dấu mộc đỏ.",
    "submittedAt": "2026-10-30T08:42:00Z",
    "reviewedAt": "2026-10-30T09:05:00Z",
    "reviewedBy": "admin"
  },
  "errors": null,
  "timestamp": "2026-10-30T09:05:00Z"
}
```

#### c. Các Thông báo Trả về (Response Messages & Error Codes)

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-MTH-04` | `BR-MTH-01` | `isApproved == true` (Phê duyệt hồ sơ và nâng cấp `Role = Recycler` thành công) | `"Hồ sơ cơ sở của bạn đã được duyệt và hiện hiển thị với người dùng."` |
| **`200 OK`** | `MSG-MTH-05` | `BR-MTH-02` | `isApproved == false` kèm `rejectionReason` hợp lệ (Từ chối hồ sơ thành công) | `"Hồ sơ cơ sở của bạn chưa được duyệt: {rejectionReason}. Vui lòng cập nhật và gửi lại."` |
| **`400 Bad Request`** | `MSG-MTH-05` | `BR-MTH-02` | `isApproved == false` nhưng `rejectionReason` để trống hoặc ngắn hơn 10 ký tự | `"Dữ liệu thẩm định không hợp lệ."` *(kèm lỗi `"Khi từ chối hồ sơ, bắt buộc phải ghi rõ lý do từ chối (BR-MTH-02)."`) * |
| **`400 Bad Request`** | `MSG-GEN-01` | — | Hồ sơ hiện tại không ở trạng thái `Pending` (đã `Verified` hoặc `Rejected` trước đó) | `"Chỉ có thể thẩm định hồ sơ đang ở trạng thái Chờ duyệt (Pending). Trạng thái hiện tại: {status}."` |
| **`404 Not Found`** | `MSG-NOT-FOUND` | — | Không tìm thấy hồ sơ ứng với `userId` | `"Không tìm thấy hồ sơ đăng ký cơ sở thu gom cho người dùng '{userId}'."` |
| **`403 Forbidden`** | — | `BR-ADM-02` | Tài khoản gọi API không có vai trò `Staff` hoặc `Admin` | — |

