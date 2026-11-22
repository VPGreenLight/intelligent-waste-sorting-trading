# Kế Hoạch Triển Khai: Thống Nhất Giao Diện Đăng Nhập / Đăng Ký Trang Nhỏ & Chuẩn Hóa Bảo Mật Token Bằng HttpOnly Cookie + Backend

> **Phạm vi:** Toàn diện cả **Frontend (`React 19 + TypeScript + Vite` — `ecolink-web`)** và **Backend (`.NET 10` — `intelligent-waste-sorting-trading`)**.  
> **Trạng thái:** Tài liệu thiết kế kỹ thuật & lộ trình triển khai chờ duyệt trước khi sửa mã nguồn.

---

## 1. Tổng Quan & Bảng Tóm Tắt Hai Hạng Mục

| Hạng mục | Vấn đề hiện tại trong Code | Giải pháp Clean Coding & Chuẩn Bảo mật | File tác động chính |
| :---: | :--- | :--- | :--- |
| **1. Giao diện Đăng nhập / Đăng ký** | Tồn tại song song **Trang Nhỏ** (popup `480x660` từ `openAuthPopup`) và **Trang Full** (`/login`, `/register` bọc bởi [`AuthLayout.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthLayout.tsx) + khung desktop lớn trong [`AuthForm.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthForm.tsx#L198-L245) đẩy người dùng sang trang trắng tách khỏi `<Layout />`). | Xoá hoàn toàn [`AuthLayout.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthLayout.tsx) và giao diện Trang Full; thống nhất [`AuthForm.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthForm.tsx) làm **Trang Nhỏ (`Compact Auth Box`, `max-w-[420px]`)** duy nhất — hoạt động mượt dưới cả dạng cửa sổ Popup lẫn Hộp thoại Modal nổi giữa màn hình trên nền `<Layout />`. | `AuthLayout.tsx` *(xoá)*, `AuthForm.tsx`, `CompactAuthModal.tsx` *(mới)*, `App.tsx`, `Login.tsx`, `Register.tsx`, `ResetPassword.tsx`, `popupAuth.ts`, `lazyPages.ts`, `check.tsx` |
| **2. Bảo mật Token (Cookie thay `localStorage` + Xác thực qua BE)** | FE đang lưu `ecolink:token`, `ecolink:refresh_token`, `ecolink:token_expires_at`, `ecolink:jwt:*` plain-text trong `localStorage` ([`session.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/session.ts#L22-L31)); mọi `fetch()` trong [`api.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/api.ts#L120-L144) thiếu `credentials: "include"`; BE mới chỉ đặt `HttpOnly Cookie` cho `refreshToken`, chưa đặt/đọc `HttpOnly Cookie` cho `accessToken`; `RequireRole` & `ensureRoleJwt` còn phụ thuộc `localStorage`. | BE đặt cả `accessToken` + `refreshToken` vào **`HttpOnly Cookie`** (cùng cờ không nhạy cảm `ecolink_session_active`) và trích xuất `accessToken` từ Cookie trong `JwtBearerEvents.OnMessageReceived`; FE bật `credentials: "include"` toàn hệ thống, xoá sạch JWT khỏi `localStorage` (chỉ giữ token trong RAM nếu cần cho SignalR) và buộc xác thực phiên/vai trò qua Backend (`GET /api/v1/auth/me`, `POST /api/v1/auth/refresh-token`). | **BE:** `AuthController.cs`, `Program.cs` <br> **FE:** `session.ts`, `api.ts`, `authApi.ts`, `RequireRole.tsx`, `recyclerWorkflowApi.ts`, `chatApi.ts`, `notificationsApi.ts`, `aiCurationApi.ts`, `governanceApi.ts` |

---

## 2. Thiết Kế Chi Tiết Hạng Mục 1: Xoá Trang Full — Thống Nhất Trang Nhỏ Làm Đăng Nhập / Đăng Ký Chính

### 2.1. Phân tích hiện trạng
1. Trong [`src/App.tsx:45-49`](file:///c:/Projects/EcoLink/ecolink-web/src/App.tsx#L45-L49):
   ```tsx
   <Route element={<AuthLayout />}>
     <Route path="/login" element={<Login />} />
     <Route path="/register" element={<Register />} />
   </Route>
   ```
   Cấu trúc này tách `/login` và `/register` ra khỏi `<Layout />` và nhét vào trang full màn hình `min-h-dvh` của [`src/components/AuthLayout.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthLayout.tsx#L10-L19).
2. Trong [`src/components/AuthForm.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthForm.tsx#L198-L245):
   - Khi `!isPopup`, component render nút `"← Về trang chủ"` và bọc form bằng khung lớn `lg:rounded-3xl lg:border lg:bg-white lg:p-8 lg:shadow-[0_4px_24px_rgba(15,31,21,0.05)]`, tạo cảm giác một trang full riêng biệt.
3. Nhiều điểm trong app (như [`ResetPassword.tsx:181,315`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/ResetPassword.tsx#L181), trình duyệt chặn popup ở [`popupAuth.ts:28`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/popupAuth.ts#L28), hoặc người dùng gõ trực tiếp URL `/login` / `/register`) sẽ rơi vào trang Full này thay vì Trang Nhỏ.

### 2.2. Giải pháp Clean Coding (Một Giao Diện Trang Nhỏ Duy Nhất)

#### Bước 1: Tạo cơ chế Hộp thoại Trang Nhỏ Toàn Cục (`AuthModal` / `popupAuth.ts`)
- Thiết kế trạng thái mở Trang Nhỏ dùng chung toàn ứng dụng (qua event store nhẹ sử dụng `useSyncExternalStore`, tương tự mẫu ở `session.ts`):
  - `openAuthModal(mode: "login" | "register" = "login")`
  - `closeAuthModal()`
  - `useAuthModalState()`
- Khi người dùng bấm **"Đăng nhập"** hoặc **"Tạo tài khoản"** trên [`Navbar.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/Navbar.tsx), [`Shell.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/admin/Shell.tsx), hoặc [`ResetPassword.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/ResetPassword.tsx):
  - Hiển thị ngay **Trang Nhỏ (`max-w-[420px]`)** nổi ở chính giữa màn hình dưới dạng hộp thoại gọn nhẹ (`CompactAuthModal`), nền phía sau vẫn là trang web hiện tại được làm mờ nhẹ (`bg-black/45 backdrop-blur-[2px]`), có nút `✕` đóng nhanh và hỗ trợ phím `Escape`.
  - Đối với ngữ cảnh mở bằng `window.open(..., "/login?popup=1")` từ luồng cũ (nếu có), giữ hỗ trợ `isAuthPopup()` trong `AuthForm` để tự `postMessage` và `window.close()`, nhưng ưu tiên mặc định `openAuthPopup()` gọi luôn **Trang Nhỏ Modal** trong cùng tab (hoặc tuỳ chọn popup) để **không bao giờ bật tab mới hoặc điều hướng sang trang Full**.

#### Bước 2: Gỡ bỏ Trang Full (`AuthLayout.tsx` & khung Full trong `AuthForm.tsx`)
- **Xoá tệp [`src/components/AuthLayout.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthLayout.tsx)** (hoặc xoá khỏi `App.tsx` và xoá file để không còn dead code).
- **Cập nhật [`src/components/AuthForm.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthForm.tsx):**
  - Hợp nhất hiển thị chỉ còn **1 giao diện Trang Nhỏ chuẩn (`w-full max-w-[420px] rounded-2xl border border-line bg-white p-6 shadow-2xl`)**:
    - Góc trên: nút đóng nhỏ gọn (`✕ Đóng` / `Đóng cửa sổ`) thay cho `"← Về trang chủ"` của trang Full.
    - Logo EcoLink gọn (`h-[52px]`), thanh tab chuyển 2 chế độ **`Tạo tài khoản` | `Đăng nhập`**, các trường form gọn gàng, nút **`Tiếp tục với Google`**, và khối **Điền nhanh 4 tài khoản mẫu** (ở tab Đăng nhập).
  - Sau khi đăng nhập/đăng ký thành công:
    - Nếu đang mở dưới dạng Modal trên trang: đóng Modal, đồng bộ Session từ Backend, và điều hướng tới `ROLE_HOME[role]` (nếu là `recycler`/`staff`/`admin` hoặc ở trang cần chuyển hướng), còn nếu đang là `user` trên trang hiện tại có thể giữ nguyên hoặc về `/`.

#### Bước 3: Xử lý route `/login` và `/register` trong [`src/App.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/App.tsx)
- Đưa `<Route path="/login" element={<Login />} />` và `<Route path="/register" element={<Register />} />` **vào bên trong `<Route element={<Layout />}>`** (hoặc giữ popup query `?popup=1` riêng cực gọn nếu mở cửa sổ nhỏ):
  - [`src/pages/Login.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Login.tsx) và [`src/pages/Register.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Register.tsx): hiển thị nền Trang chủ rút gọn (hoặc overlay `<CompactAuthModal />` nằm trên `<Layout />`), bên trên chỉ là **Trang Nhỏ `max-w-[420px]`** làm trung tâm, bấm `✕` sẽ đóng về `/`.
  - Nhờ giữ route `/login` và `/register` (nhưng render bằng **Trang Nhỏ** trên `<Layout />`), các link/test cũ như [`ResetPassword.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/ResetPassword.tsx), `check.tsx`, `check-routes.tsx` không bị `404` và tuyệt đối không bao giờ hiện trang Full màn hình.

---

## 3. Thiết Kế Chi Tiết Hạng Mục 2: Chuyển Lưu Trữ Token Sang HttpOnly Cookie & Xác Thực Qua Backend

### 3.1. Đối chiếu Lỗ Hổng Hiện Tại vs Chuẩn Bảo Mật Mục Tiêu

| Tiêu chí | Hiện tại (Chưa đạt) | Mục tiêu sau triển khai (Đạt chuẩn Cookie + BE) |
| :--- | :--- | :--- |
| **Lưu Access Token** | Lưu plain-text ở `localStorage["ecolink:token"]` và `localStorage["ecolink:jwt:<role>"]` ([`session.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/session.ts#L22-L31)) | Lưu trong **`HttpOnly Cookie` (`accessToken`)** do Backend set via `Set-Cookie`. JS **không thể đọc** từ `document.cookie` hay `localStorage` (chống XSS). |
| **Lưu Refresh Token** | Lưu ở cả `localStorage["ecolink:refresh_token"]` lẫn HttpOnly Cookie (nhưng FE gửi bằng JSON body trong `rotateRefreshTokenSingleFlight`) | Chỉ lưu trong **`HttpOnly Cookie` (`refreshToken`)** phía trình duyệt; hoàn toàn **không tồn tại** trong `localStorage`. |
| **Gửi Token lên BE** | `fetch()` không có `credentials: "include"`, luôn đọc token từ `localStorage` để gắn Header `Authorization` | Mọi `fetch()` đặt **`credentials: "include"`**. Trình duyệt tự động gửi Cookie `accessToken` và `refreshToken` tới Backend. |
| **BE xác thực Request** | `JwtBearerEvents.OnMessageReceived` ở [`Program.cs:90-103`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Program.cs#L90-L103) chưa đọc `Request.Cookies["accessToken"]` | Đọc ưu tiên Header `Authorization` (Swagger/Tests) $\rightarrow$ **`Request.Cookies["accessToken"]`** (Trình duyệt & SignalR) $\rightarrow$ `?access_token=` (fallback SignalR). |
| **Xác thực Role/Session** | [`RequireRole.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/RequireRole.tsx) chỉ đọc `localStorage["ecolink:user"]`, có fallback login offline password `"123"` | FE đồng bộ & xác minh phiên thật với BE qua **`GET /api/v1/auth/me`** (bằng Cookie); tự động xoay vòng qua **`POST /api/v1/auth/refresh-token`** bằng Cookie khi 401; xoá bỏ mật khẩu offline giả `"123"` khi BE đang hoạt động hoặc cảnh báo rõ. |

### 3.2. Thiết kế Phía Backend (`.NET 10`)

#### A. Cập nhật [`AuthController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AuthController.cs)
1. **Hợp nhất helper cấp Cookie `SetAuthCookies(AuthResponseDto result)`:**
   - Gọi `SetAuthCookies(result)` sau khi xử lý thành công ở cả 5 endpoints:
     - `POST /api/v1/auth/register`
     - `POST /api/v1/auth/register-recycler`
     - `POST /api/v1/auth/login`
     - `POST /api/v1/auth/google`
     - `POST /api/v1/auth/refresh-token`
   - Xây dựng `CookieOptions` sạch theo ngữ cảnh HTTPS/Cross-site:
     - Nếu `Request.IsHttps` hoặc `X-Forwarded-Proto: https` (như qua Cloudflare Tunnel / HTTPS domain khác nhau): dùng `SameSite = SameSiteMode.None`, `Secure = true`.
     - Nếu chạy HTTP nội bộ (`localhost:5173` $\rightarrow$ proxy `/api` hoặc `localhost:5000` / `localhost:5017`): dùng `SameSite = SameSiteMode.Lax`, `Secure = false` (vì trên HTTP thuần trình duyệt sẽ từ chối cookie `SameSite=None; Secure=true`).
   - Ghi **3 Cookie chuẩn:**
     1. **`accessToken`** (`HttpOnly = true`, `Path = "/"`, `Expires = result.ExpiresAt`): chứa JWT Access Token.
     2. **`refreshToken`** (`HttpOnly = true`, `Path = "/"`, `Expires = DateTime.UtcNow.AddDays(7)`): chứa Refresh Token (chống XSS đọc trực tiếp).
     3. **`ecolink_session_active`** (`HttpOnly = false`, `Path = "/"`, giá trị `"1"`, `Expires = DateTime.UtcNow.AddDays(7)`): chỉ là cờ boolean đánh dấu với JS rằng đang có phiên Cookie hợp lệ trên trình duyệt (tuyệt đối không chứa token hay PII).
2. **Cập nhật `POST /api/v1/auth/logout`:**
   - Đọc `refreshToken` từ `Request.Cookies["refreshToken"] ?? request?.RefreshToken`.
   - Đọc `userId` từ `User` (đã xác thực qua Header hoặc Cookie `accessToken`).
   - Thu hồi token trong DB qua `_authService.LogoutAsync(...)`.
   - Xoá triệt để cả 3 cookie (`accessToken`, `refreshToken`, `ecolink_session_active`) với **cùng `Path = "/"` và `SameSite`/`Secure` tương ứng** (nếu không truyền đúng `CookieOptions`, một số trình duyệt không xoá cookie).
3. **Cập nhật `GET /api/v1/auth/me`:**
   - Hỗ trợ đọc token theo thứ tự:
     1. Header `Authorization: Bearer ...`
     2. **`Request.Cookies["accessToken"]`**
     3. Query parameter `?token=...`
   - Kiểm tra cả trạng thái tài khoản (`Status != Suspended / Deactivated`) và trả về `UserInfoDto` chuẩn.

> [!NOTE]
> **Tính tương thích ngược (Swagger & Integration Tests):**  
> Vẫn giữ trả `accessToken` trong JSON body của `AuthResponseDto` (hoặc cấu hình theo client) để Swagger UI (`Authorize` button) và toàn bộ bộ test tự động `EcoLink.ApiTests` hoạt động bình thường không bị gãy contract, trong khi **Frontend trình duyệt sẽ không ghi bất kỳ token nào trong JSON đó vào `localStorage`**. (Tuỳ chọn tăng cường: khi request có header `X-EcoLink-Auth-Mode: cookie`, BE có thể bỏ qua/ẩn `refreshToken` khỏi JSON body để bảo mật tối đa).

#### B. Cập nhật [`Program.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Program.cs#L90-L103)
- Trong `AddJwtBearer(...).Events.OnMessageReceived`:
  - Giữ kiểm tra SignalR query `?access_token=` cho `/hubs/negotiation-chat` và `/hubs/notifications`.
  - **Bổ sung đọc từ `HttpOnly Cookie` `accessToken`:**
    ```csharp
    if (string.IsNullOrEmpty(context.Token) &&
        string.IsNullOrEmpty(context.Request.Headers.Authorization) &&
        context.Request.Cookies.TryGetValue("accessToken", out var cookieAccessToken) &&
        !string.IsNullOrWhiteSpace(cookieAccessToken))
    {
        context.Token = cookieAccessToken;
    }
    ```
  - Đảm bảo cả **REST API Controllers** lẫn **SignalR WebSocket Hubs** đều xác thực được trực tiếp từ `HttpOnly Cookie` `accessToken`.
- Bổ sung nhận diện `ForwardedHeaders` (`X-Forwarded-Proto`) nếu chạy sau reverse proxy (Cloudflare/Nginx) để `Request.IsHttps` chính xác khi set cookie `Secure`.

### 3.3. Thiết kế Phía Frontend (`React + TypeScript`)

#### A. Dọn sạch token khỏi `localStorage` tại [`src/lib/session.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/session.ts)
1. **Xoá bỏ việc lưu token trong `localStorage`:**
   - Gỡ bỏ `localStorage.setItem` cho `ACCESS_TOKEN_KEY`, `REFRESH_TOKEN_KEY`, `TOKEN_EXPIRES_AT_KEY` và `ecolink:jwt:*`.
   - Khi chạy trong trình duyệt, thực hiện **cleanup một lần** (`purgeLegacyTokenStorage()`) để xoá sạch mọi key cũ (`ecolink:token`, `ecolink:refresh_token`, `ecolink:token_expires_at`, `ecolink:jwt:user/recycler/staff/admin`) còn sót lại từ các phiên bản trước.
2. **Quản lý phiên Cookie & Token an toàn trong bộ nhớ RAM:**
   - `let memoryAccessToken: string | null = null` (chỉ sống trong RAM của tab đang mở nếu cần dùng tức thời cho SignalR hoặc chuyển đổi phiên test nội bộ; đóng/refresh tab tự sạch).
   - Hàm `hasSessionCookieFlag(): boolean`: kiểm tra `document.cookie` có chứa `ecolink_session_active=1`.
   - Chuẩn hóa các hàm hiện có để **không làm gãy bất kỳ màn hình nào** đang gọi `getAccessToken()` / `hasLiveBackendAuth()`:
     - `isRealJwt(value?: string | null)`: kiểm tra đúng cấu trúc JWT (`a.b.c`).
     - `getAccessToken()`: nếu `memoryAccessToken` có giá trị $\rightarrow$ trả về `memoryAccessToken`; nếu không nhưng có phiên Backend qua Cookie (`getSession()?.fromBackend || hasSessionCookieFlag()`) $\rightarrow$ trả về chuỗi cờ nội bộ `COOKIE_SESSION_SENTINEL` (`"__cookie_auth__"`). Mọi page đang viết `if (!getAccessToken()) return` sẽ nhận diện đúng là đã đăng nhập qua Cookie!
     - `getRefreshToken()`: với luồng Cookie, trả về `hasSessionCookieFlag() || getSession()?.fromBackend ? "__cookie_refresh__" : null` để báo hiệu cho `apiRequest` biết có thể gọi `/api/v1/auth/refresh-token` (bằng `HttpOnly Cookie`).
     - **`localStorage["ecolink:user"]` (`KEY`)** chỉ lưu dữ liệu hiển thị UI không nhạy cảm (`name`, `role`, `id`, `avatarUrl`, `fromBackend`) phục vụ `useSyncExternalStore` render tức thời ở lần mount đầu, nhưng **luôn được BE kiểm chứng lại qua `GET /api/v1/auth/me`**.
   - **Cập nhật `signOut()`:** gửi yêu cầu `POST /api/v1/auth/logout` với `credentials: "include"` để Backend thu hồi Refresh Token trong DB và xoá hết `HttpOnly Cookie`, đồng thời xoá state RAM + `ecolink:user`.

#### B. Bật `credentials: "include"` & Xác thực tự động trên [`src/lib/api.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/api.ts)
1. Trong **`fetchWithAutoHost(path, init)`:**
   - Mặc định ghép `credentials: init.credentials ?? "include"` vào mọi HTTP request.
   - Đọc rõ quy tắc ưu tiên proxy: ưu tiên đi qua đường dẫn tương đối `""` (`/api/...` qua Vite proxy cùng origin `localhost:5173`) trước khi fallback `localhost:5000`/`localhost:5017`, giúp Cookie chạy ổn định nhất theo chính sách Same-Origin/Same-Site.
2. Trong **`apiRequest(...)` và `getAuthHeaders()` dùng chung cho các module khác (`aiCurationApi.ts`, `governanceApi.ts`, `notificationsApi.ts`, `chatApi.ts`, `recyclerWorkflowApi.ts`):**
   - Chỉ gắn Header `Authorization: Bearer ${token}` khi `token` là JWT thật (`isRealJwt(token)`). Nếu là phiên Cookie thuần sau khi F5 (`token === "__cookie_auth__"`), **không gắn header giả** mà để trình duyệt tự gửi `HttpOnly Cookie` `accessToken`.
   - Loại bỏ hoàn toàn nhánh đọc `parsed?.token ?? parsed?.accessToken` từ `localStorage["ecolink:user"]` trong `readEffectiveAccessToken()`.
3. Trong **`rotateRefreshTokenSingleFlight()`:**
   - Gọi `POST /api/v1/auth/refresh-token` với `credentials: "include"` và Header `X-EcoLink-Auth-Mode: cookie` (body `{}` rỗng — Backend tự lấy `refreshToken` từ `HttpOnly Cookie`).
   - Khi thành công: cập nhật `memoryAccessToken` (nếu có) và gọi `updateSession(...)` với dữ liệu user mới nhất từ Backend.
   - Khi thất bại (`400` / `401` / `403`): gọi `clearAuthStorage()` và trả `null`.
4. Trong **`ensureActiveAuthSession()`:**
   - Chạy khi ứng dụng khởi động hoặc chuyển vào route bảo vệ:
     - Nếu trình duyệt có cờ cookie `ecolink_session_active=1` hoặc `session?.fromBackend === true`: gọi **`GET /api/v1/auth/me`** (với `credentials: "include"`).
     - Nếu `/api/v1/auth/me` trả `401`: thử gọi `rotateRefreshTokenSingleFlight()` $\rightarrow$ sau đó gọi lại `/api/v1/auth/me`.
     - Nếu vẫn thất bại hoặc tài khoản bị `Suspended`/`Deactivated`: xoá ngay session phía FE để đồng bộ trạng thái đã đăng xuất/bị khoá với Backend.

#### C. Chuẩn hóa [`src/lib/authApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/authApi.ts) & [`src/lib/recyclerWorkflowApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/recyclerWorkflowApi.ts)
1. Trong **`authApi.ts`:**
   - Gửi thêm header `X-EcoLink-Auth-Mode: cookie` ở `login`, `register`, `google`.
   - Trong `persistBackendAuth()`: gọi `setAuthTokens` (chỉ lưu RAM + session metadata không nhạy cảm, **bỏ hoàn toàn `localStorage.setItem('ecolink:jwt:...')`**).
   - Rà soát nhánh đăng nhập mẫu ngoại tuyến (`OFFLINE_LEGACY_ALIASES` với mật khẩu `"123"`): giữ đúng quy tắc **chỉ cho phép fallback khi `err.isNetworkError === true`**, còn khi Backend đang chạy hoặc người dùng đăng nhập qua luồng chính thức, mọi thông báo lỗi `400/401/403/423` từ Backend phải được giữ nguyên.
2. Trong **`recyclerWorkflowApi.ts` (`ensureRoleJwt` & `authorizedRequest`):**
   - **Xoá bỏ** cơ chế lưu/đọc `localStorage.getItem('ecolink:jwt:...')` và **không tự động gọi ngầm `/api/v1/auth/login` để tráo Cookie trình duyệt sang tài khoản Demo khác** khi người dùng đang có phiên đăng nhập Cookie thật (vì nếu `authorizedRequest` gọi `/api/v1/auth/login` với tài khoản Demo Recycler, Backend sẽ gửi `Set-Cookie` **ghi đè luôn `HttpOnly Cookie` của User thật đang đăng nhập**!).
   - Thay vào đó:
     - Mọi thao tác của người dùng đã đăng nhập đều sử dụng trực tiếp **`HttpOnly Cookie` của chính người dùng đó** (`credentials: "include"`).
     - Nếu chưa đăng nhập Backend (`!getAccessToken()`), ném lỗi yêu cầu đăng nhập hoặc dùng fallback ngoại tuyến nếu ở chế độ demo không Backend (tuỳ endpoint công khai/workflow).
     - Khi gặp `401 Unauthorized`, sử dụng cơ chế xoay vòng cookie tập trung `rotateRefreshTokenSingleFlight()` của `api.ts`.
3. Trong **các API clients còn lại (`chatApi.ts`, `notificationsApi.ts`, `aiCurationApi.ts`, `governanceApi.ts`):**
   - Bổ sung `credentials: "include"` vào mọi lời gọi `fetch` / `fetchWithAutoHost` trực tiếp (như tải file CSV/XLSX trong `governanceApi.ts`, SSE/SignalR negotiate trong `chatApi.ts` & `notificationsApi.ts`, upload dataset trong `aiCurationApi.ts`).
   - Chỉ gắn Header `Authorization` khi `isRealJwt(getAccessToken())` là `true`, còn lại dựa vào `HttpOnly Cookie` của trình duyệt.

#### D. Xác thực phân quyền trực tiếp với Backend trong [`src/components/RequireRole.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/RequireRole.tsx)
- Khi render bất kỳ route được bảo vệ nào (`/user/*`, `/recycler/*`, `/staff/*`, `/admin/*`, `/chat`):
  - Nếu đang ở SSR smoke test (`typeof window === "undefined"`): giữ nguyên behavior để `npm run check` / `npm run check:routes` hoạt động bình thường.
  - Ở trình duyệt thật: kích hoạt xác thực phiên với Backend (`ensureActiveAuthSession()`):
    - Hiển thị skeleton loading ngắn nếu phiên chưa xác minh với BE trong phiên tải trang hiện tại.
    - Nếu BE xác nhận không hợp lệ (`401`/`403`) $\rightarrow$ xoá phiên, đưa về `/` và mở **Trang Nhỏ Đăng nhập**.
    - Nếu role trả về từ Backend (`/api/v1/auth/me`) khác với `roles` cho phép $\rightarrow$ cập nhật lại đúng `session.role` từ Backend và điều hướng về `ROLE_HOME[realRole]` (ngăn việc người dùng tự sửa JSON trong `localStorage` để vào nhầm khung UI Admin/Staff).

---

## 4. Lộ Trình Triển Khai Theo Giai Đoạn & Kiểm Thử

### Giai đoạn 1: Backend Cookie Auth (`.NET 10`)
1. Cập nhật [`Program.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Program.cs): bổ sung đọc `Request.Cookies["accessToken"]` trong `JwtBearerEvents.OnMessageReceived` và hỗ trợ `X-Forwarded-Proto`.
2. Cập nhật [`AuthController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AuthController.cs): triển khai `SetAuthCookies` (`accessToken` + `refreshToken` `HttpOnly` + `ecolink_session_active`), `DeleteAuthCookies` trong `/logout`, và đọc cookie `accessToken` trong `GET /api/v1/auth/me`.
3. Chạy `dotnet build` và `dotnet test` (bổ sung test case xác thực bằng Cookie cho `login` $\rightarrow$ `/me` $\rightarrow$ `refresh-token` $\rightarrow$ `logout`).

### Giai đoạn 2: Frontend Token Security Migration (`Cookie` + BE Verification)
1. Cập nhật [`src/lib/session.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/session.ts): xoá lưu JWT khỏi `localStorage`, dọn legacy keys, dùng RAM + `ecolink_session_active` cookie sentinel.
2. Cập nhật [`src/lib/api.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/api.ts) và [`src/lib/authApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/authApi.ts): bật `credentials: "include"`, xác thực & refresh bằng Cookie, gỡ `localStorage["ecolink:jwt:*"]`.
3. Cập nhật [`src/lib/recyclerWorkflowApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/recyclerWorkflowApi.ts), [`src/lib/chatApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/chatApi.ts), [`src/lib/notificationsApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/notificationsApi.ts), [`src/lib/aiCurationApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/aiCurationApi.ts), [`src/lib/governanceApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/governanceApi.ts): bỏ đọc token từ `localStorage`, đảm bảo mọi request đi qua Cookie `HttpOnly` và không tự ghi đè cookie bằng demo login ngầm.
4. Cập nhật [`src/components/RequireRole.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/RequireRole.tsx): đối chiếu vai trò thật từ `/api/v1/auth/me` khi vào route bảo vệ.

### Giai đoạn 3: Thống Nhất Giao Diện Trang Nhỏ Đăng Nhập / Đăng Ký
1. Tạo `src/components/CompactAuthModal.tsx` và nâng cấp [`src/lib/popupAuth.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/popupAuth.ts).
2. Cập nhật [`src/components/AuthForm.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthForm.tsx) loại bỏ toàn bộ biến thể Trang Full; chuẩn hóa một giao diện Trang Nhỏ duy nhất.
3. Gỡ bỏ [`src/components/AuthLayout.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthLayout.tsx); cập nhật [`src/App.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/App.tsx), [`src/pages/Login.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Login.tsx), [`src/pages/Register.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Register.tsx), [`src/pages/ResetPassword.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/ResetPassword.tsx), [`src/components/Navbar.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/Navbar.tsx), [`src/components/admin/Shell.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/admin/Shell.tsx).

### Giai đoạn 4: Kiểm Thử Toàn Diện
1. **Frontend:** Chạy đầy đủ 4 kiểm tra tại `c:\Projects\EcoLink\ecolink-web`:
   - `npm run typecheck`
   - `npm run check`
   - `npm run check:routes`
   - `npm run build`
2. **Backend:** Chạy `dotnet test` tại `c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend`.
3. **Kiểm tra thủ công trên trình duyệt (DevTools):**
   - **Kiểm tra UI:** Mở `/`, bấm Đăng nhập / Tạo tài khoản trên Navbar, và truy cập trực tiếp `/login`, `/register` $\rightarrow$ tất cả đều chỉ hiện **Trang Nhỏ `max-w-[420px]`**, không còn trang Full màn hình.
   - **Kiểm tra Bảo mật Cookie:** Mở `DevTools -> Application -> Local Storage` $\rightarrow$ xác nhận **không còn** bất kỳ khoá `ecolink:token`, `ecolink:refresh_token`, hay `ecolink:jwt:*` nào. Mở `Cookies` $\rightarrow$ xác nhận có `accessToken` và `refreshToken` đánh dấu tích **`HttpOnly = ✓`**; gõ `document.cookie` trên Console không đọc được 2 token này.
   - **Kiểm tra vòng đời phiên:** F5 tải lại trang ở `/user`, `/recycler`, `/admin` $\rightarrow$ FE gọi `GET /api/v1/auth/me` bằng Cookie và giữ nguyên đăng nhập; thử hết hạn `accessToken` $\rightarrow$ hệ thống tự xoay vòng qua `/api/v1/auth/refresh-token`; bấm Đăng xuất $\rightarrow$ Cookie bị xoá sạch khỏi trình duyệt và thu hồi trên DB.
