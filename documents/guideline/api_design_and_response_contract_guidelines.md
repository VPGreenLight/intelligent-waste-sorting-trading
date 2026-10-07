# QUY CHUẨN THIẾT KẾ RESTFUL API VÀ KHUNG PHẢN HỒI (API DESIGN & RESPONSE CONTRACT GUIDELINE)
**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Chuẩn tham chiếu:** Đồ án tốt nghiệp Capstone Project (SEP490 - FPT University) & RESTful Enterprise Architecture  
**Phạm vi áp dụng:** Web API (.NET 10), AI Service (Python FastAPI), Web Frontend & Mobile App  

---

## 1. MỤC ĐÍCH VÀ NGUYÊN TẮC CỐT LÕI

* **Tính đồng nhất (Consistency):** Toàn bộ các endpoints từ Backend chính đến AI Service đều phải tuân thủ cùng một cấu trúc URL, phương thức HTTP và khung dữ liệu trả về (Response Envelope).
* **Độc lập nền tảng (Frontend Decoupling):** Frontend (Mobile/Web) chỉ cần xây dựng một HTTP Interceptor duy nhất để xử lý kết quả, hiển thị thông báo thành công hoặc bắt lỗi theo hệ thống mã thông điệp `MSG-[MODULE]-ID` và `BR-[MODULE]-ID`.
* **Rõ ràng và Dễ đoán (Predictability):** Trạng thái HTTP Status Code phải phản ánh đúng bản chất kỹ thuật, không trả về `200 OK` kèm theo nội dung báo lỗi bên trong.

---

## 2. NGUYÊN TẮC ĐỊNH DANH URL (URI DESIGN CONVENTIONS)

1. **Phiên bản hóa bắt buộc (Versioning):**  
   Mọi endpoint nghiệp vụ bắt buộc có tiền tố `/api/v1/`.  
   *Ví dụ:* `/api/v1/trade-orders`, `/api/v1/waste-categories`.
2. **Sử dụng Danh từ số nhiều, chữ thường, nối dấu gạch ngang (kebab-case):**  
   *  **Đúng:** `/api/v1/trade-orders`, `/api/v1/collection-points`, `/api/v1/recycling-rules`.  
   * ❌ **Sai:** `/api/v1/tradeOrder`, `/api/v1/trade_orders`, `/api/v1/getTradeOrders`.
3. **Không dùng Động từ trong URL:**  
   Hành động phải được thể hiện qua **Phương thức HTTP (HTTP Verbs)**:
   * `GET`: Lấy thông tin tài nguyên (Idempotent, Safe).
   * `POST`: Tạo mới tài nguyên hoặc kích hoạt tác vụ tính toán (như scan ảnh AI).
   * `PUT`: Cập nhật toàn bộ tài nguyên (Idempotent).
   * `PATCH`: Cập nhật một phần thuộc tính của tài nguyên.
   * `DELETE`: Xóa tài nguyên (xóa mềm hoặc xóa cứng).
4. **Quan hệ Cha - Con (Nested Resources):**  
   Khi một tài nguyên phụ thuộc hoàn toàn vào tài nguyên cha:  
   * `GET /api/v1/users/{userId}/eco-profile` (Xem hồ sơ sinh thái của user).  
   * `POST /api/v1/trade-orders/{orderId}/confirm-receipt` (Xác nhận biên nhận cho một đơn cụ thể).

---

## 3. KHUNG PHẢN HỒI CHUẨN (STANDARD API RESPONSE ENVELOPE)

Mọi phản hồi từ hệ thống EcoLink (thành công hoặc thất bại) đều bắt buộc bọc trong schema JSON tiêu chuẩn sau:

### 3.1. Phản hồi Thành công (Success Response)
**HTTP Status:** `200 OK` hoặc `201 Created`

```json
{
  "success": true,
  "messageCode": "MSG-HND-01",
  "message": "Tiếp nhận yêu cầu bàn giao thành công.",
  "data": {
    "tradeOrderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Pending",
    "qrCodeValue": "ECOLINK_ORDER_3FA85F64",
    "createdAt": "2026-10-06T14:30:00Z"
  },
  "errors": null,
  "timestamp": "2026-10-06T14:30:00Z"
}
```

### 3.2. Phản hồi Lỗi Nghiệp vụ hoặc Xác thực (Business / Validation Error)
**HTTP Status:** `400 Bad Request` hoặc `422 Unprocessable Entity`

```json
{
  "success": false,
  "messageCode": "MSG-HND-03",
  "message": "Đơn hẹn bàn giao đã hoàn tất hoặc không còn hiệu lực.",
  "data": null,
  "errors": [
    "Đơn hàng đã được xác nhận vào lúc 14:15:00. Không thể quét lại QR."
  ],
  "timestamp": "2026-10-06T14:32:10Z"
}
```

### 3.3. Phản hồi Lỗi Xác thực Đầu vào chi tiết (FluentValidation / Model Binding Error)
**HTTP Status:** `400 Bad Request`

```json
{
  "success": false,
  "messageCode": "MSG-GEN-01",
  "message": "Dữ liệu đầu vào không hợp lệ.",
  "data": null,
  "errors": [
    "ActualWeightKg: Trọng lượng thực tế phải lớn hơn 0.",
    "UnitPrice: Đơn giá phế liệu không được để trống."
  ],
  "timestamp": "2026-10-06T14:35:00Z"
}
```

---

## 4. QUY CHUẨN PHÂN TRANG, TÌM KIẾM VÀ BỘ LỌC (PAGINATION & FILTERING)

