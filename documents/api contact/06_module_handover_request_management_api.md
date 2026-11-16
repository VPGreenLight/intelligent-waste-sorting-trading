# TÀI LIỆU ĐẶC TẢ API – MODULE 7: QUẢN LÝ YÊU CẦU BÀN GIAO PHẾ LIỆU & LỊCH HẸN THU GOM (HANDOVER REQUEST MANAGEMENT)
**Mã phân hệ:** `HND` (`Use Cases U-06: Create Handover Request`, `U-07: Update / Cancel Handover Request`, `R-03: Accept / Reject Handover Request`, `R-04: Cancel Collection Appointment`, `JOB-03: Auto-Expire Pending Requests`)  
**Controllers:** [`HandoverRequestController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/HandoverRequestController.cs), [`RecyclerHandoverController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerHandoverController.cs)  
**Service thực thi & Background Job:** [`HandoverRequestService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverRequestService.cs) ([`IHandoverRequestService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Interfaces/IHandoverRequestService.cs)), [`HandoverExpirationJob.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Services/HandoverExpirationJob.cs)  
**Entities & Enums:** [`HandoverRequest.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/HandoverRequest.cs) (`HandoverRequest`, `HandoverRequestItem`), [`HandoverStatus.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Enums/HandoverStatus.cs)  
**DTOs:** [`HandoverDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/HandoverDtos.cs)  
**Base Routes:** `/api/v1/handover-requests` (Phía Người dân), `/api/v1/recycler/handover-requests` (Phía Cơ sở thu gom)

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS & TIẾN TRÌNH NỀN

### 1.1. Danh mục RESTful API Endpoints

| STT | Phương thức | Endpoint URI | Quyền truy cập (`[Authorize]`) | Use Case | Chức năng nghiệp vụ | Mã thông báo / Quy tắc |
|:---:|:---:|:---|:---:|:---:|:---|:---|
| 1 | `POST` | `/api/v1/handover-requests` | `User`, `Citizen` | `U-06` | Người dân tạo yêu cầu bàn giao phế liệu gửi tới một cơ sở thu gom (`Recycler`), sinh mã định danh `HND-yyyyMMdd-NNNN`. | `BR-HND-01`, `BR-HND-LIMIT`, `BR-NEG-04`, `MSG-HND-01` |
| 2 | `GET` | `/api/v1/handover-requests` | `User`, `Citizen` | `U-06`, `U-07` | Lấy danh sách yêu cầu bàn giao của người dân đang đăng nhập, hỗ trợ lọc theo trạng thái (`status`) và phân trang. | `200 OK`, `BR-HND-02`, `BR-HND-08` |
| 3 | `GET` | `/api/v1/handover-requests/{id:guid}` | `User`, `Citizen` (Chủ đơn) | `U-06`, `U-07` | Xem chi tiết một yêu cầu bàn giao của người dân. Số điện thoại cơ sở thu gom chỉ hiển thị sau khi đơn được `Accepted` (`BR-HND-08`). | `200 OK`, `BR-HND-08`, `MSG-NOT-FOUND` |
| 4 | `PUT` | `/api/v1/handover-requests/{id:guid}` | `User`, `Citizen` (Chủ đơn) | `U-07` | Chỉnh sửa danh sách phế liệu, ảnh chụp, khung giờ hẹn hoặc địa chỉ thu gom khi yêu cầu còn ở trạng thái `Pending`. | `BR-HND-01`, `BR-HND-02`, `BR-NEG-04`, `MSG-GEN-01` |
| 5 | `POST` | `/api/v1/handover-requests/{id:guid}/cancel` | `User`, `Citizen` (Chủ đơn) | `U-07` | Người dân hủy yêu cầu bàn giao kèm lý do bắt buộc. Nếu hủy trong vòng `4 giờ` trước lịch hẹn đã xác nhận sẽ bị ghi nhận hủy muộn (`isLateCancellation = true`). | `BR-HND-02`, `BR-HND-06`, `MSG-HND-05`, `MSG-HND-06` |
| 6 | `GET` | `/api/v1/recycler/handover-requests` | `Recycler`, `Collector` | `R-03`, `R-04` | Lấy danh sách yêu cầu bàn giao gửi đến chính cơ sở thu gom đang đăng nhập (`BR-HND-03`), lọc theo `status` và phân trang. | `200 OK`, `BR-HND-03`, `BR-HND-08` |
| 7 | `GET` | `/api/v1/recycler/handover-requests/{id:guid}` | `Recycler`, `Collector` (Nhận đơn) | `R-03`, `R-04` | Xem chi tiết yêu cầu bàn giao phía cơ sở thu gom. Địa chỉ chi tiết và SĐT người dân được ẩn khi đơn chưa `Accepted` (`BR-HND-08`); tuyệt đối không trả tọa độ GPS (`BR-MTH-05`). | `200 OK`, `BR-HND-03`, `BR-HND-08`, `BR-MTH-05` |
| 8 | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/accept` | `Recycler`, `Collector` (Nhận đơn) | `R-03` | Cơ sở thu gom chấp nhận yêu cầu bàn giao, khóa khung giờ hẹn chính thức (`confirmedTimeSlot = proposedTimeSlot`) và mở quyền xem thông tin liên hệ hai bên. | `BR-HND-02`, `BR-HND-03`, `BR-NEG-04`, `BR-NEG-05`, `BR-HND-08`, `MSG-HND-02` |
| 9 | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/reject` | `Recycler`, `Collector` (Nhận đơn) | `R-03` | Cơ sở thu gom từ chối yêu cầu bàn giao đang `Pending` / `Negotiating` kèm lý do bắt buộc (`1..500` ký tự). | `BR-HND-02`, `BR-HND-03`, `BR-HND-06`, `MSG-GEN-01` |
| 10 | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/cancel` | `Recycler`, `Collector` (Nhận đơn) | `R-04` | Cơ sở thu gom hủy lịch hẹn đã xác nhận (`Accepted` / `Scheduled`) trước khi thu gom kèm lý do bắt buộc. | `BR-HND-02`, `BR-HND-03`, `BR-HND-06`, `MSG-HND-05`, `MSG-HND-06` |

---

### 1.2. Tiến trình Nền Tự động (`BackgroundService`)

| Mã Job | Class Thực thi | Chu kỳ quét | Ngưỡng Timeout | Mô tả Nghiệp vụ |
|:---:|:---|:---:|:---:|:---|
| **`JOB-03`** | [`HandoverExpirationJob.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Services/HandoverExpirationJob.cs) | `30 phút / lần` | `24 giờ` (`ResponseTimeout`) | Tự động tìm các yêu cầu đang ở trạng thái `Pending` có `CreatedAt < UtcNow - 24h` và chuyển nguyên tử sang `Expired` bằng `ExecuteUpdateAsync`. Nếu `Recycler` vừa bấm `Accept` hoặc chuyển sang `Negotiating` trước đó, bản ghi được bảo toàn an toàn. |

