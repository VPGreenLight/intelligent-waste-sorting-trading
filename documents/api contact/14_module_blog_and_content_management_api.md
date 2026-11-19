# ĐẶC TẢ HỢP ĐỒNG API CHI TIẾT (API CONTRACT SPECIFICATION)
## PHÂN HỆ M-14 / FE-16: QUẢN TRỊ BÀI VIẾT BLOG & NỘI DUNG TRUYỀN THÔNG (`BLOG & CONTENT MANAGEMENT`)

* **Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform (`SEP490_G141`)
* **Phiên bản tài liệu:** `v1.0.0` (Cập nhật đồng bộ với mã nguồn `Ecolink.WebApi` / `.NET 10` & PostgreSQL 16 + Cloudflare R2)
* **Use Cases bao phủ:**
  * `G-01`: Browse Public Eco Blogs (*Khách / Người dân xem danh sách bài đã xuất bản, lọc theo từ khóa hoặc tag và đọc theo slug*)
  * `S-08`: Create Blog Post (*Staff `[MANAGE_BLOG]` tải ảnh Cloudflare R2, soạn bản nháp, sửa, xóa nháp, nộp duyệt & thu hồi bài*)
  * `A-09`: Approve / Reject Blog Post (*Admin kiểm duyệt hàng đợi chờ duyệt, xuất bản hoặc từ chối kèm góp ý bắt buộc + ghi AuditLog*)
* **Controllers liên quan:** `PublicBlogController` (`STT 140–141`), `StaffBlogController` (`STT 142–147`), `AdminBlogController` (`STT 148–149`)
* **Phạm vi Endpoint:** `10` REST APIs (`STT 140` – `STT 149`) trong [`00_api_inventory_and_endpoint_statistics.md`](./00_api_inventory_and_endpoint_statistics.md) + các route hỗ trợ (`GET /staff/blogs/{id}`, `POST /staff/blogs/{id}/recall`, `GET /admin/blogs/{id}`, `POST /admin/blogs/{id}/approve|reject`).
* **Tiêu chuẩn phản hồi:** Chuẩn hóa theo `ApiResponse<T>` trong [`00_api_overview_and_response_contract.md`](./00_api_overview_and_response_contract.md).

---

## 1. TỔNG QUAN KIẾN TRÚC & MA TRẬN API PHÂN HỆ `M-14`

### 1.1. Máy trạng thái Xuất bản & Quy tắc Kiểm soát Quyền (`BR-ADM-08`, `BR-ADM-05`)

1. **Quyền biên tập tách biệt xuất bản (`BR-ADM-08`):**
   * `Staff` phải sở hữu quyền `MANAGE_BLOG` (`[HasPermission(StaffPermissions.ManageBlog)]`; `Admin` đi qua mặc định). Mỗi `Staff` chỉ nhìn thấy và thao tác trên bài viết có `AuthorId == callerId`.
   * Bài do `Staff` tạo chỉ tồn tại ở trạng thái `Draft` hoặc `Pending_Approval`; chỉ `Admin` (`[Authorize(Roles = "Admin")]`) mới được duyệt xuất bản sang `Approved` (`PublishedAt = NOW()`). Khi bị từ chối (`Reject`), bài quay về trạng thái `Draft` kèm `RejectionReason` để `Staff` có thể sửa ngay và nộp duyệt lại.
2. **Sinh Slug Tiếng Việt Không Dấu & Khớp Chính xác Thẻ Tag:**
   * Tiêu đề `10..150` ký tự được chuyển sang slug ASCII URL-safe; nếu trùng trong DB (hoặc đua đồng thời chạm `ux_blog_posts_slug`), tự động thêm hậu tố `-2`, `-3`... hoặc mã ngẫu nhiên.
   * Mảng `tags` tối đa `5` thẻ (`2..30` ký tự mỗi thẻ, khử trùng lặp không phân biệt hoa thường), lưu dưới dạng JSON mảng trong cột `tags` và lọc công khai bằng khớp nguyên thẻ `%"tag"%` để không khớp nhầm chuỗi tiền tố.
3. **Upload Ảnh Cloudflare R2 (`S-08`):**
   * `POST /api/v1/staff/blogs/upload-media` kiểm tra dung lượng tối đa `5 MB` (`MSG-NTF-03`), định dạng `.jpg/.jpeg/.png/.webp` (`MSG-CLS-01`) và lưu vào prefix `blog-media/yyyy/MM/...`.

