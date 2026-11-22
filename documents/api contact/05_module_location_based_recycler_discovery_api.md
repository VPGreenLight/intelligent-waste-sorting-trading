# TÀI LIỆU ĐẶC TẢ API – MODULE 6: TÌM KIẾM CƠ SỞ THU GOM THEO VỊ TRÍ ĐỊA LÝ GPS (LOCATION-BASED RECYCLER DISCOVERY)
**Mã phân hệ:** `MTH` (`Use Case U-05: Find Nearby Recyclers (GPS)`)  
**Controllers:** [`RecyclersDiscoveryController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclersDiscoveryController.cs), [`TradeController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/TradeController.cs)  
**Services thực thi:** [`GeoMatchingService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GeoMatchingService.cs) ([`IGeoMatchingService`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Interfaces/ApplicationInterfaces.cs#L18-L36)), [`WasteRuleEngineService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/WasteRuleEngineService.cs) ([`IWasteRuleEngineService`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Interfaces/ApplicationInterfaces.cs#L11-L16))  
**Validators:** [`NearbyRecyclerFilterValidator`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/RecyclerValidators.cs#L143-L169)  
**Entities:** [`RecyclerProfile`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/AuthEntities.cs#L11-L42), [`RecyclerPrice`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/RecyclerPrice.cs), [`WasteCategory`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/WasteEntities.cs)  
**DTOs:** [`TradeAndGamificationDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/TradeAndGamificationDtos.cs#L3-L63)  
**Base Routes:** `/api/v1/recyclers`, `/api/v1/trade/partners`

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS

| STT | Phương thức | Endpoint URI | Quyền truy cập | Use Case | Chức năng nghiệp vụ | Mã thông báo / Quy tắc |
|:---:|:---:|:---|:---:|:---:|:---|:---|
| 1 | `GET` | `/api/v1/recyclers/nearby` | Public / `[Authorize]` | `U-05` | Tìm kiếm các cơ sở thu gom đã xác minh (`Verified`) và đang mở cửa (`Online`) trong bán kính GPS, hỗ trợ lọc theo loại rác (`category`), từ khóa (`keyword`), sắp xếp khoảng cách/giá và trả về gợi ý dự phòng (`FallbackSuggestion`) khi `0` kết quả. | `BR-MTH-01`, `BR-MTH-04`, `BR-MTH-05`, `BR-MTH-06`, `BR-MTH-07`, `MSG-GEN-01`, `MSG-MTH-01`, `MSG-MTH-02` |
| 2 | `GET` | `/api/v1/recyclers/{recyclerId:guid}` | Public / `[Authorize]` | `U-05` | Xem chi tiết hồ sơ công khai, giờ mở cửa (`operatingHours`), thông tin liên hệ và bảng giá niêm yết đang hiệu lực (`PriceStatus.Active`) của một cơ sở thu gom trên bản đồ. | `BR-MTH-01`, `BR-MTH-05`, `BR-MTH-07`, `MSG-GEN-01`, `MSG-NOT-FOUND` |
| 3 | `GET` | `/api/v1/trade/partners/nearby` | Public / `[Authorize]` | `U-05` *(Alias)* | Endpoint tương thích ngược trả về danh sách `CollectionPartnerDto[]` trực tiếp từ bảng `recycler_profiles` và `recycler_price_list` trong PostgreSQL. | `BR-MTH-01`, `BR-MTH-04`, `BR-MTH-06`, `BR-MTH-07` |

---

## 2. LUỒNG NGHIỆP VỤ & QUY TẮC CỐT LÕI (`U-05`)

1. **Bộ lọc 5 điều kiện liên kết trực tiếp với Module `R-01` & `R-02` (`BR-MTH-01`, `BR-MTH-03`, `BR-MTH-04`, `BR-MTH-06`, `BR-MTH-07`):**
   * Chỉ trả về các cơ sở thu gom trong bảng `recycler_profiles` thỏa mãn đồng thời:
     * `VerificationStatus == Verified` (`BR-MTH-01`: đã được `Staff` thẩm định tại Module 2).
     * `ActiveStatus == Online` (`BR-MTH-06`: đang bật nhận đơn tại Module 5 `R-02`).
     * `User.Status == Active && !IsDeleted` (tài khoản chủ cơ sở không bị khóa hoặc đình chỉ).
     * Có ít nhất 1 dòng giá trong `recycler_price_list` mang trạng thái `PriceStatus.Active` (`BR-MTH-03`, `BR-MTH-07`), và khớp với `category` nếu người dùng lọc theo loại phế liệu vừa quét AI.
     * Khoảng cách địa lý thực tế `DistanceKm <= RadiusKm` (`BR-MTH-04`).
2. **Bảo vệ quyền riêng tư vị trí của người dân (`BR-MTH-05`):**
   * Tọa độ GPS (`lat`, `lng`) của người dân chỉ được xử lý tạm thời trong bộ nhớ RAM để tính khoảng cách Haversine `DistanceKm`, **tuyệt đối không lưu xuống CSDL và không hiển thị cho phía `Recycler`**.
3. **Phương án dự phòng 3 tầng khi không có cơ sở nào trong bán kính (`TotalFound == 0` / `MSG-MTH-01`):**
   * Khi `totalFound == 0`, API trả về HTTP `200 OK` kèm `messageCode = "MSG-MTH-01"` và đối tượng `fallbackSuggestion` kết nối trực tiếp với `IWasteRuleEngineService`:
     * Đề xuất bán kính mở rộng `suggestedExpandedRadiusKm = min(50, radiusKm * 2.5)`.
     * Cung cấp hướng dẫn màu thùng rác công cộng chuẩn và các bước xử lý sạch (`publicBinSortingGuide`).
     * Bật cờ `allowCommunityPickupPost = true` cho phép đăng bài gom chung cộng đồng.

---

## 3. CHI TIẾT TỪNG API ENDPOINT

---

### 3.1. Tìm kiếm Cơ sở Thu gom Gần Vị trí GPS (`GET /api/v1/recyclers/nearby`)

* **Mô tả:** Tìm kiếm các điểm thu mua phế liệu xung quanh tọa độ GPS hiện tại của người dân, hỗ trợ lọc theo nhãn rác từ bước quét AI (`category`), tìm theo tên/địa chỉ (`keyword`) và tùy chọn sắp xếp theo khoảng cách gần nhất (`distance` - mặc định theo `BR-MTH-04`) hoặc đơn giá cao nhất (`price_desc`).

#### a. Request Parameters (Query String – `NearbyRecyclerFilterRequest`)

| Tham số Query | Kiểu dữ liệu | Bắt buộc | Mặc định | Ràng buộc (`NearbyRecyclerFilterValidator`) | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `lat` | `double` | Có | — | `[-90, 90]`, `lat != 0` | Vĩ độ GPS hiện tại của người dùng (`MSG-MTH-02` nếu bằng `0` hoặc ngoài khoảng). |
| `lng` | `double` | Có | — | `[-180, 180]`, `lng != 0` | Kinh độ GPS hiện tại của người dùng (`MSG-MTH-02` nếu bằng `0` hoặc ngoài khoảng). |
| `category` | `string?` | Không | `null` | Thuộc `plastic`, `paper`, `cardboard`, `metal`, `glass` | Mã loại phế liệu cần bán (truyền từ kết quả quét AI `BF-02`). Bỏ trống để tìm tất cả cơ sở đang mở cửa. |
| `radiusKm` | `double` | Không | `10.0` | `[0.5, 50.0]` | Bán kính tìm kiếm tính bằng kilômét (`km`). |
| `keyword` | `string?` | Không | `null` | Tự do | Từ khóa lọc theo tên cơ sở (`facilityName`) hoặc địa chỉ (`address`). |
| `sortBy` | `string` | Không | `"distance"` | `"distance"` hoặc `"price_desc"` | `"distance"`: gần nhất trước (`BR-MTH-04`); `"price_desc"`: giá mua loại `category` cao nhất trước. |

#### b. Response Parameters (`ApiResponse<NearbyRecyclerSearchResponseDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `totalFound` | `int` | Tổng số cơ sở thu gom thỏa mãn điều kiện trong bán kính. |
| `searchRadiusKm` | `double` | Bán kính tìm kiếm đã áp dụng (`km`). |
| `filteredCategory` | `string?` | Mã loại phế liệu đã lọc (nếu có). |
| `items` | `CollectionPartnerDto[]` | Danh sách cơ sở thu gom kèm khoảng cách `distanceKm` và bảng giá `materials` đang `Active`. |
| `fallbackSuggestion` | `DiscoveryFallbackSuggestionDto?` | Khác `null` khi `totalFound == 0` (`MSG-MTH-01`), chứa gợi ý mở rộng bán kính và hướng dẫn thùng rác công cộng từ Rule Engine. |

##### Cấu trúc `CollectionPartnerDto` (bên trong `items[]`):

| Trường | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `id` | `Guid` | Định danh cơ sở thu gom (`RecyclerProfile.UserId`). |
| `facilityName` | `string` | Tên cơ sở / vựa phế liệu. |
| `representativeName` | `string?` | Họ tên người đại diện pháp lý. |
| `address` | `string` | Địa chỉ kinh doanh chi tiết. |
| `latitude` | `double` | Vĩ độ GPS của cơ sở. |
| `longitude` | `double` | Kinh độ GPS của cơ sở. |
| `distanceKm` | `double` | Khoảng cách từ vị trí người dân đến cơ sở (làm tròn 2 chữ số thập phân). |
| `contactPhone` | `string` | Số điện thoại liên hệ của cơ sở. |
| `operatingHours` | `string` | Chuỗi tóm tắt giờ mở cửa (VD: `"07:30 - 18:30"` hoặc `"T2-T6: 08:00 - 17:30"`). |
| `description` | `string?` | Giới thiệu năng lực thu gom của cơ sở. |
| `businessPhotoUrl` | `string?` | Ảnh mặt tiền kho bãi / cơ sở. |
| `materials` | `AcceptedMaterialDto[]` | Danh sách các loại phế liệu cơ sở đang nhận thu mua (`PriceStatus.Active`). |

##### Ví dụ 1 – Response Tìm thấy Cơ sở Thu gom (`200 OK` – `MSG-GEN-01`):
```json
{
  "success": true,
  "messageCode": "MSG-GEN-01",
  "message": "Tìm thấy 1 cơ sở thu mua trong bán kính 10 km.",
  "data": {
    "totalFound": 1,
    "searchRadiusKm": 10.0,
    "filteredCategory": "plastic",
    "items": [
      {
        "id": "22222222-2222-2222-2222-222222222222",
        "facilityName": "Vựa phế liệu EcoGreen",
        "representativeName": "Trần Thu Gom",
        "address": "123 Đường Nguyễn Hữu Cảnh, P. 22, Bình Thạnh, TP.HCM",
        "latitude": 10.7938,
        "longitude": 106.7182,
        "distanceKm": 2.66,
        "contactPhone": "0988776655",
        "operatingHours": "07:30 - 18:30",
        "description": null,
        "businessPhotoUrl": null,
        "materials": [
          {
            "categoryCode": "cardboard",
            "categoryName": "Bìa Carton (Cardboard)",
            "pricePerKg": 3800,
            "minimumQuantityKg": 3.0,
            "collectionConditions": "Thùng carton gập phẳng, khô ráo",
            "lastUpdatedAt": "2026-10-31T14:00:00Z"
          },
          {
            "categoryCode": "plastic",
            "categoryName": "Nhựa tái chế (Plastic)",
            "pricePerKg": 7000,
            "minimumQuantityKg": 1.0,
            "collectionConditions": "Chai nhựa PET/HDPE đã tráng sạch, bẹp chai",
            "lastUpdatedAt": "2026-10-31T14:00:00Z"
          }
        ]
      }
    ],
    "fallbackSuggestion": null
  },
  "errors": null,
  "timestamp": "2026-11-02T07:15:00Z"
}
```

##### Ví dụ 2 – Response Khi Không có Cơ sở nào trong Bán kính (`200 OK` – `MSG-MTH-01` kèm `FallbackSuggestion`):
```json
{
  "success": true,
  "messageCode": "MSG-MTH-01",
  "message": "Không tìm thấy cơ sở thu mua rác gần bạn.",
  "data": {
    "totalFound": 0,
    "searchRadiusKm": 2.0,
    "filteredCategory": "plastic",
    "items": [],
    "fallbackSuggestion": {
      "suggestedExpandedRadiusKm": 5.0,
      "guidanceMessage": "Không tìm thấy cơ sở thu mua rác gần bạn trong bán kính hiện tại. Bạn có thể mở rộng bán kính tìm kiếm, hoặc xử lý theo hướng dẫn thùng rác công cộng bên dưới.",
      "publicBinSortingGuide": {
        "categoryCode": "plastic",
        "categoryName": "Nhựa tái chế (Plastic)",
        "isRecyclable": true,
        "standardBinColor": "Cam / Vàng (Thùng rác tái chế)",
        "preparationSteps": [
          "Đổ hết nước thừa và súc sạch chai nhựa.",
          "Tháo nắp và bóp dẹp chai để tiết kiệm thể tích."
        ],
        "environmentalImpactNote": "Tái chế 1 kg nhựa giúp tiết kiệm 1.5 kg khí thải CO2."
      },
      "allowCommunityPickupPost": true
    }
  },
  "errors": null,
  "timestamp": "2026-11-02T07:16:00Z"
}
```

##### Ví dụ 3 – Response Lỗi Thiếu Quyền Truy cập Vị trí GPS (`400 Bad Request` – `MSG-MTH-02`):
```json
{
  "success": false,
  "messageCode": "MSG-MTH-02",
  "message": "Cần quyền truy cập vị trí để tìm cơ sở thu mua gần bạn. Vui lòng bật trong phần cài đặt.",
  "data": null,
  "errors": [
    "Lat: Cần quyền truy cập vị trí để tìm cơ sở thu mua gần bạn. Vui lòng bật trong phần cài đặt (MSG-MTH-02).",
    "Lng: Cần quyền truy cập vị trí để tìm cơ sở thu mua gần bạn. Vui lòng bật trong phần cài đặt (MSG-MTH-02)."
  ],
  "timestamp": "2026-11-02T07:17:00Z"
}
```

---

### 3.2. Xem Chi tiết Hồ sơ Công khai & Bảng giá của Cơ sở Thu gom (`GET /api/v1/recyclers/{recyclerId:guid}`)

* **Mô tả:** Khi người dân nhấn vào một Marker trên bản đồ, API trả về thông tin chi tiết của cơ sở thu gom đó (đã `Verified` và tài khoản đang `Active`) kèm toàn bộ bảng giá phế liệu đang nhận thu mua (`PriceStatus.Active`) và khoảng cách `distanceKm` tính từ vị trí người dân (nếu có truyền `lat`, `lng`).

#### a. Request Parameters

| Tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|
| `recyclerId` | Route | `Guid` | Có | Định danh của cơ sở thu gom (`RecyclerProfile.UserId`). |
| `lat` | Query | `double?` | Không | Vĩ độ hiện tại của người dùng để tính khoảng cách `distanceKm`. |
| `lng` | Query | `double?` | Không | Kinh độ hiện tại của người dùng để tính khoảng cách `distanceKm`. |

#### b. Response Parameters (`ApiResponse<CollectionPartnerDto>`)

* **`200 OK` (`MSG-GEN-01`):** Trả về đối tượng `CollectionPartnerDto` chứa thông tin công khai và danh sách `materials` đang `Active`.
* **`404 Not Found` (`MSG-NOT-FOUND`):** Cơ sở không tồn tại, chưa được `Staff` phê duyệt (`Pending`/`Rejected`) hoặc tài khoản đang bị đình chỉ.

---

### 3.3. Endpoint Tương thích Ngược (`GET /api/v1/trade/partners/nearby`)

* **Mô tả:** Giữ nguyên hợp đồng JSON cũ cho các màn hình giao dịch hiện hữu trong [`TradeController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/TradeController.cs#L28-L38), trả về trực tiếp mảng `CollectionPartnerDto[]` được truy vấn từ bảng `recycler_profiles` và `recycler_price_list` trong PostgreSQL.

