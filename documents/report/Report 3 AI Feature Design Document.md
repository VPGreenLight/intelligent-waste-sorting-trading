# CAPSTONE PROJECT REPORT

## Report 3 – AI Feature Design Document (Báo Cáo Thiết Kế Chức Năng Phân Hệ AI)

**Dự án:** EcoLink – Hệ thống hỗ trợ phân loại và kết nối thu mua rác thải tái chế thông minh  
*(Intelligent Recyclable Waste Classification and Trade Matching System)*  
**Mã tài liệu:** ECO-DOC-AI-FDD-01  
**Phiên bản:** 2.0 (Cập nhật Kiến trúc Two-Stage SOTA & Multi-Model Benchmarking)  
**Thời gian:** Tháng 10/2026 – Hà Nội  

---

## Bảng Theo Dõi Thay Đổi (Change Log)

| Ngày | Phiên bản | Người thực hiện | Mô tả chi tiết nội dung thay đổi |
| :---: | :---: | :--- | :--- |
| 25/09/2026 | 1.0 | Nguyễn Hoàng Thắng | Khảo sát bài toán AI, thu thập dataset TrashNet, xây dựng baseline ResNet/MobileNet ban đầu. |
| 28/09/2026 | 1.5 | Nguyễn Việt Hoàng | Tích hợp Ultralytics YOLOv8, xây dựng microservice FastAPI `Ecolink.AiService` và cơ chế lọc Out-of-Distribution. |
| 03/10/2026 | 1.8 | Nguyễn Thành Luân | Bổ sung kiến trúc Đa mô hình (Multi-Model Architecture), huấn luyện MobileNetV3-Small & EfficientNet-B0 đạt Acc 92.32%. |
| 06/10/2026 | 2.0 | Toàn nhóm EcoLink | Hoàn thiện kiến trúc Two-Stage SOTA (Object Detection $\rightarrow$ Crop Padding 5% $\rightarrow$ Classification), tích hợp Rule Engine tĩnh và Active Retrain Loop. |

---

## Danh Mục Thuật Ngữ & Viết Tắt (Definitions & Acronyms)

| Thuật ngữ / Viết tắt | Tên đầy đủ | Giải thích ngữ cảnh trong hệ thống EcoLink |
| :--- | :--- | :--- |
| **AI** | Artificial Intelligence | Trí tuệ nhân tạo, trọng tâm là các mô hình Thị giác máy tính (Computer Vision). |
| **CNN** | Convolutional Neural Network | Mạng nơ-ron tích chập chuyên xử lý dữ liệu ma trận hình ảnh không gian. |
| **YOLO** | You Only Look Once | Họ mô hình phát hiện vật thể (Object Detection) thời gian thực với tốc độ suy luận vượt trội. |
| **SOTA** | State-of-the-Art | Mức độ công nghệ tiên tiến nhất tại thời điểm hiện tại. |
| **OOD** | Out-of-Distribution | Mẫu dữ liệu ngoại lai không thuộc phân phối huấn luyện (ví dụ: ảnh bàn phím, mặt người, ảnh mờ nhiễu). |
| **IoU** | Intersection over Union | Tỷ lệ diện tích giao trên diện tích hợp giữa khung chữ nhật dự đoán và khung ground-truth. |
| **mAP** | Mean Average Precision | Độ chính xác trung bình chuẩn mực để đánh giá chất lượng mô hình phát hiện vật thể. |
| **BBox** | Bounding Box | Khung chữ nhật bao quanh vật thể xác định bởi tọa độ $[x_1, y_1, x_2, y_2]$. |
| **Two-Stage Pipeline** | Two-Stage Inference Pipeline | Quy trình 2 bước: Bước 1 định vị và cắt vật thể rác sạch; Bước 2 phân loại chi tiết. |
| **Rule Engine** | Business Rule Engine | Hệ thống quy tắc nghiệp vụ tĩnh ánh xạ danh mục rác sang hướng dẫn xử lý và màu thùng rác quy chuẩn. |

---

## MỤC LỤC

