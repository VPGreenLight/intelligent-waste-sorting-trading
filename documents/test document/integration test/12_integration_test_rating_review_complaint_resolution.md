# ĐẶC TẢ KIỂM THỬ TÍCH HỢP (INTEGRATION TEST SPECIFICATION)
## PHÂN HỆ M-11 / FE-10: ĐÁNH GIÁ UY TÍN & GIẢI QUYẾT KHIẾU NẠI (`RATING, REVIEW & COMPLAINT RESOLUTION`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Sheet trong Report 5.2:** `M11_ReviewsComplaints` (`Module 12`)
* **Phạm vi API End-to-End:** `12` REST APIs (`STT 121` – `STT 132`: `ReviewsController`, `ComplaintsController`, `StaffComplaintsController`) + Hợp nhất `DisputeAsync` (`M-07`) + `GeoMatchingService` (`M-05`) + `AccountModerationLog` (`M-15`)
* **Use Cases bao phủ:** `U-10` (Rate Recycler), `U-11` (Submit Complaint), `S-05` (Handle Complaints)
* **Tệp mã kiểm thử HTTP E2E:** [`ReviewsAndComplaintsTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/ReviewsAndComplaintsTests.cs) (`[Trait("Category", "ReviewsAndComplaints")]`)
* **Ngày thực thi gần nhất:** `2026-11-18`
* **Kết quả thực thi thực tế qua Kestrel + PostgreSQL 16 + Cloudflare R2:** **`16 / 16 Passed` (`100%` Pass Rate)**

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ TÍCH HỢP (MODULE SUMMARY TABLE)

| Feature | `Rating, Review & Complaint Resolution (M11_ReviewsComplaints)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp End-to-End 3 Use Cases `U-10`, `U-11`, `S-05`: đánh giá `1–5` sao đơn `Completed` trong `14 ngày` & tính lại `AverageRating`/`TotalReviews` nguyên tử (`BR-RVW-01`), chặn sửa sao (`BR-RVW-02`) & kiểm soát quyền tự xóa/báo cáo/Staff ẩn bài (`BR-RVW-03`), tải bằng chứng Cloudflare R2 (`complaint-evidences`), khiếu nại 2 chiều `Citizen`/`Recycler` & hợp nhất `DisputeAsync`, khóa chống trùng Filtered Partial Unique Index (`BR-DSP-01`, `409 MSG-DSP-04`), phân quyền hạt mịn `HANDLE_COMPLAINT` (`BR-ADM-02`), Hồ sơ Trọng tài 360° (`ScaleProofPhotoUrls` + `ChatMessages` bất biến `BR-DSP-06`), thu hồi `GreenPoints` an toàn (`BR-GAM-01`, `BR-GAM-03`), cảnh cáo `AccountModerationLog` & ghi `AuditLog` (`BR-ADM-05`). | | | |
| **Number of TCs** | **11** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 10 | 1 | 0 | 0 |
| *Round 2* | 11 | 0 | 0 | 0 |
| *Round 3* | 0 | 0 | 11 | 0 |

---

## 2. CHI TIẾT 16 INTEGRATION TEST CASES (`ITC-RVW-001` – `ITC-RVW-016`)

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 12.1: Recycler Rating, Public Discovery & Moderation (`U-10` — `STT 121–126`)** | | | | | | | | | |
| **ITC-RVW-001** | Citizen đánh giá `5` sao đơn `Completed` tự cập nhật `AverageRating` & hiển thị trên Public Discovery (`BR-RVW-01`). | 1. Tạo & hoàn tất đơn bàn giao giữa Citizen mới & Recycler mới.<br>2. Gọi `POST /api/v1/handover-requests/{id}/reviews` (`5` sao).<br>3. Đọc `GET /api/v1/recyclers/{id}/reviews` & `GET /api/v1/recyclers/{id}`. | `201 Created` (`MSG-RVW-01`); `averageRating=5.0`, `totalReviews=1`, `starDistribution.fiveStars=1`; khớp đồng bộ trên GeoMatching. | WebApi & PostgreSQL đang chạy. | Failed | Passed | Pending | **INC-2026-DI-05** | `RVW01` | Round 1 Failed (INC-2026-DI-05): missing IReviewService / IComplaintService DI registration -> Round 2 Passed |
| **ITC-RVW-002** | Chặn đánh giá đơn chưa hoàn tất (`400 MSG-RVW-02`) và chặn đánh giá lần 2 cùng đơn (`409 Conflict`). | 1. Thử đánh giá đơn đang `Accepted` (`400`).<br>2. Hoàn tất đơn rồi đánh giá 2 lần liên tiếp. | Đơn chưa xong trả `400 MSG-RVW-02`; đánh giá trùng trả `409 MSG-RVW-02`. | Có đơn `Accepted` & `Completed`. | Passed | Passed | Pending | N/A | `RVW02` |
| **ITC-RVW-003** | Bảo đảm bất biến sao (`BR-RVW-02`) và chặn Recycler/người ngoài xóa bài đánh giá (`403 Forbidden`, `BR-RVW-03`). | 1. Đăng review `2` sao.<br>2. Gửi `PUT /api/v1/reviews/{id}` (`404/405`).<br>3. Recycler & Citizen khác gọi `DELETE /api/v1/reviews/{id}`. | Không cho sửa sao (`404/405`); bên bị đánh giá & người ngoài bị chặn `403`. | Đã có bài đánh giá hợp lệ. | Passed | Passed | Pending | N/A | `RVW03` |
| **Function 12.2: Bilateral Complaints, Cloudflare R2 Evidence & Receipt Dispute Bridge (`U-11` — `STT 127–129`)** | | | | | | | | | |
| **ITC-RVW-004** | Tải ảnh bằng chứng lên Cloudflare R2 (`complaint-evidences`) và nộp phiếu khiếu nại `Citizen`. | 1. Upload PNG qua `POST /api/v1/complaints/upload-evidence`.<br>2. Gửi `POST /api/v1/complaints` (`ScaleDiscrepancy`) đính kèm URL. | Upload trả `fileUrl` chứa `complaint-evidences`; nộp phiếu trả `201 MSG-DSP-01`, mã `CMP-...`. | Citizen tham gia đơn. | Passed | Passed | Pending | N/A | `RVW06` |
| **ITC-RVW-005** | Gửi khiếu nại 2 chiều từ phía `Recycler` đối với sự cố bỏ hẹn (`NoShow`). | 1. Tạo đơn `Accepted`.<br>2. Recycler gọi `POST /api/v1/complaints` (`NoShow`). | `201 Created`; `complainantRole="Recycler"`, `respondentId=Citizen.Id`, `status="Open"`. | Có đơn `Accepted`. | Passed | Passed | Pending | N/A | `RVW07` |
| **ITC-RVW-006** | Chặn mở quá 1 khiếu nại đang mở/đang xử lý trên cùng `HandoverRequest` (`409 MSG-DSP-04`). | 1. Citizen gửi phiếu 1 thành công.<br>2. Recycler gửi phiếu 2 trên cùng `requestId`. | Phiếu 2 bị chặn `409 Conflict` với `messageCode="MSG-DSP-04"`. | Có phiếu `Open` trên đơn. | Passed | Passed | Pending | N/A | `RVW09` |
| **ITC-RVW-007** | Chống người ngoài gửi khiếu nại hộ (`403`) và cô lập danh sách `/api/v1/complaints/me` (`BR-DSP-06`). | 1. Citizen ngoài đơn thử `POST /complaints` (`403`).<br>2. Outsider, Owner và Recycler cùng gọi `GET /complaints/me`. | Outsider `403` và không thấy phiếu; cả 2 bên trong đơn thấy đúng phiếu của mình. | Có đơn giữa 2 bên A-B & user C. | Passed | Passed | Pending | N/A | `RVW10` |
| **Function 12.3: Staff Permissions, 360° Arbitration Dossier, Clawback & Analytics (`S-05` — `STT 130–132`)** | | | | | | | | | |
| **ITC-RVW-008** | Kiểm soát phân quyền hạt mịn `[HasPermission(HANDLE_COMPLAINT)]` trên `/api/v1/staff/complaints/*`. | 1. Gọi không token (`401`), Citizen (`403`), Staff chỉ có quyền `REVIEW_AI` (`403`).<br>2. Staff có quyền `HANDLE_COMPLAINT` (`200`). | Chỉ Staff mang `HANDLE_COMPLAINT` (hoặc Admin) truy cập được; thiếu quyền trả `403`. | Đã cấu hình phân quyền Staff. | Passed | Passed | Pending | N/A | `RVW11` |
| **ITC-RVW-009** | Mở Hồ sơ Trọng tài 360° (`?claim=true`): nhận phiếu, tải ảnh cân & lịch sử chat bất biến (`BR-DSP-06`). | 1. Gửi tin nhắn chat trên đơn `M-08`, lập biên nhận có ảnh cân, nộp khiếu nại.<br>2. Staff gọi `GET /api/v1/staff/complaints/{id}?claim=true`. | Status đổi `Investigating`, gán `assignedStaffId`, trả đủ `receipt.scaleProofPhotoUrls` & `negotiationMessages`. | Có chat & biên nhận trên đơn. | Passed | Passed | Pending | N/A | `RVW12` |
| **ITC-RVW-010** | Kiểm định biên bản phán quyết `>= 10` ký tự (`400`) và chặn giải quyết lại lần 2 (`409`). | 1. Gọi `PATCH /resolve` với note `"OK xong"` (`400`).<br>2. Chốt hợp lệ `Dismissed` (`200`) rồi gọi lại lần 2 (`409`). | Note `< 10` ký tự bị chặn `400`; đóng hợp lệ `200 MSG-DSP-02`; đóng lặp `409`. | Staff `[HANDLE_COMPLAINT]`. | Passed | Passed | Pending | N/A | `RVW13` |
| **ITC-RVW-011** | Biên tối đa `5` ảnh chứng cứ (`BR-DSP-02`), bộ lọc hàng đợi & đồng bộ KPI `openComplaintsCount`. | 1. Gửi `6` ảnh (`400`), gửi `5` ảnh (`201`).<br>2. Lọc `/staff/complaints?status=Open&category=Misconduct` & gọi KPI Admin. | `6` ảnh bị chặn `400`; `5` ảnh hợp lệ; KPI `openComplaintsCount >= 1`. | Có ít nhất 1 khiếu nại đang mở. | Passed | Passed | Pending | N/A | `RVW16` |
