# ĐẶC TẢ HỢP ĐỒNG API CHI TIẾT (API CONTRACT SPECIFICATION)
## PHÂN HỆ M-11 / FE-10: ĐÁNH GIÁ UY TÍN & GIẢI QUYẾT KHIẾU NẠI (`RATING, REVIEW & COMPLAINT RESOLUTION`)

* **Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform (`SEP490_G141`)
* **Phiên bản tài liệu:** `v2.7.0` (Cập nhật đồng bộ với mã nguồn `Ecolink.WebApi` / `.NET 10` & PostgreSQL 16)
* **Use Cases bao phủ:**
  * `U-10`: Rate Recycler (*Chấm `1–5` sao, tự xóa bài, báo cáo vi phạm & kiểm duyệt ẩn/khôi phục*)
  * `U-11`: Submit Complaint (*Khiếu nại 2 chiều `Citizen` / `Recycler` + ảnh R2 & hợp nhất `DisputeAsync` từ biên nhận*)
  * `S-05`: Handle Complaints (*Hồ sơ Trọng tài 360°, phán quyết ràng buộc, thu hồi `GreenPoints` an toàn `>= 0`, cảnh cáo `AccountModerationLog` & `AuditLog`*)
* **Controllers liên quan:** `ReviewsController` (`STT 121–125`), `StaffComplaintsController` (`STT 126, 130–132`), `ComplaintsController` (`STT 127–129`)
* **Phạm vi Endpoint:** `12` REST APIs (`STT 121` – `STT 132`) trong [`00_api_inventory_and_endpoint_statistics.md`](./00_api_inventory_and_endpoint_statistics.md)
* **Tiêu chuẩn phản hồi:** Chuẩn hóa theo `ApiResponse<T>` trong [`00_api_overview_and_response_contract.md`](./00_api_overview_and_response_contract.md) (`camelCase`, trạng thái chuỗi).

---

## 1. TỔNG QUAN KIẾN TRÚC & MA TRẬN API PHÂN HỆ `M-11`

### 1.1. Kiến trúc Đánh giá Uy tín & Trọng tài Giao dịch Ba bên

1. **Toàn vẹn Đánh giá `1-Đơn / 1-Review` (`U-10`, `BR-RVW-01`..`03`):**
   * Chỉ chủ đơn của `HandoverRequest` đã `Completed` trong vòng `14 ngày` kể từ thời điểm hoàn tất mới được gửi đánh giá `1–5` sao (`POST /api/v1/handover-requests/{requestId}/reviews`).
   * **Bất biến đánh giá (`BR-RVW-02`):** Không hỗ trợ `PUT`/`PATCH` chỉnh sửa điểm sao hay nhận xét sau khi đăng.
   * **Tự xóa & Kiểm duyệt (`BR-RVW-03`):** Tác giả được quyền xóa vĩnh viễn bài đánh giá của mình (`DELETE /api/v1/reviews/{reviewId}`); bên bị đánh giá (`Recycler`) và người ngoài bị chặn `403 Forbidden`. Tài khoản đã đăng nhập có thể báo cáo vi phạm (`POST /report`) để Staff `[HANDLE_COMPLAINT]` hoặc Admin xem xét ẩn (`Hide`) / khôi phục (`Restore`), tính lại tức thời `AverageRating` & `TotalReviews` vào bảng `recycler_profiles` cho `GeoMatchingService`.
2. **Đường ống Khiếu nại 2 Chiều & Chống Trùng Mức PostgreSQL (`U-11`, `BR-DSP-01`, `BR-DSP-02`):**
   * Hỗ trợ cả `Citizen` lẫn `Recycler` gửi khiếu nại (`ScaleDiscrepancy`, `PaymentIssue`, `NoShow`, `Misconduct`, `Other`) trong thời hạn `7 ngày`, kèm `0..5` ảnh bằng chứng tải qua `POST /api/v1/complaints/upload-evidence` (`complaint-evidences`).
   * Chỉ mục duy nhất có điều kiện `uq_transaction_complaints_active_request ON transaction_complaints (request_id) WHERE status IN ('Open', 'Investigating') AND is_deleted = FALSE` khóa cứng tối đa `1` phiếu khiếu nại đang hoạt động trên mỗi đơn bàn giao (`409 Conflict`, `MSG-DSP-04`).
   * Tự động hợp nhất từ [`HandoverReceiptService.DisputeAsync`](../../src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L177-L265): khi người dân khiếu nại biên nhận ở trạng thái `AwaitingConfirmation`, hệ thống tự sinh phiếu `TransactionComplaint` (`ScaleDiscrepancy`, `Open`) đưa thẳng vào hàng đợi `S-05`.
