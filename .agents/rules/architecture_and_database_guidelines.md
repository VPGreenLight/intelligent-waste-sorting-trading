---
description: Quy chuẩn Clean Architecture .NET và Thiết kế CSDL PostgreSQL PostGIS cho EcoLink
globs: ["src/Ecolink.Backend/**/*", "documents/dbml.io", "documents/guideline/**/*"]
---

# Quy chuẩn Kiến trúc .NET và Thiết kế Cơ sở Dữ liệu cho EcoLink

Khi phát triển backend .NET và tương tác CSDL, hãy tuân thủ nghiêm ngặt:

1. **Clean Architecture 4 Tầng:**
   - `Domain`: Entities, Enums, Exceptions (0 dependencies ngoài).
   - `Application`: Use cases, DTOs, Interfaces, Validators, Events.
   - `Infrastructure`: EF Core DbContext, PostGIS mapping, Redis, External APIs, Interceptors.
   - `WebApi`: Thin Controllers chỉ nhận request, gọi application layer và trả về `ApiResponse<T>`. Không viết query EF Core trong Controller.
   - Luôn sử dụng Global Exception Middleware, không dùng `try-catch` bừa bãi trong Controller.

2. **C# Coding Style:**
   - 100% I/O phải là `async / await` và truyền `CancellationToken`. Không dùng `.Result` hay `.Wait()`.
   - PascalCase cho Methods/Properties, camelCase cho biến, `_camelCase` cho private fields.

3. **Thiết kế CSDL PostgreSQL & PostGIS:**
   - Tên bảng và cột bắt buộc dùng `snake_case` (`trade_orders`, `user_id`).
   - Mọi bảng nghiệp vụ kế thừa `BaseAuditableEntity` (`created_at`, `created_by`, `updated_at`, `updated_by`, `is_deleted`, `deleted_at`) và sử dụng Soft Delete tự động qua EF Core Interceptor.
   - Tọa độ GPS lưu bằng `geography(Point, 4326)` có chỉ mục `GIST`. Không tính khoảng cách bằng C# trong RAM.

Chi tiết xem tại:
- `documents/guideline/clean_architecture_and_coding_guidelines.md`
- `documents/guideline/database_design_and_audit_guidelines.md`