---

## 2. LUỒNG NGHIỆP VỤ & QUY TẮC CỐT LÕI (`U-06`, `U-07`, `R-03`, `R-04`)

1. **Điều kiện gửi yêu cầu bàn giao (`BR-HND-01` & `BR-HND-LIMIT`):**
   * Người dùng không thể tự gửi yêu cầu bàn giao cho chính cơ sở thu gom của mình (`recyclerId != userId`).
   * Mỗi người dân chỉ được có tối đa **`3` yêu cầu đang hoạt động cùng lúc** (`MaxActiveRequestsPerUser = 3`, tính trên các trạng thái `Pending`, `Negotiating`, `Accepted`, `Scheduled`, `AwaitingConfirmation`).
   * Cơ sở thu gom được chọn bắt buộc phải đã xác minh (`VerificationStatus == Verified`) và đang mở cửa nhận đơn (`ActiveStatus == Online`).
   * Toàn bộ các loại phế liệu trong `items` phải không trùng lặp, nằm trong bảng giá đang hoạt động (`PriceStatus.Active`) của cơ sở thu gom đó, và có khối lượng ước tính `estimatedWeightKg >= minQuantityKg` (lớn hơn `0`, tối đa `2` chữ số thập phân).
   * Bắt buộc đính kèm từ **`1` đến `5` URL ảnh chụp phế liệu thực tế** (`photoUrls` hợp lệ chuẩn `http/https`).
2. **Kiểm tra khung giờ hẹn thu gom & Lịch làm việc (`BR-NEG-04`):**
   * Khung giờ đề xuất (`proposedTimeSlot`) phải cách thời điểm hiện tại tối thiểu **`2 giờ`** (`MinLeadHours = 2`) và không vượt quá **`7 ngày`** (`MaxAheadDays = 7`).
   * Nếu cơ sở thu gom đã thiết lập lịch hoạt động tuần (`WeeklyScheduleJson` tại Module 5 `R-02`), hệ thống tự động quy đổi `proposedTimeSlot` sang múi giờ Việt Nam (`ICT / GMT+7`) để kiểm tra: chặn đặt lịch vào ngày cơ sở nghỉ (`isClosed == true`) hoặc nằm ngoài khung giờ mở cửa (`openTime .. closeTime`).
3. **Vòng đời trạng thái một chiều (`BR-HND-02`):**
   * Luồng trạng thái tiến triển một chiều: `Pending` $\rightarrow$ (`Negotiating`) $\rightarrow$ `Accepted` $\rightarrow$ `Scheduled` $\rightarrow$ `AwaitingConfirmation` $\rightarrow$ `Completed`.
   * Các trạng thái kết thúc: `Rejected`, `Cancelled`, `Expired`, `Disputed`.
   * Người dân chỉ được chỉnh sửa (`PUT`) khi đơn còn ở `Pending`, và chỉ được hủy (`POST .../cancel`) khi đơn ở `Pending`, `Negotiating`, `Accepted`, hoặc `Scheduled`.
