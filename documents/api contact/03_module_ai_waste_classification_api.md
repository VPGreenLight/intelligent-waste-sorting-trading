# TÀI LIỆU ĐẶC TẢ API – MODULE 3: PHÂN LOẠI RÁC AI & HƯỚNG DẪN XỬ LÝ RULE ENGINE (AI WASTE CLASSIFICATION & SORTING GUIDANCE)
**Mã phân hệ:** `CLS` (Phân loại AI & Dữ liệu mô hình) & `SRT` (Hướng dẫn phân loại & Danh mục rác)  
**Core Backend Controller:** [`WasteScanController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/WasteScanController.cs) (`/api/v1/waste-scan`)  
**Core Backend Services:** [`AiVisionHttpClient.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AiVisionHttpClient.cs), [`WasteRuleEngineService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/WasteRuleEngineService.cs)  
**AI Vision Microservice (FastAPI):** [`app.py`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.AiService/app.py) (`http://127.0.0.1:8000/api/v1/...`)  
**DTOs:** [`WasteScanDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/WasteScanDtos.cs)

---

## 1. TỔNG QUAN KIẾN TRÚC & DANH SÁCH ENDPOINTS

Phân hệ Phân loại rác thông minh của EcoLink bao gồm 2 tầng API phối hợp chặt chẽ:
1. **Tầng Core Business Web API (`.NET 10` – Cổng `5000`):** Tiếp nhận yêu cầu từ Mobile/Web App, kiểm tra tính hợp lệ của tệp ảnh và mô hình, gọi sang AI Microservice, kiểm tra ngưỡng an toàn **Out-of-Distribution (OOD = 60%)** và ánh xạ nhãn rác sang quy tắc xử lý trong **Rule Engine**.
2. **Tầng Dedicated AI Vision Microservice (`Python FastAPI` – Cổng `8000`):** Thực thi suy luận học sâu đa mô hình (`yolov8_detect`, `yolov8n`, `mobilenet_v3`, `efficientnet_b0`), khoanh vùng Bounding Box, phân loại theo lô (Batch) và thu thập dữ liệu phản hồi để huấn luyện lại (Retrain Loop).

### 1.1. Danh sách Endpoints tại Core Business Web API (`/api/v1/waste-scan`)

| STT | Phương thức | Endpoint URI | Quyền truy cập | Chức năng nghiệp vụ | Mã thông báo chính |
|:---:|:---:|:---|:---:|:---|:---|
| 1 | `POST` | `/api/v1/waste-scan/classify` | Public / `Citizen` | Nhận diện rác thải qua ảnh, kiểm tra ngưỡng tin cậy OOD (`60%`) và trả về hướng dẫn xử lý từ Rule Engine. | `MSG-CLS-01`, `MSG-CLS-02`, `MSG-CLS-03`, `MSG-CLS-04`, `MSG-CLS-05`, `MSG-CLS-06` |
| 2 | `GET` | `/api/v1/waste-scan/models` | Public | Lấy danh sách các mô hình AI đang sẵn sàng phục vụ suy luận. | `200 OK` |
| 3 | `GET` | `/api/v1/waste-scan/rules` | Public | Tra cứu toàn bộ danh mục quy tắc phân loại rác tĩnh, màu thùng rác và các bước xử lý (`BR-SRT-01`). | `200 OK` |
| 4 | `POST` | `/api/v1/waste-scan/feedback` | Public / `Citizen` | Người dùng gửi báo cáo phản hồi khi AI nhận diện sai nhãn rác (`BR-CLS-05`). | `200 OK` (`MSG-CLS-06`) |

### 1.2. Danh sách Endpoints tại Dedicated AI Vision Microservice (`FastAPI - Port 8000`)

| STT | Phương thức | Endpoint URI | Chức năng chuyên sâu |
|:---:|:---:|:---|:---|
| 5 | `POST` | `/api/v1/classify` | Suy luận phân loại ảnh đơn (hỗ trợ Two-Stage Object Detection Crop + Classifier). |
| 6 | `POST` | `/api/v1/detect` | Khoanh vùng Bounding Box từng món rác bằng YOLOv8 Object Detection, đếm số lượng và trả về ảnh Base64 đã vẽ khung. |
| 7 | `POST` | `/api/v1/classify/batch` | Phân loại hàng loạt nhiều file ảnh cùng lúc để kiểm thử hiệu năng (Benchmarking). |
| 8 | `GET` | `/api/v1/models` & `/api/v1/models/{model_id}` | Xem thông số kỹ thuật, kiến trúc mạng và độ chính xác Validation của từng mô hình. |
| 9 | `GET` / `PUT` | `/api/v1/rules` | Đọc hoặc cập nhật ngưỡng `confidence_threshold` và hướng dẫn xử lý trong Knowledge Base. |
| 10 | `GET` / `POST` | `/api/v1/feedback` | Lưu trữ và truy xuất nhật ký báo sai phục vụ vòng lặp huấn luyện lại (Retrain Loop). |
| 11 | `GET` | `/api/v1/stats` | Thống kê tổng lượt quét, tỷ lệ OOD (`ood_rate_percent`), độ trễ trung bình (`avg_latency_ms`) và 25 lượt quét gần nhất. |

---

## 2. CHI TIẾT CÁC API TẠI CORE BACKEND (`/api/v1/waste-scan`)

---

### 2.1. Nhận diện rác qua ảnh & Tra cứu Hướng dẫn Phân loại (`POST /api/v1/waste-scan/classify`)

* **Mô tả:** Endpoint trung tâm của chức năng Quét rác AI:
  1. Kiểm tra file ảnh đính kèm và định dạng đuôi file (`.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp` – `BR-CLS-01`).
  2. Kiểm tra mã mô hình yêu cầu (`yolov8_detect`, `mobilenet_v3`, `efficientnet_b0`, `yolov8n`).
  3. Gửi luồng ảnh (`Stream`) sang AI Vision Service để suy luận.
  4. Kiểm tra cờ `isOutOfDistribution` hoặc `topPrediction.confidence < confidenceThreshold` (`0.60`):
     * Nếu độ tin cậy `< 60%` (`BR-CLS-03`): Trả về cảnh báo không chắc chắn (`MSG-CLS-02`) và không đính kèm hướng dẫn cứng nhắc để yêu cầu người dùng chụp lại hoặc chọn thủ công.
     * Nếu độ tin cậy $\ge 60\%$ (`BR-CLS-02`, `BR-SRT-01`): Tự động tra cứu `WasteRuleEngineService` theo `categoryCode` (`plastic`, `paper`, `cardboard`, `metal`, `glass`, `trash`) để trả về màu thùng rác tiêu chuẩn, các bước sơ chế làm sạch và hành động gợi ý.

#### a. Request Parameters (`multipart/form-data`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Giá trị hợp lệ / Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `file` | Form-Data | `IFormFile` | **Có** | `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp` | Tệp hình ảnh vật phẩm rác cần nhận diện. |
| `model` | Form-Data | `string?` | Không | Mặc định: `"yolov8_detect"`<br>Hỗ trợ: `"yolov8_detect"`, `"mobilenet_v3"`, `"efficientnet_b0"`, `"yolov8n"` | Mã mô hình AI muốn sử dụng để phân tích. |

#### b. Response Parameters (`ApiResponse<WasteScanResponseDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `aiResult` | `AiScanResultDto` | Kết quả suy luận chi tiết từ mô hình AI (xem bảng dưới). |
| `sortingGuide` | `WasteSortingGuideDto?` | Hướng dẫn phân loại và sơ chế từ Rule Engine (`null` nếu rơi vào ngưỡng OOD `< 60%`). |
| `message` | `string` | Thông báo tổng hợp kết quả nhận diện kèm phần trăm độ tin cậy. |

##### Cấu trúc `AiScanResultDto`:
| Trường | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `success` | `boolean` | `true` nếu AI suy luận thành công. |
| `isOutOfDistribution` | `boolean` | `true` nếu vật thể lạ hoặc độ tin cậy thấp hơn ngưỡng `confidenceThreshold` (`0.60`). |
| `confidenceThreshold` | `double` | Ngưỡng độ tin cậy tối thiểu áp dụng (mặc định `0.60`). |
| `topPrediction` | `WastePredictionDto?` | Nhãn dự đoán có xác suất cao nhất (`label`, `confidence`, `displayName`). |
| `predictions` | `WastePredictionDto[]` | Danh sách Top-3 nhãn dự đoán có điểm tin cậy cao nhất. |
| `inferenceTimeMs` | `double` | Thời gian suy luận của mô hình AI tính bằng mili-giây (`ms`). |
| `errorMessage` | `string?` | Thông báo lỗi từ AI Service (nếu `success == false`). |

##### Cấu trúc `WasteSortingGuideDto`:
| Trường | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `categoryCode` | `string` | Mã nhóm rác: `"plastic"`, `"paper"`, `"cardboard"`, `"metal"`, `"glass"`, `"trash"`. |
| `categoryName` | `string` | Tên tiếng Việt của nhóm rác (VD: `"Nhựa tái chế (Plastic)"`). |
| `standardBinColor` | `string` | Màu thùng rác tiêu chuẩn theo quy định phân loại tại nguồn. |
| `preparationSteps` | `string` | Các bước sơ chế trước khi bàn giao (VD: đổ hết nước, tráng sạch, bóp dẹp). |
| `handlingAction` | `string` | Hành động gợi ý trên EcoLink (VD: kết nối điểm thu mua để bán và tích Điểm Xanh). |
| `hazardWarning` | `string?` | Cảnh báo an toàn lao động (VD: bọc kín mảnh thủy tinh vỡ hoặc cạnh lon sắc nhọn). |

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
| **`400 Bad Request`** | `MSG-CLS-06` | — | Tham số `model` không nằm trong 4 mô hình hỗ trợ | `"Mô hình '{model}' không tồn tại. Danh sách mô hình hỗ trợ: yolov8_detect, mobilenet_v3, efficientnet_b0, yolov8n."` |
| **`400 Bad Request`** | `MSG-CLS-03` | `MSG-CLS-05` | AI Vision Service không phản hồi hoặc ảnh bị hỏng byte | `"Không thể phân tích hình ảnh từ AI Vision Service."` |

---

### 2.2. Lấy Danh sách Mô hình AI (`GET /api/v1/waste-scan/models`)

* **Mô tả:** Trả về danh sách các mô hình học sâu có sẵn kèm trạng thái sẵn sàng (`is_ready`) và danh sách 6 nhãn rác hỗ trợ (`cardboard`, `glass`, `metal`, `paper`, `plastic`, `trash`).

#### a. Request Parameters
Không có tham số đầu vào.

#### b. Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "messageCode": null,
  "message": null,
  "data": {
    "default_model": "yolov8_detect",
    "supported_classes": ["cardboard", "glass", "metal", "paper", "plastic", "trash"],
    "classes_count": 6,
    "models": [
      {
        "id": "yolov8_detect",
        "name": "YOLOv8 Detection (Khuyên dùng - Định vị & Cắt nền)",
        "family": "Object Detection (Ultralytics)",
        "is_ready": true
      },
      {
        "id": "mobilenet_v3",
        "name": "MobileNetV3-Small (Siêu nhẹ, tối ưu di động)",
        "family": "CNN (Torchvision)",
        "is_ready": true
      },
      {
        "id": "efficientnet_b0",
        "name": "EfficientNet-B0 (Độ chính xác cao)",
        "family": "CNN (Torchvision)",
        "is_ready": true
      },
      {
        "id": "yolov8n",
        "name": "YOLOv8 Nano Classifier",
        "family": "CNN (Ultralytics)",
        "is_ready": true
      }
    ]
  },
  "errors": null,
  "timestamp": "2026-10-30T09:12:00Z"
}
```

---

### 2.3. Tra cứu Toàn bộ Quy tắc Phân loại Tĩnh (`GET /api/v1/waste-scan/rules`)

* **Mô tả:** Trả về danh sách toàn bộ 6 nhóm danh mục rác trong Rule Engine (`plastic`, `paper`, `cardboard`, `metal`, `glass`, `trash`) kèm màu thùng rác, các bước sơ chế, hành động gợi ý và cảnh báo an toàn (`BR-SRT-01`).

#### a. Request Parameters
Không có tham số đầu vào.

#### b. Ví dụ Response Thành công (`200 OK` – `IEnumerable<WasteSortingGuideDto>`):
```json
[
  {
    "categoryCode": "plastic",
    "categoryName": "Nhựa tái chế (Plastic)",
    "standardBinColor": "Thùng màu Vàng hoặc Trắng (Rác tái chế)",
    "preparationSteps": "1. Đổ hết chất lỏng còn thừa. 2. Tráng sơ qua bằng nước sạch. 3. Tháo rời nhãn mác (nếu có) và bóp dẹp chai nhựa để tiết kiệm diện tích.",
    "handlingAction": "Có thể gom lại để bán hoặc đổi Điểm Xanh tại các Điểm thu mua phế liệu trên bản đồ EcoLink.",
    "hazardWarning": "Không đốt nhựa tại nhà vì sinh ra khí Dioxin cực độc. Tránh tái sử dụng chai nhựa PET (số 1) nhiều lần để đựng nước nóng."
  },
  {
    "categoryCode": "metal",
    "categoryName": "Kim loại / Vỏ lon (Metal)",
    "standardBinColor": "Thùng màu Vàng hoặc Trắng (Rác tái chế)",
    "preparationSteps": "1. Đổ hết nước ngọt/bia thừa trong lon. 2. Tráng sạch để tránh thu hút côn trùng. 3. Có thể đạp dẹp vỏ lon nhôm.",
    "handlingAction": "Nhôm và kim loại có giá trị thu mua rất cao! Hãy tích trữ và đặt lịch thu gom trên EcoLink để nhận tiền mặt và Điểm Xanh.",
    "hazardWarning": "Cẩn thận với nắp hộp thiếc hoặc cạnh vỏ lon bị cắt sắc nhọn gây đứt tay."
  }
]
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

* **Mô tả:** Sử dụng mô hình YOLOv8 Object Detection để tự động định vị tọa độ khung chữ nhật (`bbox: [x1, y1, x2, y2]`) của từng món rác trong một bức ảnh có nhiều vật thể, loại bỏ hoàn toàn phông nền gây nhiễu, thống kê số lượng từng loại và trả về ảnh đã vẽ khung Bounding Box dưới dạng chuỗi `Base64 Data URL`.

#### a. Request Parameters (`multipart/form-data`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---:|:---|
| `file` | Form-Data | `UploadFile` | **Có** | — | Tệp ảnh chứa một hoặc nhiều món rác (`.jpg`, `.png`, `.webp`). |
| `confidence_threshold` | Form-Data | `float` | Không | `0.25` | Ngưỡng độ tự tin tối thiểu để vẽ khung phát hiện vật thể (`0.0 - 1.0`). |

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
| `model` | `string` | Không | `"yolov8n"` | Mã mô hình AI sử dụng. |
| `confidence_threshold` | `float?` | Không | `0.60` | Ngưỡng tin cậy OOD. |

#### b. Ví dụ Response (`200 OK` – `BatchScanResponse`):
```json
{
  "success": true,
  "total_files": 2,
  "model_used": "yolov8n",
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
| `category` | `string?` | Không | Mã danh mục rác cần sửa (VD: `"plastic"`, `"paper"`). |
| `bin_color_name` | `string?` | Không | Tên màu thùng rác mới. |
| `instructions` | `string[]?` | Không | Danh sách các bước hướng dẫn sơ chế mới. |
| `confidence_threshold` | `float?` | Không | Ngưỡng OOD mới (VD: `0.65`). |

#### b. Ví dụ Response (`200 OK`):
```json
{
  "success": true,
  "message": "Cập nhật quy tắc phân loại thành công",
  "confidence_threshold": 0.65
}
```

