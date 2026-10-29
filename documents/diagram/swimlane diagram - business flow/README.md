# TÀI LIỆU QUẢN LÝ SƠ ĐỒ LUỒNG NGHIỆP VỤ PHÂN LÀN (SWIMLANE BUSINESS FLOW DIAGRAMS)

**Dự án:** EcoLink – Nền tảng Phân loại Rác Thông minh & Kết nối Thu mua Phế liệu  
*(Intelligent Recyclable Waste Classification and Trade Matching Platform)*  
**Mã tài liệu:** ECO-DOC-BF-01  
**Phiên bản:** 2.0.0 (Chuẩn hóa toàn diện theo góp ý Hội đồng bảo vệ & Giảng viên phản biện)  
**Căn cứ thiết kế:**
* Báo cáo đánh giá: [`documents/Báo cáo Đánh giá và Chuẩn hóa Luồng Nghiệp vụ Business Flow.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/B%C3%A1o%20c%C3%A1o%20%C4%90%C3%A1nh%20gi%C3%A1%20v%C3%A0%20Chu%E1%BA%A9n%20h%C3%B3a%20Lu%E1%BB%93ng%20Nghi%E1%BB%87p%20v%E1%BB%A5%20Business%20Flow.md)
* Kế hoạch triển khai: [`documents/implementation_plan/business_flow_diagrams_implementation_plan.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/implementation_plan/business_flow_diagrams_implementation_plan.md)
* Biên bản góp ý Hội đồng: [`documents/Note Hội Đồng.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/Note%20H%E1%BB%99i%20%C4%90%E1%BB%93ng.md)
* Biên bản phản biện: [`documents/Note Review.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/Note%20Review.md) (Thầy Kiên & Thầy Giang)
* Quy tắc nghiệp vụ: [`documents/business_rule_&_system_message.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/business_rule_&_system_message.md)

---

## 📌 1. DANH MỤC CÁC SƠ ĐỒ TRONG THƯ MỤC

Thư mục này quản lý toàn bộ các sơ đồ Swimlane Activity Diagram biểu diễn luồng nghiệp vụ mức User Requirement của hệ thống EcoLink dưới định dạng chuẩn **Draw.io XML (`.xml`, `.drawio`)** để mở, tùy chỉnh và xuất slide trên [app.diagrams.net](https://app.diagrams.net).

> [!TIP]
> **File All-in-One tiện lợi:** File [`ecolink_all_business_flows.drawio.xml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/ecolink_all_business_flows.drawio.xml) (hoặc [`ecolink_all_business_flows.drawio`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/ecolink_all_business_flows.drawio)) chứa **đầy đủ cả 7 sơ đồ phân thành 7 Tabs/Trang** riêng biệt. Bạn chỉ cần mở 1 file duy nhất là có thể xem và trình chiếu toàn bộ các luồng!

