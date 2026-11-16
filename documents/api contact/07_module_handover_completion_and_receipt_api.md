# TÀI LIỆU ĐẶC TẢ API – MODULE 8: HOÀN TẤT BÀN GIAO, BIÊN NHẬN ĐIỆN TỬ & LỊCH SỬ GIAO DỊCH (HANDOVER COMPLETION & TRANSACTION RECEIPT)
**Mã phân hệ:** `HND` & `GAM` (`Use Cases R-06: Issue Digital Receipt`, `U-09: Confirm Receipt & Complete Handover`, `U-12: View Transaction History`, `JOB-01: Auto-Complete Unconfirmed Receipts`)  
**Controllers:** [`RecyclerReceiptController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerReceiptController.cs) (`RecyclerReceiptController`, `RecyclerTransactionController`), [`HandoverReceiptController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/HandoverReceiptController.cs) (`HandoverReceiptController`, `TransactionHistoryController`)  
**Services thực thi & Background Job:** [`HandoverReceiptService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs) ([`IHandoverReceiptService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Interfaces/IHandoverReceiptService.cs)), [`GamificationService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/TradeAndGamificationServices.cs#L156-L246) ([`IGamificationService`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Interfaces/ApplicationInterfaces.cs#L49-L55)), [`ReceiptAutoCompleteJob.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Services/ReceiptAutoCompleteJob.cs)  
**Entities & Enums:** [`HandoverReceipt.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/HandoverReceipt.cs), [`HandoverRequest.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/HandoverRequest.cs), [`HandoverStatus.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Enums/HandoverStatus.cs)  
**DTOs:** [`HandoverReceiptDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/HandoverReceiptDtos.cs)  
**Base Routes:**
- `/api/v1/recycler/handover-requests/{requestId:guid}/receipt` (Recycler lập & xem biên nhận – `R-06`)
- `/api/v1/handover-requests/{requestId:guid}/receipt` (Người dân xem & xác nhận biên nhận – `U-09`)
- `/api/v1/transactions` (Lịch sử giao dịch Người dân – `U-12`)
- `/api/v1/recycler/transactions` (Lịch sử giao dịch Cơ sở thu gom – `U-12`)

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS & TIẾN TRÌNH NỀN

### 1.1. Danh mục RESTful API Endpoints

| STT | Phương thức | Endpoint URI | Quyền truy cập (`[Authorize]`) | Use Case | Chức năng nghiệp vụ | Mã thông báo / Quy tắc |
|:---:|:---:|:---|:---:|:---:|:---|:---|
| 1 | `POST` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt/preview` | `Recycler`, `Collector` (Nhận đơn) | `R-06` | Xem trước biên nhận số (không lưu CSDL): tính thành tiền từng dòng, tổng khối lượng, tổng tiền, điểm thưởng dự kiến và kiểm tra độ lệch khối lượng $> 20\%$ (`MSG-HND-04`). | `BR-HND-02`, `BR-HND-03`, `BR-HND-04`, `BR-HND-05`, `MSG-HND-04` |
| 2 | `POST` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt` | `Recycler`, `Collector` (Nhận đơn) | `R-06` | Phát hành biên nhận số chính thức (`RCP-yyyy-NNNN`), khóa cứng đơn giá giao dịch (`AgreedPricePerKg`), lưu 1–5 ảnh đồng hồ cân (`BR-HND-04`) và chuyển đơn sang `AwaitingConfirmation`. | `201 Created`, `BR-HND-04`, `BR-HND-05`, `AF-01`, `EF-02`, `MSG-HND-04` |
| 3 | `GET` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt` | `Recycler`, `Collector` (Nhận đơn) | `R-06` | Cơ sở thu gom xem lại chi tiết biên nhận điện tử đã phát hành cho đơn bàn giao. | `200 OK`, `BR-HND-03`, `MSG-NOT-FOUND` |
| 4 | `GET` | `/api/v1/handover-requests/{requestId:guid}/receipt` | `User`, `Citizen` (Chủ đơn) | `U-09` | Người dân tra cứu chi tiết biên nhận, ảnh minh chứng đồng hồ cân, cảnh báo lệch khối lượng (`MSG-HND-04`) và điểm xanh sẽ nhận trước khi xác nhận. | `200 OK`, `BR-HND-04`, `MSG-HND-04`, `MSG-NOT-FOUND` |
| 5 | `POST` | `/api/v1/handover-requests/{requestId:guid}/receipt/confirm` | `User`, `Citizen` (Chủ đơn) | `U-09` | Người dân xác nhận đã nhận đủ tiền và hoàn tất giao dịch (`Completed`). Khóa biên nhận vĩnh viễn (`BR-HND-07`) và cộng điểm xanh cho cả hai bên (`BR-GAM-01`). | `200 OK`, `BR-HND-04`, `BR-HND-07`, `BR-GAM-01`, `MSG-HND-03` |
| 6 | `GET` | `/api/v1/transactions` | `User`, `Citizen` | `U-12` | Xem lịch sử các giao dịch đã hoàn tất của người dân, hỗ trợ lọc theo khoảng ngày (`from`, `to`), phân trang và thống kê tổng lũy kế toàn thời gian. | `200 OK` |
| 7 | `GET` | `/api/v1/recycler/transactions` | `Recycler`, `Collector` | `U-12` | Xem lịch sử các giao dịch thu mua đã hoàn tất của cơ sở thu gom, hỗ trợ lọc theo khoảng ngày (`from`, `to`), phân trang và thống kê tổng lũy kế. | `200 OK` |

---

### 1.2. Tiến trình Nền Tự động (`BackgroundService`)

| Mã Job | Class Thực thi | Chu kỳ quét | Ngưỡng Timeout | Mô tả Nghiệp vụ |
|:---:|:---|:---:|:---:|:---|
| **`JOB-01`** | [`ReceiptAutoCompleteJob.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Services/ReceiptAutoCompleteJob.cs) | `60 phút / lần` | `48 giờ` (`ConfirmationTimeout`) | Tìm các biên nhận đang ở trạng thái `AwaitingConfirmation` có `CreatedAt < UtcNow - 48h` (người dân quên bấm xác nhận) và tự động hoàn tất giao dịch (`Completed`), khóa biên nhận (`BR-HND-07`) và cộng điểm thưởng xanh cho cả hai bên (`BR-GAM-01`). |