4. **Cô lập dữ liệu đối tác (`BR-HND-03`):**
   * Mọi truy vấn phía `Recycler` đều kiểm tra `h.RecyclerId == currentUserId`. Cơ sở thu gom A không thể xem, chấp nhận, từ chối hay hủy đơn bàn giao gửi cho cơ sở thu gom B.
5. **Kiểm soát hủy đơn & Ghi nhận hủy muộn (`BR-HND-06`):**
   * Mọi thao tác từ chối (`Reject`) hoặc hủy (`Cancel`) từ cả hai phía đều **bắt buộc nhập lý do** (`1..500` ký tự sau khi `Trim()`).
   * Khi đơn đã chốt khung giờ hẹn (`ConfirmedTimeSlot != null`), nếu người dân hoặc cơ sở thu gom hủy trong vòng **`4 giờ`** trước giờ hẹn (`ConfirmedTimeSlot - UtcNow <= 4h`), hệ thống tự động đánh dấu `isLateCancellation = true` và trả về mã cảnh báo `MSG-HND-06`.
6. **Bảo vệ quyền riêng tư liên hệ & Tọa độ GPS (`BR-HND-08` & `BR-MTH-05`):**
   * Khi đơn đang ở trạng thái `Pending` hoặc `Negotiating` (`isContactRevealed == false`):
     * Phía `Recycler` xem đơn sẽ thấy `pickupAddress = null`, `requester.phone = null`, `requester.address = null`.
     * Phía `Citizen` xem đơn sẽ thấy `recycler.phone = null`.
   * Ngay khi `Recycler` bấm **Chấp nhận** (`Status == Accepted`), cờ `isContactRevealed` bật thành `true`, mở khóa số điện thoại và địa chỉ thu gom cho cả hai bên.
   * Tọa độ GPS (`Latitude`, `Longitude`) của người dân **không bao giờ được trả ra trong `HandoverRequestDto`** ở bất kỳ trạng thái nào (`BR-MTH-05`).

---

## 3. CHI TIẾT TỪNG API ENDPOINT

---

### 3.1. Tạo Yêu cầu Bàn giao Phế liệu (`POST /api/v1/handover-requests`)

* **Mô tả:** Người dân (`Citizen`) gửi yêu cầu đặt lịch bàn giao phế liệu tới một cơ sở thu gom (`Use Case U-06`). Hệ thống tự động sinh mã yêu cầu duy nhất theo ngày định dạng `HND-yyyyMMdd-NNNN` (VD: `"HND-20261109-0001"`).
* **Phân quyền:** `[Authorize(Roles = "User,Citizen")]`
* **Quy tắc nghiệp vụ áp dụng:** `BR-HND-01`, `BR-HND-LIMIT`, `BR-NEG-04`, `BR-HND-08`.

#### a. Request Parameters (`application/json` – `CreateHandoverRequest`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `recyclerId` | Body | `Guid` | **Có** | Khác `userId` hiện tại | Định danh cơ sở thu gom nhận yêu cầu (`RecyclerProfile.UserId`). |
| `items` | Body | `HandoverItemRequest[]` | **Có** | Tối thiểu `1` phần tử, không trùng `wasteCategoryId` | Danh sách các loại phế liệu và khối lượng ước tính cần bàn giao. |
| `items[].wasteCategoryId` | Body | `Guid` | **Có** | Phải thuộc bảng giá `Active` của `recyclerId` | ID danh mục phế liệu (`waste_categories.id`). |
| `items[].estimatedWeightKg` | Body | `decimal` | **Có** | `> 0`, tối đa `2` số lẻ, `>= minQuantityKg` của cơ sở | Khối lượng ước tính (`kg`) của loại phế liệu đó. |
| `items[].itemNote` | Body | `string?` | Không | Tối đa `200` ký tự | Ghi chú riêng cho dòng phế liệu (VD: `"Đã tháo nắp và ép dẹp"`). |
| `photoUrls` | Body | `string[]` | **Có** | Từ `1` đến `5` URL (`http/https`, `<= 1000` ký tự/URL) | Danh sách URL ảnh chụp phế liệu thực tế đã tải lên Cloudflare R2. |
| `proposedTimeSlot` | Body | `DateTimeOffset` | **Có** | `[UtcNow + 2h, UtcNow + 7d]`, trong giờ mở cửa của cơ sở | Thời điểm / khung giờ mong muốn cơ sở đến thu gom. |
| `pickupAddress` | Body | `string` | **Có** | Không rỗng, tối đa `500` ký tự | Địa chỉ thu gom thực tế của người dân. |
| `latitude` | Body | `decimal?` | Không | `[-90, 90]` | Vĩ độ điểm thu gom (chỉ lưu nội bộ, không trả cho `Recycler`). |
| `longitude` | Body | `decimal?` | Không | `[-180, 180]` | Kinh độ điểm thu gom (chỉ lưu nội bộ, không trả cho `Recycler`). |
| `userNote` | Body | `string?` | Không | Tối đa `200` ký tự | Ghi chú chung cho đơn bàn giao (VD: `"Gọi trước 15 phút khi vào hẻm"`). |

