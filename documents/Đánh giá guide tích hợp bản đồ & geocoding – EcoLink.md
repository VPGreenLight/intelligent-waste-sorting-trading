# Đánh giá guide tích hợp bản đồ & geocoding – EcoLink

*Đánh giá `map_and_geocoding_integration_guide.md` (ngày 05/11/2026)*

## Đánh giá chung

Kiến trúc của guide hợp lý: tách 3 lớp (Leaflet, tile, geocoding), có cơ chế fallback khi thiếu key, và phần privacy (làm mờ tọa độ, tách bước tra cứu khỏi bước đặt lịch) làm khá tốt. Tuy nhiên guide có **hai chỗ sai/lỗi thời** ảnh hưởng trực tiếp đến việc "triển khai ngay", cộng thêm một số điểm cần chỉnh.

## Hai vấn đề cần sửa trước khi code

### 1. CARTO Voyager không còn dùng được mà không cần key

Guide ghi CARTO "0đ, không cần Key", và mục 5 khẳng định bỏ trống key vẫn chạy 100%. Điều này không còn đúng. Từ 25/9/2026, raster tiles của CARTO không được phục vụ nếu thiếu key; bản đồ sẽ hiện thông báo "API KEY REQUIRED" thay cho nền bản đồ. Trang "Introducing Voyager" trong tab trình duyệt của bạn cũng nói rõ điều này.

Tin tốt là key miễn phí và rất nhanh để lấy:

- Không cần tài khoản CARTO.
- Miễn phí đến 5 triệu tile/tháng cho mục đích phi thương mại, và 1 triệu tile/tháng cho thương mại.
- Xin key tại `carto.com/basemaps/apikey`, rồi thêm `?key=...` vào URL tile.

Với đồ án, quota này dư dùng.

### 2. Debounce 500ms + Nominatim vẫn vi phạm chính sách

Guide hướng dẫn gợi ý địa chỉ khi gõ (từ 3 ký tự, debounce 500ms). Nhưng chính sách của Nominatim công cộng cấm tự cài autocomplete phía client qua API, và vi phạm có thể bị chặn. Debounce chỉ giảm số request, không làm cho cách dùng này hợp lệ.

Cách sửa, theo thứ tự khuyến nghị:

1. Dùng bộ `LOCATION_PRESETS` (quận/phường nội bộ) làm gợi ý tức thì khi gõ; chỉ gọi Nominatim khi người dùng bấm Enter hoặc nút tìm kiếm.
2. Hoặc dùng Photon, search engine OSM được xây dựng cho kiểu search-as-you-type.
3. Hoặc LocationIQ, nhưng cần tự xác nhận free plan có endpoint autocomplete hay không (chưa kiểm chứng được).

## Các điểm cần lưu ý thêm

**Bảo vệ API key chưa thực sự an toàn.** Mọi biến `VITE_*` đều nằm trong bundle trình duyệt, nên `.gitignore` chỉ chống lộ key lên GitHub, không chống lộ qua DevTools. Bước "HTTP Referrer Restrictions" ở mục 4.1 cũng có thể không dùng được: theo một nguồn tổng hợp, free plan của LocationIQ tắt tính năng giới hạn IP và HTTP (nên kiểm tra lại trong Dashboard). Ngoài ra free plan có giới hạn 60 request/phút, và chỉ dùng thương mại được khi gắn link ghi nguồn "Search by LocationIQ.com". Vì EcoLink đã có backend, cách gọn nhất là cho backend làm proxy geocoding: giấu key, cache kết quả và tự giới hạn tốc độ.

**Tile OpenStreetMap chuẩn** (`tile.openstreetmap.org`) chỉ nên dùng khi dev hoặc demo nhẹ, không nên làm nền cho bản chạy thật có nhiều người dùng.

**MapTiler:** nhiều nguồn ghi gói free chỉ dành cho phi thương mại. Nếu EcoLink có định hướng thương mại, cần đọc kỹ điều khoản.

**Leaflet 2.0 vs 1.9.4:** khuyến nghị dùng 1.9.4 hợp lý, nhưng guide viết lúc 2.0 còn beta, nên kiểm tra lại trạng thái hiện tại trước khi chốt.

**Privacy:** thuật toán làm tròn lưới 0.005° ổn. Lưu ý Lớp 2 (click-to-pin) nên áp dụng cùng cách làm mờ, nếu không tọa độ điểm click sẽ được gửi nguyên vẹn lên backend.

## Phương án đề xuất: triển khai ngay, miễn phí

| Thành phần | Chọn | Ghi chú |
| --- | --- | --- |
| Renderer | Leaflet 1.9.4 | Giữ nguyên như guide |
| Tile | CARTO Voyager **có key free** | Chỉ thêm `?key=`; OSM tile làm dự phòng khi dev |
| Gợi ý khi gõ | `LOCATION_PRESETS` nội bộ | Tức thì, không tốn request |
| Tìm địa danh | Nominatim, chỉ gọi khi Enter | Qua backend proxy nếu có thể |
| Nâng cấp sau | Photon hoặc LocationIQ | Khi cần autocomplete thật |
| Goong, Google | Chưa cần | Xem bên dưới |

**Về Goong và Google:** use case của EcoLink là tìm theo phường, quận, chợ hay ngã tư (đúng tinh thần privacy ở Lớp 1), nên không cần địa chỉ số nhà/hẻm. Goong còn đòi xác thực số điện thoại và tách 2 loại key, thêm phức tạp mà không đem lại lợi ích rõ ràng. Google yêu cầu thẻ quốc tế, không đáng với đồ án.

## Việc cần làm tiếp

1. Xin key CARTO miễn phí và thêm `?key=` vào `L.tileLayer`; cập nhật lại mục 3.1, 5 và 6.2 của guide.
2. Bỏ cơ chế debounce gọi Nominatim khi gõ; chuyển sang gợi ý bằng `LOCATION_PRESETS` và gọi Nominatim khi submit.
3. Cân nhắc backend proxy cho geocoding để không lộ key và để cache/rate-limit.
4. Áp dụng làm mờ tọa độ cho cả điểm click-to-pin.
5. Kiểm tra lại điều khoản (MapTiler, LocationIQ) nếu dự án có định hướng thương mại.

## Nguồn tham khảo

- CARTO Basemaps FAQ: https://docs.carto.com/faqs/carto-basemaps
- CARTO – Introducing Voyager: https://carto.com/blog/new-voyager-basemap/
- Nominatim usage policy (thảo luận về autocomplete): https://github.com/tomickigrzegorz/leaflet.autocomplete/issues/22
- Photon và Nominatim trong plugin MapLibre: https://www.sanity.io/plugins/maplibre-input
- LocationIQ pricing: https://web.locationiq.com/pricing
- LocationIQ free tier: https://freetier.co/directory/products/locationiq