---

## 2. LUỒNG NGHIỆP VỤ & QUY TẮC CỐT LÕI (`R-06`, `U-09`, `U-12`, `JOB-01`)

1. **Điều kiện lập biên nhận & Minh chứng ảnh cân (`BR-HND-02`, `BR-HND-03`, `BR-HND-04`):**
   * Chỉ cơ sở thu gom được chỉ định trong đơn (`h.RecyclerId == recyclerId` – `BR-HND-03`) mới có quyền lập biên nhận khi đơn đang ở trạng thái `Accepted` hoặc `Scheduled` (`BR-HND-02`).
   * Mỗi yêu cầu bàn giao chỉ có **duy nhất 1 biên nhận số** (quan hệ `1-1` với ràng buộc `UNIQUE(request_id)` trên bảng `handover_receipts`).
   * Bắt buộc đính kèm từ **`1` đến `5` URL ảnh chụp đồng hồ cân thực tế** (`scaleProofPhotoUrls` – `BR-HND-04`) và tùy chọn `1` URL ảnh biên nhận giấy (`receiptImageUrl`).
2. **Quyết toán theo khối lượng thực tế, Khóa đơn giá & Phế liệu phát sinh (`BR-HND-05`, `AF-01`, `EF-02`):**
   * Cơ sở thu gom bắt buộc nhập khối lượng cân thực tế (`actualWeightKg > 0`, tối đa `2` chữ số thập phân) cho **tất cả các dòng phế liệu đã có trong yêu cầu ban đầu** (`EF-02`).
   * **Khóa đơn giá giao dịch (`Transaction Price Locking`):** Nếu dòng phế liệu đã có `AgreedPricePerKg` (chốt qua Chatbox thương lượng), hệ thống dùng đơn giá đó; nếu chưa có, hệ thống lấy `PricePerKg` hiện tại trong `recycler_price_list` của cơ sở và khóa cứng vào `HandoverRequestItem.AgreedPricePerKg`.
   * **Hỗ trợ thêm phế liệu phát sinh tại chỗ (`AF-01`):** Nếu khi cân thực tế người dân bán thêm loại phế liệu chưa khai báo trong đơn ban đầu, `Recycler` gửi phần tử có `itemId = null` kèm `wasteCategoryId` (phải nằm trong bảng giá `Active` của cơ sở và không trùng với loại đã có). Hệ thống tự động tạo thêm dòng `HandoverRequestItem` với `estimatedWeightKg = 0`, `isExtra = true` và `itemNote = "Phát sinh khi cân tại chỗ"`.
   * **Thanh toán ngoài hệ thống (`BR-HND-05`):** Tiền thu mua phế liệu (`totalAmountCash = SUM(round(actualWeightKg * unitPricePerKg))`) được hai bên thanh toán trực tiếp bằng tiền mặt hoặc chuyển khoản tại điểm cân; hệ thống EcoLink chỉ ghi nhận số liệu trên biên nhận số để đối soát và tích điểm.