| Mã sơ đồ | File Draw.io XML (`.xml`) | Tên luồng nghiệp vụ chuẩn hóa | Tác nhân tham gia (Swimlanes) |
| :---: | :--- | :--- | :--- |
| **BF-00** | [`00_macro_e2e_business_value_chain.xml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/00_macro_e2e_business_value_chain.xml) | **Chuỗi Giá Trị Nghiệp Vụ Tổng Thể Khép Kín (Macro E2E)** | Citizen, Recycler, Core System, AI Service, Staff, MLOps |
| **BF-01** | [`01_user_registration_and_onboarding_flow.xml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/01_user_registration_and_onboarding_flow.xml) | **Đăng ký Tài khoản Công dân & Welcome Onboarding** | Guest / Citizen, EcoLink System, Gmail SMTP Service |
| **BF-02** | [`02_recycler_registration_and_depot_setup_flow.xml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/02_recycler_registration_and_depot_setup_flow.xml) | **Đăng ký, Kiểm duyệt & Cấu hình Kho bãi Recycler** | Recycler Candidate, EcoLink System, Staff, External Mail |
| **BF-03** | [`03_ai_waste_classification_and_sorting_guide_flow.xml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/03_ai_waste_classification_and_sorting_guide_flow.xml) | **Phân loại Rác Hai Giai đoạn & Hướng dẫn Phân loại** | User / Citizen, EcoLink Core System, FastAPI AI Service |
| **BF-04** | [`04_recycler_matching_negotiation_and_handover_flow.xml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/04_recycler_matching_negotiation_and_handover_flow.xml) | **Tìm kiếm Cơ sở Thu mua & Bàn giao Phế liệu QR** | User, EcoLink System, Recycler, Eco-Staff |
| **BF-05** | [`05_gamification_green_points_and_reward_redemption_flow.xml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/05_gamification_green_points_and_reward_redemption_flow.xml) | **Tích lũy Eco Point (Điểm danh & Ads) và Đổi Quà Cửa Hàng** | User, EcoLink System |
| **BF-06** | [`06_ai_active_learning_and_batch_retraining_flow.xml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/06_ai_active_learning_and_batch_retraining_flow.xml) | **Chu trình Bán Tự Động Thu Thập Phản Hồi & Cập Nhật Mô Hình AI** | User, EcoLink System, Eco-Staff, AI Service |

---

## 🎯 2. ĐỊNH HƯỚNG NGHIỆP VỤ THỰC TẾ & CHUẨN HÓA HỆ THỐNG

### 2.1. Tách biệt Luồng Kết Nối Thu Mua (BF-04) và Không cộng Eco Point từ Giao dịch
* **Tách riêng hoàn toàn với phân loại rác:** Người dùng chủ động xác nhận thông tin rác, chọn bán kính tìm kiếm lân cận trên bản đồ, không lấy danh mục rác tự động từ phân loại AI.
* **Hoàn thành giao dịch chỉ log lịch sử:** Hệ thống ghi nhận khối lượng thu gom và lưu vết giao dịch (`Audit Log`). **Không cộng điểm Eco Point** tại bước này nhằm tránh gian lận khối lượng và giữ đúng mô hình kinh tế phế liệu (người bán nhận tiền mặt/chuyển khoản từ bên thu mua).

### 2.2. Cơ chế Kiếm Điểm Eco Point và Đổi Quà Cửa Hàng (BF-05)
* **Kênh kiếm Eco Point hợp lệ:**
  1. *Điểm danh hàng ngày (Daily Check-in):* Điểm thưởng mỗi ngày kèm chuỗi ngày liên tiếp (`Streak Bonus`).
  2. *Xem video quảng cáo (Watch Ads):* Có giới hạn số lượt xem tối đa trong ngày (`Daily Ad Limit`) để kiểm soát lạm phát điểm.
* **Đổi quà / Voucher Cửa hàng:** Kiểm tra nguyên tử (`Atomic Check`) số dư Eco Point và số lượng tồn kho quà tặng, trừ điểm và sinh mã voucher điện tử lưu vào ví người dùng.

### 2.3. Chu trình Cập nhật Mô hình AI Bán Tự Động (BF-06)
* **Tách biệt xử lý ngoại tuyến:** Quá trình huấn luyện mô hình sâu (Fine-tuning YOLO/PyTorch trên GPU server) diễn ra ngoại tuyến ngoài hệ thống phần mềm EcoLink.
* **Các luồng chạy trực tiếp trong hệ thống EcoLink:**
  1. *Thu thập phản hồi:* Người dùng báo sai kết quả phân loại, hệ thống lưu vào hàng đợi.
  2. *Kiểm duyệt & Gán nhãn:* Staff lọc ảnh rác, loại bỏ ảnh mờ/spam, gán nhãn chuẩn hóa lưu vào kho dữ liệu.
  3. *Xuất dataset:* Staff yêu cầu xuất gói dữ liệu huấn luyện (dạng chuẩn COCO/YOLO) và tải về máy.
  4. *Upload & Hot-reload:* Sau khi huấn luyện ngoại tuyến hoàn tất, Staff upload model mới lên hệ thống; hệ thống xác thực tệp (checksum, định dạng) và gửi tín hiệu Hot-reload nạp model mới vào RAM của AI Service mà không cần khởi động lại dịch vụ.

---

## 🛠️ 3. HƯỚNG DẪN MỞ VÀ TRÌNH BÀY TRÊN DRAW.IO (DIAGRAM OPEN & PRESENTATION)

### 3.1. Mở trực tiếp các file `.xml` / `.drawio` trên Draw.io
1. **Truy cập Draw.io:** Mở ứng dụng Draw.io Desktop hoặc truy cập web: [app.diagrams.net](https://app.diagrams.net).
2. **Mở file All-in-One (Khuyên dùng):**
   * Chọn `File` $\rightarrow$ `Open From` $\rightarrow$ `Device...`
   * Chọn file [`ecolink_all_business_flows.drawio.xml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/ecolink_all_business_flows.drawio.xml) hoặc [`ecolink_all_business_flows.drawio`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/swimlane%20diagram%20-%20business%20flow/ecolink_all_business_flows.drawio).
   * Phía dưới màn hình sẽ hiển thị thanh công cụ gồm **7 Tabs riêng biệt** (`BF-00` đến `BF-06`). Bạn có thể nhấp chuyển tab để trình chiếu từng luồng một cách mượt mà và trực quan!
3. **Mở file đơn lẻ:**
   * Bạn cũng có thể mở từng file `.xml` độc lập (`00_...xml` đến `06_...xml`) nếu muốn chỉnh sửa hoặc xuất ảnh riêng cho từng mục của báo cáo.
4. **Tùy chỉnh & Xuất ảnh làm slide:**
   * Các khối hình, màu sắc và mũi tên đều là shape vector nguyên bản của Draw.io, cho phép bạn tự do kéo thả, đổi font chữ, thu phóng không vỡ hạt.
   * Để xuất ảnh vào PowerPoint hoặc Word: Chọn `File` $\rightarrow$ `Export as` $\rightarrow$ `PNG...` (chọn DPI 300 để in ấn sắc nét) hoặc `SVG...` (vector cho slide thuyết trình).

### 3.2. Xem file Mermaid (`.mmd`)
* **Trong VS Code:** Cài extension `Markdown Preview Mermaid Support` hoặc `Mermaid Previewer` để xem trực tiếp code `.mmd`.
* **Trực tuyến:** Copy code `.mmd` dán vào [Mermaid Live Editor](https://mermaid.live).