### 1.2. Bảng Quy chiếu Nhanh `10` Endpoints (`STT 140` – `STT 149`) & Alias Route Tương thích

| STT | Phương thức | Tuyến đường Chuẩn (`Canonical Route`) | Alias Route Tương thích | Controller / Action | Phân quyền (`RBAC`) |
|:---:|:---:|:---|:---|:---|:---|
| **140** | `GET` | `/api/v1/blogs` | `/api/v1/blog-posts` | `PublicBlogController.List` | `AllowAnonymous` |
| **141** | `GET` | `/api/v1/blogs/{slug}` | `/api/v1/blog-posts/{slug}` | `PublicBlogController.GetBySlug` | `AllowAnonymous` |
| **142** | `POST` | `/api/v1/staff/blogs/upload-media` | `/api/v1/staff/blog-posts/upload-media` | `StaffBlogController.UploadMedia` | `Staff [MANAGE_BLOG]`, `Admin` |
| **143** | `GET` | `/api/v1/staff/blogs`<br>*(+ `/{id:guid}`)* | `/api/v1/staff/blog-posts`<br>*(+ `/{id:guid}`)* | `StaffBlogController.List` / `GetById` | `Staff [MANAGE_BLOG]`, `Admin` |
| **144** | `POST` | `/api/v1/staff/blogs` | `/api/v1/staff/blog-posts` | `StaffBlogController.Create` | `Staff [MANAGE_BLOG]`, `Admin` |
| **145** | `PUT` | `/api/v1/staff/blogs/{id:guid}` | `/api/v1/staff/blog-posts/{id:guid}` | `StaffBlogController.Update` | `Staff [MANAGE_BLOG]`, `Admin` |
| **146** | `DELETE` | `/api/v1/staff/blogs/{id:guid}` | `/api/v1/staff/blog-posts/{id:guid}` | `StaffBlogController.Delete` | `Staff [MANAGE_BLOG]`, `Admin` |
| **147** | `POST` | `/api/v1/staff/blogs/{id:guid}/submit`<br>*(+ `/recall`)* | `/api/v1/staff/blog-posts/{id:guid}/submit`<br>*(+ `/recall`)* | `StaffBlogController.Submit` / `Recall` | `Staff [MANAGE_BLOG]`, `Admin` |
| **148** | `GET` | `/api/v1/admin/blogs/pending`<br>*(+ `/{id:guid}`)* | `/api/v1/admin/blog-posts/queue`<br>*(+ `/{id:guid}`)* | `AdminBlogController.Queue` / `GetById` | `Admin` |
| **149** | `PATCH` | `/api/v1/admin/blogs/{id:guid}/review` | `/api/v1/admin/blog-posts/{id:guid}/review`<br>*(+ `POST /approve`, `/reject`)* | `AdminBlogController.Review` / `Approve` / `Reject` | `Admin` |

---

## 2. ĐẶC TẢ CHI TIẾT CÁC ENDPOINTS (`STT 140` – `STT 149`)

### 2.1. Nhóm Công khai (`PublicBlogController` — `STT 140–141`)

#### `[STT 140]` `GET /api/v1/blogs`
* **Query Parameters:** `search` (`string?`), `tag` (`string?`), `page` (`1`), `pageSize` (`12`, kẹp `[1..50]`).
* **Response `200 OK`:** `BlogPagedResult<BlogPostSummaryDto>` — chỉ chứa bài có `Status = Approved` và `PublishedAt <= NOW()`, sắp xếp mới xuất bản trước.

#### `[STT 141]` `GET /api/v1/blogs/{slug}`
* **Response `200 OK` (`PublicBlogPostDto`):** `id`, `title`, `slug`, `summary`, `content`, `coverImageUrl`, `tags`, `authorName`, `publishedAt` (ẩn hoàn toàn các trường kiểm duyệt nội bộ như `rejectionReason`, `status`).
* **Response `404 Not Found`:** Nếu slug không tồn tại hoặc bài viết chưa `Approved` (`Draft` / `Pending_Approval`).

### 2.2. Nhóm Soạn thảo & Quản lý Bài viết Staff (`StaffBlogController` — `STT 142–147`)

