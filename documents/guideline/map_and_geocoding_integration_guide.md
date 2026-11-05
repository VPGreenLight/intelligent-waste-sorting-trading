# HƯỚNG DẪN TÍCH HỢP BẢN ĐỒ TƯƠNG TÁC, LẤY API KEY & XỬ LÝ BẢO MẬT VỊ TRÍ (PRIVACY-FIRST GEOCODING)
**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ áp dụng:** Module 6 – Tìm kiếm Cơ sở Thu gom theo Vị trí (`U-05`, `BR-MTH-04`, `BR-MTH-05`)  
**Frontend mục tiêu:** `ecolink-web` (`React 19 + TypeScript + Vite 8 + Tailwind CSS v4`)  
**Backend liên kết:** `Ecolink.WebApi` (`GET /api/v1/recyclers/nearby`, `GET /api/v1/recyclers/{id}` & Proxy `GET /api/v1/geocoding/search`)  
**Cập nhật lần cuối:** 05/11/2026 *(Đã hiệu đính theo chính sách mới của CARTO Basemaps & Nominatim Usage Policy)*

---

## 📌 MỤC LỤC
1. [Giải đáp nhanh 3 điểm dễ nhầm khi mở trang chủ OpenStreetMap, Leaflet & Nominatim](#1-giải-đáp-nhanh-3-điểm-dễ-nhầm-khi-mở-trang-chủ-openstreetmap-leaflet--nominatim)
2. [Tổng quan Kiến trúc & Phương án chốt triển khai ngay cho EcoLink](#2-tổng-quan-kiến-trúc--phương-án-chốt-triển-khai-ngay-cho-ecolink)
3. [Hướng dẫn triển khai Bộ công cụ Miễn phí chuẩn chính sách (CARTO Free Key + Presets + Nominatim Submit)](#3-hướng-dẫn-triển-khai-bộ-công-cụ-miễn-phí-chuẩn-chính-sách-carto-free-key--presets--nominatim-submit)
   - [3.1. Ảnh nền bản đồ: Xin API Key miễn phí của CARTO Voyager (Không cần tạo tài khoản)](#31-ảnh-nền-bản-đồ-xin-api-key-miễn-phí-của-carto-voyager-không-cần-tạo-tài-khoản)
   - [3.2. Tìm kiếm địa điểm hợp lệ: `LOCATION_PRESETS` khi gõ + `Nominatim` chỉ gọi khi nhấn Enter](#32-tìm-kiếm-địa-điểm-hợp-lệ-location_presets-khi-gõ--nominatim-chỉ-gọi-khi-nhấn-enter)
4. [Các lựa chọn Nâng cấp khi cần Autocomplete thời gian thực hoặc Bản đồ thương mại](#4-các-lựa-chọn-nâng-cấp-khi-cần-autocomplete-thời-gian-thực-hoặc-bản-đồ-thương-mại)
   - [4.1. Photon (Komoot) — Search-as-you-type miễn phí từ dữ liệu OSM (Không cần Key)](#41-photon-komoot--search-as-you-type-miễn-phí-từ-dữ-liệu-osm-không-cần-key)
   - [4.2. LocationIQ — Autocomplete & Geocoding 5.000 req/ngày (Không cần thẻ Visa)](#42-locationiq--autocomplete--geocoding-5000-reqngày-không-cần-thẻ-visa)
   - [4.3. MapTiler Cloud — Ảnh nền Retina sắc nét (Lưu ý điều khoản Phi thương mại)](#43-maptiler-cloud--ảnh-nền-retina-sắc-nét-lưu-ý-điều-khoản-phi-thương-mại)
   - [4.4. Phụ lục tham khảo (Chưa cần cho EcoLink): Goong Maps & Google Maps Platform](#44-phụ-lục-tham-khảo-chưa-cần-cho-ecolink-goong-maps--google-maps-platform)
5. [Bảo mật API Key thực chất: Giới hạn của biến `VITE_*` & Kiến trúc Backend Proxy](#5-bảo-mật-api-key-thực-chất-giới-hạn-của-biến-vite_--kiến-trúc-backend-proxy)
6. [Kỹ thuật xử lý cốt lõi trong `ecolink-web`: Cài đặt Leaflet, Tương thích SSR & Marker tùy biến](#6-kỹ-thuật-xử-lý-cốt-lõi-trong-ecolink-web-cài-đặt-leaflet-tương-thích-ssr--marker-tùy-biến)
7. [Thiết kế Bảo mật Vị trí Người dân 4 Lớp (Privacy-by-Design theo `BR-MTH-05`)](#7-thiết-kế-bảo-mật-vị-trí-người-dân-4-lớp-privacy-by-design-theo-br-mth-05)
8. [Tài liệu tham khảo & Chính sách dịch vụ chính thức](#8-tài-liệu-tham-khảo--chính-sách-dịch-vụ-chính-thức)

---

## 1. GIẢI ĐÁP NHANH 3 ĐIỂM DỄ NHẦM KHI MỞ TRANG CHỦ OPENSTREETMAP, LEAFLET & NOMINATIM

Khi mới mở 3 trang `openstreetmap.org`, `leafletjs.com` và `nominatim.org`, lập trình viên rất dễ vướng 3 điểm sau:

### ⚠️ Lưu ý 1: KHÔNG dùng mục "Export" trên `openstreetmap.org/export`
* Mục **Export** trên trang chủ OpenStreetMap dùng để tải dữ liệu bản đồ thô dạng XML (`.osm` hàng chục GB) về tự dựng máy chủ GIS riêng — **không phải** cách nhúng bản đồ lên trang web.
* **Cách đúng:** Bạn **không cần tải file `.osm`** và **không cần tạo tài khoản** trên `openstreetmap.org`. Thư viện `Leaflet` trên trình duyệt sẽ tự động tải từng ô ảnh nhỏ `256x256px` (**Map Tiles**) theo tọa độ `(z, x, y)` mỗi khi người dùng kéo hoặc phóng to bản đồ.

### ⚠️ Lưu ý 2: Trạng thái phiên bản `Leaflet 2.0 beta` vs `Leaflet 1.9.4` (Stable)
* Trang chủ `leafletjs.com` hiện giới thiệu nhánh thử nghiệm **`Leaflet 2.0.0-beta.x`** (`npm install leaflet@beta`) với cú pháp ES6 Class mới (`import { LeafletMap, TileLayer } from 'leaflet'`, loại bỏ biến toàn cục `L`).
* Tính đến tháng 11/2026, phiên bản chính thức ổn định (`latest` dist-tag trên `npm`) vẫn là **`leaflet@1.9.4`** (`L.map(...)`, `L.tileLayer(...)`, `L.marker(...)`, `L.circle(...)`), đi kèm bộ kiểu chuẩn `@types/leaflet`.
* **Khuyến nghị cho `ecolink-web`:** Dùng **`leaflet@^1.9.4` + `@types/leaflet`** để đảm bảo ổn định tuyệt đối khi biên dịch TypeScript và SSR. *(Trước khi nâng cấp thư viện trong tương lai, có thể kiểm tra nhanh bằng lệnh `npm view leaflet dist-tags`).*

### ⚠️ Lưu ý 3: Trên `nominatim.org` chọn "Use the API", nhưng TUYỆT ĐỐI KHÔNG gắn Autocomplete khi gõ phím
* Không chọn mục **Install your own** (yêu cầu máy chủ RAM 32GB–64GB để tự vận hành CSDL địa lý toàn cầu).
* Với mục **Use the API** (`https://nominatim.openstreetmap.org/search`), dù không cần API Key nhưng **Nominatim Usage Policy nghiêm cấm tự cài đặt tính năng gợi ý tự động khi đang gõ phím (client-side autocomplete / type-ahead)** — kể cả khi bạn đã gắn Debounce `500ms`. Nếu cố tình gọi theo từng nhịp gõ phím, IP của ứng dụng sẽ bị chặn (`HTTP 403 / 429`).
* **Cách dùng hợp lệ:** Dùng bộ địa danh nội bộ (`LOCATION_PRESETS`) để gợi ý tức thì khi đang gõ, và **chỉ gọi Nominatim đúng 1 lần khi người dùng nhấn phím `Enter` hoặc bấm nút `Tìm kiếm`** (ưu tiên gọi qua Backend Proxy để gắn `User-Agent` định danh ứng dụng và cache kết quả).

---

## 2. TỔNG QUAN KIẾN TRÚC & PHƯƠNG ÁN CHỐT TRIỂN KHAI NGAY CHO ECOLINK

Hệ thống bản đồ tra cứu của EcoLink gồm **3 lớp tách rời**, phối hợp cùng **Backend Proxy** để vừa miễn phí, đúng chính sách dịch vụ, vừa bảo mật danh tính vị trí người dân:

```
┌────────────────────────────────────────────────────────────────────────────────┐
│                          TRANG BẢN ĐỒ ECOLINK (/map)                           │
├────────────────────────┬───────────────────────────┬───────────────────────────┤
│ 1. Map Renderer        │ 2. Map Tiles (Ảnh nền)    │ 3. Tìm kiếm Địa điểm      │
│ Vẽ khung bản đồ, vòng  │ Tải các mảnh ảnh đường    │ • Khi đang gõ: Lọc tức thì│
│ bán kính bảo mật & các │ phố, phường/xã theo mức   │   từ LOCATION_PRESETS     │
│ ghim cơ sở thu gom.    │ phóng to (zoom/pan).      │ • Khi bấm Enter: Tìm địa  │
│                        │                           │   danh rộng qua Nominatim │
├────────────────────────┼───────────────────────────┼───────────────────────────┤
│ Chốt dùng:             │ Chốt dùng:                │ Chốt dùng:                │
│ • Leaflet 1.9.4        │ • CARTO Voyager (?key=    │ • Presets nội bộ (0 req)  │
│   (Dynamic import SSR) │   miễn phí 1M–5M/tháng)   │ • Nominatim qua Backend   │
│                        │ • OSM Tile (chỉ dự phòng  │   Proxy (hoặc Photon khi  │
│                        │   khi dev local thiếu key)│   cần autocomplete thật)  │
└────────────────────────┴───────────────────────────┴───────────────────────────┘
                                       │
          ┌────────────────────────────┴────────────────────────────┐
          ▼ (Khi bấm Enter tìm địa danh)                            ▼ (Tọa độ đã làm mờ lưới ~500m cho cả GPS & Click-to-Pin)
┌───────────────────────────────────────────┐   ┌────────────────────────────────────────────────┐
│ BACKEND PROXY: /api/v1/geocoding/search   │   │ BACKEND MATCHING: /api/v1/recyclers/nearby     │
│ • Gắn User-Agent chuẩn + Giấu API Key     │   │ • Tính khoảng cách Haversine trên RAM          │
│ • Cache kết quả (IMemoryCache) + RateLimit│   │ • Trả danh sách cơ sở thu mua Verified mở cửa  │
└───────────────────────────────────────────┘   └────────────────────────────────────────────────┘
```

### Bảng tổng hợp phương án khuyến nghị triển khai ngay

| Thành phần | Lựa chọn chốt | Lý do & Lưu ý vận hành |
| :--- | :--- | :--- |
| **Map Renderer** | **Leaflet `1.9.4`** + `@types/leaflet` | Bản ổn định chính thức trên `npm`, gọn nhẹ (~40 KB), nạp động tương thích SSR. |
| **Ảnh nền (Map Tiles)** | **CARTO Voyager** *(có `?key=` miễn phí)* | Xin key nhanh tại `carto.com/basemaps/apikey` (không cần tạo tài khoản, miễn phí 5M tile/tháng phi thương mại, 1M thương mại). Dùng `tile.openstreetmap.org` làm fallback **chỉ khi dev nhẹ**. |
| **Gợi ý khi đang gõ** | **`LOCATION_PRESETS` nội bộ** | Phản hồi `0ms`, không tốn request mạng, hướng người dùng chọn theo Phường/Quận/Chợ đúng tinh thần bảo mật vị trí. |
| **Tìm địa danh mở rộng** | **Nominatim** *(chỉ gọi khi nhấn `Enter`)* | Hợp lệ 100% theo Nominatim Usage Policy; đi qua Backend Proxy để gắn `User-Agent` và lưu cache. |
| **Nâng cấp về sau** | **Photon** hoặc **LocationIQ** | Chỉ bật thêm nếu về sau thực sự cần autocomplete từng ký tự cho mọi tên đường nhỏ. |
| **Goong / Google Maps** | **Chưa cần thiết** | EcoLink không tìm đến từng số nhà/hẻm riêng lẻ ở bước tra cứu bản đồ nên không cần gánh thêm xác thực SĐT/2 key (Goong) hay thẻ tín dụng quốc tế (Google). |

---

## 3. HƯỚNG DẪN TRIỂN KHAI BỘ CÔNG CỤ MIỄN PHÍ CHUẨN CHÍNH SÁCH (CARTO FREE KEY + PRESETS + NOMINATIM SUBMIT)

### 3.1. Ảnh nền bản đồ: Xin API Key miễn phí của CARTO Voyager (Không cần tạo tài khoản)

> **Lưu ý quan trọng (Cập nhật chính sách CARTO từ 25/09/2026):**  
> Các đường dẫn **Raster Tiles** của CARTO (`basemaps.cartocdn.com/rastertiles/...`) hiện **bắt buộc phải đính kèm tham số `?key=...`**. Nếu gọi URL cũ không có key, bản đồ sẽ hiển thị chữ cảnh báo **`"API KEY REQUIRED"`** đè lên nền bản đồ.

Tin vui là bạn lấy key miễn phí chỉ mất chưa tới 1 phút:
* **Không cần đăng ký tài khoản CARTO**, không cần thẻ tín dụng.
* **Hạn mức miễn phí rất lớn:** Tới **5.000.000 tiles/tháng** cho mục đích phi thương mại / học thuật (đồ án) và **1.000.000 tiles/tháng** cho mục đích thương mại.

#### Các bước lấy CARTO Basemaps API Key:
1. Truy cập trực tiếp: [https://carto.com/basemaps/apikey](https://carto.com/basemaps/apikey)
2. Điền thông tin cơ bản (Email nhận key + mục đích sử dụng dự án) để nhận chuỗi API Key miễn phí.
3. Khai báo vào file `c:\Projects\EcoLink\ecolink-web\.env.local`:
   ```env
   VITE_CARTO_API_KEY=your_carto_basemap_key_here
   ```
4. Cấu trúc URL chuẩn đưa vào `L.tileLayer(...)`:
   ```text
   https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png?key=YOUR_CARTO_KEY
   ```
   * **Ghi nguồn bắt buộc (`attribution`):** `&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>`

#### Phương án dự phòng khi đang Dev trên máy cá nhân chưa điền Key:
* Nếu thành viên mới clone code về chưa kịp điền `VITE_CARTO_API_KEY`, code Frontend có thể tạm fallback sang OpenStreetMap Standard Tile:
  ```text
  https://tile.openstreetmap.org/{z}/{x}/{y}.png
  ```
* **⚠️ Cảnh báo chính sách OSM Tile:** Máy chủ `tile.openstreetmap.org` được vận hành bằng nguồn lực quyên góp cộng đồng, **chỉ được dùng khi phát triển cục bộ (dev) hoặc demo lưu lượng rất thấp**, tuyệt đối không dùng làm nền mặc định cho bản chạy thật có nhiều người dùng.

---

### 3.2. Tìm kiếm địa điểm hợp lệ: `LOCATION_PRESETS` khi gõ + `Nominatim` chỉ gọi khi nhấn Enter

Để vừa cho trải nghiệm mượt mà, vừa tuân thủ nghiêm ngặt [Nominatim Usage Policy](https://operations.osmfoundation.org/policies/nominatim/), luồng tìm kiếm trên `/map` được chia làm 2 tầng rõ rệt:

#### Tầng 1 — Khi người dùng đang gõ chữ (`onChange`): Lọc tức thì trên `LOCATION_PRESETS`
* Xây dựng danh mục địa danh khu vực phổ biến ngay trong code Frontend (các Quận/Phường trung tâm, Chợ đầu mối, Trường Đại học, Khu đô thị tại TP.HCM, Hà Nội, Đà Nẵng, Cần Thơ...).
* Khi người dùng gõ `"Bình Thạnh"`, `"Thủ Đức"`, `"Bến Thành"`, hệ thống so khớp chuỗi không dấu ngay trên RAM (`0ms`, `0 HTTP request`) và xổ xuống danh sách gợi ý khu vực.
* Cách này đồng thời khuyến khích người dùng chọn **tâm khu vực chung** thay vì nhập địa chỉ nhà riêng.

#### Tầng 2 — Chỉ khi người dùng nhấn `Enter` hoặc bấm nút `Tìm kiếm` (`onSubmit`): Gọi Nominatim
Khi địa danh người dùng cần tìm không nằm trong `LOCATION_PRESETS`, thao tác nhấn `Enter` / nút `Tìm` sẽ gửi **1 truy vấn hoàn chỉnh duy nhất**:

```http
GET https://nominatim.openstreetmap.org/search?format=jsonv2&q=Chợ+Bà+Chiểu,+TP.HCM&countrycodes=vn&accept-language=vi&addressdetails=1&limit=5
```

**4 quy tắc bắt buộc của Nominatim Public API:**
1. **Không gọi theo từng phím gõ (No client-side autocomplete):** Chỉ kích hoạt ở sự kiện `form onSubmit` (phím `Enter` hoặc click nút kính lúp).
2. **Định danh ứng dụng qua `User-Agent`:** Nominatim cấm các `User-Agent` mặc định của thư viện HTTP (`axios`, `python-requests`,...). Khi gọi từ Backend Proxy ASP.NET Core, luôn gắn header rõ ràng:
   ```text
   User-Agent: EcoLinkWastePlatform/1.0 (https://github.com/your-org/ecolink; contact@ecolink.edu.vn)
   ```
3. **Giới hạn tốc độ tối đa `1 request/giây`** cho toàn ứng dụng.
4. **Lưu đệm (Cache) kết quả:** Lưu kết quả tìm kiếm theo từ khóa đã chuẩn hóa (`q.trim().toLowerCase()`) vào `IMemoryCache` ở Backend (ví dụ 24 giờ) và `Map` trên trình duyệt để không bao giờ hỏi lại Nominatim cùng một địa danh.

---

## 4. CÁC LỰA CHỌN NÂNG CẤP KHI CẦN AUTOCOMPLETE THỜI GIAN THỰC HOẶC BẢN ĐỒ THƯƠNG MẠI

Nếu ở các giai đoạn sau nhóm muốn nâng cấp ô tìm kiếm thành kiểu **gõ tới đâu gợi ý mọi tên đường tới đó (Search-as-you-type)** mà vẫn đúng luật, hãy chọn 1 trong 2 công cụ sau:

### 4.1. Photon (Komoot) — Search-as-you-type miễn phí từ dữ liệu OSM (Không cần Key)

Khác với Nominatim, **Photon** ([https://photon.komoot.io/](https://photon.komoot.io/)) được xây dựng trên nền Elasticsearch chuyên dụng cho tác vụ **search-as-you-type / autocomplete** trên dữ liệu OpenStreetMap.

* **Endpoint gọi trực tiếp (trả về chuẩn GeoJSON):**
  ```http
  GET https://photon.komoot.io/api/?q=Nguyen+Van+Cu&limit=5&bbox=102.14,8.18,109.46,23.39
  ```
* **Giải thích tham số & Lưu ý kỹ thuật quan trọng:**
  * `bbox=102.14,8.18,109.46,23.39`: Giới hạn khung tọa độ (`minLon,minLat,maxLon,maxLat`) bao quanh lãnh thổ Việt Nam để kết quả chính xác hơn.
  * **⚠️ KHÔNG truyền `lang=vi` lên server công cộng `photon.komoot.io`:** Máy chủ demo của Komoot chỉ mở các mã ngôn ngữ `default`, `en`, `de`, `fr` — nếu truyền `?lang=vi` API sẽ trả lỗi `HTTP 400 ("language vi is not supported")`. Hãy **bỏ trống tham số `lang`** (hoặc dùng `lang=default`) để Photon tự trả về tên địa danh Tiếng Việt gốc lưu trong dữ liệu OpenStreetMap!
  * Vẫn nên kết hợp Debounce `300ms–400ms` và `AbortController` để tránh lãng phí băng thông cho máy chủ cộng đồng.

---

### 4.2. LocationIQ — Autocomplete & Geocoding 5.000 req/ngày (Không cần thẻ Visa)

**LocationIQ** cung cấp endpoint `/v1/autocomplete` chuyên biệt cho gợi ý địa chỉ theo nhịp gõ phím, cấu trúc dữ liệu tương thích rất gần với Nominatim và không yêu cầu thẻ tín dụng khi đăng ký.

#### Điều khoản & Hạn mức gói Free cần nắm rõ:
* **Hạn mức:** `5.000 requests/ngày`, tốc độ tối đa **`2 requests/giây`** và **`60 requests/phút`**.
* **Điều kiện sử dụng thương mại:** Gói Free cho phép dùng cả cá nhân lẫn thương mại **nhưng bắt buộc phải hiển thị dòng ghi nguồn có gắn link `"Search by LocationIQ.com"`** rõ ràng trên giao diện tìm kiếm (kèm `© OpenStreetMap contributors`).
* **Lưu ý về tính năng khóa domain (`HTTP Referrer / IP Restrictions`):** Khi tạo Token tại mục **API Access Tokens** trên [https://locationiq.com/](https://locationiq.com/), hãy kiểm tra trực tiếp trong Dashboard xem tài khoản Free hiện tại của bạn có được bật mục *HTTP Referrer / IP Restrictions* hay không (một số đợt cập nhật gói Free có giới hạn tính năng này theo gói cước). Để an toàn tuyệt đối và không phụ thuộc vào giới hạn Dashboard, **hãy đặt LocationIQ Key ở phía Backend Proxy của EcoLink** (xem Mục 5).

#### Endpoint Autocomplete của LocationIQ:
```http
GET https://api.locationiq.com/v1/autocomplete?key=YOUR_SERVER_KEY&q=Bình+Thạnh&countrycodes=vn&accept-language=vi&limit=5
```

---

### 4.3. MapTiler Cloud — Ảnh nền Retina sắc nét (Lưu ý điều khoản Phi thương mại)

Nếu muốn đổi sang nền bản đồ Vector/Raster khác ngoài CARTO Voyager:
1. Đăng ký bằng Google tại [https://cloud.maptiler.com/](https://cloud.maptiler.com/) $\rightarrow$ **Account -> Keys -> + New Key** (`100.000 lượt/tháng`, không cần thẻ Visa).
2. Cấu hình **Allowed HTTP origins** (`localhost`, `*.trycloudflare.com`).
3. **⚠️ Lưu ý pháp lý quan trọng:** Gói **Free Plan của MapTiler chỉ dành cho mục đích phi thương mại, cá nhân, học tập hoặc thử nghiệm**. Nếu dự án EcoLink chuyển sang vận hành thương mại thực tế, bạn cần nâng cấp gói trả phí của MapTiler hoặc ưu tiên giữ nguyên **CARTO Basemaps Free Key** (vốn cho phép tới `1.000.000 tiles/tháng` cho cả mục đích thương mại).

---

### 4.4. Phụ lục tham khảo (Chưa cần cho EcoLink): Goong Maps & Google Maps Platform

Với bài toán hiện tại của EcoLink (`BR-MTH-05` — người dân chỉ chọn **Phường / Quận / Chợ / Ngã tư** để dò cơ sở thu mua quanh bán kính 2–25 km mà không muốn lộ số nhà riêng), nhóm **chưa cần dùng đến Goong Maps hay Google Maps**:
* **Goong Maps (`account.goong.io`):** Mạnh về dữ liệu số nhà ngõ/ngách sâu tại Việt Nam, nhưng yêu cầu xác thực SĐT, phải quản lý tách biệt 2 loại key (`REST API Key` & `Maptiles Key`) và phải gọi tốn 2 bước (`Place/AutoComplete` lấy `place_id` $\rightarrow$ `Place/Detail` lấy tọa độ). Chỉ nên cân nhắc ở giai đoạn tương lai nếu làm thêm phân hệ dẫn đường tài xế thu gom tận cửa.
* **Google Maps Platform (`console.cloud.google.com`):** Bắt buộc thẻ tín dụng quốc tế (Visa/Mastercard) để mở Billing Account và tiềm ẩn rủi ro phát sinh phí nếu quên khóa chặt hạn mức API; không cần thiết cho phạm vi đồ án.

---

## 5. BẢO MẬT API KEY THỰC CHẤT: GIỚI HẠN CỦA BIẾN `VITE_*` & KIẾN TRÚC BACKEND PROXY

### 5.1. Hiểu đúng về `.env.local` và biến `VITE_*` trên Frontend
* Việc lưu key vào `c:\Projects\EcoLink\ecolink-web\.env.local` (được `.gitignore` chặn) **chỉ giúp không đẩy nhầm key lên kho mã nguồn GitHub**.
* Tuy nhiên, theo cơ chế của Vite, **mọi biến bắt đầu bằng `VITE_*` đều được nhúng thẳng vào gói JavaScript tải xuống trình duyệt** — bất kỳ ai mở DevTools (`F12 -> Network / Sources`) đều nhìn thấy chuỗi key đó.
* **Nguyên tắc phân quyền Key chuẩn:**
  1. **Public Tile Key (`VITE_CARTO_API_KEY`):** Được phép đặt ở `.env.local` của Frontend vì trình duyệt bắt buộc phải tải trực tiếp hàng chục mảnh ảnh nền từ CDN của CARTO khi người dùng kéo bản đồ.
  2. **Secret Geocoding Key (`LocationIQ`,...) & truy vấn `Nominatim`:** **KHÔNG đặt ở Frontend** mà đưa vào **ASP.NET Core Backend (`Ecolink.WebApi`)** làm lớp trung gian (**Backend Geocoding Proxy**).

### 5.2. Lợi ích 4-trong-1 khi dùng Backend Geocoding Proxy (`GET /api/v1/geocoding/search`)
Vì EcoLink đã có sẵn `Ecolink.WebApi`, việc thêm 1 endpoint mỏng đứng trước Nominatim / LocationIQ giải quyết gọn toàn bộ bài toán kỹ thuật:
1. **Giấu kín 100% API Key:** Key nằm trong `appsettings.Development.json` hoặc `dotnet user-secrets` trên server, trình duyệt hoàn toàn không nhìn thấy (kể cả khi chạy qua Cloudflare Tunnel `*.trycloudflare.com`).
2. **Đính kèm `User-Agent` chuẩn xác cho Nominatim:** Trên trình duyệt, hàm `fetch()` thường bị chặn sửa header `User-Agent`, trong khi `HttpClient` trên ASP.NET Core gắn `User-Agent: EcoLinkWastePlatform/1.0` dễ dàng và đúng chuẩn OSM.
3. **Bộ nhớ đệm dùng chung (`IMemoryCache`):** Người dùng A đã tìm *"Chợ Bến Thành"*, khi người dùng B tìm lại từ khóa đó thì Backend trả kết quả từ RAM trong `< 1ms`, tiết kiệm quota cho cả hệ thống.
4. **Chủ động kiểm soát tốc độ (Rate-Limiting):** Đảm bảo toàn hệ thống không bao giờ vượt ngưỡng `1 req/giây` của Nominatim hay `60 req/phút` của LocationIQ.

---

## 6. KỸ THUẬT XỬ LÝ CỐT LÕI TRONG `ECOLINK-WEB`: CÀI ĐẶT LEAFLET, TƯƠNG THÍCH SSR & MARKER TÙY BIẾN

### 6.1. Cài đặt gói chuẩn ổn định (`Leaflet 1.9.4`)
Chạy tại thư mục `c:\Projects\EcoLink\ecolink-web`:
```powershell
npm install leaflet@^1.9.4
npm install -D @types/leaflet@^1.9.16
```

### 6.2. Nạp động `Leaflet` tương thích SSR (`npm run check`) + Tự động chọn Tile URL
Trong `ecolink-web`, lệnh `npm run check` biên dịch SSR toàn bộ 61 trang và chạy trên Node.js (không có biến `window`). Do đó:
* Nạp CSS của Leaflet trong `src/main.tsx` (chỉ chạy phía trình duyệt):
  ```tsx
  import "leaflet/dist/leaflet.css"
  ```
* Trong component bản đồ (`LeafletRecyclerMap.tsx`), dùng `import("leaflet")` bên trong `useEffect` và ưu tiên **CARTO Voyager có `?key=`**, chỉ fallback về OSM Tile khi đang dev cục bộ chưa cấu hình key:
  ```tsx
  useEffect(() => {
    let cancelled = false
    let mapInstance: import("leaflet").Map | null = null

    void import("leaflet").then((leafletModule) => {
      if (cancelled || !containerRef.current) return
      const L = leafletModule.default ?? leafletModule

      mapInstance = L.map(containerRef.current, {
        center: [coords.lat, coords.lng],
        zoom: 13,
        zoomControl: false,
      })

      const cartoKey = import.meta.env.VITE_CARTO_API_KEY?.trim()
      const tileUrl = cartoKey
        ? `https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png?key=${encodeURIComponent(cartoKey)}`
        : "https://tile.openstreetmap.org/{z}/{x}/{y}.png" // Chỉ dùng dự phòng khi dev cục bộ

      const attribution = cartoKey
        ? '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>'
        : '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'

      L.tileLayer(tileUrl, {
        attribution,
        maxZoom: 19,
      }).addTo(mapInstance)
    })

    return () => {
      cancelled = true
      mapInstance?.remove()
    }
  }, [])
  ```

### 6.3. Vẽ ghim bằng `L.divIcon` thay cho ảnh PNG mặc định
Khi đóng gói bằng Vite, icon PNG mặc định của Leaflet hay gặp lỗi đường dẫn tài nguyên tĩnh. Thay vào đó, sử dụng **`L.divIcon`** kết hợp HTML + màu thương hiệu EcoLink (`bg-brand-deep`, `border-mint-bright`): đồng bộ nhận diện giao diện, dung lượng nhẹ và không bao giờ vỡ ảnh.

---

## 7. THIẾT KẾ BẢO MẬT VỊ TRÍ NGƯỜI DÂN 4 LỚP (PRIVACY-BY-DESIGN THEO `BR-MTH-05`)

Để đảm bảo người dùng yên tâm tra cứu mà không lo lộ địa chỉ nhà riêng:

### Lớp 1: Mặc định Không tự xin quyền GPS (Zero-Permission Startup)
* Khi mở trang `/map`, bản đồ khởi tạo ở tâm khu vực mặc định (ví dụ trung tâm Quận 1 hoặc khu vực người dùng chọn từ `LOCATION_PRESETS`), **không tự bật hộp thoại xin quyền GPS của trình duyệt**.
* Ô tìm kiếm hướng dẫn rõ: *"Nhập Phường, Quận, Chợ hoặc Ngã tư gần bạn (không cần nhập số nhà)"*.

### Lớp 2: Chấm tâm tra cứu ảo trên bản đồ (`Click-to-Pin`) — Đồng bộ cơ chế làm mờ
* Người dùng có thể bấm trực tiếp lên bản đồ (`map.on("click", ...)`) tại một ngã tư, chợ hoặc công viên gần khu vực sinh sống để đặt tâm quét bán kính.
* **Quy tắc bảo mật nhất quán:** Ngay cả khi người dùng bấm chọn điểm trên bản đồ (`Click-to-Pin`) mà lỡ bấm sát vị trí nhà mình, nếu công tắc **🔒 Ẩn vị trí chính xác (~500m)** đang bật, tọa độ điểm click cũng **bắt buộc đi qua cùng hàm làm mờ lưới `~500m`** trước khi gửi lên Backend (không gửi tọa độ thô `e.latlng` nguyên vẹn).

### Lớp 3: Thuật toán Làm mờ tọa độ sang ô lưới `0.005°` (~500m) cho cả GPS & Click-to-Pin
Công tắc **🔒 Ẩn vị trí chính xác (~500m)** được bật mặc định (`privacyMode = true`). Mọi tọa độ đầu vào nhạy cảm (từ nút **GPS hiện tại** hoặc thao tác **Click-to-Pin**) đều được chuẩn hóa qua hàm `resolveSearchCoordinates` ngay trên trình duyệt **trước khi gọi API**:

```ts
/**
 * Chuẩn hóa tọa độ tra cứu theo chế độ bảo mật vị trí (BR-MTH-05).
 * Khi privacyMode = true, làm tròn tọa độ sang tâm ô lưới 0.005 độ (~550m tại Việt Nam)
 * áp dụng thống nhất cho cả định vị GPS lẫn thao tác Click-to-Pin trên bản đồ.
 */
export function resolveSearchCoordinates(
  rawLat: number,
  rawLng: number,
  privacyMode: boolean = true,
  gridStepDeg: number = 0.005, // ~550m tại vĩ độ Việt Nam
): { lat: number; lng: number } {
  if (!privacyMode) {
    return {
      lat: Number(rawLat.toFixed(5)),
      lng: Number(rawLng.toFixed(5)),
    }
  }

  const fuzzedLat = Math.round(rawLat / gridStepDeg) * gridStepDeg
  const fuzzedLng = Math.round(rawLng / gridStepDeg) * gridStepDeg
  return {
    lat: Number(fuzzedLat.toFixed(4)),
    lng: Number(fuzzedLng.toFixed(4)),
  }
}
```
* **Phản hồi thị giác rõ ràng:** Khi `privacyMode = true`, thay vì cắm ghim nhọn chỉ đích danh một điểm, bản đồ hiển thị **vòng tròn vùng lân cận bán kính `~500m`** kèm nhãn trạng thái *"Đang làm mờ vị trí chính xác (~500m)"*.

### Lớp 4: Tách bạch hoàn toàn bước Tra cứu và bước Đặt lịch hẹn (`BR-MTH-05`)
* **Khi tra cứu (`/map`) & nhắn tin thương lượng (`/chat`):** Tọa độ đã làm mờ chỉ dùng tạm thời trên RAM để tính khoảng cách Haversine `distanceKm`, không ghi lưu vào bảng hồ sơ người dùng và không tiết lộ cho cơ sở thu gom (`Recycler`).
* **Khi chốt bàn giao (`/user/handover`):** Chỉ khi người dân đồng ý mức giá, tự tay nhập điểm hẹn thu gom (có thể chọn đầu ngõ, sảnh chung cư hoặc điểm công cộng) và đối tác xác nhận đơn thì địa chỉ điểm hẹn đó mới được hiển thị cho bên thu gom.

---

## 8. TÀI LIỆU THAM KHẢO & CHÍNH SÁCH DỊCH VỤ CHÍNH THỨC

1. **CARTO Basemaps API Key & FAQ:** [https://carto.com/basemaps/apikey](https://carto.com/basemaps/apikey) | [https://docs.carto.com/faqs/carto-basemaps](https://docs.carto.com/faqs/carto-basemaps)
2. **Nominatim Usage Policy (Quy định cấm client-side autocomplete & yêu cầu `User-Agent`):** [https://operations.osmfoundation.org/policies/nominatim/](https://operations.osmfoundation.org/policies/nominatim/)
3. **OpenStreetMap Tile Usage Policy (Giới hạn sử dụng `tile.openstreetmap.org`):** [https://operations.osmfoundation.org/policies/tiles/](https://operations.osmfoundation.org/policies/tiles/)
4. **Photon Search-as-you-type Geocoder (Komoot):** [https://photon.komoot.io/](https://photon.komoot.io/)
5. **LocationIQ Pricing & Attribution Terms:** [https://locationiq.com/pricing](https://locationiq.com/pricing)
6. **Leaflet Releases (`1.9.4` Stable vs `2.0.0-beta`):** [https://leafletjs.com/download.html](https://leafletjs.com/download.html)