3. **Cảnh báo độ lệch khối lượng vượt ngưỡng $20\%$ (`MSG-HND-04`):**
   * Hệ thống tính độ lệch phần trăm giữa tổng khối lượng thực tế và tổng khối lượng ước tính trên các dòng ban đầu:
     $$\text{DeviationPercent} = \text{Round}\left(\frac{\sum \text{Actual}_{\text{listed}} - \sum \text{Estimated}_{\text{listed}}}{\sum \text{Estimated}_{\text{listed}}} \times 100,\ 2\right)$$
   * Nếu $|\text{DeviationPercent}| > 20\%$ (`DeviationThresholdPercent = 20`), hệ thống bật cờ `hasDeviationWarning = true` và trả về `messageCode = "MSG-HND-04"` để cảnh báo trên màn hình của cả hai bên (chỉ cảnh báo minh bạch, không chặn phát hành biên nhận).
4. **Tính bất biến sau hoàn tất & Cơ chế chống cộng điểm trùng (`BR-HND-07`, `BR-GAM-01`):**
   * Khi người dân bấm **Xác nhận** (`POST .../receipt/confirm`) hoặc khi `JOB-01` tự động hoàn tất sau `48 giờ`:
     * Hệ thống thực thi câu lệnh nguyên tử `ExecuteUpdateAsync` với điều kiện `WHERE Id = @requestId AND Status = 'AwaitingConfirmation'`. Nếu cả người dân và `JOB-01` chạy đồng thời, chỉ đúng **1 tác vụ** chuyển trạng thái thành công sang `Completed`, đảm bảo tuyệt đối không bao giờ cộng điểm 2 lần.
     * Sau khi chuyển sang `Completed`, biên nhận bị khóa vĩnh viễn (`isLocked = true`, `canConfirm = false` – `BR-HND-07`), mọi thao tác chỉnh sửa hoặc xác nhận lại đều bị từ chối.
     * Hệ thống tính điểm thưởng theo công thức `points = Round(totalWeightKg * 10)` (`PointsPerKg = 10`) và gọi `IGamificationService.AwardPointsForRecyclingAsync` để cộng điểm xanh & nâng cấp Cây ảo cho **cả Người dân và Cơ sở thu gom** (`BR-GAM-01`).

---

## 3. CHI TIẾT TỪNG API ENDPOINT

---

### 3.1. Xem trước Biên nhận Số trước khi Phát hành (`POST /api/v1/recycler/handover-requests/{requestId:guid}/receipt/preview`)

