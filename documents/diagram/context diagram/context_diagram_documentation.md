# **TÀI LIỆU ĐÁNH GIÁ VÀ ĐẶC TẢ SƠ ĐỒ NGỮ CẢNH HỆ THỐNG (CONTEXT DIAGRAM - DFD LEVEL 0)**

> **Hệ thống:** EcoLink — Nền tảng phân loại rác thông minh tích hợp AI & Thu gom thương mại phế liệu  
> **Dự án:** Capstone Project SEP490-G141  
> **Thư mục lưu trữ:** `documents/diagram/context diagram/`  
> **Tệp sơ đồ Draw.io:** [`ecolink_context_diagram.drawio`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/context%20diagram/ecolink_context_diagram.drawio)  

---

## **I. ĐÁNH GIÁ SƠ ĐỒ HIỆN TẠI VÀ ĐỐI CHIẾU MẪU THAM KHẢO**

### **1. Đánh giá sơ đồ cũ của EcoLink (Hình 1) — Tại sao bị nhận xét là "to và bị dày"?**

| Vấn đề gặp phải | Biểu hiện trên sơ đồ cũ | Hậu quả kỹ thuật & Trực quan |
| :--- | :--- | :--- |
| **Bùng nổ mũi tên (Arrow Explosion)** | Riêng Actor `User` có tới **8 mũi tên song song**, `Recycler` có **5 mũi tên**, các hệ thống ngoài mỗi bên 2 mũi tên. Tổng cộng có hơn **30 mũi tên chằng chịt**. | Biến Context Diagram thành một "mạng nhện" (Spaghetti diagram). Người chấm thi / hội đồng không thể nắm bắt được ranh giới tổng quan của hệ thống. |
| **Nhầm lẫn mức độ trừu tượng (Abstraction Level Mismatch)** | Đang cố vẽ chi tiết từng hành vi / use case nhỏ ở mức DFD Level 1 & Level 2 vào DFD Level 0 (Context). Ví dụ: vẽ riêng *"Waste photos & AI feedback"*, *"Handover requests, updates/cancels"*, *"Partner ratings"*, *"Dispute results"*,... | Dòng chữ nhãn (Label) trên mỗi đường nối dài tới 3–4 dòng text, các dòng chữ đè lên nhau, làm sơ đồ phình to và dày đặc chữ một cách không cần thiết. |
| **Trộn lẫn Input và Output** | Các mũi tên vào (Request) và ra (Response) đều dùng cùng một kiểu nét liền, đâm chéo nhau vào hình tròn trung tâm. | Khó theo dõi luồng thông tin nào là do con người gửi vào, luồng thông tin nào là hệ thống phản hồi trả ra. |
| **Sai lệch khái niệm kiến trúc** | Khối bản đồ ghi là *"OpenStreetMap (PostGIS)"*. | **PostGIS** là extension cơ sở dữ liệu nội bộ của hệ thống (thuộc PostgreSQL), KHÔNG PHẢI là một External Entity. Chỉ có **OpenStreetMap / Nominatim** mới là dịch vụ bản đồ bên ngoài. |

---

### **2. Bài học và quy chiếu từ 2 mẫu tham khảo**

#### **A. Mẫu SCS System (Hình 2):**
* **Ưu điểm học tập:**
  * Rất súc tích: Mỗi Actor / Hệ thống ngoài chỉ có từ **2 đến 4 luồng dữ liệu gộp**.
  * Tên luồng dữ liệu ngắn gọn, chuẩn ngữ nghĩa danh từ (Data Flow): `Account list`, `Activity lists`, `Auth Token`, `Attendance data`,...

#### **B. Mẫu EELPM-FPT (Hình 3) — Chuẩn mực xuất sắc cho Capstone FPTU:**
* **Ưu điểm vượt trội:**
  1. **Quy ước đường nét khoa học (Visual Grammar):**
     * **Nét liền (Solid Line ───►):** Thể hiện **Luồng dữ liệu vào (Input / Request)** từ thực thể ngoài gửi vào hệ thống.
     * **Nét đứt (Dashed Line - - -►):** Thể hiện **Luồng dữ liệu ra (Output / Response / Status / Notification)** từ hệ thống trả về cho thực thể.
  2. **Gộp luồng dữ liệu (Composite Data Flows):** Thay vì vẽ 10 mũi tên cho 10 màn hình, hệ thống gộp các luồng dữ liệu có cùng ngữ cảnh nghiệp vụ thành 1–2 mũi tên lớn mang tính đại diện.
  3. **Màu sắc Pastel trang nhã:** Mỗi nhóm vai trò mang một tông màu nhẹ nhàng, phân biệt rõ ràng giữa Người dùng (Human Actors) và Dịch vụ ngoài (External Services).
  4. **Bố cục đường đi trực giao (Orthogonal Routing):** Các đường nối bo góc mượt mà, không đè chéo qua các khối hộp, giữ khoảng thở (white space) thoáng đãng.