##### Ví dụ Request Body:
```json
{
  "recyclerId": "22222222-2222-2222-2222-222222222222",
  "items": [
    {
      "wasteCategoryId": "a4444444-4444-4444-4444-444444444444",
      "estimatedWeightKg": 5.5,
      "itemNote": "Chai nhựa PET sạch đã ép dẹp"
    },
    {
      "wasteCategoryId": "a1111111-1111-1111-1111-111111111111",
      "estimatedWeightKg": 10.0,
      "itemNote": "Thùng carton đóng gói buộc gọn"
    }
  ],
  "photoUrls": [
    "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/handover-photos/pet_bottles_01.jpg",
    "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/handover-photos/carton_boxes_02.jpg"
  ],
  "proposedTimeSlot": "2026-11-10T09:30:00+07:00",
  "pickupAddress": "Số 45 Lê Duẩn, Phường Bến Nghé, Quận 1, TP.HCM",
  "latitude": 10.7802,
  "longitude": 106.7015,
  "userNote": "Hẻm rộng xe ba gác vào tận cửa, vui lòng gọi trước 10 phút."
}
```

#### b. Response Parameters (`201 Created` – `ApiResponse<HandoverRequestDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `id` | `Guid` | Khóa chính của yêu cầu bàn giao (`handover_requests.request_id`). |
| `requestCode` | `string` | Mã yêu cầu hiển thị chuẩn `HND-yyyyMMdd-NNNN`. |
| `status` | `string` | Trạng thái hiện tại của yêu cầu (`"Pending"`, `"Negotiating"`, `"Accepted"`, `"Scheduled"`, `"AwaitingConfirmation"`, `"Completed"`, `"Rejected"`, `"Cancelled"`, `"Expired"`, `"Disputed"`). |
| `items` | `HandoverItemDto[]` | Danh sách chi tiết các dòng phế liệu trong đơn. |
| `totalEstimatedWeightKg` | `decimal` | Tổng khối lượng ước tính của toàn bộ các dòng phế liệu (`kg`). |
| `photoUrls` | `string[]` | Mảng từ `1..5` URL ảnh chụp phế liệu đính kèm. |
| `proposedTimeSlot` | `string (ISO 8601)` | Thời điểm thu gom đề xuất ban đầu (chuẩn UTC). |
| `confirmedTimeSlot` | `string? (ISO 8601)` | Thời điểm thu gom đã được xác nhận chính thức (`null` khi chưa `Accepted`). |
| `pickupAddress` | `string?` | Địa chỉ thu gom (luôn hiển thị với `Citizen`; chỉ hiển thị với `Recycler` khi `isContactRevealed == true`). |
| `userNote` | `string?` | Ghi chú của người dân gửi cơ sở thu gom. |
| `recyclerNote` | `string?` | Ghi chú phản hồi của cơ sở thu gom khi chấp nhận đơn. |
| `cancelReason` | `string?` | Lý do hủy hoặc từ chối đơn (nếu `status` là `"Cancelled"` hoặc `"Rejected"`). |
| `cancelledByRole` | `string?` | Vai trò người thực hiện hủy/từ chối: `"User"` hoặc `"Recycler"`. |
| `isLateCancellation` | `boolean` | `true` nếu hủy trong vòng `4 giờ` trước `confirmedTimeSlot` (`BR-HND-06`). |
| `isContactRevealed` | `boolean` | `true` khi đơn đã được chấp nhận (`Accepted` trở đi), cho phép hai bên thấy SĐT và địa chỉ của nhau (`BR-HND-08`). |
| `hasReceipt` | `boolean` | `true` nếu cơ sở thu gom đã phát hành biên nhận số cho đơn này (`R-06`). |
| `canEdit` | `boolean` | `true` nếu người dùng hiện tại được phép chỉnh sửa đơn (chỉ `Citizen` khi `status == "Pending"`). |
| `canCancel` | `boolean` | `true` nếu người dùng hiện tại được phép hủy đơn. |
| `recycler` | `HandoverPartyDto?` | Thông tin cơ sở thu gom (trả về khi người gọi là `Citizen`; `phone` ẩn khi `isContactRevealed == false`). |
| `requester` | `HandoverPartyDto?` | Thông tin người dân gửi đơn (trả về khi người gọi là `Recycler`; `phone` và `address` ẩn khi `isContactRevealed == false`). |
| `createdAt` | `string (ISO 8601)` | Thời điểm tạo yêu cầu (UTC). |
| `updatedAt` | `string (ISO 8601)` | Thời điểm cập nhật gần nhất (UTC). |

