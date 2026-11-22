# BÁO CÁO SỰ CỐ KỸ THUẬT VÒNG LẶP GỌI API `/api/v1/auth/me` LIÊN TỤC (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-AUTH-10`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Frontend Authentication Session & Role Guard (`ecolink-web`), Auth API (`M-01`)  
**Các tệp ảnh hưởng chính:**
* **Frontend (`ecolink-web`):** [`src/lib/api.ts`](file:///d:/Project/ecolink-web/src/lib/api.ts), [`src/lib/session.ts`](file:///d:/Project/ecolink-web/src/lib/session.ts), [`src/components/RequireRole.tsx`](file:///d:/Project/ecolink-web/src/components/RequireRole.tsx), [`src/App.tsx`](file:///d:/Project/ecolink-web/src/App.tsx)
* **Backend (`intelligent-waste-sorting-trading`):** [`AuthController.cs`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AuthController.cs) (`GET /api/v1/auth/me`)  
**Thời điểm ghi nhận:** 22/11/2026 (Live Testing tại `/user/receipt` qua Cloudflare Tunnel)  
**Mức độ nghiêm trọng:** Nghiêm trọng (Critical – Gây vòng lặp vô tận gọi `GET /api/v1/auth/me` liên tục hàng chục/hàng trăm request trong vài giây, làm re-render liên tục toàn bộ cây Portal, tốn băng thông/tài nguyên Backend & Cloudflare Tunnel)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Khi người dùng đã đăng nhập (tài khoản Citizen `VD` – `citizen@ecolink.vn`) truy cập hoặc tải lại các trang thuộc phân hệ `/user/*` (ví dụ `/user/receipt` trong ảnh chụp DevTools Network):

1. Toàn bộ cây route `/user/*` vẫn hiển thị bình thường, nhưng tại tab **Network** của DevTools xuất hiện **hàng loạt request `me` (`GET /api/v1/auth/me`) nối đuôi nhau liên tục không dừng** (ghi nhận **97 requests / 34.0 kB trong ~36.5 giây** trên timeline liên tục màu xanh dương).
2. Mỗi request `GET /api/v1/auth/me` đều trả về **`200 OK`** với payload JSON thông tin người dùng (`email`, `fullName`, `role`, `status`, `currentGreenPoints`, `treeLevel`, `totalRecycledKg`, `currentTier`, `isAdFree`, ...), không có request `refresh-token` và cũng không dừng lại theo chu kỳ `SESSION_VERIFY_COOLDOWN_MS = 15_000` (15 giây) như thiết kế.
3. Lỗi này ảnh hưởng tới tất cả các route được bọc bởi [`RequireRole`](file:///d:/Project/ecolink-web/src/components/RequireRole.tsx) trong [`App.tsx`](file:///d:/Project/ecolink-web/src/App.tsx#L71-L170) gồm `/user/*`, `/recycler/*`, `/staff/*`, `/admin/*`, `/account/*`, `/chat` khi trình duyệt đang xác thực bằng **HttpOnly Cookie** (không có JWT nằm sẵn trong biến RAM `inMemoryAccessToken`).

---

## 2. CHUỖI TRUY VẾT VÒNG LẶP E2E (END-TO-END INFINITE LOOP TRACE)

```text
[1] Người dùng vào /user hoặc /user/receipt (sau F5/reload, biến RAM inMemoryAccessToken === null)
      │ -> Xác thực hoạt động thông qua HttpOnly Cookie ("accessToken") + flag cookie "ecolink_session_active=1"
      ▼
[2] <RequireRole roles={["user"]}> render và chạy useEffect(..., [session])
      │ -> Gọi ensureActiveAuthSession() trong api.ts
      ▼
[3] ensureActiveAuthSession() gọi readEffectiveAccessToken()
      │ -> getAccessToken() trả về sentinel "__cookie_auth__"
      │ -> isRealJwt("__cookie_auth__") === false  ==>  token === null !
      ▼
[4] Điều kiện shouldVerifyWithBackend có chứa `!token`:
      │ forceVerify || !token || !session || Date.now() - lastSessionVerifiedAt > 15_000
      │ -> Vì !token === true, BỎ QUA HOÀN TOÀN cooldown 15 giây!
      ▼
[5] Gọi verifySessionWithBackend() -> fetch GET /api/v1/auth/me (credentials: "include")
      │ -> Backend AuthController.GetCurrentUser() đọc cookie "accessToken" và trả 200 OK (UserInfoDto)
      │ -> (Endpoint /me chỉ trả thông tin user, KHÔNG cấp/nạp lại inMemoryAccessToken -> token vẫn null!)
      ▼
[6] verifySessionWithBackend() gọi updateSession({ id, name, role, ... }) bất chấp dữ liệu không đổi
      │ -> session.ts: localStorage.setItem(...) -> invalidate() -> emit()
      │ -> useSession() (useSyncExternalStore) nhận tham chiếu object Session MỚI hoàn toàn!
      ▼
[7] <RequireRole> re-render với tham chiếu `session` mới
      │ -> useEffect([session]) thấy `session` khác tham chiếu trước -> KÍCH HOẠT LẠI [2] NGAY LẬP TỨC!
      └───────────────────────────────────────────────────────────────────────────────────────────► Lặp lại [2] -> [7] vô hạn!
```

---

## 3. PHÂN TÍCH CHI TIẾT NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

### 3.1. Điểm gãy 1: `readEffectiveAccessToken()` luôn trả về `null` ở chế độ HttpOnly Cookie sau reload
* Trong [`ecolink-web/src/lib/session.ts#L37`](file:///d:/Project/ecolink-web/src/lib/session.ts#L37), Access Token không còn được lưu vào `localStorage` mà chỉ giữ trên biến RAM `inMemoryAccessToken`. Mỗi lần tải lại trang (`F5`) hoặc mở tab mới, `inMemoryAccessToken` khởi tạo bằng `null`.
* Khi đó [`getAccessToken()`](file:///d:/Project/ecolink-web/src/lib/session.ts#L191-L197) kiểm tra thấy `hasSessionCookieFlag() || getSession()?.fromBackend` nên trả về chuỗi sentinel `"__cookie_auth__"` (`COOKIE_SESSION_SENTINEL`).
* Tuy nhiên tại [`ecolink-web/src/lib/api.ts#L110-L113`](file:///d:/Project/ecolink-web/src/lib/api.ts#L110-L113):
  ```ts
  function readEffectiveAccessToken(): string | null {
    const direct = getAccessToken()
    return isRealJwt(direct) ? direct : null
  }
  ```
  Vì `"__cookie_auth__"` không phải JWT 3 phần (`a.b.c`), `isRealJwt(direct)` trả về `false` $\rightarrow$ **`readEffectiveAccessToken()` luôn trả về `null`** trong suốt phiên HttpOnly Cookie.

### 3.2. Điểm gãy 2: Điều kiện `!token` trong `ensureActiveAuthSession()` vô hiệu hóa cooldown 15 giây
* Tại [`ecolink-web/src/lib/api.ts#L340-L361`](file:///d:/Project/ecolink-web/src/lib/api.ts#L340-L361):
  ```ts
  const token = readEffectiveAccessToken()
  if (token && isStoredAccessTokenExpired(10)) {
    await rotateRefreshTokenSingleFlight()
    return
  }

  const shouldVerifyWithBackend =
    forceVerify ||
    !token ||
    !session ||
    Date.now() - lastSessionVerifiedAt > SESSION_VERIFY_COOLDOWN_MS
  ```
* Dù `verifySessionWithBackend()` đã cập nhật `lastSessionVerifiedAt = Date.now()` ([dòng 307](file:///d:/Project/ecolink-web/src/lib/api.ts#L307)), biến `token` vẫn luôn bằng `null` (vì [`AuthController.GetCurrentUser`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AuthController.cs#L266-L288) khi đọc cookie `accessToken` chỉ trả về `UserInfoDto`, không cấp lại `accessToken` trong body và `verifySessionWithBackend()` cũng không gọi `setAuthTokens`).
* Kết quả: biểu thức `!token` luôn đánh giá thành **`true`**, khiến phép kiểm tra `Date.now() - lastSessionVerifiedAt > 15_000` **không bao giờ có tác dụng chặn spam request**.

### 3.3. Điểm gãy 3: `updateSession()` phát tham chiếu object mới vô điều kiện & `RequireRole` phụ thuộc `[session]`
* Trong [`ecolink-web/src/lib/api.ts#L276-L291`](file:///d:/Project/ecolink-web/src/lib/api.ts#L276-L291), mỗi lần `GET /api/v1/auth/me` trả về `200 OK`, hàm gọi thẳng `updateSession({ ... })` ngay cả khi dữ liệu người dùng không thay đổi.
* Trong [`ecolink-web/src/lib/session.ts#L276-L289`](file:///d:/Project/ecolink-web/src/lib/session.ts#L276-L289):
  ```ts
  localStorage.setItem(KEY, JSON.stringify(next))
  invalidate() // Đặt cached = null, primed = false
  emit()       // Ép useSyncExternalStore đọc lại JSON.parse() -> tạo object tham chiếu MỚI!
  ```
  Việc `invalidate()` + `emit()` tạo ra một object `Session` mới tinh trong RAM.
* Đồng thời, [`RequireRole.tsx#L16-L25`](file:///d:/Project/ecolink-web/src/components/RequireRole.tsx#L16-L25) khai báo effect:
  ```tsx
  const session = useSession()

  useEffect(() => {
    if (typeof window === "undefined") return
    if (!session) {
      openAuthPopup("login")
      return
    }
    void ensureActiveAuthSession()
  }, [session])
  ```
  Vì dependency array là `[session]` (so sánh bằng `Object.is`), mọi lần `updateSession()` chạy đều khiến `useEffect` của `RequireRole` kích hoạt lại ngay lập tức $\rightarrow$ gọi `ensureActiveAuthSession()` $\rightarrow$ gọi `/api/v1/auth/me` $\rightarrow$ lại `updateSession()` $\rightarrow$ khép kín **vòng lặp vô tận**.

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

| Thành phần | Mức độ ảnh hưởng | Mô tả tác động thực tế |
| :--- | :---: | :--- |
| **Client Browser (React SPA)** | Cao | `RequireRole` và toàn bộ các component dùng `useSession()` (`Navbar`, `Portal`, `SponsoredAdBanner`, `CheckIn`, `Transactions`, `Receipt`...) bị re-render liên tục theo tốc độ mạng. |
| **Network & Cloudflare Tunnel** | Cao | Bão request `GET /api/v1/auth/me` (hàng trăm request/phút trên 1 tab mở), dễ chạm giới hạn rate-limit của tunnel hoặc làm nghẽn băng thông trình duyệt. |
| **Backend `Ecolink.WebApi`** | Trung bình – Cao | Mỗi request `/me` phải giải mã JWT từ Cookie và truy vấn `Users` + `UserEcoProfiles` + `Subscriptions` liên tục không cần thiết. |

---

## 5. GIẢI PHÁP KHẮC PHỤC ĐỀ XUẤT (ACTIONABLE REMEDIATION)

Cần xử lý đồng bộ cả 3 mắt xích gây vòng lặp:

### 5.1. Sửa điều kiện cooldown trong `ensureActiveAuthSession()` ([`api.ts#L346-L360`](file:///d:/Project/ecolink-web/src/lib/api.ts#L346-L360))
Không sử dụng `!token` để ép verify liên tục khi đang chạy ở chế độ **HttpOnly Cookie** (`hasCookie` hoặc `session?.fromBackend`). Khi `lastSessionVerifiedAt === 0` ở lần vào trang đầu tiên, `Date.now() - lastSessionVerifiedAt > SESSION_VERIFY_COOLDOWN_MS` vốn dĩ đã là `true`:
```ts
const hasCookieAuth = hasCookie || Boolean(session?.fromBackend)
const shouldVerifyWithBackend =
  forceVerify ||
  !session ||
  (!token && !hasCookieAuth) ||
  Date.now() - lastSessionVerifiedAt > SESSION_VERIFY_COOLDOWN_MS
```
*(Nếu `lastSessionVerifiedAt` nên được khởi tạo/gán ngay từ khi gọi để đồng thời chặn cả các caller song song trong lúc chưa hoàn tất, giữ `sessionVerifyInFlight` như hiện có là đúng, nhưng phải bỏ `!token` khi `hasCookieAuth === true`).*

### 5.2. Thu hẹp dependency của `useEffect` trong [`RequireRole.tsx#L18-L25`](file:///d:/Project/ecolink-web/src/components/RequireRole.tsx#L18-L25)
Chỉ kích hoạt kiểm tra phiên khi trạng thái định danh/vai trò thay đổi thực sự (`session?.id`, `session?.role`), không chạy lại mỗi khi `updateSession()` cập nhật các thông tin phụ:
```tsx
const sessionId = session?.id ?? null
const sessionRole = session?.role ?? null

useEffect(() => {
  if (typeof window === "undefined") return
  if (!sessionId || !sessionRole) {
    if (!session) openAuthPopup("login")
    return
  }
  void ensureActiveAuthSession()
}, [sessionId, sessionRole])
```

### 5.3. Chống `invalidate()` / `emit()` thừa trong `updateSession()` ([`session.ts#L276-L289`](file:///d:/Project/ecolink-web/src/lib/session.ts#L276-L289))
Trước khi ghi `localStorage`, xóa cache và phát thông báo, so sánh nội dung mới `next` với `current` hiện tại:
```ts
const prevJson = JSON.stringify(current)
const nextJson = JSON.stringify(next)
if (prevJson === nextJson) return

localStorage.setItem(KEY, nextJson)
invalidate()
emit()
```
Nếu thông tin user trả về từ `/api/v1/auth/me` không có sự thay đổi nào, `useSession()` sẽ giữ nguyên tham chiếu object cũ, triệt tiêu hoàn toàn re-render dư thừa cho toàn bộ ứng dụng.

---

## 6. BÀI HỌC VÀ BIỆN PHÁP PHÒNG NGỪA (LESSONS LEARNED & PREVENTIONS)

1. **Tuyệt đối không đặt cả object từ `useSyncExternalStore` vào dependency `useEffect` gọi lại hàm có side-effect `updateStore()`:**
   * Bất kỳ `useEffect` nào gián tiếp kích hoạt cập nhật external store đều phải phụ thuộc vào **primitive keys** (`id`, `role`) hoặc store phải bảo đảm idempotency (không `emit()` khi snapshot không đổi giá trị).
2. **Phân biệt rõ chế độ `Bearer JWT in RAM` vs `HttpOnly Cookie Sentinel`:**
   * Khi thiết kế cơ chế không lưu JWT vào `localStorage`, mọi logic kiểm tra thiếu token (`!token`) đều phải tính tới trạng thái `COOKIE_SESSION_SENTINEL` sau khi tải lại trang.
3. **Kiểm tra tab Network ở trạng thái đứng yên (Idle Page Check) sau mỗi thay đổi Auth/Guard:**
   * Sau khi vào `/user`, `/recycler`, `/staff`, `/admin` và `F5` reload, cần theo dõi tab Network tối thiểu 15–20 giây để đảm bảo không phát sinh polling/loop ngầm trên `/auth/me` hoặc `/auth/refresh-token`.