### 4.1. Tham số Request gửi lên (Query Parameters)
* `pageNumber` (int, mặc định `1`): Số thứ tự trang (1-indexed).
* `pageSize` (int, mặc định `10` hoặc `20`, tối đa `100`): Số lượng bản ghi mỗi trang.
* `sortBy` (string): Tên trường cần sắp xếp (ví dụ: `createdAt`, `actualWeightKg`).
* `sortOrder` (string): Chiều sắp xếp (`asc` hoặc `desc`, mặc định `desc`).
* `keyword` (string, optional): Từ khóa tìm kiếm tự do.

*Ví dụ:* `GET /api/v1/trade-orders?pageNumber=1&pageSize=10&sortBy=createdAt&sortOrder=desc&status=Pending`

### 4.2. Khung dữ liệu trả về cho Danh sách phân trang (PagedResult<T>)
```json
{
  "success": true,
  "messageCode": null,
  "message": null,
  "data": {
    "items": [
      { "id": "uuid-1", "orderCode": "ORD-001", "status": "Pending" },
      { "id": "uuid-2", "orderCode": "ORD-002", "status": "Completed" }
    ],
    "pageNumber": 1,
    "pageSize": 10,
    "totalRecords": 45,
    "totalPages": 5,
    "hasPreviousPage": false,
    "hasNextPage": true
  },
  "errors": null,
  "timestamp": "2026-10-06T14:30:00Z"
}
```

---

## 5. BẢNG ÁNH XẠ MÃ HTTP STATUS CODE VÀ USE CASE

| HTTP Status Code | Ý nghĩa | Khi nào áp dụng trong EcoLink? |
| :---: | :--- | :--- |
| **`200 OK`** | Thành công | GET dữ liệu thành công; PUT/PATCH cập nhật thành công; POST xử lý tác vụ có trả về kết quả (như nhận diện ảnh AI). |
| **`201 Created`** | Tạo mới thành công | POST tạo tài khoản mới, tạo đơn hẹn bàn giao `trade-orders`, tạo biên nhận bàn giao. Kèm theo URI của tài nguyên mới. |
| **`204 No Content`**| Thành công không có body | DELETE tài nguyên (ví dụ xóa mềm một ảnh tạm hoặc hủy yêu cầu). |
| **`400 Bad Request`**| Dữ liệu sai quy chuẩn | Vi phạm định dạng đầu vào, sai validation rules, hoặc vi phạm Business Rules (`BR-HND-xx`). |
| **`401 Unauthorized`**| Chưa xác thực | Không truyền JWT Bearer Token, Token hết hạn, hoặc chữ ký Token không hợp lệ. |
| **`403 Forbidden`**| Không có quyền | Đăng nhập đúng nhưng không đủ Role (ví dụ: `User` cố tình gọi API duyệt đơn của `Staff` hoặc `Admin`). |
| **`404 Not Found`**| Không tìm thấy | ID đơn hàng, ID loại rác, hoặc ID người dùng không tồn tại trong hệ thống. |
| **`409 Conflict`** | Xung đột trạng thái | Trùng lặp dữ liệu độc nhất (ví dụ: Số điện thoại đã được đăng ký, đơn hàng đang được xử lý đồng thời). |
| **`500 Internal Error`**| Lỗi hệ thống ngoài ý muốn | Lỗi kết nối CSDL, crash ứng dụng. Trả về thông báo chung cho client, không lộ stack trace. |

---

## 6. QUY TẮC ĐẶT TÊN THUỘC TÍNH (JSON PROPERTY NAMING)

* Bắt buộc sử dụng chuẩn **camelCase** cho toàn bộ JSON keys:
  *  `tradeOrderId`, `actualWeightKg`, `unitPrice`, `createdAt`.
  * ❌ `TradeOrderId`, `actual_weight_kg`, `UNIT_PRICE`.
* Kiểu thời gian (Date/Time): Luôn định dạng theo chuẩn **ISO 8601 UTC** kết thúc bằng chữ `Z`:  
  * `2026-10-06T14:30:00Z`
* Kiểu tiền tệ và khối lượng: Sử dụng kiểu số nguyên (`long`) cho tiền VND (không lẻ xu), và kiểu số thực (`decimal`) làm tròn tối đa 2 chữ số thập phân cho số kg (`5.25`).

---

## 7. MẪU TRIỂN KHAI BACKEND C# (.NET 10)

### 7.1. Lớp DTO Phản hồi dùng chung (ApiResponse<T>)
```csharp
namespace Ecolink.Application.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? MessageCode { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? message = null, string? messageCode = null) =>
        new() { Success = true, Data = data, Message = message, MessageCode = messageCode };

    public static ApiResponse<T> Fail(string message, string? messageCode = null, List<string>? errors = null) =>
        new() { Success = false, Message = message, MessageCode = messageCode, Errors = errors };
}
```

### 7.2. Controller chuẩn mực
```csharp
[ApiController]
[Route("api/v1/trade-orders")]
public class TradeOrderController : ControllerBase
{
    private readonly ITradeTransactionService _tradeService;

    public TradeOrderController(ITradeTransactionService tradeService)
    {
        _tradeService = tradeService;
    }

    [HttpPost("{id:guid}/confirm-receipt")]
    [ProducesResponseType(typeof(ApiResponse<TransactionReceiptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmReceipt(Guid id, [FromBody] ConfirmQrReceiptRequest request)
    {
        request.TradeOrderId = id;
        var receipt = await _tradeService.ConfirmReceiptByQrAsync(request);

        return Ok(ApiResponse<TransactionReceiptDto>.Ok(
            receipt, 
            message: "Xác nhận biên nhận bàn giao thành công.", 
            messageCode: "MSG-HND-01"
        ));
    }
}
```