##### Ví dụ Response Thành công (`201 Created` – `MSG-HND-01`):
```json
{
  "success": true,
  "messageCode": "MSG-HND-01",
  "message": "Yêu cầu bàn giao của bạn đã được gửi.",
  "data": {
    "id": "d4e5f6a7-1234-4b5c-89ab-001122334455",
    "requestCode": "HND-20261109-0001",
    "status": "Pending",
    "items": [
      {
        "id": "e1111111-2222-3333-4444-555555555551",
        "wasteCategoryId": "a4444444-4444-4444-4444-444444444444",
        "wasteCategoryCode": "plastic",
        "wasteCategoryName": "Nhựa tái chế (Plastic)",
        "estimatedWeightKg": 5.5,
        "actualWeightKg": null,
        "agreedPricePerKg": null,
        "itemNote": "Chai nhựa PET sạch đã ép dẹp"
      },
      {
        "id": "e1111111-2222-3333-4444-555555555552",
        "wasteCategoryId": "a1111111-1111-1111-1111-111111111111",
        "wasteCategoryCode": "cardboard",
        "wasteCategoryName": "Bìa Carton (Cardboard)",
        "estimatedWeightKg": 10.0,
        "actualWeightKg": null,
        "agreedPricePerKg": null,
        "itemNote": "Thùng carton đóng gói buộc gọn"
      }
    ],
    "totalEstimatedWeightKg": 15.5,
    "photoUrls": [
      "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/handover-photos/pet_bottles_01.jpg",
      "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/handover-photos/carton_boxes_02.jpg"
    ],
    "proposedTimeSlot": "2026-11-10T02:30:00Z",
    "confirmedTimeSlot": null,
    "pickupAddress": "Số 45 Lê Duẩn, Phường Bến Nghé, Quận 1, TP.HCM",
    "userNote": "Hẻm rộng xe ba gác vào tận cửa, vui lòng gọi trước 10 phút.",
    "recyclerNote": null,
    "cancelReason": null,
    "cancelledByRole": null,
    "isLateCancellation": false,
    "isContactRevealed": false,
    "hasReceipt": false,
    "canEdit": true,
    "canCancel": true,
    "recycler": {
      "id": "22222222-2222-2222-2222-222222222222",
      "name": "Vựa phế liệu EcoGreen",
      "phone": null,
      "address": "123 Lê Văn Việt, Phường Hiệp Phú, TP. Thủ Đức, TP.HCM"
    },
    "requester": null,
    "createdAt": "2026-11-09T10:00:00Z",
    "updatedAt": "2026-11-09T10:00:00Z"
  },
  "errors": null,
  "timestamp": "2026-11-09T10:00:00Z"
}
```

---

### 3.2. Danh sách Yêu cầu Bàn giao của Người dân (`GET /api/v1/handover-requests`)

* **Mô tả:** Lấy danh sách các yêu cầu bàn giao do chính người dân đang đăng nhập tạo ra, sắp xếp theo thời gian tạo mới nhất (`CreatedAt DESC`).
* **Phân quyền:** `[Authorize(Roles = "User,Citizen")]`

#### a. Request Parameters (Query String)

| Tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---:|:---|
| `status` | Query | `string?` | Không | `null` (Tất cả) | Lọc theo tên trạng thái trong `HandoverStatus` (không phân biệt hoa thường, VD: `"Pending"`, `"Accepted"`, `"AwaitingConfirmation"`, `"Completed"`, `"Cancelled"`). |
| `page` | Query | `int` | Không | `1` | Trang cần lấy (`>= 1`). |
| `pageSize` | Query | `int` | Không | `20` | Số bản ghi mỗi trang (tự động kẹp biên trong khoảng `[1, 50]`). |

#### b. Response Parameters (`200 OK` – `ApiResponse<HandoverPagedResult<HandoverRequestDto>>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `items` | `HandoverRequestDto[]` | Danh sách các yêu cầu bàn giao thuộc trang hiện tại. |
| `page` | `int` | Chỉ số trang hiện tại. |
| `pageSize` | `int` | Số lượng bản ghi tối đa trên mỗi trang. |
| `totalCount` | `int` | Tổng số bản ghi thỏa mãn bộ lọc. |

---

### 3.3. Xem Chi tiết Yêu cầu Bàn giao của Người dân (`GET /api/v1/handover-requests/{id:guid}`)

* **Mô tả:** Trả về thông tin chi tiết của một yêu cầu bàn giao thuộc sở hữu của người dân đang đăng nhập.
* **Phân quyền:** `[Authorize(Roles = "User,Citizen")]`
* **Response:**
  * `200 OK`: Trả về `HandoverRequestDto`. Khi `status` từ `"Accepted"` trở đi, trường `recycler.phone` sẽ hiển thị số điện thoại liên hệ của cơ sở thu gom (`BR-HND-08`).
  * `404 Not Found` (`MSG-NOT-FOUND`): Không tìm thấy yêu cầu hoặc yêu cầu thuộc về người dùng khác.

---

### 3.4. Chỉnh sửa Yêu cầu Bàn giao khi còn `Pending` (`PUT /api/v1/handover-requests/{id:guid}`)

