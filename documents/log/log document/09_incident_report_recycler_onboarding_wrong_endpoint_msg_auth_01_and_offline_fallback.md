# BÁO CÁO SỰ CỐ KỸ THUẬT LUỒNG ĐĂNG KÝ & THẨM ĐỊNH VỰA THU MUA (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-ONB-09`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Recycler Onboarding & Verification (`M-03` / `UC U-17`, `UC S-04`), Authentication (`M-01`), Frontend SPA (`ecolink-web`)  
**Các tệp ảnh hưởng chính:**
* **Frontend (`ecolink-web`):** [`src/pages/JoinRecycler.tsx`](file:///d:/Project/ecolink-web/src/pages/JoinRecycler.tsx), [`src/lib/recyclerWorkflowApi.ts`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts), [`src/pages/staff/Applications.tsx`](file:///d:/Project/ecolink-web/src/pages/staff/Applications.tsx)
* **Backend (`intelligent-waste-sorting-trading`):** [`AuthController.cs`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AuthController.cs), [`AuthService.cs`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AuthService.cs), [`RecyclerApplicationsController.cs`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerApplicationsController.cs), [`RecyclerOnboardingService.cs`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/RecyclerOnboardingService.cs)  
**Thời điểm ghi nhận:** 19/11/2026 (Live E2E Testing qua Cloudflare Tunnel giữa cửa sổ Citizen đang đăng nhập và cửa sổ Staff `[InPrivate]`)  
**Mức độ nghiêm trọng:** Nghiêm trọng (Critical – API trả lỗi `400 Bad Request` / `MSG-AUTH-01` nhưng giao diện vẫn báo nhận hồ sơ giả `HS-2026-6111` trên trình duyệt, khiến hồ sơ không ghi vào PostgreSQL và hoàn toàn biến mất ở hàng đợi thẩm định của Staff)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Trong quá trình kiểm thử đầu-cuối (End-to-End) Use Case **`U-17` (Đăng ký cơ sở thu mua)** và **`S-04` (Staff thẩm định hồ sơ)**:

1. **Phía Người đăng ký (`/join-recycler`, cửa sổ thường đang đăng nhập tài khoản Citizen `VD` – Nguyễn Văn Dân):**
   * Người dùng điền thông tin vựa thu mua, chọn vị trí bản đồ, tải chứng từ lên (`POST /api/v1/recycler-applications/upload-document` thành công) và bấm *"Gửi hồ sơ thẩm định"*.
   * Giao diện chuyển sang màn hình thành công giả **`Đã nhận hồ sơ`** với mã hồ sơ **`HS-2026-6111`** và nhãn **`Đã lưu hàng đợi thẩm định trình duyệt`**.
   * Tuy nhiên, tại tab **Network** của DevTools, request **`register-recycler`** ở cuối danh sách bị báo lỗi đỏ kèm response JSON:
     ```json
     {
       "success": false,
       "messageCode": "MSG-AUTH-01",
       "message": "Email này đã được sử dụng.",
       "data": null,
       "errors": null,
       "timestamp": "2026-11-19T06:02:22.5449355Z"
     }
     ```

2. **Phía Nhân viên thẩm định (`/staff/applications`, cửa sổ `[InPrivate]` đăng nhập tài khoản Staff `Lê Thu Hà`):**
   * Danh sách **`Chờ thẩm định (3)`** chỉ hiển thị các hồ sơ có sẵn trong PostgreSQL (ví dụ `Anh Tuấn – Thu gom tận nhà`, `Vựa Minh Khai` với mã `HS-999999`).
   * Hồ sơ **`HS-2026-6111`** vừa nộp hoàn toàn **không xuất hiện** để Nhân viên kiểm tra và phê duyệt.

---

## 2. CHUỖI TRUY VẾT LỖI E2E (END-TO-END TRACE & EVIDENCE)

```text
[1] Citizen "VD" (đã đăng nhập sẵn) mở /join-recycler và điền form kèm Email cá nhân/email đã tồn tại
      │
      ▼ Giao diện hứa hẹn: "Chờ duyệt thì bạn vẫn dùng được tài khoản người dân như thường."
[2] JoinRecycler.tsx gọi submitRecyclerOnboarding() -> upload-document lên R2 (200 OK)
      │
      ▼ NHƯNG lại gọi nhầm POST /api/v1/auth/register-recycler (API tạo User MỚI hoàn toàn)
      │ thay vì POST /api/v1/recycler-applications (API U-17 nâng cấp User hiện tại)
[3] AuthService.RegisterRecyclerAsync kiểm tra Users.AnyAsync(u => u.Email == normalizedEmail)
      │ -> Trùng email trong bảng users!
      │ -> Ném BusinessRuleException("BR-AUTH-01", "MSG-AUTH-01", "Email này đã được sử dụng.")
      │ -> GlobalExceptionMiddleware trả HTTP 400 Bad Request
[4] Khối catch {} trong submitRecyclerOnboarding() (recyclerWorkflowApi.ts) NUỐT TOÀN BỘ LỖI 400
      │ -> Không throw lại cho JoinRecycler.tsx hiển thị err
      │ -> Tự sinh mã giả HS-2026-6111 và ghi vào localStorage ("ecolink:workflow-sync:v1")
      │ -> UI hiển thị: "HS-2026-6111 | Đã lưu hàng đợi thẩm định trình duyệt"
[5] Staff mở /staff/applications trong cửa sổ [InPrivate]
      │ -> localStorage hoàn toàn cô lập giữa cửa sổ thường và InPrivate
      │ -> PostgreSQL (recycler_profiles) không có hồ sơ mới -> HS-2026-6111 biến mất hoàn toàn
```

---

## 3. PHÂN TÍCH CHI TIẾT CÁC NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

### 3.1. Lỗi 1 (Critical): Frontend gọi nhầm API tạo tài khoản mới (`/api/v1/auth/register-recycler`) khi người dùng đã đăng nhập

Backend EcoLink thiết kế **2 endpoint tách biệt** cho 2 ngữ cảnh sử dụng khác nhau, nhưng Frontend đang gộp chung vào một lệnh gọi sai ngữ cảnh:

1. **Endpoint dành cho khách vãng lai chưa có tài khoản (`POST /api/v1/auth/register-recycler`):**
   * Định tuyến tại [`AuthController.cs#L58-L64`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AuthController.cs#L58-L64) $\rightarrow$ [`AuthService.RegisterRecyclerAsync`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AuthService.cs#L106-L166).
   * Thực thi việc **tạo mới hoàn toàn một bản ghi `User`** (`Role = UserRole.Recycler`, `Status = PendingVerification`) kèm `RecyclerProfile`.
   * Vì tạo tài khoản mới nên hàm bắt buộc kiểm tra tính duy nhất của Email và Username tại dòng 114–122:
     ```csharp
     if (await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail, ct))
     {
         throw new BusinessRuleException("BR-AUTH-01", "MSG-AUTH-01", "Email này đã được sử dụng.");
     }
     ```
2. **Endpoint dành cho Người dân đã đăng nhập xin nâng cấp lên Cơ sở thu gom (`POST /api/v1/recycler-applications`):**
   * Định tuyến tại [`RecyclerApplicationsController.cs#L81-L103`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerApplicationsController.cs#L81-L103) (`[Authorize]`) $\rightarrow$ [`RecyclerOnboardingService.SubmitUpgradeApplicationAsync`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/RecyclerOnboardingService.cs#L45-L134).
   * Gắn thêm (hoặc cập nhật khi nộp lại sau từ chối theo `BR-MTH-02`) thực thể `RecyclerProfile` vào đúng **`UserId` đang đăng nhập** qua JWT, **không tạo `User` mới**, không xung đột Email/Username, và giữ nguyên quyền sử dụng tài khoản người dân trong thời gian chờ Staff thẩm định (sau khi Staff duyệt mới nâng `User.Role = UserRole.Recycler` tại [`VerifyApplicationAsync`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/RecyclerOnboardingService.cs#L256-L267)).
3. **Sai sót tại Frontend (`ecolink-web`):**
   * Trong [`ecolink-web/src/lib/recyclerWorkflowApi.ts#L522-L541`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts#L522-L541), hàm `submitRecyclerOnboarding` **luôn gọi cứng `/api/v1/auth/register-recycler`** bất kể người dùng có đang đăng nhập sẵn hay chưa.
   * Chính giao diện [`JoinRecycler.tsx#L262`](file:///d:/Project/ecolink-web/src/pages/JoinRecycler.tsx#L262) lại hướng dẫn: *"Chờ duyệt thì bạn vẫn dùng được tài khoản người dân như thường."* — dẫn dắt người dùng (như tài khoản **`VD`** trong Ảnh 1) giữ nguyên phiên đăng nhập Citizen, điền lại chính email của mình và lập tức chạm lỗi trùng email `MSG-AUTH-01`.

---

### 3.2. Lỗi 2 (Critical): Khối `catch {}` trong `submitRecyclerOnboarding` nuốt lỗi `400` và tạo hồ sơ giả `HS-2026-6111`

* Trong [`ecolink-web/src/pages/JoinRecycler.tsx#L68-L86`](file:///d:/Project/ecolink-web/src/pages/JoinRecycler.tsx#L68-L86), UI đã chuẩn bị sẵn khối `try { ... } catch (submitErr) { setErr(...) }` để hiển thị thông báo lỗi nghiệp vụ lên màn hình form.
* Tuy nhiên, bên trong [`submitRecyclerOnboarding`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts#L553-L581), khối `catch {}` lại **nuốt trọn mọi ngoại lệ** (kể cả lỗi `400 Bad Request` `"MSG-AUTH-01"` từ Backend) mà không kiểm tra `err instanceof ApiError && !err.isNetworkError`:
  ```ts
  } catch {
    // Dự phòng ngoại tuyến: lưu vào bộ nhớ đồng bộ trình duyệt để Staff vẫn thấy ngay bên /staff/applications
    const fallbackCode = `HS-${yyyy}-${suffix.slice(-4)}` // -> HS-2026-6111
    ...
    writeSyncState({
      ...state,
      customApplications: [offlineApp, ...state.customApplications],
    })
    return { referenceCode: fallbackCode, syncedWithBackend: false }
  }
  ```
* Đáng chú ý là hàm ngay phía dưới trong cùng tệp — [`decideStaffApplication`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts#L849-L853) — đã có cơ chế chặn lỗi `4xx` không rơi vào fallback:
  ```ts
  if (err instanceof ApiError && !err.isNetworkError && err.status >= 400 && err.status < 500) {
    throw err
  }
  ```
  nhưng `submitRecyclerOnboarding` bị bỏ sót quy tắc này.

---

### 3.3. Lỗi 3 (Hệ quả): Mất đồng bộ dữ liệu sang hàng đợi thẩm định của Staff (`/staff/applications`)

* Khi fallback ngoại tuyến chạy, mã `HS-2026-6111` chỉ nằm trong `localStorage` (`ecolink:workflow-sync:v1` $\rightarrow$ `customApplications`) trên trình duyệt thường của người nộp.
* Trong thực tế vận hành hoặc kiểm thử bằng 2 trình duyệt / 2 cửa sổ tách biệt (cửa sổ thường cho Người nộp và **`[InPrivate]`** cho Staff trong Ảnh 2), `localStorage` hoàn toàn không được chia sẻ.
* Đồng thời, [`fetchStaffApplications`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts#L807-L824) khi gọi `GET /api/v1/recycler-applications` thành công từ Backend sẽ trả trực tiếp `fromBackend: true` mà **không trộn** `state.customApplications`, nên kể cả có lưu cùng trình duyệt thì hồ sơ giả `HS-2026-6111` vẫn bị bỏ qua khi Backend đang bật!

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

| Khía cạnh | Mức độ ảnh hưởng | Mô tả chi tiết |
| :--- | :--- | :--- |
| **Luồng đăng ký nâng cấp (`U-17`)** | Cao | Người dùng đang đăng nhập không thể đăng ký làm vựa thu mua bằng chính email của mình do vướng `MSG-AUTH-01` trên endpoint `register-recycler`. |
| **Tính trung thực giao diện UI** | Cao | Giao diện hiển thị sai lệch thành màn hình *"Đã nhận hồ sơ `HS-2026-6111`"* dù thao tác tạo bản ghi trên server đã thất bại `400 Bad Request`. |
| **Luồng thẩm định Staff (`S-04`)** | Cao | Bảng `recycler_profiles` không phát sinh hồ sơ `Pending` mới; Staff ở `/staff/applications` không nhận được đơn để duyệt (`VerifyApplication`). |
| **R2 Document Storage** | Nhẹ (Rác dữ liệu) | Bước `POST /api/v1/recycler-applications/upload-document` chạy trước khi gọi `register-recycler` đã tải tệp thành công lên R2 và để lại object mồ côi khi bước đăng ký chính bị từ chối. |

---

## 5. GIẢI PHÁP KHẮC PHỤC ĐỀ XUẤT (ACTIONABLE REMEDIATION)

### 5.1. Rẽ nhánh API theo trạng thái đăng nhập trong `submitRecyclerOnboarding`
Cập nhật [`ecolink-web/src/lib/recyclerWorkflowApi.ts`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts#L480-L582):

1. **Khi người dùng ĐÃ đăng nhập (`Boolean(getAccessToken())`):**
   * Gọi **`POST /api/v1/recycler-applications`** qua `authorizedRequest<RecyclerApplicationDetailDto>("user", ...)` với payload đúng hợp đồng `ApplyRecyclerUpgradeRequest`:
     * `facilityName`, `representativeName`, `businessAddress`, `latitude`, `longitude`, `operatingHours`, `contactPhone`, `description`, `legalPermitUrl`, `businessPhotoUrl` (không gửi `username`, `email`, `password`).
   * Lấy `createdId = res.data?.userId` (thuộc tính của `RecyclerApplicationDetailDto`, thay vì `res.data?.user?.id` của `AuthLoginResponseDto`) để tạo `shortCode = HS-${yyyy}-${createdId.slice(0, 6).toUpperCase()}` khớp hoàn toàn với cách `mapDtoToStaffView` ([dòng 787](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts#L787)) hiển thị bên màn hình Staff!
2. **Khi người dùng CHƯA đăng nhập (đối tác hoàn toàn mới):**
   * Giữ nguyên gọi **`POST /api/v1/auth/register-recycler`** để tạo tài khoản `User` + `RecyclerProfile` mới.

### 5.2. Chặn nuốt lỗi `4xx/5xx` trong `submitRecyclerOnboarding`
Trong khối `catch (err)` tại [`ecolink-web/src/lib/recyclerWorkflowApi.ts#L553`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts#L553):
```ts
} catch (err) {
  // Mọi lỗi nghiệp vụ / validation / trùng email từ Backend (4xx/5xx) bắt buộc phải hiển thị ra UI
  if (err instanceof ApiError && !err.isNetworkError) {
    throw err
  }
  // Chỉ fallback lưu đệm ngoại tuyến khi thực sự mất kết nối mạng (isNetworkError === true)
  ...
}
```
Khi đó, nếu người dùng nhập trùng email hoặc vi phạm dữ liệu, khối `catch` trong `JoinRecycler.tsx` sẽ bắt được `err.message` (*"Email này đã được sử dụng."*) và hiện cảnh báo đỏ ngay dưới form thay vì chuyển sang màn hình `Done` giả.

### 5.3. Cách kiểm thử tạm thời (Workaround trước khi vá Frontend)
Nếu cần test ngay luồng `U-17` $\rightarrow$ `S-04` trên bản build hiện tại:
1. Đăng xuất tài khoản `VD` (hoặc mở tab chưa đăng nhập) khi vào `/join-recycler`.
2. Nhập **địa chỉ Email mới hoàn toàn** chưa từng tồn tại trong bảng `users` (hoặc để trống nếu form cho phép tự sinh `@partner.ecolink.vn` theo timestamp).
3. Kiểm tra badge xác nhận trên màn hình kết quả: phải là **`Đã ghi nhận vào CSDL PostGIS (Trạng thái: Chờ thẩm định)`** (`syncedWithBackend: true`) thay vì *"Đã lưu hàng đợi thẩm định trình duyệt"*. Khi đó hồ sơ sẽ xuất hiện ngay tại `/staff/applications`.

---

## 6. BÀI HỌC VÀ BIỆN PHÁP PHÒNG NGỪA (LESSONS LEARNED & PREVENTIONS)

1. **Đồng bộ phân tách luồng "Tạo tài khoản mới" vs. "Nâng cấp tài khoản hiện có":**
   * Cần kiểm tra trạng thái phiên đăng nhập ở tất cả các biểu mẫu công khai có chức năng kép (ví dụ `/join-recycler`), đảm bảo luồng nâng cấp vai trò sử dụng đúng endpoint `[Authorize]` của chính người dùng đó thay vì endpoint đăng ký `AllowAnonymous`.
2. **Thống nhất chuẩn xử lý ngoại lệ trên toàn bộ `recyclerWorkflowApi.ts`:**
   * Rà soát toàn bộ các hàm API còn dùng `catch {}` trống (`submitRecyclerOnboarding`, `createHandoverReceipt`, `confirmHandoverReceipt`, v.v.) để áp dụng đồng nhất quy tắc: **chỉ fallback ngoại tuyến khi `err.isNetworkError === true`**, mọi lỗi `4xx/5xx` phải `throw` về component hiển thị cho người dùng.
3. **Nhận diện nhanh lỗi lưu giả qua badge trạng thái UI:**
   * Cần coi trạng thái *"Đã lưu hàng đợi thẩm định trình duyệt"* (`syncedWithBackend: false`) khi đang bật máy chủ Backend là một **dấu hiệu lỗi đồng bộ** thay vì kết thúc thành công, tránh bỏ sót khi nghiệm thu tính năng.