---

## **II. ĐỀ XUẤT CẢI TIẾN CHO ECOLINK (PHIÊN BẢN TỐI ƯU)**

1. **Rút gọn số lượng luồng dữ liệu từ 30+ xuống còn 20 luồng gộp chuẩn mực:**
   * Mỗi Actor con người chỉ có **2 đến 4 luồng** (Cân đối 1–2 luồng vào và 1–2 luồng ra).
   * Mỗi Hệ thống bên ngoài chỉ có đúng **2 luồng** (1 Request đi, 1 Callback/Response về).
2. **Áp dụng chuẩn nét liền / nét đứt của EELPM:**
   * Giúp người đọc phân biệt ngay lập tức: Đâu là dữ liệu người dùng nộp vào hệ thống, và đâu là kết quả xử lý hệ thống phản hồi lại.
3. **Chuẩn hóa các External Entities:**
   * Tách rõ **OpenStreetMap / Nominatim** (Bản đồ ngoài) khỏi PostGIS (Database trong).
   * Gộp kênh thông báo ngoài thành **Google SMTP & Firebase FCM**.
   * Đặt **Google OAuth 2.0**, **Cloudflare R2**, **VietQR / PayOS** theo hàng dọc bên phải ngay ngắn.

---

## **III. BẢNG TỪ ĐIỂN DỮ LIỆU LUỒNG CONTEXT (DATA FLOW DICTIONARY)**

### **1. Tác nhân con người (Human Actors)**

| Tác nhân | Chiều | Kiểu nét | Tên luồng dữ liệu gộp | Chi tiết các thông tin thành phần |
| :--- | :---: | :---: | :--- | :--- |
| **Guest** *(Khách vãng lai)* | **Vào** | Nét liền | `Registration info, login credentials & search queries` | Thông tin đăng ký tài khoản, thông tin đăng nhập, yêu cầu tra cứu cẩm nang rác công khai. |
| | **Ra** | Nét đứt | `Auth status, public sorting guides & platform info` | Trạng thái đăng nhập/xác minh, cẩm nang phân loại rác chuẩn và giới thiệu hệ sinh thái. |
| **Citizen (User)** *(Công dân)* | **Vào** | Nét liền | `Waste scan photos, handover requests & AI error feedback` | Ảnh chụp rác phân loại, đơn hẹn bàn giao phế liệu, báo cáo AI nhận diện sai. |
| | **Vào** | Nét liền | `Partner reviews, dispute claims & tree care / voucher redemption` | Đánh giá đối tác thu gom, đơn khiếu nại tranh chấp, hành động tưới Cây ảo & lệnh đổi voucher. |
| | **Ra** | Nét đứt | `AI classification results, sorting rules & nearby recyclers` | Nhãn dự đoán AI (YOLOv8), quy tắc xử lý rác, danh sách vựa ve chai lân cận trên bản đồ. |
| | **Ra** | Nét đứt | `Confirmed receipts, reward points & transaction notifications` | Biên lai xác nhận khối lượng/tiền, điểm xanh thưởng, thông báo trạng thái đơn hàng. |
| **Recycler** *(Cơ sở thu mua)* | **Vào** | Nét liền | `Facility profile, accepted materials price list & active status` | Hồ sơ cơ sở, giấy phép, bảng giá phế liệu thu mua, bật/tắt trạng thái hoạt động trên GPS. |
| | **Vào** | Nét liền | `Handover acceptances, scale proof photos & service fee payments` | Chấp nhận đơn gom, ảnh chụp cân thực tế lập biên lai, xác nhận thanh toán phí dịch vụ VietQR. |
| | **Ra** | Nét đứt | `New handover requests, collection time slots & chat messages` | Yêu cầu gom rác mới, khung giờ thu gom người dân đề xuất, tin nhắn thương lượng SignalR. |
| | **Ra** | Nét đứt | `Trade reward points, service order QR codes & reminders` | Điểm thưởng hoàn tất giao dịch, mã VietQR mua gói tích xanh, nhắc nhở lịch hẹn thu gom. |
| **Staff** *(Nhân viên vận hành)* | **Vào** | Nét liền | `Misclassified AI reviews, dataset labels & dispute resolutions` | Thẩm định ảnh AI báo sai, gán nhãn tập dữ liệu huấn luyện, phán quyết giải quyết tranh chấp. |
| | **Ra** | Nét đứt | `Review queues, pending recycler applications & user accounts` | Hàng đợi duyệt AI, danh sách hồ sơ vựa chờ thẩm định, danh sách tài khoản người dùng vi phạm. |
| **Admin** *(Quản trị viên)* | **Vào** | Nét liền | `Master categories, sorting policies, staff permissions & blog approvals` | Quản trị danh mục rác, cấu hình Rule Engine, phân quyền nhân viên Staff, duyệt bài viết Blog. |
| | **Ra** | Nét đứt | `Dashboard analytics, financial reconciliation reports & audit logs` | Thống kê số liệu hệ sinh thái, báo cáo đối soát dòng tiền VietQR, nhật ký kiểm toán bất biến. |