* **Mô tả:** Cho phép người dân cập nhật lại danh sách loại phế liệu, khối lượng ước tính, ảnh đính kèm, khung giờ hẹn hoặc địa chỉ thu gom khi yêu cầu **đang ở trạng thái `Pending`** (`Use Case U-07`). Không cho phép thay đổi cơ sở thu gom (`recyclerId` giữ nguyên).
* **Phân quyền:** `[Authorize(Roles = "User,Citizen")]`
* **Quy tắc nghiệp vụ áp dụng:** `BR-HND-01`, `BR-HND-02`, `BR-NEG-04`.

#### a. Request Parameters (`application/json` – `UpdateHandoverRequest`)

Cấu trúc Body giống hệt `CreateHandoverRequest` (ngoại trừ việc không có trường `recyclerId`): gồm `items`, `photoUrls`, `proposedTimeSlot`, `pickupAddress`, `latitude`, `longitude`, `userNote`.

#### b. Các Thông báo Trả về

| HTTP Status | Mã `messageCode` | Quy tắc | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `null` | `U-07` | Cập nhật yêu cầu đang `Pending` thành công | `"Cập nhật yêu cầu bàn giao thành công."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-HND-02` | Yêu cầu đã chuyển sang `Accepted`, `Negotiating`, `Completed`... | `"Chỉ có thể chỉnh sửa yêu cầu đang ở trạng thái Pending."` |
| **`404 Not Found`** | `MSG-NOT-FOUND` | — | Không tìm thấy yêu cầu thuộc về người dùng hiện tại | `"Không tìm thấy yêu cầu bàn giao."` |

---

### 3.5. Người dân Hủy Yêu cầu Bàn giao (`POST /api/v1/handover-requests/{id:guid}/cancel`)

* **Mô tả:** Người dân chủ động hủy yêu cầu bàn giao khi đơn đang ở trạng thái `Pending`, `Negotiating`, `Accepted` hoặc `Scheduled` (`Use Case U-07`).
  * Nếu đơn đã được xác nhận lịch hẹn (`ConfirmedTimeSlot != null`) và thời điểm hủy cách giờ hẹn `<= 4 giờ` (`LateCancelWindowHours = 4`), hệ thống bật cờ `isLateCancellation = true` và trả về mã `MSG-HND-06` (`BR-HND-06`).
* **Phân quyền:** `[Authorize(Roles = "User,Citizen")]`

#### a. Request Body (`HandoverReasonRequest`)

| Trường | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---|:---|
| `reason` | `string` | **Có** | Không được chỉ chứa khoảng trắng, `1..500` ký tự | Lý do hủy yêu cầu bàn giao. |

##### Ví dụ Request Body:
```json
{
  "reason": "Gia đình có việc đột xuất phải đi công tác, xin hẹn lại vào tuần sau."
}
```

#### b. Các Thông báo Trả về

| HTTP Status | Mã `messageCode` | Quy tắc | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-HND-05` | `BR-HND-06` | Hủy khi chưa chốt giờ hẹn hoặc hủy trước giờ hẹn `> 4 giờ` (`isLateCancellation == false`) | `"Yêu cầu bàn giao đã được hủy."` |
| **`200 OK`** | `MSG-HND-06` | `BR-HND-06` | Hủy trong vòng `4 giờ` trước lịch hẹn đã xác nhận (`isLateCancellation == true`) | `"Đã hủy. Việc hủy trong vòng 4 giờ trước lịch hẹn được ghi nhận là hủy muộn."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-HND-06` | Bỏ trống lý do hủy hoặc vượt quá `500` ký tự | `"Vui lòng nhập lý do."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-HND-02` | Đơn đang ở trạng thái `AwaitingConfirmation`, `Completed`, `Rejected`, `Cancelled`, `Expired` | `"Không thể hủy yêu cầu đang ở trạng thái {Status}."` |

---

### 3.6. Danh sách Yêu cầu Bàn giao Gửi đến Cơ sở Thu gom (`GET /api/v1/recycler/handover-requests`)

* **Mô tả:** Trả về danh sách các yêu cầu bàn giao mà người dân đã gửi đích danh tới cơ sở thu gom đang đăng nhập (`BR-HND-03`).
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`
* **Query Parameters:** `status` (`string?`), `page` (`int = 1`), `pageSize` (`int = 20`).

---

### 3.7. Xem Chi tiết Yêu cầu Bàn giao phía Cơ sở Thu gom (`GET /api/v1/recycler/handover-requests/{id:guid}`)