#### `[STT 142]` `POST /api/v1/staff/blogs/upload-media`
* **Content-Type:** `multipart/form-data` (field `file`, tối đa `5 MB`, chỉ `.jpg/.jpeg/.png/.webp`).
* **Response `200 OK` (`MSG-GEN-01`):** `BlogMediaUploadResponseDto { url, fileUrl, objectKey, contentType, sizeBytes }`.

#### `[STT 144]` & `[STT 145]` `POST /api/v1/staff/blogs` & `PUT /api/v1/staff/blogs/{id:guid}`
* **Request Body (`SaveBlogPostRequest`):**
  ```json
  {
    "title": "Hướng dẫn phân loại nhựa PET và HDPE tại nhà",
    "summary": "Nhận biết ký hiệu đáy chai nhựa trước khi thu gom.",
    "content": "<p>Nội dung HTML/Markdown chi tiết...</p>",
    "coverImageUrl": "https://cdn.ecolink.test/blog-media/cover.jpg",
    "tags": ["nhua-pet", "phan-loai-rac"],
    "submitForApproval": true
  }
  ```
* **Validation:**
  * `title`: `10..150` ký tự; `tags`: `0..5` thẻ (`2..30` ký tự/thẻ); `coverImageUrl`: URL `http(s)` hoặc `/assets/` đuôi `.jpg/.png/.webp`.
  * Nếu `submitForApproval == true`: bắt buộc phải có `coverImageUrl` và `content` (`MSG-AUTH-02`), tự động gọi nộp duyệt (`MSG-ADM-06`) và tạo `Notification` tới toàn bộ `Admin Active`.
* **Ownership & State Rules:** Chỉ tác giả chính (`AuthorId == callerId`) mới được sửa/nộp/thu hồi/xóa; không thể sửa hoặc xóa bài đang `Pending_Approval` hay đã `Approved` (`400 MSG-GEN-01`).

#### `[STT 146]` & `[STT 147]` `DELETE /api/v1/staff/blogs/{id:guid}`, `POST .../submit`, `POST .../recall`
* **`POST .../submit`:** Chuyển `Draft -> Pending_Approval` và thông báo `Admin` (`200 OK`, `MSG-ADM-06`).
* **`POST .../recall`:** Chuyển `Pending_Approval -> Draft`, xóa `SubmittedAt` và gửi thông báo thu hồi cho `Admin`.
* **`DELETE`:** Xóa cứng bản ghi `Draft` thuộc quyền sở hữu của `Staff`.

### 2.3. Nhóm Kiểm duyệt Admin (`AdminBlogController` — `STT 148–149`)

#### `[STT 148]` `GET /api/v1/admin/blogs/pending`
* **Query Parameters:** `search` (lọc theo tiêu đề hoặc tên tác giả), `sort` (`oldest` mặc định | `newest`), `page`, `pageSize`.
* **Response `200 OK`:** `BlogPagedResult<BlogPostSummaryDto>` chỉ gồm các bài `Pending_Approval`.

#### `[STT 149]` `PATCH /api/v1/admin/blogs/{id:guid}/review` *(+ `POST .../approve`, `POST .../reject`)*
* **Request Body (`ReviewBlogPostRequest`):**
  ```json
  {
    "action": "Reject",
    "feedback": "Cần bổ sung bảng phân biệt mã nhựa 1, 2, 5 và nguồn dẫn Bộ TNMT."
  }
  ```
* **Responses:**
  * **Approve (`action = "Approve"` / `approve = true`):** Chuyển `Pending_Approval -> Approved`, gán `ApprovedByAdminId`, `ApprovedAt`, `PublishedAt = NOW()`, gửi `Notification` cho tác giả và ghi `AuditLog` (`BLOG_APPROVE`), trả `200 OK` (`MSG-ADM-05`).
  * **Reject (`action = "Reject"` / `approve = false`):** Bắt buộc có `feedback` khác rỗng (`<= 2000` ký tự, nếu trống trả `400 MSG-AUTH-02`); chuyển `Pending_Approval -> Draft` kèm `RejectionReason = feedback`, xóa `SubmittedAt`, thông báo tác giả và ghi `AuditLog` (`BLOG_REJECT`), trả `200 OK`.
  * Nếu bài không còn ở `Pending_Approval` (do đã bị thu hồi hoặc Admin khác xử lý trước): trả `400 MSG-GEN-01`.