* **Mô tả:** Cho phép cơ sở thu gom kiểm tra trước bảng tính thành tiền từng hạng mục, tổng khối lượng, tổng số tiền cần thanh toán cho người dân, số điểm thưởng dự kiến và cảnh báo chênh lệch khối lượng (`MSG-HND-04`) trước khi bấm phát hành chính thức (`Use Case R-06`). Không ghi dữ liệu vào CSDL (`receiptId = null`).
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`

#### a. Request Parameters (`application/json` – `CreateReceiptRequest`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `requestId` | Route | `Guid` | **Có** | Đơn thuộc về `Recycler` hiện tại và đang `Accepted`/`Scheduled` | Định danh yêu cầu bàn giao (`handover_requests.request_id`). |
| `items` | Body | `ReceiptItemRequest[]` | **Có** | Phải bao gồm đầy đủ tất cả `itemId` có trong đơn gốc (`EF-02`) | Danh sách khối lượng cân thực tế của từng loại phế liệu. |
| `items[].itemId` | Body | `Guid?` | Có (với dòng gốc) | Khớp với `HandoverRequestItem.Id` của đơn; truyền `null` nếu là dòng phát sinh | ID dòng phế liệu trong yêu cầu bàn giao ban đầu. |
| `items[].wasteCategoryId` | Body | `Guid?` | Có (với dòng phát sinh `AF-01`) | Phải thuộc bảng giá `Active` của cơ sở và chưa có trong đơn gốc | ID danh mục phế liệu phát sinh thêm tại chỗ khi cân. |
| `items[].actualWeightKg` | Body | `decimal` | **Có** | `> 0`, tối đa `2` chữ số thập phân | Khối lượng cân thực tế (`kg`) trên đồng hồ cân. |
| `scaleProofPhotoUrls` | Body | `string[]` | **Có** | Từ `1` đến `5` URL (`http/https`, `<= 1000` ký tự) | Mảng URL ảnh chụp mặt đồng hồ cân minh chứng (`BR-HND-04`). |
| `receiptImageUrl` | Body | `string?` | Không | URL `http/https` hợp lệ (`<= 1000` ký tự) | URL ảnh chụp hóa đơn / biên nhận giấy viết tay (nếu có). |

##### Ví dụ Request Body (gồm 2 dòng gốc + 1 dòng phát sinh `glass` tại chỗ):
```json
{
  "items": [
    {
      "itemId": "e1111111-2222-3333-4444-555555555551",
      "wasteCategoryId": null,
      "actualWeightKg": 7.0
    },
    {
      "itemId": "e1111111-2222-3333-4444-555555555552",
      "wasteCategoryId": null,
      "actualWeightKg": 12.5
    },
    {
      "itemId": null,
      "wasteCategoryId": "a5555555-5555-5555-5555-555555555555",
      "actualWeightKg": 3.0
    }
  ],
  "scaleProofPhotoUrls": [
    "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/scale-proofs/scale_pet_01.jpg",
    "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/scale-proofs/scale_carton_02.jpg"
  ],
  "receiptImageUrl": null
}
```

#### b. Ví dụ Response (`200 OK` – Có cảnh báo lệch khối lượng `> 20%` `MSG-HND-04`):
```json
{
  "success": true,
  "messageCode": "MSG-HND-04",
  "message": "Khối lượng xác nhận chênh lệch so với thỏa thuận hơn 20%. Vui lòng kiểm tra trước khi xác nhận.",
  "data": {
    "receiptId": null,
    "receiptCode": null,
    "requestId": "d4e5f6a7-1234-4b5c-89ab-001122334455",
    "requestCode": "HND-20261109-0001",
    "requestStatus": "Accepted",
    "lines": [
      {
        "itemId": "e1111111-2222-3333-4444-555555555551",
        "wasteCategoryId": "a4444444-4444-4444-4444-444444444444",
        "wasteCategoryCode": "plastic",
        "wasteCategoryName": "Nhựa tái chế (Plastic)",
        "estimatedWeightKg": 5.5,
        "actualWeightKg": 7.0,
        "unitPricePerKg": 7000,
        "subTotal": 49000,
        "isExtra": false
      },
      {
        "itemId": "e1111111-2222-3333-4444-555555555552",
        "wasteCategoryId": "a1111111-1111-1111-1111-111111111111",
        "wasteCategoryCode": "cardboard",
        "wasteCategoryName": "Bìa Carton (Cardboard)",
        "estimatedWeightKg": 10.0,
        "actualWeightKg": 12.5,
        "unitPricePerKg": 3800,
        "subTotal": 47500,
        "isExtra": false
      },
      {
        "itemId": null,
        "wasteCategoryId": "a5555555-5555-5555-5555-555555555555",
        "wasteCategoryCode": "glass",
        "wasteCategoryName": "Thủy tinh (Glass)",
        "estimatedWeightKg": 0,
        "actualWeightKg": 3.0,
        "unitPricePerKg": 2500,
        "subTotal": 7500,
        "isExtra": true
      }
    ],
    "totalWeightKg": 22.5,
    "totalAmountCash": 104000,
    "estimatedWeightKg": 15.5,
    "deviationPercent": 25.81,
    "hasDeviationWarning": true,
    "deviationMessage": "Khối lượng xác nhận chênh lệch so với thỏa thuận hơn 20%. Vui lòng kiểm tra trước khi xác nhận.",
    "points": 225,
    "pointsCredited": false,
    "scaleProofPhotoUrls": [
      "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/scale-proofs/scale_pet_01.jpg",
      "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/scale-proofs/scale_carton_02.jpg"
    ],
    "receiptImageUrl": null,
    "createdAt": null,
    "confirmedByUserAt": null,
    "isLocked": false,
    "canConfirm": false,
    "recycler": null,
    "requester": null
  },
  "errors": null,
  "timestamp": "2026-11-10T02:45:00Z"
}
```

---

### 3.2. Phát hành Biên nhận Số Chính thức (`POST /api/v1/recycler/handover-requests/{requestId:guid}/receipt`)

* **Mô tả:** Cơ sở thu gom phát hành biên nhận điện tử chính thức sau khi cân phế liệu tại điểm hẹn (`Use Case R-06`):
  * Sinh mã biên nhận duy nhất theo năm định dạng `RCP-yyyy-NNNN` (VD: `"RCP-2026-0001"`).
  * Cập nhật `ActualWeightKg` và khóa cứng `AgreedPricePerKg` vào từng `HandoverRequestItem` (thêm dòng mới nếu có phế liệu phát sinh `AF-01`).
  * Tạo bản ghi `HandoverReceipt` và chuyển trạng thái `HandoverRequest` sang `"AwaitingConfirmation"` kèm `ActualHandoverAt = UtcNow`.
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`
* **Request Body:** Giống hệt `CreateReceiptRequest` tại mục 3.1.

