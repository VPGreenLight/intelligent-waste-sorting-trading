# TÀI LIỆU ĐẶC TẢ API – MODULE 3: PHÂN LOẠI RÁC AI & HƯỚNG DẪN XỬ LÝ RULE ENGINE (AI WASTE CLASSIFICATION & SORTING GUIDANCE)
**Mã phân hệ:** `CLS` (Phân loại AI & Dữ liệu mô hình) & `SRT` (Hướng dẫn phân loại & Danh mục rác)  
**Core Backend Controller:** [`WasteScanController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/WasteScanController.cs) (`/api/v1/waste-scan`)  
**Core Backend Services:** [`AiVisionHttpClient.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AiVisionHttpClient.cs), [`WasteRuleEngineService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/WasteRuleEngineService.cs)  
**AI Vision Microservice (FastAPI):** [`app.py`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.AiService/app.py) (`http://127.0.0.1:8000/api/v1/...`)  
**DTOs:** [`WasteScanDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/WasteScanDtos.cs)

---

## 1. TỔNG QUAN KIẾN TRÚC & DANH SÁCH ENDPOINTS

Phân hệ Phân loại rác thông minh của EcoLink bao gồm 2 tầng API phối hợp chặt chẽ:
1. **Tầng Core Business Web API (`.NET 10` – Cổng `5000`):** Tiếp nhận yêu cầu từ Mobile/Web App, kiểm tra tính hợp lệ của tệp ảnh và mô hình, lưu vết lịch sử quét (SHA-256), gọi sang AI Microservice, kiểm tra ngưỡng an toàn **Out-of-Distribution (OOD = 60%)** và ánh xạ 10 nhãn rác sang quy tắc xử lý trong **Rule Engine**.
2. **Tầng Dedicated AI Vision Microservice (`Python FastAPI` – Cổng `8000`):** Thực thi suy luận học sâu đa mô hình chuẩn **YOLO26** (`yolo26_detect` Two-Stage End-to-End NMS-Free, `yolo26s_cls`, `yolo26n_cls`, kèm fallback `efficientnet_b0`, `mobilenet_v3` và các alias tương thích `yolov8_detect`, `yolov8n`, `yolo26`, `yolo26s`, `yolo26n`), nhận diện 10 lớp rác chuẩn EcoLink (5 nhóm phế liệu thương mại `plastic`, `paper`, `glass`, `metal`, `cardboard` + 5 nhóm hướng dẫn xử lý/an toàn `battery`, `biological`, `clothes`, `shoes`, `trash`).

### 1.1. Danh sách Endpoints tại Core Business Web API (`M-03` – `11` APIs, `STT 021–031`)

> Tham chiếu Inventory: [`00_api_inventory_and_endpoint_statistics.md`](./00_api_inventory_and_endpoint_statistics.md) (`STT 021` $\rightarrow$ `031`).

| STT | Inventory ID | Phương thức | Endpoint URI | Quyền truy cập | Chức năng nghiệp vụ | Mã thông báo chính |
|:---:|:---:|:---:|:---|:---:|:---|:---|
| 1 | `021` | `POST` | `/api/v1/waste-scan/classify` | `[Authorize]` | Nhận diện đa vật thể qua ảnh (`yolo26_detect`), kiểm tra ngưỡng OOD (`60%`), lưu `WasteScanHistory` (`SHA-256`) và trả `sortingGuide`. | `MSG-CLS-01`..`06` |
| 2 | `022` | `GET` | `/api/v1/waste-scan/models` | Public | Lấy danh sách 5 mô hình AI sẵn sàng và 10 lớp rác hỗ trợ từ FastAPI Service. | `200 OK` |
| 3 | `023` | `GET` | `/api/v1/waste-scan/rules` | Public *(Admin/Staff xem Draft)* | Tra cứu quy tắc phân loại rác (`BR-SRT-01`); hỗ trợ `?detailed=true&includeDraft=true`. | `200 OK` |
| 4 | `024` | `POST` | `/api/v1/waste-scan/feedback` | `[Authorize]` | Người dùng gửi báo cáo phản hồi khi AI nhận diện sai nhãn rác (`BR-CLS-05`). | `200 OK` (`MSG-CLS-06`) |
| 5 | `025` | `GET` | `/api/v1/waste-categories` | Public (`[AllowAnonymous]`) | Lấy danh sách danh mục rác chuẩn (mặc định trả nhóm tái chế `Active`; Admin có thể kèm `?includeInactive=true&includeNonRecyclable=true`). | `200 OK` |
| 6 | `026` | `GET` | `/api/v1/waste-scan/history` | `[Authorize]` | Lịch sử quét AI cá nhân phân trang theo tài khoản đang đăng nhập (`U-01`, `U-03`). | `200 OK` |
| 7 | `027` | `POST` | `/api/v1/waste-categories` | `Admin` | Thêm mới danh mục phế liệu chuẩn (`A-05`, kiểm tra trùng `code` trả `409 Conflict`). | `201 Created` |
| 8 | `028` | `PUT` | `/api/v1/waste-categories/{categoryId:guid}` | `Admin` | Cập nhật thông tin danh mục (`code` bất biến theo `BR-SRT-02`). | `200 OK` |
| 9 | `029` | `PATCH` | `/api/v1/waste-categories/{categoryId:guid}/status` | `Admin` | Bật/tắt trạng thái `Active` $\leftrightarrow$ `Inactive` (xóa mềm `BR-SRT-04`). | `200 OK` |
| 10 | `030` | `PUT` | `/api/v1/waste-scan/rules/{categoryId:guid}` | `Admin` | Tạo/cập nhật quy tắc phân loại `DRAFT` gắn `1-1` với `categoryId` (`A-06`). | `200 OK` |
| 11 | `031` | `PATCH` | `/api/v1/waste-scan/rules/{ruleId:guid}/publish` | `Admin` | Phát hành chính thức `DRAFT -> ACTIVE` và đồng bộ sang AI Service (`A-06`, `BR-SRT-03`). | `200 OK` |

### 1.2. Danh sách Endpoints tại Dedicated AI Vision Microservice (`AIS` – `16` APIs, `STT 203–218`, `Port 8000`)

| Inventory ID | Phương thức | Endpoint URI | Quyền / Nguồn gọi | Chức năng chuyên sâu |
|:---:|:---:|:---|:---:|:---|
| `203` | `GET` | `/` | Internal / Docs | Chuyển hướng về OpenAPI Swagger UI (`/docs`) của AI Vision Service. |
| `204` | `GET` | `/health` | Internal / Infra | Kiểm tra trạng thái sức khỏe, phần cứng `CUDA/CPU`, danh sách model sẵn sàng và ngưỡng OOD. |
| `205` | `GET` | `/api/v1/models` | Public (Internal) | Danh sách 5 mô hình AI (`yolo26_detect`, `yolo26s_cls`, `yolo26n_cls`, `efficientnet_b0`, `mobilenet_v3`) và 10 lớp rác chuẩn (`CLASS_NAMES`). |
| `206` | `GET` | `/api/v1/models/{model_id}` | Public (Internal) | Thông số kỹ thuật, kiến trúc mạng và benchmark của mô hình (hỗ trợ alias `yolov8_detect`, `yolov8n`). |
| `207` | `POST` | `/api/v1/classify` | Core Backend / Test | Suy luận phân loại đa vật thể Two-Stage (`yolo26_detect` + `yolo26s_cls`), kiểm tra ngưỡng OOD `0.60`, trả `detected_objects` & `sorting_rule`. |
| `208` | `POST` | `/api/v1/detect` | Core Backend / Test | Khoanh vùng Bounding Box từng món rác bằng `yolo26_detect` (ngưỡng mặc định `0.20`), đếm số lượng và trả ảnh Base64 đã vẽ khung. |
| `209` | `POST` | `/api/v1/classify/batch` | Benchmark / Staff | Phân loại hàng loạt nhiều file ảnh cùng lúc (mặc định `yolo26n_cls`) để đo hiệu năng (`avg_inference_time_ms`). |
| `210` | `GET` | `/api/v1/samples` | Dev / QA | Liệt kê ảnh mẫu validation theo từng danh mục kèm `download_url`. |
| `211` | `GET` | `/api/v1/samples/{category}/{filename}` | Dev / QA | Tải về file ảnh mẫu cụ thể phục vụ kiểm thử Swagger/Postman. |
| `212` | `GET` | `/api/v1/rules` | Public (Internal) | Đọc toàn bộ quy tắc phân loại tĩnh (`rules.json`) và `confidence_threshold`. |
| `213` | `PUT` | `/api/v1/rules` | Core Backend (`A-06`) | Đồng bộ/Upsert ngưỡng `confidence_threshold` và quy tắc hướng dẫn (`title`, `bin_color_name`, `instructions`, `handling_action`, `hazard_warning`). |
| `214` | `POST` | `/api/v1/feedback` | Core Backend (`U-04`) | Tiếp nhận mẫu phản hồi nhận diện sai lưu vào `feedback.json`. |
| `215` | `GET` | `/api/v1/feedback` | Internal / Staff | Truy xuất danh sách log phản hồi sai trên AI Service. |
| `216` | `POST` | `/api/v1/datasets/curate` | Core Backend (`S-03`) | Đóng gói tập dữ liệu `Train/Val/Test` kèm kiểm tra chống rò rỉ `SHA-256` (`BR-CLS-07`) và xuất `manifest.json`. |
| `217` | `POST` | `/api/v1/models/reload` | Core Backend (`S-03`) | Nạp nóng (`Hot-reload`) trọng số mới trong `models/` không cần khởi động lại dịch vụ. |
| `218` | `GET` | `/api/v1/stats` | Admin / Monitoring | Thống kê tổng lượt quét, tỷ lệ OOD (`ood_rate_percent`), phân bổ danh mục, độ trễ trung bình và 25 lượt gần nhất. |

---

## 2. CHI TIẾT CÁC API TẠI CORE BACKEND (`/api/v1/waste-scan`)

---

### 2.1. Nhận diện rác qua ảnh & Tra cứu Hướng dẫn Phân loại (`POST /api/v1/waste-scan/classify`)

* **Phân quyền:** `[Authorize]` *(yêu cầu JWT Bearer Token hoặc HttpOnly Cookie `accessToken` để ghi nhận `UserId` vào `WasteScanHistory` và bảng xếp hạng).*
* **Mô tả:** Endpoint trung tâm của chức năng Quét rác AI:
  1. Kiểm tra file ảnh đính kèm và định dạng đuôi file (`.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp` – `BR-CLS-01`).
  2. Kiểm tra mã mô hình yêu cầu (`yolo26_detect`, `yolo26s_cls`, `yolo26n_cls`, `yolo26s`, `yolo26n`, `yolo26`, `yolov8_detect`, `mobilenet_v3`, `efficientnet_b0`, `yolov8n`).
  3. Gửi luồng ảnh (`Stream`) sang AI Vision Service để suy luận Two-Stage YOLO26.
  4. Kiểm tra cờ `isOutOfDistribution` hoặc `topPrediction.confidence < confidenceThreshold` (`0.60`):
     * Nếu độ tin cậy `< 60%` (`BR-CLS-03`): Trả về cảnh báo không chắc chắn (`MSG-CLS-02`) và đặt `sortingGuide = null` để yêu cầu người dùng chụp lại hoặc chọn thủ công.
     * Nếu độ tin cậy $\ge 60\%$ (`BR-CLS-02`, `BR-SRT-01`): Tự động tra cứu `WasteRuleEngineService` theo 10 `categoryCode` chuẩn (`battery`, `biological`, `cardboard`, `clothes`, `glass`, `metal`, `paper`, `plastic`, `shoes`, `trash`) để trả về màu thùng rác tiêu chuẩn, các bước sơ chế làm sạch, hành động gợi ý và cảnh báo an toàn (đặc biệt với nhóm không nhận thu mua thương mại như `battery`, `biological`, `trash`).

#### a. Request Parameters (`multipart/form-data`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Giá trị hợp lệ / Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `file` | Form-Data | `IFormFile` | **Có** | `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp` | Tệp hình ảnh vật phẩm rác cần nhận diện. |
| `model` | Form-Data | `string?` | Không | Mặc định: `"yolo26_detect"`<br>Hỗ trợ: `"yolo26_detect"`, `"yolo26s_cls"`, `"yolo26n_cls"`, `"efficientnet_b0"`, `"mobilenet_v3"` *(cùng alias `yolo26s`, `yolo26n`, `yolo26`, `yolov8_detect`, `yolov8n`)* | Mã mô hình AI muốn sử dụng để phân tích. |

#### b. Response Parameters (`ApiResponse<WasteScanResponseDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `scanId` | `Guid?` | ID bản ghi lịch sử quét (`WasteScanHistory`) vừa lưu. |
| `imageUrl` | `string?` | Đường dẫn ảnh đã lưu trữ phục vụ xem lại lịch sử hoặc gửi báo sai. |
| `aiResult` | `AiScanResultDto` | Kết quả suy luận chi tiết từ mô hình AI (xem bảng dưới). |
| `sortingGuide` | `WasteSortingGuideDto?` | Hướng dẫn phân loại và sơ chế từ Rule Engine (`null` nếu rơi vào ngưỡng OOD `< 60%`). |
| `message` | `string` | Thông báo tổng hợp kết quả nhận diện kèm phần trăm độ tin cậy. |

##### Cấu trúc `AiScanResultDto`:
| Trường | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `success` | `boolean` | `true` nếu AI suy luận thành công. |
| `isOutOfDistribution` | `boolean` | `true` nếu vật thể lạ hoặc độ tin cậy thấp hơn ngưỡng `confidenceThreshold` (`0.60`). |
| `confidenceThreshold` | `double` | Ngưỡng độ tin cậy tối thiểu áp dụng (mặc định `0.60`). |
| `modelUsed` / `modelName` / `modelFamily` | `string?` | Mã, tên hiển thị và kiến trúc mô hình thực tế đã thực thi suy luận (VD: `"yolo26_detect"`). |
| `imageWidth` / `imageHeight` | `int` | Kích thước pixel ảnh gốc để đối chiếu hệ tọa độ Bounding Box. |
| `topPrediction` | `WastePredictionDto?` | Nhãn dự đoán có xác suất cao nhất (`label`, `confidence`, `displayName`). |
| `predictions` | `WastePredictionDto[]` | Danh sách Top-3 nhãn dự đoán có điểm tin cậy cao nhất. |
| `allProbabilities` | `WastePredictionDto[]` | Phân phối xác suất toàn bộ 10 lớp rác. |
| `detectedCount` | `int` | Tổng số vật thể được khoanh khung trong ảnh (khi dùng pipeline Two-Stage). |
| `categorySummary` | `Record<string, int>` | Thống kê số lượng vật thể theo từng `categoryCode`. |
| `detectedObjects` | `DetectedWasteObjectDto[]` | Danh sách chi tiết từng vật thể (`id`, `rawClass`, `label`, `displayName`, `confidence`, `centerScore`, `isPrimary`, `source`, `bbox`, `sortingGuide`). |
| `pipeline` | `string?` | Tên pipeline thực thi (VD: `"yolo26_unified_two_stage"`). |
| `croppedBbox` | `double[]?` | Tọa độ hộp bao `[x1, y1, x2, y2]` của vật thể chính gần tâm khung hình. |
| `inferenceTimeMs` | `double` | Thời gian suy luận của mô hình AI tính bằng mili-giây (`ms`). |
| `errorMessage` | `string?` | Thông báo lỗi từ AI Service (nếu `success == false`). |

##### Cấu trúc `WasteSortingGuideDto`:
| Trường | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `categoryCode` | `string` | 1 trong 10 mã nhóm rác: `"battery"`, `"biological"`, `"cardboard"`, `"clothes"`, `"glass"`, `"metal"`, `"paper"`, `"plastic"`, `"shoes"`, `"trash"`. |
| `categoryName` | `string` | Tên tiếng Việt của nhóm rác (VD: `"Nhựa tái chế (Plastic)"`). |
| `standardBinColor` | `string` | Màu thùng rác tiêu chuẩn theo quy định phân loại tại nguồn. |
| `preparationSteps` | `string` | Các bước sơ chế trước khi bàn giao (VD: đổ hết nước, tráng sạch, bóp dẹp). |
| `handlingAction` | `string` | Hành động gợi ý trên EcoLink (VD: bán phế liệu tái chế hoặc xử lý theo quy định an toàn). |
| `hazardWarning` | `string?` | Cảnh báo an toàn lao động (VD: pin rò rỉ/cháy nổ, mảnh thủy tinh vỡ hoặc cạnh lon sắc nhọn). |

##### Ví dụ 1 – Response Nhận diện Thành công (`200 OK` – Độ tin cậy $\ge 60\%$):
```json
{
  "success": true,
  "messageCode": "MSG-CLS-01",
  "message": "Nhận diện thành công loại rác: Nhựa tái chế (Plastic) với độ tin cậy 96.4% (Mô hình: yolov8_detect).",
  "data": {
    "aiResult": {
      "success": true,
      "isOutOfDistribution": false,
      "confidenceThreshold": 0.6,
      "topPrediction": {
        "label": "plastic",
        "confidence": 0.964,
        "displayName": "Nhựa tái chế (Plastic)"
      },
      "predictions": [
        {
          "label": "plastic",
          "confidence": 0.964,
          "displayName": "Nhựa tái chế (Plastic)"
        },
        {
          "label": "glass",
          "confidence": 0.025,
          "displayName": "Thủy tinh (Glass)"
        },
        {
          "label": "metal",
          "confidence": 0.011,
          "displayName": "Kim loại / Vỏ lon (Metal)"
        }
      ],
      "inferenceTimeMs": 18.4,
      "errorMessage": null
    },
    "sortingGuide": {
      "categoryCode": "plastic",
      "categoryName": "Nhựa tái chế (Plastic)",
      "standardBinColor": "Thùng màu Vàng hoặc Trắng (Rác tái chế)",
      "preparationSteps": "1. Đổ hết chất lỏng còn thừa. 2. Tráng sơ qua bằng nước sạch. 3. Tháo rời nhãn mác (nếu có) và bóp dẹp chai nhựa để tiết kiệm diện tích.",
      "handlingAction": "Có thể gom lại để bán hoặc đổi Điểm Xanh tại các Điểm thu mua phế liệu trên bản đồ EcoLink.",
      "hazardWarning": "Không đốt nhựa tại nhà vì sinh ra khí Dioxin cực độc. Tránh tái sử dụng chai nhựa PET (số 1) nhiều lần để đựng nước nóng."
    },
    "message": "Nhận diện thành công loại rác: Nhựa tái chế (Plastic) với độ tin cậy 96.4% (Mô hình: yolov8_detect)."
  },
  "errors": null,
  "timestamp": "2026-10-30T09:10:00Z"
}
```

##### Ví dụ 2 – Response Cảnh báo Độ tin cậy thấp / OOD (`200 OK` – Độ tin cậy `< 60%`):
```json
{
  "success": true,
  "messageCode": "MSG-CLS-02",
  "message": "Hệ thống không nhận diện chắc chắn (> 60%) hoặc vật thể không thuộc nhóm rác hỗ trợ. Vui lòng chụp lại hình ảnh ở góc rõ hơn.",
  "data": {
    "aiResult": {
      "success": true,
      "isOutOfDistribution": true,
      "confidenceThreshold": 0.6,
      "topPrediction": {
        "label": "trash",
        "confidence": 0.412,
        "displayName": "Rác sinh hoạt khác (Trash)"
      },
      "predictions": [
        {
          "label": "trash",
          "confidence": 0.412,
          "displayName": "Rác sinh hoạt khác (Trash)"
        }
      ],
      "inferenceTimeMs": 15.2,
      "errorMessage": null
    },
    "sortingGuide": null,
    "message": "Hệ thống không nhận diện chắc chắn (> 60%) hoặc vật thể không thuộc nhóm rác hỗ trợ. Vui lòng chụp lại hình ảnh ở góc rõ hơn."
  },
  "errors": null,
  "timestamp": "2026-10-30T09:11:00Z"
}
```

#### c. Bảng Mã Thông báo Trả về của `POST /api/v1/waste-scan/classify`

| HTTP Status | Mã `messageCode` (Code) | Mã tương ứng trong SRS (`BR`/`MSG`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-CLS-01` | `MSG-CLS-03` (`BR-CLS-02`) | Phân loại thành công với độ tin cậy $\ge 60\%$ | `"Nhận diện thành công loại rác: {DisplayName} với độ tin cậy {Confidence}% (Mô hình: {model})."` |
| **`200 OK`** | `MSG-CLS-02` | `MSG-CLS-04` (`BR-CLS-03`) | Ảnh thuộc diện OOD hoặc độ tin cậy `< 60%` | `"Hệ thống không nhận diện chắc chắn (> 60%) hoặc vật thể không thuộc nhóm rác hỗ trợ. Vui lòng chụp lại hình ảnh ở góc rõ hơn."` |
| **`400 Bad Request`** | `MSG-CLS-04` | `BR-CLS-01` | Không đính kèm `file` hoặc `file.Length == 0` | `"Vui lòng tải lên file ảnh hợp lệ."` |
| **`400 Bad Request`** | `MSG-CLS-05` | `MSG-CLS-01` (`BR-CLS-01`) | Đuôi file không thuộc `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp` | `"Tập tin tải lên phải là hình ảnh hợp lệ (.jpg, .jpeg, .png, .webp)."` |
| **`400 Bad Request`** | `MSG-CLS-06` | — | Tham số `model` không nằm trong danh sách mô hình hỗ trợ | `"Mô hình '{model}' không tồn tại. Danh sách mô hình hỗ trợ: yolo26_detect, yolo26s_cls, yolo26n_cls, yolo26s, yolo26n, yolo26, yolov8_detect, mobilenet_v3, efficientnet_b0, yolov8n."` |
| **`400 Bad Request`** | `MSG-CLS-03` | `MSG-CLS-05` | AI Vision Service không phản hồi hoặc ảnh bị hỏng byte | `"Không thể phân tích hình ảnh từ AI Vision Service."` |

---

### 2.2. Lấy Danh sách Mô hình AI (`GET /api/v1/waste-scan/models`)

* **Mô tả:** Trả về danh sách 5 mô hình học sâu có sẵn kèm trạng thái sẵn sàng (`is_ready`) và danh sách 10 nhãn rác hỗ trợ (`battery`, `biological`, `cardboard`, `clothes`, `glass`, `metal`, `paper`, `plastic`, `shoes`, `trash`).

#### a. Request Parameters
Không có tham số đầu vào.

#### b. Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "messageCode": null,
  "message": null,
  "data": {
    "default_model": "yolo26_detect",
    "supported_classes": [
      "battery", "biological", "cardboard", "clothes", "glass",
      "metal", "paper", "plastic", "shoes", "trash"
    ],
    "classes_count": 10,
    "models": [
      {
        "id": "yolo26_detect",
        "name": "YOLO26 Waste Object Detection (End-to-End NMS-Free)",
        "family": "YOLO26 End-to-End NMS-Free (Two-Stage)",
        "is_ready": true
      },
      {
        "id": "yolo26s_cls",
        "name": "YOLO26 Small Classification (12-Class Fine-Tuned)",
        "family": "YOLO26-cls (Ultralytics)",
        "is_ready": true
      },
      {
        "id": "yolo26n_cls",
        "name": "YOLO26 Nano Classification",
        "family": "YOLO26-cls (Ultralytics)",
        "is_ready": true
      },
      {
        "id": "efficientnet_b0",
        "name": "EfficientNet-B0",
        "family": "CNN (Torchvision)",
        "is_ready": true
      },
      {
        "id": "mobilenet_v3",
        "name": "MobileNetV3-Small",
        "family": "CNN (Torchvision)",
        "is_ready": true
      }
    ]
  },
  "errors": null,
  "timestamp": "2026-11-23T09:12:00Z"
}
```

---

### 2.3. Tra cứu Toàn bộ Quy tắc Phân loại Tĩnh (`GET /api/v1/waste-scan/rules`)

* **Mô tả:** Trả về danh sách quy tắc phân loại của cả 10 nhóm rác trong Rule Engine (gồm 5 nhóm phế liệu tái chế thương mại: `plastic`, `paper`, `cardboard`, `metal`, `glass` và 5 nhóm hướng dẫn phân loại/an toàn không nhận thu mua qua sàn: `battery`, `biological`, `clothes`, `shoes`, `trash`) kèm màu thùng rác, các bước sơ chế, hành động gợi ý và cảnh báo an toàn (`BR-SRT-01`).

#### a. Request Parameters
* `[FromQuery] bool detailed = false` *(Admin/Staff có thể đặt `true` để nhận `SortingRuleDetailDto[]` gồm `ruleId`, `wasteCategoryId`, `status`)*
* `[FromQuery] bool includeDraft = false` *(Khi `detailed=true`, lấy cả quy tắc đang ở trạng thái `DRAFT`)*

#### b. Ví dụ Response Thành công (`200 OK` – bọc chuẩn `ApiResponse<T>`):
```json
{
  "success": true,
  "messageCode": "MSG-SRT-RULE-LIST-OK",
  "message": "Tải quy tắc phân loại rác thành công.",
  "data": [
    {
      "categoryCode": "plastic",
      "categoryName": "Nhựa tái chế (Plastic)",
      "standardBinColor": "Cam",
      "preparationSteps": "1. Tráng sạch cặn nước ngọt, dầu mỡ.\n2. Tháo nắp chai và gỡ màng co ni-lông.\n3. Bẹp chai để tiết kiệm thể tích lưu trữ.",
      "handlingAction": "Bỏ vào thùng rác màu Cam hoặc mang đến các điểm đối tác thu mua phế liệu.",
      "hazardWarning": "Không trộn lẫn các loại chai nhựa chứa hóa chất độc hại, thuốc trừ sâu."
    },
    {
      "categoryCode": "metal",
      "categoryName": "Kim loại & Vỏ lon (Metal)",
      "standardBinColor": "Xám Kim Loại",
      "preparationSteps": "1. Dốc sạch chất lỏng bên trong vỏ lon.\n2. Có thể giẫm bẹp vỏ lon bia/nước ngọt.\n3. Để riêng đồ nhôm, sắt thép vụn.",
      "handlingAction": "Mang đến cơ sở thu mua phế liệu có giá thu mua cao nhất trên EcoLink.",
      "hazardWarning": "Cẩn thận mép lon sắc nhọn có thể cứa đứt tay."
    }
  ]
}
```

---

### 2.4. Gửi Báo cáo AI Nhận diện Sai (`POST /api/v1/waste-scan/feedback`)

* **Mô tả:** Khi người dùng nhận thấy kết quả AI dự đoán chưa chính xác, người dùng gửi nhãn đúng (`userReportedLabel`) kèm ảnh gốc và ghi chú để đưa vào hàng đợi kiểm duyệt của Staff (`BR-CLS-05`, `MSG-CLS-06`).

#### a. Request Parameters (`application/json` – `SubmitFeedbackDto`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|
| `imageUrl` | Body | `string` | Không | Đường dẫn ảnh rác đã quét. |
| `predictedLabel` | Body | `string` | **Có** | Mã nhãn rác do AI dự đoán ban đầu (VD: `"plastic"`). |
| `confidenceScore` | Body | `double` | **Có** | Độ tin cậy của dự đoán ban đầu (từ `0.0` đến `1.0`). |
| `userReportedLabel` | Body | `string` | **Có** | Mã nhãn rác thực tế do người dùng đề xuất (VD: `"glass"`). |
| `note` | Body | `string?` | Không | Ghi chú mô tả thêm của người dùng (VD: `"Chai thủy tinh trong suốt bị nhận nhầm là chai nhựa"`). |

##### Ví dụ Request Body:
```json
{
  "imageUrl": "https://pub-f15071b71f995ac69532cf5541f31737.r2.dev/scans/sample_bottle.jpg",
  "predictedLabel": "plastic",
  "confidenceScore": 0.64,
  "userReportedLabel": "glass",
  "note": "Chai thủy tinh đựng nước suối trong suốt"
}
```

#### b. Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "message": "Cảm ơn bạn đã phản hồi! Dữ liệu đã được chuyển tới Admin để kiểm chứng và cải thiện mô hình AI."
}
```

---

## 3. CHI TIẾT CÁC API CHUYÊN SÂU TẠI AI VISION MICROSERVICE (`PORT 8000`)

---

### 3.1. Khoanh vùng Vật thể Rác bằng Object Detection (`POST /api/v1/detect`)

* **Mô tả:** Sử dụng mô hình **YOLO26 Object Detection (`yolo26_detect`)** để tự động định vị tọa độ khung chữ nhật (`bbox: [x1, y1, x2, y2]`) của từng món rác trong một bức ảnh có nhiều vật thể, chấm điểm ưu tiên tâm khung ngắm (`center_score` / `is_primary`), thống kê số lượng từng loại theo 10 danh mục chuẩn và trả về ảnh đã vẽ khung Bounding Box dưới dạng chuỗi `Base64 Data URL`.

#### a. Request Parameters (`multipart/form-data`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---:|:---|
| `file` | Form-Data | `UploadFile` | **Có** | — | Tệp ảnh chứa một hoặc nhiều món rác (`.jpg`, `.png`, `.webp`). |
| `confidence_threshold` | Form-Data | `float` | Không | `0.20` | Ngưỡng độ tự tin tối thiểu để vẽ khung phát hiện vật thể (`0.0 - 1.0`). |

#### b. Response Parameters (`WasteDetectionResponse`)

| Trường | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `success` | `boolean` | Trạng thái thực thi (`true`). |
| `total_items` | `integer` | Tổng số món rác được phát hiện trong bức ảnh. |
| `category_summary` | `Record<string, int>` | Thống kê số lượng món rác theo từng nhóm (VD: `{"plastic": 3, "metal": 1}`). |
| `detected_objects` | `DetectedItem[]` | Mảng chi tiết từng vật thể (`raw_class`, `category`, `display_name`, `confidence`, `bbox: [x1, y1, x2, y2]`). |
| `annotated_image_base64` | `string?` | Chuỗi ảnh `data:image/jpeg;base64,...` đã vẽ khung Bounding Box và nhãn để hiển thị ngay trên UI. |
| `inference_time_ms` | `float` | Thời gian suy luận (mili-giây). |

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "total_items": 2,
  "category_summary": {
    "plastic": 1,
    "metal": 1
  },
  "detected_objects": [
    {
      "raw_class": "plastic_bottle",
      "category": "plastic",
      "display_name": "Nhựa tái chế (Plastic)",
      "confidence": 0.942,
      "bbox": [120.5, 85.0, 340.2, 510.8]
    },
    {
      "raw_class": "can",
      "category": "metal",
      "display_name": "Kim loại (Metal)",
      "confidence": 0.915,
      "bbox": [380.0, 210.4, 520.6, 505.0]
    }
  ],
  "annotated_image_base64": "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAAAQABAAD...",
  "inference_time_ms": 22.8
}
```

---

### 3.2. Phân loại Rác theo Lô Nhiều Ảnh (`POST /api/v1/classify/batch`)

* **Mô tả:** Nhận danh sách nhiều file ảnh trong một lần gọi để đánh giá độ chính xác và tốc độ suy luận trung bình (`avg_inference_time_ms`).

#### a. Request Parameters (`multipart/form-data`)

| Tên tham số | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---|
| `files` | `List<UploadFile>` | **Có** | — | Danh sách các tệp ảnh cần phân loại. |
| `model` | `string` | Không | `"yolo26n_cls"` | Mã mô hình AI sử dụng. |
| `confidence_threshold` | `float?` | Không | `0.60` | Ngưỡng tin cậy OOD. |

#### b. Ví dụ Response (`200 OK` – `BatchScanResponse`):
```json
{
  "success": true,
  "total_files": 2,
  "model_used": "yolo26n_cls",
  "total_inference_time_ms": 24.6,
  "avg_inference_time_ms": 12.3,
  "results": [
    {
      "filename": "plastic_101.jpg",
      "success": true,
      "is_out_of_distribution": false,
      "top_prediction": {
        "label": "plastic",
        "confidence": 0.978,
        "display_name": "Nhựa tái chế (Plastic)"
      },
      "inference_time_ms": 12.1
    },
    {
      "filename": "cardboard_205.jpg",
      "success": true,
      "is_out_of_distribution": false,
      "top_prediction": {
        "label": "cardboard",
        "confidence": 0.934,
        "display_name": "Bìa Carton (Cardboard)"
      },
      "inference_time_ms": 12.5
    }
  ]
}
```

---

### 3.3. Cập nhật Cấu hình Ngưỡng Tin cậy & Quy tắc Phân loại (`PUT /api/v1/rules`)

* **Mô tả:** Cho phép Admin điều chỉnh ngưỡng tin cậy `confidence_threshold` (`BR-CLS-03`) hoặc cập nhật tên màu thùng rác và danh sách hướng dẫn xử lý cho từng danh mục (`BR-SRT-02`).

#### a. Request Parameters (`application/json` – `RuleUpdateRequest`)

| Tên tham số | Kiểu dữ liệu | Bắt buộc | Mô tả |
|:---|:---:|:---:|:---|
| `category` | `string?` | Không | Mã danh mục rác cần sửa (VD: `"plastic"`, `"battery"`). |
| `bin_color_name` | `string?` | Không | Tên màu thùng rác mới. |
| `instructions` | `string[]?` | Không | Danh sách các bước hướng dẫn sơ chế mới. |
| `confidence_threshold` | `float?` | Không | Ngưỡng OOD mới (VD: `0.65`). |
| `title` | `string?` | Không | Tiêu đề quy tắc phân loại (đồng bộ từ Core Backend `PATCH .../publish`). |
| `handling_action` | `string?` | Không | Hành động gợi ý xử lý / tái chế. |
| `hazard_warning` | `string?` | Không | Cảnh báo an toàn lao động hoặc xử lý chất thải nguy hại. |

#### b. Ví dụ Response (`200 OK`):
```json
{
  "success": true,
  "message": "Cập nhật quy tắc phân loại thành công",
  "confidence_threshold": 0.65
}
```
*(Ghi chú: Từ phiên bản `v2.6.0`, endpoint `PUT /api/v1/rules` hỗ trợ tự động thêm mới (Upsert) khóa danh mục nếu mã danh mục mới được phát hành từ Core Backend `PATCH /api/v1/waste-scan/rules/{ruleId}/publish`).*

---

## 4. LỊCH SỬ QUÉT AI CÁ NHÂN, QUẢN TRỊ DANH MỤC & RULE ENGINE (CORE BACKEND – `STT 025–031`)

### 4.1. Lịch sử Quét AI Cá nhân (`GET /api/v1/waste-scan/history` – `STT 026`, `U-01`, `U-03`, `BR-CLS-08`)
* **Phân quyền:** `[Authorize]` (mọi tài khoản đã đăng nhập; cô lập dữ liệu nghiêm ngặt theo `UserId` từ JWT).
* **Query Parameters:** `categoryCode?`, `fromDate?`, `toDate?`, `pageNumber` (mặc định `1`), `pageSize` (mặc định `20`, tối đa `50`).
* **Response (`200 OK`):** `ApiResponse<PagedResult<MyWasteScanHistoryItemDto>>` gồm `scanId`, `imageUrl`, `predictedLabel`, `confidenceScore`, `pipelineUsed`, `sortingGuide` (được hydrate từ `IWasteRuleEngineService`), `scannedAt`.

### 4.2. Quản trị Danh mục Phế liệu Chuẩn (`WasteCategoryController` – `STT 025, 027–029`, `A-05`)
* **`GET /api/v1/waste-categories` (`[AllowAnonymous]` – `STT 025`):** Công khai danh mục đang `Active` & có thể tái chế; `Admin`/`Staff` có thể truyền `?includeInactive=true&includeNonRecyclable=true`.
* **`POST /api/v1/waste-categories` (`[Authorize(Roles = "Admin")]` – `STT 027`):** Thêm mới `MasterWasteCategory` (`code` theo regex `^[a-z0-9_]+$`, kiểm tra duy nhất trả `409 Conflict` / `MSG-SRT-02` nếu trùng, ghi `AuditLog`). Trả về `201 Created`.
* **`PUT /api/v1/waste-categories/{categoryId:guid}` (`[Authorize(Roles = "Admin")]` – `STT 028`):** Cập nhật `name`, `description`, `standardBinColor`, `iconUrl`, `isRecyclable` (`code` bất biến theo `BR-SRT-02`), tự động xóa cache Rule Engine & ghi `AuditLog`.
* **`PATCH /api/v1/waste-categories/{categoryId:guid}/status` (`[Authorize(Roles = "Admin")]` – `STT 029`):** Chuyển đổi trạng thái `Active` ↔ `Inactive` (xóa mềm theo `BR-SRT-04`, bảo toàn lịch sử giao dịch/biên nhận cũ, đồng bộ xóa cache Rule Engine).

### 4.3. Cấu hình & Phát hành Rule Engine (`WasteScanController` – `STT 030–031`, `A-06`)
* **`PUT /api/v1/waste-scan/rules/{categoryId:guid}` (`[Authorize(Roles = "Admin")]` – `STT 030`):** Tạo mới hoặc cập nhật quy tắc phân loại `1-1` gắn với `categoryId` (`BR-SRT-01`) gồm `title`, `preparationSteps`, `sortingInstruction`, `handlingAction`, `hazardWarning`, `applicableRegion`. Phiên bản mới chỉnh sửa mang trạng thái `DRAFT` cho tới khi được phát hành.
* **`PATCH /api/v1/waste-scan/rules/{ruleId:guid}/publish` (`[Authorize(Roles = "Admin")]` – `STT 031`):** Phát hành chính thức quy tắc `DRAFT -> ACTIVE` (`BR-SRT-03`), cập nhật ngay bộ nhớ đệm `IWasteRuleEngineService`, ghi `AuditLog`, và gọi đồng bộ `PUT /api/v1/rules` sang Python AI Service (cô lập lỗi nếu AI Service đang offline).