* **Mô tả:** Cơ sở thu gom xem chi tiết một yêu cầu bàn giao gửi đến mình (`Use Case R-03`).
  * **Thực thi bảo mật `BR-HND-08` & `BR-MTH-05`:** Khi đơn đang `Pending` hoặc `Negotiating` (`isContactRevealed == false`), các trường `pickupAddress`, `requester.phone`, `requester.address` đều trả về `null`. Tọa độ GPS không bao giờ xuất hiện trong DTO.
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`

##### Ví dụ Response khi Đơn đang `Pending` (`isContactRevealed = false`):
```json
{
  "success": true,
  "messageCode": null,
  "message": null,
  "data": {
    "id": "d4e5f6a7-1234-4b5c-89ab-001122334455",
    "requestCode": "HND-20261109-0001",
    "status": "Pending",
    "items": [
      {
        "id": "e1111111-2222-3333-4444-555555555551",
        "wasteCategoryId": "a4444444-4444-4444-4444-444444444444",
        "wasteCategoryCode": "plastic",
        "wasteCategoryName": "Nhựa tái chế (Plastic)",
        "estimatedWeightKg": 5.5,
        "actualWeightKg": null,
        "agreedPricePerKg": null,
        "itemNote": "Chai nhựa PET sạch đã ép dẹp"
      }
    ],
    "totalEstimatedWeightKg": 5.5,
    "photoUrls": [
      "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/handover-photos/pet_bottles_01.jpg"
    ],
    "proposedTimeSlot": "2026-11-10T02:30:00Z",
    "confirmedTimeSlot": null,
    "pickupAddress": null,
    "userNote": "Hẻm rộng xe ba gác vào tận cửa, vui lòng gọi trước 10 phút.",
    "recyclerNote": null,
    "cancelReason": null,
    "cancelledByRole": null,
    "isLateCancellation": false,
    "isContactRevealed": false,
    "hasReceipt": false,
    "canEdit": false,
    "canCancel": false,
    "recycler": null,
    "requester": {
      "id": "11111111-1111-1111-1111-111111111111",
      "name": "Nguyễn Văn An",
      "phone": null,
      "address": null
    },
    "createdAt": "2026-11-09T10:00:00Z",
    "updatedAt": "2026-11-09T10:00:00Z"
  },
  "errors": null,
  "timestamp": "2026-11-09T10:05:00Z"
}
```

---

### 3.8. Cơ sở Thu gom Chấp nhận Yêu cầu Bàn giao (`POST /api/v1/recycler/handover-requests/{id:guid}/accept`)

* **Mô tả:** Cơ sở thu gom bấm **Chấp nhận** yêu cầu đang ở trạng thái `Pending` hoặc `Negotiating` (`Use Case R-03`).
  * Hệ thống kiểm tra khung giờ `proposedTimeSlot` chưa bị trôi qua trong quá khứ (`proposedTimeSlot > UtcNow` – `BR-NEG-04`).
  * Chuyển trạng thái đơn sang `"Accepted"`, khóa lịch hẹn chính thức `confirmedTimeSlot = proposedTimeSlot` (`BR-NEG-05`), và bật `isContactRevealed = true` để hiển thị địa chỉ + số điện thoại người dân (`BR-HND-08`).
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`

#### a. Request Body (`AcceptHandoverRequest` – Tùy chọn)

| Trường | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---|:---|
| `note` | `string?` | Không | Tối đa `200` ký tự | Ghi chú xác nhận của cơ sở thu gom gửi tới người dân. |

##### Ví dụ Request Body:
```json
{
  "note": "Đã ghi nhận lịch hẹn, nhân viên thu gom sẽ liên hệ trước 15 phút khi đến."
}
```

##### Ví dụ Response Thành công (`200 OK` – `MSG-HND-02`, mở khóa thông tin liên hệ `isContactRevealed = true`):
```json
{
  "success": true,
  "messageCode": "MSG-HND-02",
  "message": "Yêu cầu bàn giao đã được chấp nhận.",
  "data": {
    "id": "d4e5f6a7-1234-4b5c-89ab-001122334455",
    "requestCode": "HND-20261109-0001",
    "status": "Accepted",
    "items": [
      {
        "id": "e1111111-2222-3333-4444-555555555551",
        "wasteCategoryId": "a4444444-4444-4444-4444-444444444444",
        "wasteCategoryCode": "plastic",
        "wasteCategoryName": "Nhựa tái chế (Plastic)",
        "estimatedWeightKg": 5.5,
        "actualWeightKg": null,
        "agreedPricePerKg": null,
        "itemNote": "Chai nhựa PET sạch đã ép dẹp"
      }
    ],
    "totalEstimatedWeightKg": 5.5,
    "photoUrls": [
      "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/handover-photos/pet_bottles_01.jpg"
    ],
    "proposedTimeSlot": "2026-11-10T02:30:00Z",
    "confirmedTimeSlot": "2026-11-10T02:30:00Z",
    "pickupAddress": "Số 45 Lê Duẩn, Phường Bến Nghé, Quận 1, TP.HCM",
    "userNote": "Hẻm rộng xe ba gác vào tận cửa, vui lòng gọi trước 10 phút.",
    "recyclerNote": "Đã ghi nhận lịch hẹn, nhân viên thu gom sẽ liên hệ trước 15 phút khi đến.",
    "cancelReason": null,
    "cancelledByRole": null,
    "isLateCancellation": false,
    "isContactRevealed": true,
    "hasReceipt": false,
    "canEdit": false,
    "canCancel": true,
    "recycler": null,
    "requester": {
      "id": "11111111-1111-1111-1111-111111111111",
      "name": "Nguyễn Văn An",
      "phone": "0901234567",
      "address": "Số 45 Lê Duẩn, Phường Bến Nghé, Quận 1, TP.HCM"
    },
    "createdAt": "2026-11-09T10:00:00Z",
    "updatedAt": "2026-11-09T10:10:00Z"
  },
  "errors": null,
  "timestamp": "2026-11-09T10:10:00Z"
}
```