3. **Hồ sơ Trọng tài 360° & Phán quyết Đa phân hệ (`S-05`, `BR-DSP-04`, `BR-DSP-06`, `BR-GAM-01`, `BR-GAM-03`, `BR-ADM-05`):**
   * Yêu cầu `[Authorize(Roles = "Staff,Admin")]` + `[HasPermission(StaffPermissions.HandleComplaint)]`.
   * Khi mở `GET /api/v1/staff/complaints/{complaintId}?claim=true`, phiếu chuyển `Open -> Investigating` và tổng hợp trọn vẹn: Bằng chứng 2 bên + Biên nhận cân điện tử (`ScaleProofPhotoUrls`) + Toàn bộ `ChatMessages` bất biến (`BR-NEG-01`, `BR-DSP-06`).
   * Phán quyết `PATCH /resolve` bắt buộc `resolutionNote >= 10` ký tự (`BR-DSP-04`): đồng bộ trạng thái `HandoverRequest` & `TradeOrder`; nếu `CancelDisputedOrder`, thu hồi điểm qua `ClawBackPointsSafeAsync` trên cả `user_eco_profiles` lẫn ví điểm thật `user_point_wallets` (kẹp đáy `>= 0` theo `BR-GAM-03`, ghi bút toán âm `PointActionType.Adjustment` / `SourceType = "ComplaintClawback"` với khóa idempotent `COMPLAINT_CLAWBACK:{complaintId}:{userId}` trong `point_transactions`); nếu `ConfirmReceiptAsIs`/`CompensateUser` từ trạng thái `Disputed` thì chốt `Completed` & gọi `IPointsService.AwardHandoverPointsAsync` cấp điểm chuẩn; ghi cảnh cáo `User.WarningCount` + `AccountModerationLog` (`RelatedComplaintId`) và `AuditLog` (`RESOLVE_COMPLAINT`).

### 1.2. Bảng Quy chiếu Nhanh `12` Endpoints (`STT 121` – `STT 132`)

| STT | Phương thức | Tuyến đường Endpoint | Controller / Action | Phân quyền (`RBAC`) |
|:---:|:---:|:---|:---|:---|
| **121** | `POST` | `/api/v1/handover-requests/{requestId:guid}/reviews` | `ReviewsController.CreateReview` | `Authorize` (Chủ đơn `Citizen`) |
| **122** | `GET` | `/api/v1/recyclers/{recyclerId:guid}/reviews` | `ReviewsController.GetRecyclerReviews` | `AllowAnonymous` |
| **123** | `GET` | `/api/v1/reviews/me` | `ReviewsController.GetMyReviews` | `Authorize` |
| **124** | `DELETE` | `/api/v1/reviews/{reviewId:guid}` | `ReviewsController.DeleteMyReview` | `Authorize` (Chỉ tác giả) |
| **125** | `POST` | `/api/v1/reviews/{reviewId:guid}/report` | `ReviewsController.ReportReview` | `Authorize` |
| **126** | `PATCH` | `/api/v1/staff/reviews/{reviewId:guid}/moderate` | `StaffComplaintsController.ModerateReview` | `Staff [HANDLE_COMPLAINT]`, `Admin` |
| **127** | `POST` | `/api/v1/complaints/upload-evidence` | `ComplaintsController.UploadEvidence` | `Authorize` |
| **128** | `POST` | `/api/v1/complaints` | `ComplaintsController.SubmitComplaint` | `Authorize` (`Citizen` / `Recycler` đơn) |
| **129** | `GET` | `/api/v1/complaints/me` | `ComplaintsController.GetMyComplaints` | `Authorize` (Cô lập 2 bên đơn) |
| **130** | `GET` | `/api/v1/staff/complaints` | `StaffComplaintsController.GetComplaintQueue` | `Staff [HANDLE_COMPLAINT]`, `Admin` |
| **131** | `GET` | `/api/v1/staff/complaints/{complaintId:guid}` | `StaffComplaintsController.GetComplaintDossier` | `Staff [HANDLE_COMPLAINT]`, `Admin` |
| **132** | `PATCH` | `/api/v1/staff/complaints/{complaintId:guid}/resolve` | `StaffComplaintsController.ResolveComplaint` | `Staff [HANDLE_COMPLAINT]`, `Admin` |

