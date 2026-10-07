---
description: Quy chuẩn thiết kế RESTful API và Viết Test Case cho EcoLink
globs: ["src/Ecolink.Backend/**/*", "src/Ecolink.AiService/**/*", "documents/**/*"]
---

# Quy chuẩn Thiết kế API & Kiểm thử (API Design & Testing Rules)

Khi viết API Controllers, DTOs hoặc tài liệu kiểm thử cho EcoLink, luôn tuân thủ:

1. **Chuẩn RESTful API & Response Envelope:**
   - URL: Chữ thường, danh từ số nhiều, kebab-case (`/api/v1/trade-orders`).
   - Mọi response phải bọc trong `ApiResponse<T>`:
     `{ success, messageCode, message, data, errors, timestamp }`.
   - Lỗi trả về HTTP status tương ứng (400, 401, 403, 404, 409), không trả HTTP 200 kèm error body.
   - Luôn sử dụng mã thông điệp chuẩn `MSG-[MODULE]-ID` và ràng buộc `BR-[MODULE]-ID`.

2. **Quy chuẩn Viết Test Case:**
   - Mã Test Case: `TC-[MODULE]-[UC_NUM]-[STT]_[TYPE]` (với TYPE là `_P`, `_N`, `_B`, `_S`, `_OOD`).
   - Đảm bảo tính truy xuất nguồn gốc (Traceability): Mỗi test case phải liên kết đến `UC-[MODULE]-[ID]`, `BR-[MODULE]-[ID]` và `MSG-[MODULE]-[ID]`.

Chi tiết xem tại:
- `documents/api_design_and_response_contract_guidelines.md`
- `documents/test_case_specification_guidelines.md`