---

### 3.9. Cơ sở Thu gom Từ chối Yêu cầu Bàn giao (`POST /api/v1/recycler/handover-requests/{id:guid}/reject`)

* **Mô tả:** Cơ sở thu gom từ chối yêu cầu đang ở trạng thái `Pending` hoặc `Negotiating` kèm lý do bắt buộc (`Use Case R-03`). Đơn chuyển sang trạng thái kết thúc `"Rejected"` (`cancelledByRole = "Recycler"`).
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`
* **Request Body (`HandoverReasonRequest`):** `{ "reason": "Kho bãi hôm nay đã đầy tải, không thể bố trí xe thu gom kịp." }`

---

### 3.10. Cơ sở Thu gom Hủy Lịch hẹn Đã Xác nhận (`POST /api/v1/recycler/handover-requests/{id:guid}/cancel`)

* **Mô tả:** Khi đơn đã được chấp nhận (`Accepted` hoặc `Scheduled`), nếu cơ sở thu gom gặp sự cố bất khả kháng không thể đến thu gom, cơ sở gọi API này để hủy lịch hẹn kèm lý do bắt buộc (`Use Case R-04`).
  * Nếu hủy trong vòng `4 giờ` trước `confirmedTimeSlot`, hệ thống đánh dấu `isLateCancellation = true` và trả về `messageCode = "MSG-HND-06"` (`BR-HND-06`).
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`
* **Request Body (`HandoverReasonRequest`):** `{ "reason": "Xe tải thu gom gặp sự cố kỹ thuật đột xuất trên đường." }`

---

## 4. BẢNG MÃ LỖI & MÃ THÔNG BÁO TRONG MODULE 7

| HTTP Status | `messageCode` | Quy tắc (`BR`) | Trường hợp xảy ra & Nội dung thông báo |
|:---:|:---|:---:|:---|
| **`201 Created`** | `MSG-HND-01` | `U-06` | `"Yêu cầu bàn giao của bạn đã được gửi."` |
| **`200 OK`** | `MSG-HND-02` | `R-03`, `BR-NEG-05` | `"Yêu cầu bàn giao đã được chấp nhận."` |
| **`200 OK`** | `MSG-HND-05` | `U-07`, `R-04` | `"Yêu cầu bàn giao đã được hủy."` / `"Lịch thu gom đã được hủy."` (Hủy đúng hạn). |
| **`200 OK`** | `MSG-HND-06` | `BR-HND-06` | `"Đã hủy. Việc hủy trong vòng 4 giờ trước lịch hẹn được ghi nhận là hủy muộn."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-HND-LIMIT` | `"Bạn chỉ có thể có tối đa 3 yêu cầu đang xử lý cùng lúc."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-HND-01` | `"Bạn không thể gửi yêu cầu bàn giao cho chính cơ sở của mình."`<br>`"Cơ sở thu gom hiện không nhận yêu cầu bàn giao."`<br>`"Cơ sở thu gom không nhận một trong các loại phế liệu đã chọn."`<br>`"Khối lượng tối thiểu cơ sở thu gom nhận là {Min} kg cho mỗi loại."`<br>`"Vui lòng đính kèm từ 1 đến 5 ảnh."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-NEG-04` | `"Khung giờ thu gom phải cách hiện tại tối thiểu 2 giờ."`<br>`"Chỉ được hẹn trong vòng 7 ngày tới."`<br>`"Cơ sở thu gom nghỉ vào ngày đã chọn."`<br>`"Khung giờ nằm ngoài giờ hoạt động của cơ sở ({Open} - {Close})."`<br>`"Khung giờ đề xuất đã qua, không thể chấp nhận. Vui lòng từ chối hoặc trao đổi lại với người dân."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-HND-02` | `"Chỉ có thể chỉnh sửa yêu cầu đang ở trạng thái Pending."`<br>`"Không thể chấp nhận / từ chối / hủy yêu cầu đang ở trạng thái {Status}."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-HND-06` | `"Vui lòng nhập lý do."` / `"Lý do tối đa 500 ký tự."` |
| **`404 Not Found`** | `MSG-NOT-FOUND` | `BR-HND-03` | `"Không tìm thấy yêu cầu bàn giao."` / `"Không tìm thấy cơ sở thu gom."` |