---

## 2. ĐẶC TẢ CHI TIẾT CÁC ENDPOINTS (`STT 121` – `STT 132`)

### 2.1. Nhóm Đánh giá Cơ sở (`ReviewsController` — `STT 121–125`)

#### `[STT 121]` `POST /api/v1/handover-requests/{requestId:guid}/reviews`
* **Request Body (`CreateReviewRequest`):**
  ```json
  {
    "ratingStars": 5,
    "comment": "Cân điện tử chuẩn xác, thu gom đúng giờ."
  }
  ```
* **Validation:** `ratingStars` $\in [1, 5]$; `comment` tối đa `1000` ký tự.
* **Responses:**
  * `201 Created` (`MSG-RVW-01`): Trả `TransactionReviewDto` gồm `reviewId`, `ratingStars`, `status: "Visible"`, `updatedRecyclerAverageRating`, `updatedRecyclerTotalReviews`.
  * `400 Bad Request` (`MSG-RVW-02`): Đơn chưa `Completed` hoặc quá hạn `14 ngày`.
  * `403 Forbidden`: Người gọi không phải chủ đơn (`HandoverRequest.UserId`).
  * `409 Conflict` (`MSG-RVW-02`): Đơn này đã từng được đánh giá trước đó (`BR-RVW-01`).

#### `[STT 122]` `GET /api/v1/recyclers/{recyclerId:guid}/reviews`
* **Query Parameters:** `ratingStars` (`1..5`), `pageNumber` (`1`), `pageSize` (`10`).
* **Response `200 OK` (`MSG-GEN-01`):** `RecyclerReviewsSummaryDto` chứa `averageRating`, `totalReviews`, `starDistribution` (`fiveStars..oneStar`) và `reviews: PagedResult<TransactionReviewDto>` (ẩn bài `Hidden`/`Deleted`, che tên họ tác giả).

#### `[STT 123]` `GET /api/v1/reviews/me`
* **Query Parameters:** `pageNumber` (`1`), `pageSize` (`10`).
* **Response `200 OK` (`MSG-GEN-01`):** `PagedResult<TransactionReviewDto>` chỉ gồm các đánh giá có `ReviewerId == callerUserId` và `Status != Deleted`.

#### `[STT 124]` `DELETE /api/v1/reviews/{reviewId:guid}`
* **Response `200 OK` (`MSG-RVW-03`):** Đặt `Status = Deleted`, tính lại `AverageRating` & `TotalReviews` và ghi `AuditLog` (`DELETE_OWN_REVIEW`). Trả `403 Forbidden` nếu không phải tác giả (bao gồm cơ sở thu mua bị đánh giá — `BR-RVW-03`).

#### `[STT 125]` `POST /api/v1/reviews/{reviewId:guid}/report`
* **Request Body (`ReportReviewRequest`):** `{ "reason": "Spam quảng cáo số điện thoại lạ trong nhận xét." }` (`5..500` ký tự).
* **Response `200 OK` (`MSG-GEN-01`):** Chuyển `Status = Reported`, tăng `ReportCount += 1`.

---

### 2.2. Nhóm Khiếu nại 2 Chiều (`ComplaintsController` — `STT 127–129`)

#### `[STT 127]` `POST /api/v1/complaints/upload-evidence`
* **Content-Type:** `multipart/form-data` (field `file`, tối đa `10 MB`, `.jpg/.jpeg/.png/.webp/.pdf`).
* **Response `200 OK` (`MSG-GEN-01`):** `RecyclerDocumentUploadResponseDto` (`fileUrl`, `objectKey` trong tiền tố `complaint-evidences/yyyy/MM/...`, `contentType`, `sizeBytes`).

#### `[STT 128]` `POST /api/v1/complaints`
* **Request Body (`SubmitComplaintRequest`):**
  ```json
  {
    "handoverRequestId": "b9f21a10-2222-4333-8444-556677889900",
    "category": "ScaleDiscrepancy",
    "description": "Khối lượng cân thực tế 18.5 kg nhưng biên nhận chỉ ghi 14.0 kg.",
    "evidencePhotoUrls": [
      "https://pub-71848fa50a564bf9b27f3607254f44a0.r2.dev/complaint-evidences/2026/11/scale.jpg"
    ]
  }
  ```