#### a. Response Parameters (`201 Created` – `ApiResponse<ReceiptDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `receiptId` | `Guid?` | Khóa chính của biên nhận (`handover_receipts.receipt_id`). |
| `receiptCode` | `string?` | Mã biên nhận chuẩn `RCP-yyyy-NNNN`. |
| `requestId` | `Guid` | Khóa chính của yêu cầu bàn giao tương ứng. |
| `requestCode` | `string` | Mã yêu cầu bàn giao (`HND-yyyyMMdd-NNNN`). |
| `requestStatus` | `string` | Trạng thái yêu cầu bàn giao (`"AwaitingConfirmation"` hoặc `"Completed"`). |
| `lines` | `ReceiptLineDto[]` | Chi tiết từng dòng phế liệu: khối lượng ước tính, khối lượng thực tế, đơn giá chốt (`unitPricePerKg`), thành tiền (`subTotal`) và cờ phát sinh (`isExtra`). |
| `totalWeightKg` | `decimal` | Tổng khối lượng cân thực tế của toàn bộ các dòng (`kg`). |
| `totalAmountCash` | `decimal` | Tổng số tiền mặt/chuyển khoản thanh toán cho người dân (`VND`). |
| `estimatedWeightKg` | `decimal` | Tổng khối lượng ước tính ban đầu (chỉ tính các dòng không phải phát sinh). |
| `deviationPercent` | `decimal` | Phần trăm chênh lệch giữa khối lượng thực tế và ước tính (`%`). |
| `hasDeviationWarning` | `boolean` | `true` nếu `|deviationPercent| > 20%` (`MSG-HND-04`). |
| `deviationMessage` | `string?` | Nội dung cảnh báo khi lệch quá `20%`. |
| `points` | `long` | Số điểm thưởng xanh (`10 điểm / kg`) sẽ được cộng hoặc đã cộng. |
| `pointsCredited` | `boolean` | `true` nếu điểm thưởng đã được cộng vào ví điểm của hai bên (`ConfirmedByUserAt != null`). |
| `scaleProofPhotoUrls` | `string[]` | Danh sách `1..5` URL ảnh chụp mặt đồng hồ cân (`BR-HND-04`). |
| `receiptImageUrl` | `string?` | URL ảnh chụp biên nhận giấy (nếu có). |
| `createdAt` | `string? (ISO 8601)` | Thời điểm phát hành biên nhận (UTC). |
| `confirmedByUserAt` | `string? (ISO 8601)` | Thời điểm người dân xác nhận hoặc `JOB-01` tự động hoàn tất (UTC). |
| `isLocked` | `boolean` | `true` khi giao dịch đã `Completed`, khóa vĩnh viễn mọi chỉnh sửa (`BR-HND-07`). |
| `canConfirm` | `boolean` | `true` khi người xem là `Citizen` và đơn đang ở trạng thái `"AwaitingConfirmation"`. |
| `recycler` | `HandoverPartyDto?` | Thông tin cơ sở thu gom đứng tên biên nhận. |
| `requester` | `HandoverPartyDto?` | Thông tin người dân bàn giao phế liệu. |