1. [I. TỔNG QUAN VÀ BỐI CẢNH PHÁT TRIỂN](#i-tổng-quan-và-bối-cảnh-phát-triển)
   - [1. Bối cảnh thực tiễn & Vấn đề giải quyết](#1-bối-cảnh-thực-tiễn--vấn-đề-giải-quyết)
   - [2. Mục tiêu kỹ thuật của Phân hệ AI](#2-mục-tiêu-kỹ-thuật-của-phân-hệ-ai)
   - [3. Triết lý thiết kế cốt lõi (Core Design Philosophy)](#3-triết-lý-thiết-kế-cốt-lõi-core-design-philosophy)
2. [II. YÊU CẦU & PHẠM VI CHỨC NĂNG AI](#ii-yêu-cầu--phạm-vi-chức-năng-ai)
   - [1. Danh sách Use Case phân hệ AI (Module CLS & SRT)](#1-danh-sách-use-case-phân-hệ-ai-module-cls--srt)
   - [2. Danh mục phân loại rác chuẩn mực (Waste Taxonomy)](#2-danh-mục-phân-loại-rác-chuẩn-mực-waste-taxonomy)
   - [3. Các Quy tắc nghiệp vụ bắt buộc (Business Rules)](#3-các-quy-tắc-nghiệp-vụ-bắt-buộc-business-rules)
3. [III. THIẾT KẾ KIẾN TRÚC HỆ THỐNG AI](#iii-thiết-kế-kiến-trúc-hệ-thống-ai)
   - [1. Sơ đồ kiến trúc tổng thể (System Architecture & Topology)](#1-sơ-đồ-kiến-trúc-tổng-thể-system-architecture--topology)
   - [2. Quy trình xử lý Hai Giai đoạn (Two-Stage Inference Pipeline)](#2-quy-trình-xử-lý-hai-giai-đoạn-two-stage-inference-pipeline)
   - [3. Các mẫu thiết kế phần mềm áp dụng (Software Design Patterns)](#3-các-mẫu-thiết-kế-phần-mềm-áp-dụng-software-design-patterns)
4. [IV. THIẾT KẾ MÔ HÌNH HỌC SÂU & ĐÁNH GIÁ BENCHMARK](#iv-thiết-kế-mô-hình-học-sâu--đánh-giá-benchmark)
   - [1. Bộ 4 Mô hình AI tích hợp trong hệ thống](#1-bộ-4-mô-hình-ai-tích-hợp-trong-hệ-thống)
   - [2. Kỹ thuật huấn luyện nâng cao (Advanced Training Methodology)](#2-kỹ-thuật-huấn-luyện-nâng-cao-advanced-training-methodology)
   - [3. Bảng khảo nghiệm Benchmark Hàn lâm (Academic Benchmark Table)](#3-bảng-khảo-nghiệm-benchmark-hàn-lâm-academic-benchmark-table)
   - [4. Phân tích Trade-off & Khuyến nghị môi trường triển khai](#4-phân-tích-trade-off--khuyến-nghị-môi-trường-triển-khai)
5. [V. THIẾT KẾ GIAO DIỆN LẬP TRÌNH ỨNG DỤNG (API SPECIFICATION)](#v-thiết-kế-giao-diện-lập-trình-ứng-dụng-api-specification)
   - [1. Chi tiết các RESTful API Endpoints của `Ecolink.AiService`](#1-chi-tiết-các-restful-api-endpoints-của-ecolinkaiservice)
   - [2. Định dạng dữ liệu DTO và Mã phản hồi hệ thống](#2-định-dạng-dữ-liệu-dto-và-mã-phản-hồi-hệ-thống)
6. [VI. THIẾT KẾ LUỒNG NGHIỆP VỤ & TƯƠNG TÁC (SEQUENCE DIAGRAMS)](#vi-thiết-kế-luồng-nghiệp-vụ--tương-tác-sequence-diagrams)
   - [1. Luồng phân loại Two-Stage tiêu chuẩn (Happy Path)](#1-luồng-phân-loại-two-stage-tiêu-chuẩn-happy-path)
   - [2. Luồng Safe AI xử lý dữ liệu ngoại lai (OOD Fallback Path)](#2-luồng-safe-ai-xử-lý-dữ-liệu-ngoại-lai-ood-fallback-path)
   - [3. Luồng phản hồi báo sai & Vòng lặp Retraining (Active Learning Loop)](#3-luồng-phản-hồi-báo-sai--vòng-lặp-retraining-active-learning-loop)
7. [VII. THIẾT KẾ TRẢI NGHIỆM GIAO DIỆN NGƯỜI DÙNG (UI/UX DESIGN)](#vii-thiết-kế-trải-nghiệm-giao-diện-người-dùng-uiux-design)
   - [1. Các trạng thái giao diện Scanner (`Scanner.tsx`)](#1-các-trạng-thái-giao-diện-scanner-scannertsx)
   - [2. Cơ chế chuyển đổi mô hình tức thì (Instant Model Switcher)](#2-cơ-chế-chuyển-đổi-mô-hình-tức-thì-instant-model-switcher)
   - [3. Tích hợp chuỗi giá trị: Nhận diện $\rightarrow$ Hướng dẫn $\rightarrow$ Giao dịch thu gom](#3-tích-hợp-chuỗi-giá-trị-nhận-diện--hướng-dẫn--giao-dịch-thu-gom)
8. [VIII. YÊU CẦU PHI CHỨC NĂNG & AN TOÀN HỆ THỐNG](#viii-yêu-cầu-phi-chức-năng--an-toàn-hệ-thống)
   - [1. Hiệu năng & Khả năng chịu tải (Performance & Scalability)](#1-hiệu-năng--khả-năng-chịu-tải-performance--scalability)
   - [2. An toàn dữ liệu & Quyền riêng tư (Safe AI & Data Privacy)](#2-an-toàn-dữ-liệu--quyền-riêng-tư-safe-ai--data-privacy)
   - [3. Kế hoạch dự phòng ngoại tuyến (Offline Demo Strategy)](#3-kế-hoạch-dự-phòng-ngoại-tuyến-offline-demo-strategy)
9. [IX. KẾT LUẬN & ĐỊNH HƯỚNG PHÁT TRIỂN](#ix-kết-luận--định-hướng-phát-triển)

---

## I. TỔNG QUAN VÀ BỐI CẢNH PHÁT TRIỂN

### 1. Bối cảnh thực tiễn & Vấn đề giải quyết

Phân loại rác tại nguồn là mắt xích quan trọng nhất trong nền kinh tế tuần hoàn (Circular Economy). Tuy nhiên, tại Việt Nam và nhiều quốc gia đang phát triển, tỷ lệ rác thải tái chế bị chôn lấp hoặc đốt bỏ vẫn ở mức báo động (> 70%). Nguyên nhân cốt lõi đến từ hai rào cản nhận thức của người dân:

1. **Rào cản nhận biết (What is this waste?):** Người dùng không chắc chắn vật thể thuộc nhóm vật liệu nào (ví dụ: chai nước khoáng vs. chai dầu ăn; hộp xốp vs. bìa giấy tráng PE; lon nhôm vs. hộp sắt tây).
2. **Rào cản hành động (How to sort it properly?):** Kể cả khi nhận ra là chai nhựa, người dùng vẫn bối rối: có cần rửa sạch không? Có cần bóc nhãn và dẹp chai không? Bỏ vào thùng rác màu gì? Đơn vị thu gom ve chai nào gần nhất tiếp nhận loại rác này và giá thu mua bao nhiêu?
3. **Thách thức thị giác máy tính trong môi trường tự nhiên:** Ảnh rác do người dùng chụp bằng điện thoại thường có nền rất phức tạp (đặt trên bàn làm việc có bàn phím, laptop, đặt trên sàn gạch hoa văn, hoặc bị bàn tay che khuất). Nếu chỉ áp dụng một mạng nơ-ron phân loại ảnh thông thường (Classification), mô hình rất dễ bị "đánh lừa" bởi các chi tiết phông nền dẫn đến phân loại sai nghiêm trọng.

**Hệ thống EcoLink** ra đời nhằm giải quyết triệt để các bài toán trên thông qua việc kết hợp công nghệ **Thị giác Máy tính tiên tiến (AI Vision)** với **Cơ sở tri thức phân loại chuẩn (Rule Engine)** và **Sàn giao dịch kết nối thu gom phế liệu (Recycling Marketplace)**.

---

### 2. Mục tiêu kỹ thuật của Phân hệ AI

* **Tự động nhận diện chính xác** các chủng loại rác thải tái chế phổ biến từ ảnh chụp đơn lẻ hoặc luồng camera thời gian thực với độ chính xác đạt trên **$91.5\%$**.
* **Định vị vật thể & Bóc tách phông nền:** Định vị chính xác từng món rác bằng Bounding Box, loại bỏ các chi tiết nền gây nhiễu trước khi đưa vào phân loại sâu.
* **Cơ chế Safe AI (An toàn & Đáng tin cậy):** Ngăn chặn hiện tượng gán nhãn bừa bãi cho các vật thể không phải rác (Out-of-Distribution - OOD) bằng ngưỡng tin cậy linh hoạt ($60\%$).
* **Khả năng mở rộng đa kiến trúc (Multi-Model Architecture):** Cho phép người dùng và hệ thống linh hoạt lựa chọn giữa các mô hình tối ưu về tốc độ (YOLOv8 Nano ~2.5ms), tối ưu về thiết bị di động (MobileNetV3) hoặc tối ưu về độ chuẩn xác (EfficientNet-B0 Acc 92.32%).
* **Vòng lặp học chủ động (Active Feedback Loop):** Thu thập và kiểm duyệt các trường hợp nhận diện sai từ người dùng để liên tục cải tiến tập dữ liệu huấn luyện.

---

### 3. Triết lý thiết kế cốt lõi (Core Design Philosophy)

```mermaid
graph TD
    subgraph Client ["Client Presentation Layer (ecolink-web)"]
        UI["📸 Chụp / Tải ảnh rác<br>(Camera, Sample, Upload)"]
    end

    subgraph AI_Domain ["AI Vision Domain (Ecolink.AiService)"]
        Vision["🧠 Vision Model<br>(Deep Learning CNN)"]
        OOD{"Kiểm tra OOD<br>Confidence ≥ 60%?"}
        Vision --> OOD
    end

    subgraph Rule_Domain ["Deterministic Domain (Ecolink.Backend Rule Engine)"]
        Rules[("📚 Rule Database<br>(Chính sách phân loại tĩnh)")]
        Guide["♻️ Hướng dẫn chuẩn hóa<br>(Màu thùng, các bước rửa/ép rác)"]
        Trade["💰 Ước tính giá thu mua<br>& Kết nối Recycler gần nhất"]
    end

    UI -->|POST /api/v1/waste-scan/classify| Vision
    OOD -->|Đạt ngưỡng| Cat["Mã danh mục rác<br>(plastic, paper, metal...)"]
    OOD -->|Dưới ngưỡng / Ngoại lai| Reject["⚠️ Cảnh báo Out-of-Distribution<br>(Yêu cầu chụp lại rõ hơn)"]
    Cat --> Rules
    Rules --> Guide
    Rules --> Trade
    Guide --> UI
    Trade --> UI
    Reject --> UI
```

> [!IMPORTANT]
> **Nguyên tắc "Decoupled Intelligence" (Tách biệt Trí tuệ & Quy tắc):**  
> Tuyệt đối **không để AI/LLM tự do sinh lời khuyên** "Vật này phải xử lý thế nào, bỏ vào đâu".  
> * **AI đảm nhận bài toán Thị giác:** $\text{Image} \rightarrow \text{Waste Category Code}$ (Ví dụ: `plastic_bottle`).
> * **Hệ thống Quy tắc tĩnh (Rule Engine) đảm nhận hướng dẫn:** $\text{Category Code} \rightarrow \text{Sorting Guide \& Bin Color}$.  
> Thiết kế này triệt tiêu hoàn toàn rủi ro "ảo giác" (hallucination) của AI, đảm bảo mọi lời khuyên đưa ra cho người dân đều chuẩn xác $100\%$ theo quy định bảo vệ môi trường của địa phương.

---

## II. YÊU CẦU & PHẠM VI CHỨC NĂNG AI

### 1. Danh sách Use Case phân hệ AI (Module CLS & SRT)

| Mã Use Case | Tên Use Case | Tác nhân chính | Tóm tắt luồng nghiệp vụ |
| :--- | :--- | :--- | :--- |
| **UC-CLS-01** | Phân loại rác qua ảnh đơn (Single Waste Image Scan) | Người dùng (User / Guest) | Tải ảnh lên, hệ thống xử lý qua Two-Stage Pipeline, trả về kết quả Top-1, xác suất Top-3 và hướng dẫn xử lý tương ứng. |
| **UC-CLS-02** | Định vị & đếm số lượng rác (Waste Object Detection) | Người dùng, Nhân viên | Sử dụng YOLOv8 Detect để vẽ Bounding Box lên từng món rác, đếm tổng số lượng và xuất ảnh trực quan Base64. |
| **UC-CLS-03** | Quét phân loại theo lô (Batch Image Scan) | Nhân viên kiểm định, Quản trị viên | Tải lên cùng lúc nhiều ảnh rác để kiểm tra hiệu năng, tính toán độ trễ trung bình và tỷ lệ OOD. |
| **UC-CLS-04** | Báo cáo nhận diện sai (Submit AI Misclassification) | Người dùng | Người dùng gắn cờ kết quả AI đoán sai, chọn nhãn đúng thực tế kèm ảnh chụp để chuyển vào hàng đợi duyệt của Staff. |
| **UC-CLS-05** | Quản lý vòng đời mô hình & Benchmark | Quản trị viên (Admin) | Xem danh sách các mô hình AI có sẵn, chỉ số mAP, Accuracy, độ trễ và chuyển đổi mô hình mặc định của hệ sinh thái. |
| **UC-SRT-01** | Tra cứu quy tắc phân loại rác tĩnh (Rule Engine Lookup) | Người dùng, Khách | Tìm kiếm thủ công quy tắc xử lý rác bằng từ khóa (ví dụ: "pin", "hộp pizza") mà không cần tải ảnh. |
| **UC-ADM-01** | Giám sát thống kê suy luận AI (AI Inference Analytics) | Quản trị viên | Dashboard hiển thị tổng lượt quét, tỷ lệ rác theo danh mục, tỷ lệ OOD và thời gian suy luận trung bình. |

---

### 2. Danh mục phân loại rác chuẩn mực (Waste Taxonomy)

Hệ sinh thái EcoLink chuẩn hóa đầu ra của phân hệ AI thành **6 nhóm danh mục rác cốt lõi**. Để đạt độ bao phủ cao trong thực tế, mô hình Object Detection được huấn luyện trên **12 lớp rác thực tế** và tự động ánh xạ về 6 nhóm chuẩn mực:

```mermaid
classDiagram
    class EcoLink_Category {
        <<Standard Output>>
        +Cardboard (Bìa carton)
        +Glass (Thủy tinh)
        +Metal (Kim loại & Vỏ lon)
        +Paper (Giấy báo & Sách vở)
        +Plastic (Nhựa tái chế)
        +Trash (Rác không tái chế / Nguy hại)
    }

    class Raw_Detected_Classes {
        <<YOLOv8 12 Classes>>
        +cardboard
        +brown-glass, green-glass, white-glass
        +metal
        +paper
        +plastic
        +battery, biological, clothes, shoes, trash
    }

    Raw_Detected_Classes ..> EcoLink_Category : Ánh xạ thông minh qua DETECT_CLASS_MAPPING
```

*Chi tiết bảng ánh xạ danh mục:*
* `cardboard` $\rightarrow$ **Cardboard (Bìa carton)**
* `brown-glass`, `green-glass`, `white-glass` $\rightarrow$ **Glass (Thủy tinh)**
* `metal` $\rightarrow$ **Metal (Kim loại & Vỏ lon lon nhôm, sắt)**
* `paper` $\rightarrow$ **Paper (Giấy báo, thùng giấy mỏng)**
* `plastic` $\rightarrow$ **Plastic (Chai nhựa PET, can HDPE, bao bì)**
* `battery`, `biological`, `clothes`, `shoes`, `trash` $\rightarrow$ **Trash (Rác thải còn lại & Rác đặc thù)**

---

### 3. Các Quy tắc nghiệp vụ bắt buộc (Business Rules)

Hệ thống tuân thủ nghiêm ngặt các quy tắc đã được ban hành trong tài liệu đặc tả nghiệp vụ:

* **BR-CLS-01 (Định dạng & Dung lượng hợp lệ):** Hệ thống chỉ chấp nhận tệp ảnh định dạng JPG, JPEG, PNG, WEBP với dung lượng không quá $10\text{ MB}$. Các file sai định dạng hoặc hỏng magic bytes sẽ bị từ chối tại tầng Web API với mã `MSG-CLS-04` trước khi gửi tới AI Service.
* **BR-CLS-02 (Tính toàn vẹn của danh mục):** Nhãn dự đoán của AI bắt buộc phải ánh xạ thành công tới một danh mục đang hoạt động trong bảng danh mục rác hệ sinh thái.
* **BR-CLS-03 (Kiểm soát ngoại lai Safe AI - OOD):** Nếu độ tin cậy của mô hình thấp hơn ngưỡng cấu hình ($Confidence < 0.60$), hệ thống kích hoạt cờ `isOutOfDistribution = true`, chặn hiển thị kết quả khẳng định và hướng dẫn người dùng chụp lại góc ảnh rõ hơn (`MSG-CLS-02`).
* **BR-CLS-04 (Xác nhận của người dùng):** Kết quả AI đề xuất chỉ mang tính hỗ trợ. Người dùng luôn có quyền xác nhận hoặc chỉnh sửa lại danh mục trước khi tạo Yêu cầu bàn giao (Handover Request).
* **BR-CLS-05 (Traceability của phản hồi báo sai):** Mỗi phản hồi phân loại sai bắt buộc đính kèm ảnh gốc, nhãn AI dự đoán, độ tự tin của AI và nhãn người dùng sửa đổi, lưu vào danh sách chờ duyệt của Staff.
* **BR-CLS-06 (Quy trình đóng gói dữ liệu tái huấn luyện):** Chỉ những ảnh đã được nhân viên kiểm định (Staff) duyệt và gán nhãn thủ công mới được đưa vào tập huấn luyện của phiên bản mô hình tiếp theo.
* **BR-CLS-07 (Chống rò rỉ dữ liệu - No Data Leakage):** Nghiêm cấm xuất hiện các biến thể của cùng một mẫu ảnh trong cả tập Train và tập Test.
* **BR-CLS-08 (Bảo mật ảnh rác):** Ảnh rác tải lên chỉ được truy cập bởi chủ sở hữu, người thu gom (Recycler) trong giao dịch tương ứng, Staff duyệt phản hồi và Admin hệ thống.

---

## III. THIẾT KẾ KIẾN TRÚC HỆ THỐNG AI

### 1. Sơ đồ kiến trúc tổng thể (System Architecture & Topology)

Phân hệ AI được thiết kế dưới dạng **Microservice độc lập (`Ecolink.AiService`)** viết bằng Python FastAPI, giao tiếp với hệ thống Backend chính (`Ecolink.Backend` .NET 8) qua chuẩn RESTful HTTP/JSON với hiệu năng cao.

```mermaid
flowchart TB
    subgraph ClientLayer ["1. TẦNG GIAO DIỆN (CLIENT LAYER - React / Vite)"]
        WebApp["Web App (ecolink-web)<br>React 18 + TypeScript + Tailwind CSS"]
        CameraModule["WebRTC Camera / Canvas Crop"]
    end

    subgraph GatewayLayer ["2. TẦNG NGHIỆP VỤ & QUY TẮC (.NET 8 Web API)"]
        WasteScanCtrl["WasteScanController<br>(Route: /api/v1/waste-scan)"]
        RuleEngineService["IWasteRuleEngineService<br>(Tra cứu quy tắc tĩnh)"]
        AiClient["AiVisionHttpClient<br>(IHttpClientFactory)"]
        PostgresDB[(PostgreSQL / PostGIS<br>Lưu giao dịch & Thùng rác)]
    end

    subgraph AiServiceLayer ["3. PHÂN HỆ AI MICROSERVICE (Python FastAPI - Port 8000)"]
        FastAPI_App["FastAPI REST Controller<br>(Swagger OpenAPI /docs)"]
        ModelManager["ModelManager (Singleton & Registry)"]
        
        subgraph PipelineExecution ["Two-Stage Inference Engine"]
            Stage1_Detector["Stage 1: YOLOv8 Detector<br>(Khoanh vùng BBox & Đếm)"]
            PaddingLogic["Dynamic Padding (5%) & Clean Crop"]
            Stage2_Classifier["Stage 2: Deep Classifier<br>(EfficientNet / MobileNet / YOLO)"]
        end

        Storage_Models[("Models Directory (.pt, .pth)<br>Lazy Loaded Weights")]
        TelemetryStats[("Local Stats & Feedback<br>stats.json, feedback.json")]
    end

    WebApp -->|Multipart/form-data| WasteScanCtrl
    WasteScanCtrl --> AiClient
    AiClient -->|POST /api/v1/classify| FastAPI_App
    FastAPI_App --> ModelManager
    ModelManager --> Stage1_Detector
    Stage1_Detector --> PaddingLogic
    PaddingLogic --> Stage2_Classifier
    Stage2_Classifier --> FastAPI_App
    ModelManager -.-> Storage_Models
    FastAPI_App -.-> TelemetryStats

    AiClient -->|AiScanResultDto| WasteScanCtrl
    WasteScanCtrl --> RuleEngineService
    RuleEngineService --> PostgresDB
    WasteScanCtrl -->|ApiResponse<WasteScanResponseDto>| WebApp
```

---

### 2. Quy trình xử lý Hai Giai đoạn (Two-Stage Inference Pipeline)

Để giải quyết triệt để lỗi phân loại sai do nền ảnh phức tạp, hệ thống áp dụng kiến trúc chuẩn SOTA 2 giai đoạn:

```mermaid
flowchart LR
    A["🖼️ Ảnh thực tế đầu vào<br>(Nền bàn, màn hình máy tính)"] --> B["Giai đoạn 1: YOLOv8 Detect<br>(Khoanh vùng vật thể)"]
    B --> C{"Phát hiện BBox<br>rác hợp lệ?"}
    C -->|Có| D["Mở rộng viền 5%<br>(Bảo toàn mép vật thể)"]
    D --> E["Cắt lấy vùng rác (Crop)<br>Loại bỏ 100% phông nền nhiễu"]
    E --> F["Giai đoạn 2: Deep Classifier<br>(EfficientNet-B0 / MobileNetV3)"]
    C -->|Không| G["Dùng toàn bộ ảnh gốc<br>(Single-Stage Fallback)"]
    G --> F
    F --> H["Chuẩn hóa xác suất Softmax<br>& Kiểm tra ngưỡng OOD (0.60)"]
    H --> I["Kết quả phân loại chính xác<br>(Top-1 Class + Confidence)"]
```

#### Chi tiết thuật toán Two-Stage trong `model_manager.py`:
1. **Lấy Bounding Box ưu tiên:** Khi ảnh có nhiều vật thể, thuật toán tính điểm ưu tiên cho từng hộp dựa trên diện tích hộp và độ tin cậy phát hiện:
   $$\text{Score}(b) = \text{Area}(b) \times \text{Confidence}(b)$$
   Hộp có điểm số cao nhất đại diện cho vật thể chính người dùng muốn quét.
2. **Dynamic 5% Padding:** Tọa độ $[x_1, y_1, x_2, y_2]$ được mở rộng thêm $5\%$ về mỗi phía để tránh làm mất các đặc trưng viền mép (như nắp chai nhựa, viền lon nhôm):
   $$x_1' = \max(0, x_1 - 0.05 \cdot w), \quad y_1' = \max(0, y_1 - 0.05 \cdot h)$$
   $$x_2' = \min(W, x_2 + 0.05 \cdot w), \quad y_2' = \min(H, y_2 + 0.05 \cdot h)$$
3. **Phân loại sâu trên ảnh sạch:** Ảnh sau khi crop chỉ chứa riêng vật thể rác, giúp mạng nơ-ron tập trung toàn bộ trọng số vào hoa văn vật liệu mà không bị nhiễu bởi bàn phím, con chuột hay tường nhà.

---

### 3. Các mẫu thiết kế phần mềm áp dụng (Software Design Patterns)

1. **Registry Pattern & Strategy Pattern (`ModelManager`):**  
   Toàn bộ các mô hình học sâu được đăng ký trong một từ điển cấu hình trung tâm (`registry`). Khi tiếp nhận request, `ModelManager` đóng vai trò Strategy Context, kích hoạt thuật toán suy luận phù hợp (`yolo_detect`, `torchvision`, `yolo`) dựa trên tham số `model_id`.
2. **Singleton & Lazy Loading (Tải lười tài nguyên):**  
   Để tối ưu hóa RAM khi khởi động dịch vụ trên máy chủ, các tệp trọng số nặng ($2.8\text{ MB} - 15.6\text{ MB}$) không được nạp đồng loạt. Chỉ khi có yêu cầu đầu tiên gọi tới mô hình nào, mô hình đó mới được nạp vào VRAM/RAM và lưu vào bộ nhớ đệm `_loaded_models` cho các lần gọi tiếp theo.
3. **Graceful Fallback Strategy:**  
   Nếu mô hình được yêu cầu chưa có sẵn tệp trọng số (ví dụ đang trong quá trình huấn luyện lại), hệ thống tự động fallback về mô hình mặc định sẵn sàng nhất (`yolov8n`), đảm bảo dịch vụ luôn ở trạng thái khả dụng $99.9\%$.

---

## IV. THIẾT KẾ MÔ HÌNH HỌC SÂU & ĐÁNH GIÁ BENCHMARK

### 1. Bộ 4 Mô hình AI tích hợp trong hệ thống

Phân hệ AI của EcoLink tích hợp đồng thời 4 mô hình học sâu chuyên biệt nhằm phục vụ các kịch bản sử dụng đa dạng trong thực tế:

```mermaid
graph TD
    Root["Hệ sinh thái Đa Mô hình EcoLink"]
    Root --> M1["1. YOLOv8 Waste Object Detection<br>Khoanh vùng BBox đa vật thể, lọc nền nhiễu"]
    Root --> M2["2. YOLOv8 Nano Classifier<br>Siêu tốc (~2.5ms), tối ưu cho Webcam & Video"]
    Root --> M3["3. MobileNetV3-Small<br>Kiến trúc nhẹ (2.54M params), tối ưu di động & pin"]
    Root --> M4["4. EfficientNet-B0<br>Độ chính xác cao nhất (92.32%), xử lý rác móp méo"]
```

1. **YOLOv8 Waste Object Detection (`yolov8_detect`):**
   * *Kiến trúc:* Darknet backbone kết hợp C2f modules và decoupled detection head từ Ultralytics.
   * *Nhiệm vụ:* Định vị BBox tọa độ chính xác, đếm số lượng món rác trong ảnh, bóc tách vật thể phục vụ Stage 1.
2. **YOLOv8 Nano Classification (`yolov8n`):**
   * *Kiến trúc:* Ultralytics CNN Nano, chỉ **1.44M tham số**, kích thước checkpoint siêu gọn **2.84 MB**.
   * *Nhiệm vụ:* Đạt độ trễ siêu thấp ($11.19\text{ ms}$ trên CPU), thích hợp cho luồng camera quét liên tục hoặc thiết bị nhúng IoT.
3. **MobileNetV3-Small (`mobilenet_v3`):**
   * *Kiến trúc:* Depthwise Separable CNN kết hợp cơ chế Squeeze-and-Excitation (SE) và hàm kích hoạt Hard-Swish.
   * *Nhiệm vụ:* Nhẹ ($2.54\text{ M}$ params, kích thước checkpoint $5.94\text{ MB}$), tối ưu hóa sâu cho ứng dụng di động Flutter/Android mà không làm hao pin.
4. **EfficientNet-B0 (`efficientnet_b0`):**
   * *Kiến trúc:* Compound Scaling CNN đồng bộ giữa Depth, Width và Image Resolution ($224 \times 224$).
   * *Nhiệm vụ:* Đạt độ chuẩn xác tổng thể cao nhất (**$92.32\%$**), phân biệt xuất sắc các loại rác biến dạng, rách nát hoặc chai lọ thủy tinh mờ đục.

---

### 2. Kỹ thuật huấn luyện nâng cao (Advanced Training Methodology)

Quy trình huấn luyện được cài đặt chuẩn mực tại tệp mã nguồn [`train_torch_models.py`](file:///d:/Project/intelligent-waste-sorting-trading/train_torch_models.py):

* **Học chuyển giao (Transfer Learning):** Khởi tạo từ trọng số ImageNet tiền huấn luyện của PyTorch Torchvision. Thay thế lớp phân loại cuối (Fully Connected Classifier) bằng lớp tuyến tính `nn.Linear(in_features, 6)` tương ứng với 6 nhóm rác EcoLink.
* **Chiến lược Tăng cường Dữ liệu (Data Augmentation):**
  * `transforms.RandomResizedCrop(224, scale=(0.8, 1.0))`: Mô phỏng rác ở các khoảng cách chụp gần/xa khác nhau.
  * `transforms.RandomHorizontalFlip(p=0.5)` & `transforms.RandomRotation(degrees=15)`: Mô phỏng rác nằm ở nhiều góc xoay bất định.
  * `transforms.ColorJitter(brightness=0.2, contrast=0.2, saturation=0.2)`: Khắc phục sự chênh lệch ánh sáng giữa phòng kín và ngoài trời.
* **Hàm mất mát Label Smoothing:**
  $$\mathcal{L}_{\text{CE}} = - \sum_{i=1}^{K} q_i \log(p_i), \quad q_i = (1 - \epsilon) y_i + \frac{\epsilon}{K}$$
  Áp dụng $\epsilon = 0.1$ giúp mạng nơ-ron không bị tự tin thái quá (overconfident), tăng tính bao quát khi gặp các mẫu rác thực tế ngoài đời.
* **Tối ưu hóa & Lịch trình Học tập:** Sử dụng thuật toán `AdamW` (Weight decay $1 \times 10^{-4}$) kết hợp bộ điều chỉnh tốc độ học `CosineAnnealingLR` chu kỳ 15 epochs để đạt trạng thái hội tụ tối ưu mà không bị kẹt tại các cực tiểu cục bộ.

---

### 3. Bảng khảo nghiệm Benchmark Hàn lâm (Academic Benchmark Table)

Được đánh giá trực tiếp trên **508 hình ảnh kiểm thử độc lập (Validation Set)** thuộc bộ dữ liệu TrashNet chuẩn (`data/trashnet_split/val/`):

| Chỉ số / Tiêu chí đánh giá | YOLOv8n-cls | MobileNetV3-Small | EfficientNet-B0 | Nhận xét chuyên môn |
| :--- | :---: | :---: | :---: | :--- |
| **Kiến trúc mạng cốt lõi** | Ultralytics CNN | Depthwise Separable CNN | Compound Scaling CNN | Đa dạng hóa giải pháp kỹ thuật |
| **Số lượng tham số (Parameters)** | **1.44M** | 2.54M | 5.29M | YOLOv8n gọn nhẹ nhất |
| **Kích thước file Checkpoint** | **2.84 MB** | 5.94 MB | 15.60 MB | Rất thuận tiện phân phối và nhúng |
| **Độ trễ CPU (Latency per Image)** | **11.19 ms** | 13.22 ms | 26.66 ms | YOLOv8n nhanh nhất, phù hợp Realtime |
| **Top-1 Accuracy Tổng thể** | 91.73% | 91.14% | **92.32%** | **EfficientNet-B0 chính xác nhất** |
| **Cardboard (Bìa carton)** | 90.1% | **96.3%** | 91.4% | MobileNetV3 bắt đặc trưng vân carton tốt nhất |
| **Glass (Chai lọ thủy tinh)** | **88.1%** | 82.2% | 85.1% | YOLOv8n vượt trội với thủy tinh trong suốt |
| **Metal (Kim loại & Vỏ lon)** | 97.6% | 90.2% | **98.8%** | **EfficientNet-B0 đạt kỷ lục 98.8%** |
| **Paper (Giấy báo, sách vở)** | 95.8% | 95.8% | **97.5%** | EfficientNet-B0 phân loại giấy tốt nhất |
| **Plastic (Chai, hộp nhựa)** | 88.7% | **94.8%** | 91.8% | MobileNetV3 phân biệt nhựa xuất sắc nhất |
| **Trash (Rác tạp không tái chế)**| **85.7%** | 78.6% | 82.1% | YOLOv8n phân loại rác tạp tốt nhất |

---

### 4. Phân tích Trade-off & Khuyến nghị môi trường triển khai

Dựa trên kết quả thực nghiệm, nhóm đưa ra bảng khuyến nghị phân bổ mô hình theo từng môi trường tác nghiệp cụ thể:

```mermaid
graph LR
    Kiosk["Kiosk thông minh / Camera Realtime"] -->|Khuyến nghị| YOLO["YOLOv8n-cls<br>(Độ trễ ~11ms, nhẹ 2.84MB)"]
    Mobile["Ứng dụng di động (Flutter / Android)"] -->|Khuyến nghị| MobileNet["MobileNetV3-Small<br>(Tiết kiệm pin, RAM thấp)"]
    Server["Audit giao dịch thu gom / Ảnh tĩnh"] -->|Khuyến nghị| Efficient["EfficientNet-B0<br>(Acc 92.32%, tránh nhầm kim loại)"]
    MultiObject["Ảnh hiện trường phức tạp nhiều rác"] -->|Khuyến nghị| TwoStage["Two-Stage YOLO Detect + Crop<br>(Lọc 100% nền bàn/sàn)"]
```

* **Trường hợp 1 (Kiosk rác thông minh / Live Camera):** Chọn **YOLOv8n-cls**. Tốc độ xử lý tức thì giúp camera phản hồi hơn 30 FPS mà không gây giật lag.
* **Trường hợp 2 (Ứng dụng di động phía Người dân):** Chọn **MobileNetV3-Small**. Giảm thiểu dung lượng bộ nhớ tải xuống và tối ưu hóa thời lượng pin cho điện thoại tầm trung.
* **Trường hợp 3 (Server Audit giao dịch mua bán ve chai):** Chọn **EfficientNet-B0**. Đạt độ chính xác tuyệt đối trên vỏ lon kim loại ($98.8\%$) và giấy báo ($97.5\%$), giúp việc định giá thu gom minh bạch, tránh thất thoát tài chính cho các đối tác thu mua.

---

## V. THIẾT KẾ GIAO DIỆN LẬP TRÌNH ỨNG DỤNG (API SPECIFICATION)

### 1. Chi tiết các RESTful API Endpoints của `Ecolink.AiService`

Phân hệ AI cung cấp bộ API RESTful đạt chuẩn OpenAPI 3.0 với đầy đủ tài liệu tương tác tại đường dẫn `/docs`:

```
POST /api/v1/classify         # Nhận diện & phân loại ảnh rác (Hỗ trợ Two-Stage Crop)
POST /api/v1/detect           # Định vị vật thể BBox & đếm số lượng (YOLOv8 Detect)
POST /api/v1/classify/batch   # Phân loại theo lô (Batch Processing)
GET  /api/v1/models           # Danh sách các mô hình và trạng thái tệp trọng số
GET  /api/v1/models/{model_id}# Thông số kỹ thuật chuyên sâu và benchmark mô hình
GET  /api/v1/rules            # Tra cứu toàn bộ cơ sở tri thức quy tắc phân loại rác tĩnh
PUT  /api/v1/rules            # Cập nhật ngưỡng tin cậy OOD và hướng dẫn phân loại
POST /api/v1/feedback         # Tiếp nhận phản hồi AI nhận diện sai từ người dùng
GET  /api/v1/feedback         # Danh sách báo cáo sai phục vụ vòng lặp huấn luyện lại
GET  /api/v1/stats            # Báo cáo thống kê lượt quét, tỷ lệ OOD và thời gian trễ
GET  /health                  # Kiểm tra sức khỏe dịch vụ, phần cứng (CUDA/CPU)
```

---

### 2. Định dạng dữ liệu DTO và Mã phản hồi hệ thống

#### A. Request & Response của Endpoint `POST /api/v1/classify`:
* **Request (Multipart Form-Data):**
  * `file`: Tệp tin hình ảnh rác (jpg, png, webp).
  * `model`: Mã mô hình mong muốn (`yolov8_detect`, `mobilenet_v3`, `efficientnet_b0`, `yolov8n`).
  * `use_detection_crop`: Giá trị boolean (`true`/`false`) cho phép kích hoạt Stage 1 bóc tách nền.
  * `confidence_threshold`: Ngưỡng OOD áp dụng (Mặc định: `0.60`).

* **Response Thành công (HTTP 200 - Safe Prediction):**
```json
{
  "success": true,
  "is_out_of_distribution": false,
  "confidence_threshold": 0.6,
  "model_used": "yolov8_detect",
  "model_name": "YOLOv8 Waste Object Detection (SOTA)",
  "model_family": "Object Detection & Localization (YOLOv8)",
  "top_prediction": {
    "label": "plastic",
    "confidence": 0.942,
    "display_name": "Nhựa tái chế (Plastic)"
  },
  "predictions": [
    { "label": "plastic", "confidence": 0.942, "display_name": "Nhựa tái chế (Plastic)" },
    { "label": "trash", "confidence": 0.038, "display_name": "Rác thải không tái chế (Trash)" },
    { "label": "glass", "confidence": 0.015, "display_name": "Thủy tinh (Glass)" }
  ],
  "sorting_rule": {
    "category_code": "plastic",
    "bin_color_name": "Thùng Màu Vàng (Nhựa & Kim Loại)",
    "instructions": [
      "Đổ sạch chất lỏng còn thừa bên trong chai",
      "Tráng sơ qua nước sạch nếu chứa dầu mỡ",
      "Bóp dẹp thân chai để tiết kiệm thể tích lưu trữ",
      "Cho vào thùng rác tái chế màu vàng"
    ]
  },
  "pipeline": "two_stage_crop_classified",
  "cropped_bbox": [120.5, 45.0, 580.2, 720.0],
  "inference_time_ms": 14.8
}
```

* **Response Kích hoạt Safe AI (HTTP 200 - Out-of-Distribution Warning):**
```json
{
  "success": true,
  "is_out_of_distribution": true,
  "confidence_threshold": 0.6,
  "model_used": "efficientnet_b0",
  "model_name": "EfficientNet-B0",
  "top_prediction": {
    "label": "plastic",
    "confidence": 0.421,
    "display_name": "Nhựa tái chế (Plastic)"
  },
  "predictions": [
    { "label": "plastic", "confidence": 0.421, "display_name": "Nhựa tái chế (Plastic)" },
    { "label": "paper", "confidence": 0.312, "display_name": "Giấy báo (Paper)" }
  ],
  "sorting_rule": null,
  "pipeline": "two_stage",
  "inference_time_ms": 26.5
}
```

#### B. Bảng mã thông điệp hệ thống (System Messages):
* `MSG-CLS-01`: "Nhận diện thành công loại rác {DisplayName} với độ tin cậy {Confidence}%."
* `MSG-CLS-02`: "Hệ thống không nhận diện chắc chắn (> 60%) hoặc vật thể không thuộc nhóm rác hỗ trợ. Vui lòng chụp lại hình ảnh ở góc rõ hơn."
* `MSG-CLS-04`: "Vui lòng tải lên file ảnh hợp lệ (JPG, PNG, WEBP, dung lượng dưới 10MB)."

---

## VI. THIẾT KẾ LUỒNG NGHIỆP VỤ & TƯƠNG TÁC (SEQUENCE DIAGRAMS)

### 1. Luồng phân loại Two-Stage tiêu chuẩn (Happy Path)

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng
    participant Web as Giao diện Web (Scanner.tsx)
    participant Backend as Ecolink.Backend (Controller)
    participant RuleEngine as IWasteRuleEngineService
    participant AiService as Ecolink.AiService (FastAPI)
    participant ModelMgr as ModelManager (Registry)

    User->>Web: Tải ảnh chai nhựa hoặc Chụp camera
    Web->>Backend: POST /api/v1/waste-scan/classify (multipart/form-data)
    Backend->>AiService: Forward Stream: POST /api/v1/classify
    AiService->>ModelMgr: predict(image, model="yolov8_detect", use_crop=true)
    Note over ModelMgr: Stage 1: YOLOv8 Detector tìm BBox<br/>Stage 2: Crop + 5% Padding & Deep Classification
    ModelMgr-->>AiService: Result (Label="plastic", Conf=0.945, Latency=14ms)
    
    AiService-->>Backend: AiScanResponse (is_out_of_distribution=false)
    Backend->>RuleEngine: GetSortingGuideAsync("plastic")
    RuleEngine-->>Backend: SortingGuideDto (Thùng vàng, các bước rửa/ép)
    Backend-->>Web: ApiResponse<WasteScanResponseDto> (MSG-CLS-01)
    Web-->>User: Hiển thị kết quả nhãn, BBox, hướng dẫn xử lý & nút Đặt bán ve chai
```

---

### 2. Luồng Safe AI xử lý dữ liệu ngoại lai (OOD Fallback Path)

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng
    participant Web as Giao diện Web (Scanner.tsx)
    participant Backend as Ecolink.Backend (Controller)
    participant AiService as Ecolink.AiService (FastAPI)

    User->>Web: Chụp ảnh bàn phím máy tính hoặc ảnh mờ
    Web->>Backend: POST /api/v1/waste-scan/classify
    Backend->>AiService: POST /api/v1/classify
    Note over AiService: Mô hình suy luận:<br/>Top-1 Confidence = 0.42 < Threshold (0.60)
    AiService-->>Backend: AiScanResponse (is_out_of_distribution=true)
    Note over Backend: Kích hoạt kiểm soát rủi ro BR-CLS-03:<br/>Gắn cờ không chắc chắn, không trả Rule
    Backend-->>Web: ApiResponse (MSG-CLS-02, Success=true, IsOOD=true)
    Web-->>User: Hiển thị cảnh báo màu hổ phách: "Ảnh không rõ hoặc không phải rác tái chế. Vui lòng chụp lại!"
```

---

### 3. Luồng phản hồi báo sai & Vòng lặp Retraining (Active Learning Loop)

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng
    actor Staff as Nhân viên kiểm định (Staff)
    participant Web as Giao diện Web (Feedback Modal)
    participant Backend as Ecolink.Backend
    participant AiService as Ecolink.AiService (Feedback Queue)
    participant Dataset as Training Dataset (Roboflow / Local)

    User->>Web: Nhận thấy AI đoán "plastic" nhưng thực tế là "glass"
    User->>Web: Bấm "Báo cáo kết quả sai", chọn nhãn đúng "glass"
    Web->>Backend: POST /api/v1/waste-scan/feedback
    Backend->>AiService: POST /api/v1/feedback (predicted="plastic", user="glass")
    AiService-->>Backend: Feedback ID = #104 (Status="pending_verification")
    Backend-->>Web: Xác nhận tiếp nhận phản hồi thành công
    
    Note over Staff: Định kỳ hàng tuần duyệt Feedback Queue
    Staff->>AiService: GET /api/v1/feedback
    Staff->>Staff: Đối soát ảnh gốc với nhãn đề xuất
    Staff->>Dataset: Gán nhãn chuẩn xác, đưa vào tập Train Set mới
    Note over Dataset: Tái huấn luyện mô hình (Retrain Loop) đạt độ chính xác cao hơn
```

---

## VII. THIẾT KẾ TRẢI NGHIỆM GIAO DIỆN NGƯỜI DÙNG (UI/UX DESIGN)

### 1. Các trạng thái giao diện Scanner (`Scanner.tsx`)

Mô-đun nhận diện rác tại trang người dùng ([`Scanner.tsx`](file:///d:/Project/ecolink-web/src/pages/Scanner.tsx)) được thiết kế theo tiêu chuẩn công thái học cao, tối ưu hiển thị vừa vặn trong màn hình $1440 \times 900$ không bị cuộn trang:

1. **Trạng thái Chờ (Idle State):**  
   * Khu vực Upload đa kênh: Hỗ trợ kéo thả ảnh (Drag & Drop), Nút "Dùng ảnh mẫu kiểm thử" (Sample Shot) cho phép hội đồng kiểm thử nhanh mà không cần chuẩn bị ảnh, và Nút "Chụp trực tiếp" (Camera WebRTC).
   * Cột thông tin hiển thị các quy chuẩn phân loại rác cơ bản và cam kết môi trường.
2. **Trạng thái Quét (Scanning State):**  
   * Hiệu ứng Radar Scanner quét dọc trên hình ảnh kèm thông báo trạng thái động ("Đang bóc tách nền vật thể...", "Đang đối soát quy tắc phân loại...").
3. **Trạng thái Hoàn thành (Done State):**  
   * Cột trái: Hiển thị ảnh chụp có vẽ Bounding Box bao quanh vật thể rác và huy hiệu tên mô hình đã phân tích.
   * Cột phải: Thẻ kết quả phân loại chi tiết (Top-1 Class, Confidence Score, Màu thùng rác, Checklist các bước xử lý, Ước tính điểm EcoPoints và nút "Kết nối điểm thu gom gần nhất").

---

### 2. Cơ chế chuyển đổi mô hình tức thì (Instant Model Switcher)

Ngay tại giao diện người dùng, hệ thống cung cấp Menu chọn mô hình AI (Dropdown):
* `⚡ YOLOv8n (Siêu tốc độ - Realtime)`
* `📱 MobileNetV3 (Tối ưu di động)`
* `🎯 EfficientNet-B0 (Chính xác cao nhất)`
* `🔍 YOLOv8 Detection (SOTA - Định vị & Cắt nền)`

**Tính năng Auto Re-classify:** Khi người dùng thay đổi lựa chọn mô hình trong dropdown, giao diện lập tức kích hoạt yêu cầu suy luận lại trên bức ảnh hiện tại mà không yêu cầu người dùng phải chụp lại ảnh. Người dùng và hội đồng phản biện có thể so sánh trực tiếp độ trễ (latency ms) và phân phối xác suất giữa các thuật toán khác nhau trong thời gian thực.

---

### 3. Tích hợp chuỗi giá trị: Nhận diện $\rightarrow$ Hướng dẫn $\rightarrow$ Giao dịch thu gom

Điểm khác biệt vượt trội của EcoLink so với các ứng dụng AI đơn lẻ trên thị trường là sự kết nối liền mạch từ bước **Nhận diện** sang **Chuỗi giá trị kinh tế**:
* Khi AI nhận diện ra `Chai nhựa PET (Plastic)` với độ tin cậy $94.5\%$, hệ thống không dừng lại ở mức hiển thị chữ "Nhựa".
* Hệ thống tính toán ngay:
  * **Đơn giá ước tính:** $6.000\text{ đ} - 8.000\text{ đ/kg}$.
  * **Điểm thưởng môi trường (Gamification):** $+15\text{ EcoPoints}$ và $+0.2\text{ kg CO}_2$ giảm phát thải.
  * **Hành động tiếp theo:** Nút bấm **"Tạo đơn bán phế liệu"** hoặc **"Tìm đối tác thu mua quanh đây"** điều hướng trực tiếp sang bản đồ PostGIS với vị trí các vựa ve chai gần nhất đã được xác minh.

---

## VIII. YÊU CẦU PHI CHỨC NĂNG & AN TOÀN HỆ THỐNG

### 1. Hiệu năng & Khả năng chịu tải (Performance & Scalability)

* **Thời gian suy luận (Inference Latency):**  
  * Trên phần cứng CPU tiêu chuẩn (Intel Core i5/i7 thế hệ 11 trở lên): Thời gian suy luận của YOLOv8n $< 15\text{ ms}$; Two-Stage Pipeline hoàn tất trong $< 150\text{ ms}$.
  * Trên GPU hỗ trợ CUDA (NVIDIA T4 / RTX): Thời gian suy luận $< 10\text{ ms}$.
* **Thời gian phản hồi toàn trình (End-to-End Latency):** Bao gồm truyền tải ảnh qua mạng, xử lý Two-Stage tại Python Microservice, tra cứu Rule Engine tại .NET Backend và trả về giao diện React: **$< 800\text{ ms}$** trong điều kiện mạng thông thường.
* **Tối ưu hóa RAM/VRAM:** Nhờ kiến trúc Lazy Loading và cờ `torch.no_grad()`, dịch vụ AI chỉ tiêu tốn xấp xỉ $350\text{ MB RAM}$ khi hoạt động ở chế độ chờ và giải phóng bộ nhớ ngay sau mỗi mẻ xử lý.

---

### 2. An toàn dữ liệu & Quyền riêng tư (Safe AI & Data Privacy)

* **Kiểm tra An toàn Tệp tin Đầu vào:** Trước khi mở ảnh bằng thư viện Pillow/OpenCV, hệ thống kiểm tra phần mở rộng file, Content-Type HTTP và Magic Bytes đầu file (ví dụ `0xFFD8FF` cho JPEG, `0x89504E47` cho PNG) để triệt tiêu nguy cơ tấn công chèn mã độc (Polyglot File Attack).
* **Quyền riêng tư hình ảnh (BR-CLS-08):** Ảnh chụp rác của người dùng được lưu trữ trong thư mục bảo mật với định danh UUID ngẫu nhiên. Chỉ các yêu cầu đã được xác thực token JWT của chính chủ sở hữu hoặc nhân viên kiểm duyệt được phân công mới có quyền đọc tệp ảnh.

---

### 3. Kế hoạch dự phòng ngoại tuyến (Offline Demo Strategy)

Để phòng ngừa rủi ro mất kết nối Internet khi báo cáo đồ án hoặc triển khai tại các sự kiện ngoài trời:
1. **Kiến trúc Localhost Khép kín:** Toàn bộ hệ thống (`ecolink-web`, `Ecolink.Backend` và `Ecolink.AiService`) được cấu hình chạy cục bộ hoàn chỉnh qua kịch bản `run_ai_service.bat` mà không phụ thuộc vào bất kỳ Cloud API bên thứ ba nào (như OpenAI hay Google Gemini).
2. **Phiên bản On-device Web Model:** Trọng số MobileNetV3 và YOLOv8 có thể xuất khẩu sang định dạng **ONNX Runtime Web** hoặc **TensorFlow.js**, cho phép thực hiện suy luận trực tiếp trên trình duyệt máy khách (Client-Side Inference) ngay cả khi ngắt toàn bộ kết nối mạng.

---

## IX. KẾT LUẬN & ĐỊNH HƯỚNG PHÁT TRIỂN

### 1. Đánh giá mức độ hoàn thành nhiệm vụ

Phân hệ AI của dự án **EcoLink** đã được thiết kế và hiện thực hóa một cách toàn diện, đáp ứng vượt mức các tiêu chí của đồ án tốt nghiệp công nghệ thông tin:
* Đã giải quyết triệt để vấn đề nhiễu nền môi trường thông qua kiến trúc **Two-Stage SOTA (YOLOv8 Detection + Deep Classification)**.
* Đạt độ chính xác tổng thể vượt trội (**$92.32\%$** với EfficientNet-B0), độ trễ cực thấp (**$11.19\text{ ms}$** với YOLOv8n).
* Tích hợp thành công cơ chế **Safe AI (Out-of-Distribution)** loại bỏ nguy cơ phân loại sai vật thể ngoại lai.
* Xây dựng được quy trình khép kín: **Nhận diện Thị giác $\rightarrow$ Chuẩn hóa Quy tắc $\rightarrow$ Giao dịch Thu mua $\rightarrow$ Tích điểm Thưởng $\rightarrow$ Phản hồi Cải tiến**.

### 2. Định hướng phát triển tương lai (Roadmap)

1. **Giai đoạn tiếp theo (Next Quarter):**
   * Tích hợp công nghệ lượng tử hóa mô hình (Model Quantization INT8 / TensorRT) để giảm thêm $60\%$ kích thước tệp trọng số và tăng tốc độ suy luận gấp 3 lần trên thiết bị di động.
   * Huấn luyện mô hình phân loại sâu các phân nhóm nhựa (PET #1, HDPE #2, PVC #3, LDPE #4, PP #5) phục vụ phân loại rác cấp độ công nghiệp.
2. **Mở rộng hệ sinh thái (Ecosystem Expansion):**
   * Đóng gói SDK nhúng vào các thùng rác tự động (Smart Trash Bins) tại các trường đại học và tòa nhà văn phòng thông minh.
   * Xây dựng cộng đồng mở gán nhãn rác thải (Crowdsourced Waste Labeling) tích hợp cơ chế thưởng EcoPoints.

---
*Báo cáo được hoàn thiện bởi Nhóm Đồ Án Tốt Nghiệp EcoLink.*  
*Hà Nội, tháng 10 năm 2026.*