* **Validation & Rules:** `category` $\in \{\text{ScaleDiscrepancy}, \text{PaymentIssue}, \text{NoShow}, \text{Misconduct}, \text{Other}\}$; `description` `15..2000` ký tự; `evidencePhotoUrls` tối đa `5` URL HTTP/HTTPS (`BR-DSP-02`). Hạn `7 ngày` (`BR-DSP-01`).
* **Responses:**
  * `201 Created` (`MSG-DSP-01`): `ComplaintDetailDto` (`complaintCode = "CMP-yyyyMMdd-XXXX"`, `status = "Open"`).
  * `400 Bad Request` (`MSG-DSP-03`): Quá `7 ngày` hoặc trạng thái đơn không cho phép.
  * `403 Forbidden` (`MSG-ADM-04`): Người gọi không phải `Citizen` hay `Recycler` của đơn.
  * `409 Conflict` (`MSG-DSP-04`): Đơn đã có phiếu khiếu nại `Open` hoặc `Investigating`.

#### `[STT 129]` `GET /api/v1/complaints/me`
* **Query Parameters:** `role` (`all|submitted|against`), `status` (`Open|Investigating|Resolved|Dismissed`), `pageNumber`, `pageSize`.
* **Response `200 OK` (`MSG-GEN-01`):** `PagedResult<ComplaintSummaryDto>` cô lập cứng chỉ các phiếu mà tài khoản hiện tại là `ComplainantId` hoặc `RespondentId` (`BR-DSP-06`).

---

### 2.3. Nhóm Kiểm duyệt & Trọng tài Staff (`StaffComplaintsController` — `STT 126, 130–132`)

*(Toàn bộ yêu cầu Role `Staff`/`Admin` kèm quyền `HANDLE_COMPLAINT` — trả `403 MSG-ADM-04` nếu thiếu quyền)*

#### `[STT 126]` `PATCH /api/v1/staff/reviews/{reviewId:guid}/moderate`
* **Request Body (`ModerateReviewRequest`):** `{ "action": "Hide", "moderationReason": "Chứa ngôn từ công kích cá nhân." }` (`Hide` | `Restore`, lý do `5..500` ký tự).
* **Response `200 OK` (`MSG-RVW-03`):** Đổi trạng thái `Hidden` / `Visible`, tính lại `AverageRating` / `TotalReviews` và ghi `AuditLog` (`MODERATE_REVIEW`).

#### `[STT 130]` `GET /api/v1/staff/complaints`
* **Query Parameters:** `status`, `category`, `search`, `includeReportedReviews` (`true`), `pageNumber`, `pageSize`.
* **Response `200 OK` (`MSG-GEN-01`):** `StaffComplaintQueueResponseDto` (`complaints`, `openCount`, `investigatingCount`, `resolvedCount`, `dismissedCount`, `reportedReviewsCount`, `reportedReviews`).

#### `[STT 131]` `GET /api/v1/staff/complaints/{complaintId:guid}?claim=true`
* **Response `200 OK` (`MSG-GEN-01`):** `StaffComplaintDossierDto` tổng hợp 360° gồm `complaint`, `complainant`, `respondent` (kèm `warningCount`), thông tin bàn giao (`items`, `wastePhotoUrls`), ảnh cân biên nhận (`receipt.scaleProofPhotoUrls`) và mảng lịch sử tin nhắn bất biến (`negotiationMessages`). Nếu phiếu đang `Open` và `claim=true`, chuyển sang `Investigating` và ghi `CLAIM_COMPLAINT`.

#### `[STT 132]` `PATCH /api/v1/staff/complaints/{complaintId:guid}/resolve`
* **Request Body (`ResolveComplaintRequest`):**
  ```json
  {
    "resolutionOutcome": "CancelDisputedOrder",
    "resolutionNote": "Đối chiếu ảnh cân thực tế và tin nhắn xác nhận cân thiếu 30%. Hủy đơn tranh chấp.",
    "issueWarningToRespondent": true,
    "warningReason": "Vi phạm trung thực khi cân đo tại điểm thu gom."
  }
  ```
* **Allowed Outcomes (`ResolutionOutcome`):** `ConfirmReceiptAsIs` | `CompensateUser` | `UpholdComplainant` | `CancelDisputedOrder` | `Dismissed`.
* **Response `200 OK` (`MSG-DSP-02`):** Trả `ComplaintDetailDto` kèm `pointsRevoked`, `warningLogged`; trả `409 Conflict` nếu phiếu đã đóng trước đó.