---

### 3.3. Xem Biên nhận Đã Phát hành phía Cơ sở Thu gom (`GET /api/v1/recycler/handover-requests/{requestId:guid}/receipt`)

* **Mô tả:** Cơ sở thu gom xem lại thông tin chi tiết biên nhận đã lập của đơn bàn giao (`requestId`).
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`
* **Response:** `200 OK` (`ApiResponse<ReceiptDto>`) hoặc `404 Not Found` (`MSG-NOT-FOUND` nếu đơn chưa có biên nhận hoặc không thuộc cơ sở hiện tại).

---

### 3.4. Người dân Xem Chi tiết Biên nhận Số (`GET /api/v1/handover-requests/{requestId:guid}/receipt`)

* **Mô tả:** Người dân mở biên nhận do cơ sở thu gom vừa phát hành để kiểm tra đối chiếu khối lượng cân thực tế, ảnh chụp đồng hồ cân, thành tiền và số điểm xanh sẽ nhận (`Use Case U-09`).
  * Nếu biên nhận có `hasDeviationWarning == true`, API tự động trả kèm `messageCode = "MSG-HND-04"` và `message = data.DeviationMessage` để giao diện hiển thị hộp cảnh báo nổi bật.
* **Phân quyền:** `[Authorize(Roles = "User,Citizen")]`

---

### 3.5. Người dân Xác nhận Biên nhận & Hoàn tất Giao dịch (`POST /api/v1/handover-requests/{requestId:guid}/receipt/confirm`)

* **Mô tả:** Sau khi kiểm tra biên nhận và nhận đủ tiền thanh toán trực tiếp từ cơ sở thu gom, người dân bấm nút **"Xác nhận đã nhận tiền & Hoàn tất"** (`Use Case U-09`):
  * Chuyển trạng thái đơn bàn giao sang `"Completed"` một cách nguyên tử.
  * Ghi nhận `ConfirmedByUserAt = UtcNow`, khóa vĩnh viễn biên nhận (`isLocked = true`, `canConfirm = false` – `BR-HND-07`).
  * Tính và lưu `PointsAwarded = Round(totalWeightKg * 10)`, đồng thời gọi `IGamificationService.AwardPointsForRecyclingAsync` để cộng điểm thưởng xanh cho **cả Người dân và Cơ sở thu gom** (`BR-GAM-01`).
* **Phân quyền:** `[Authorize(Roles = "User,Citizen")]`

#### a. Request Parameters
* **Route Parameter:** `requestId` (`Guid` – ID yêu cầu bàn giao).
* **Request Body:** Không yêu cầu Body.

#### b. Ví dụ Response Thành công (`200 OK` – `MSG-HND-03`):
```json
{
  "success": true,
  "messageCode": "MSG-HND-03",
  "message": "Giao dịch #RCP-2026-0001 đã được đánh dấu hoàn tất.",
  "data": {
    "receiptId": "f9876543-1234-4abc-9def-001122334455",
    "receiptCode": "RCP-2026-0001",
    "requestId": "d4e5f6a7-1234-4b5c-89ab-001122334455",
    "requestCode": "HND-20261109-0001",
    "requestStatus": "Completed",
    "lines": [
      {
        "itemId": "e1111111-2222-3333-4444-555555555551",
        "wasteCategoryId": "a4444444-4444-4444-4444-444444444444",
        "wasteCategoryCode": "plastic",
        "wasteCategoryName": "Nhựa tái chế (Plastic)",
        "estimatedWeightKg": 5.5,
        "actualWeightKg": 5.5,
        "unitPricePerKg": 7000,
        "subTotal": 38500,
        "isExtra": false
      },
      {
        "itemId": "e1111111-2222-3333-4444-555555555552",
        "wasteCategoryId": "a1111111-1111-1111-1111-111111111111",
        "wasteCategoryCode": "cardboard",
        "wasteCategoryName": "Bìa Carton (Cardboard)",
        "estimatedWeightKg": 10.0,
        "actualWeightKg": 10.0,
        "unitPricePerKg": 3800,
        "subTotal": 38000,
        "isExtra": false
      }
    ],
    "totalWeightKg": 15.5,
    "totalAmountCash": 76500,
    "estimatedWeightKg": 15.5,
    "deviationPercent": 0.0,
    "hasDeviationWarning": false,
    "deviationMessage": null,
    "points": 155,
    "pointsCredited": true,
    "scaleProofPhotoUrls": [
      "https://pub-44be889e3c6b4fd4ae0e2cca3ac1fdd2.r2.dev/scale-proofs/scale_pet_01.jpg"
    ],
    "receiptImageUrl": null,
    "createdAt": "2026-11-10T02:48:00Z",
    "confirmedByUserAt": "2026-11-10T02:55:12Z",
    "isLocked": true,
    "canConfirm": false,
    "recycler": {
      "id": "22222222-2222-2222-2222-222222222222",
      "name": "Vựa phế liệu EcoGreen",
      "phone": "0902222222",
      "address": "123 Lê Văn Việt, Phường Hiệp Phú, TP. Thủ Đức, TP.HCM"
    },
    "requester": {
      "id": "11111111-1111-1111-1111-111111111111",
      "name": "Nguyễn Văn An",
      "phone": "0901234567",
      "address": "Số 45 Lê Duẩn, Phường Bến Nghé, Quận 1, TP.HCM"
    }
  },
  "errors": null,
  "timestamp": "2026-11-10T02:55:12Z"
}
```

---

### 3.6. Xem Lịch sử Giao dịch Hoàn tất của Người dân (`GET /api/v1/transactions`)

* **Mô tả:** Trả về danh sách các giao dịch đã hoàn tất (`ConfirmedByUserAt != null`) của người dân đang đăng nhập (`Use Case U-12`), kèm theo số liệu tổng cộng lũy kế trên toàn bộ kết quả lọc (`totalWeightKg`, `totalAmountCash`, `totalPoints`).
* **Phân quyền:** `[Authorize(Roles = "User,Citizen")]`

#### a. Request Parameters (Query String)

| Tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---:|:---|
| `from` | Query | `DateTime?` | Không | `null` | Lọc từ ngày hoàn tất (`yyyy-MM-dd`). |
| `to` | Query | `DateTime?` | Không | `null` | Lọc đến hết ngày hoàn tất (`yyyy-MM-dd` – nếu không truyền giờ, hệ thống tự động cộng thêm `1 ngày` để lấy trọn vẹn cả ngày kết thúc). |
| `page` | Query | `int` | Không | `1` | Trang cần lấy (`>= 1`). |
| `pageSize` | Query | `int` | Không | `20` | Số bản ghi mỗi trang (`[1, 50]`). |

#### b. Response Parameters (`200 OK` – `ApiResponse<TransactionHistoryDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `items` | `TransactionHistoryItemDto[]` | Danh sách các giao dịch hoàn tất trong trang hiện tại. |
| `items[].receiptId` | `Guid` | ID biên nhận số. |
| `items[].receiptCode` | `string` | Mã biên nhận (`RCP-yyyy-NNNN`). |
| `items[].requestId` | `Guid` | ID yêu cầu bàn giao gốc. |
| `items[].requestCode` | `string` | Mã yêu cầu bàn giao (`HND-yyyyMMdd-NNNN`). |
| `items[].completedAt` | `string (ISO 8601)` | Thời điểm hoàn tất giao dịch (`ConfirmedByUserAt`). |
| `items[].counterpartName` | `string` | Tên đối tác giao dịch (Tên cơ sở thu gom khi người xem là `Citizen`; Họ tên người dân khi người xem là `Recycler`). |
| `items[].categories` | `string[]` | Danh sách tên các loại phế liệu đã bàn giao trong đơn. |
| `items[].totalWeightKg` | `decimal` | Tổng khối lượng cân thực tế của giao dịch (`kg`). |
| `items[].totalAmountCash` | `decimal` | Tổng số tiền của giao dịch (`VND`). |
| `items[].pointsAwarded` | `long` | Số điểm xanh đã nhận từ giao dịch này. |
| `page` | `int` | Trang hiện tại. |
| `pageSize` | `int` | Kích thước trang. |
| `totalCount` | `int` | Tổng số giao dịch thỏa mãn điều kiện lọc. |
| `totalWeightKg` | `decimal` | Tổng khối lượng lũy kế trên toàn bộ kết quả lọc (`kg`). |
| `totalAmountCash` | `decimal` | Tổng giá trị tiền lũy kế trên toàn bộ kết quả lọc (`VND`). |
| `totalPoints` | `long` | Tổng điểm thưởng xanh lũy kế trên toàn bộ kết quả lọc. |

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "messageCode": null,
  "message": null,
  "data": {
    "items": [
      {
        "receiptId": "f9876543-1234-4abc-9def-001122334455",
        "receiptCode": "RCP-2026-0001",
        "requestId": "d4e5f6a7-1234-4b5c-89ab-001122334455",
        "requestCode": "HND-20261109-0001",
        "completedAt": "2026-11-10T02:55:12Z",
        "counterpartName": "Vựa phế liệu EcoGreen",
        "categories": [
          "Nhựa tái chế (Plastic)",
          "Bìa Carton (Cardboard)"
        ],
        "totalWeightKg": 15.5,
        "totalAmountCash": 76500,
        "pointsAwarded": 155
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalWeightKg": 15.5,
    "totalAmountCash": 76500,
    "totalPoints": 155
  },
  "errors": null,
  "timestamp": "2026-11-10T03:00:00Z"
}
```

---

### 3.7. Xem Lịch sử Giao dịch Hoàn tất của Cơ sở Thu gom (`GET /api/v1/recycler/transactions`)

* **Mô tả:** Trả về danh sách các giao dịch thu mua đã hoàn tất của cơ sở thu gom đang đăng nhập (`Use Case U-12` phía `Recycler`). Trường `counterpartName` trong từng phần tử sẽ hiển thị họ tên của người dân (`FullName`) đã bán phế liệu.
* **Phân quyền:** `[Authorize(Roles = "Recycler,Collector")]`
* **Query Parameters & Response Schema:** Giống hệt mục 3.6 (`from`, `to`, `page`, `pageSize` $\rightarrow$ `ApiResponse<TransactionHistoryDto>`).

---

## 4. BẢNG MÃ LỖI & MÃ THÔNG BÁO TRONG MODULE 8

| HTTP Status | `messageCode` | Quy tắc (`BR`) | Trường hợp xảy ra & Nội dung thông báo |
|:---:|:---|:---:|:---|
| **`200 OK`** | `MSG-HND-03` | `U-09`, `BR-HND-07`, `BR-GAM-01` | `"Giao dịch #{ReceiptCode} đã được đánh dấu hoàn tất."` |
| **`200` / `201`** | `MSG-HND-04` | `R-06`, `U-09` | `"Khối lượng xác nhận chênh lệch so với thỏa thuận hơn 20%. Vui lòng kiểm tra trước khi xác nhận."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-HND-02` | `"Chỉ lập biên nhận cho yêu cầu đã được chấp nhận (hiện tại: {Status})."`<br>`"Yêu cầu đã có biên nhận đang chờ người dân xác nhận."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-HND-04`, `BR-HND-07` | `"Yêu cầu này đã có biên nhận."`<br>`"Giao dịch đã hoàn tất và không thể thay đổi."`<br>`"Yêu cầu chưa có biên nhận chờ xác nhận."`<br>`"Tối đa 5 ảnh cân."` |
| **`400 Bad Request`** | `MSG-AUTH-02` | `BR-HND-04`, `BR-HND-05`, `EF-02` | `"Vui lòng tải lên ít nhất 1 ảnh cân (BR-HND-04)."`<br>`"Khối lượng thực tế phải lớn hơn 0 và tối đa 2 chữ số thập phân."`<br>`"Vui lòng nhập khối lượng thực tế cho tất cả loại phế liệu trong yêu cầu."`<br>`"Dòng phát sinh cần chọn loại phế liệu."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `BR-HND-05`, `AF-01` | `"Có dòng phế liệu không thuộc yêu cầu này."`<br>`"Mỗi dòng phế liệu chỉ được nhập một lần."`<br>`"Loại phế liệu này đã có trong yêu cầu, hãy nhập theo dòng có sẵn."`<br>`"Loại \"{Name}\" không nằm trong danh mục đang thu mua của cơ sở."` |
| **`400 Bad Request`** | `MSG-CLS-01` | `BR-HND-04` | `"URL ảnh không hợp lệ."` |
| **`404 Not Found`** | `MSG-NOT-FOUND` | `BR-HND-03` | `"Không tìm thấy yêu cầu bàn giao."` / `"Không tìm thấy biên nhận."` |