---

### **2. Hệ thống bên ngoài (External Systems)**

| Hệ thống ngoài | Chiều | Kiểu nét | Tên luồng dữ liệu gộp | Chi tiết nghiệp vụ |
| :--- | :---: | :---: | :--- | :--- |
| **Google OAuth 2.0** | **Đi** | Nét liền | `OAuth 2.0 authentication & token verification requests` | Gửi mã ủy quyền / ID token từ client lên Google API để xác minh danh tính. |
| | **Về** | Nét đứt | `Verified user profile, email & ID tokens` | Trả về email đã xác thực, họ tên, avatar và thông tin xác minh. |
| **Cloudflare R2** | **Đi** | Nét liền | `Binary file upload streams (photos, scale proofs, permits)` | Tải lên file nhị phân: ảnh rác quét AI, ảnh chụp cân phế liệu, giấy phép kinh doanh. |
| | **Về** | Nét đứt | `Secure public CDN image URLs & storage status` | Trả về đường dẫn CDN công khai an toàn và trạng thái lưu trữ thành công. |
| **VietQR / PayOS** | **Đi** | Nét liền | `Dynamic VietQR generation requests (order code & amount)` | Yêu cầu tạo mã VietQR động theo đơn mua gói dịch vụ (mã đơn, số tiền chính xác). |
| | **Về** | Nét đứt | `Instant payment success webhook callbacks` | Webhook thông báo giao dịch thành công theo thời gian thực (xử lý Idempotent). |
| **Google SMTP & FCM** | **Đi** | Nét liền | `Email OTP & push notification dispatch payloads` | Lệnh gửi email OTP đặt lại mật khẩu, mã xác nhận và payload push thông báo di động. |
| | **Về** | Nét đứt | `Message transmission & delivery confirmation status` | Trạng thái chuyển phát thư điện tử và thông báo đẩy. |
| **OpenStreetMap / Nominatim** | **Đi** | Nét liền | `Forward / reverse geocoding & tile queries` | Gửi tọa độ GPS hoặc chuỗi địa chỉ để tra cứu bản đồ số. |
| | **Về** | Nét đứt | `Normalized map coordinates & address metadata` | Dữ liệu mảnh bản đồ (Map Tiles) và thông tin địa chỉ đã được chuẩn hóa. |

---

## **IV. SƠ ĐỒ ĐÃ TRIỂN KHAI**

File Draw.io chuẩn XML đã được lưu tại:  
👉 [`ecolink_context_diagram.drawio`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/context%20diagram/ecolink_context_diagram.drawio)

* **Không chứa chú thích XML (`<!-- -->`)** để đảm bảo tương thích tuyệt đối với Draw.io Desktop và Web app.
* Có sẵn **khung Chú giải (Legend)** rõ ràng ở góc dưới bên trái giải thích đầy đủ quy ước DFD Level 0 cho giảng viên và hội đồng chấm thi.
