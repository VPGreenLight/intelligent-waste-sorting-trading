# **HƯỚNG DẪN SỬ DỤNG VÀ TRA CỨU CƠ SỞ DỮ LIỆU ECOLINK (DBDIAGRAM.IO)**

> **Hệ quản trị CSDL:** PostgreSQL 16+ với phần mở rộng **PostGIS**  
> **Định dạng sơ đồ:** DBML (Database Markup Language) tương thích 100% với [dbdiagram.io](https://dbdiagram.io)  
> **Thư mục lưu trữ:** `documents/diagram/database design/`  

---

## **1. DANH SÁCH FILE VÀ CẤU TRÚC PHÂN CHIA**

Hệ thống cơ sở dữ liệu EcoLink gồm **33 bảng**, **17 Enums** và được chia thành **1 file Master tổng hợp** cùng **7 file Module luồng nghiệp vụ riêng biệt** để dễ dàng quan sát, báo cáo và import vào `dbdiagram.io`:

| File | Tên phân hệ / Luồng nghiệp vụ | Số bảng | Mô tả / Mục đích sử dụng |
| :--- | :--- | :---: | :--- |
| 📄 [`ecolink_master_schema.dbml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/database%20design/ecolink_master_schema.dbml) | **Toàn bộ hệ thống (Master Schema)** | **33 bảng** | Bản đồ ERD tổng thể toàn dự án với 7 nhóm TableGroup phân màu trực quan. |
| 📄 [`01_auth_account_module.dbml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/database%20design/01_auth_account_module.dbml) | **Tài khoản & Phân quyền (Auth)** | 5 bảng | `User`, `RecyclerProfile` (PostGIS), `StaffProfile`, `StaffPermissionAssignment`, `PasswordResetToken`. |
| 📄 [`02_ai_waste_classification_module.dbml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/database%20design/02_ai_waste_classification_module.dbml) | **AI Scanner & Danh mục rác** | 6 bảng | `MasterWasteCategory`, `SortingGuidanceRule`, `AIScanHistory`, `AIScanDetection` (YOLOv8), `MisclassifiedReport`, `VerifiedDataset`. |
| 📄 [`03_collection_trading_module.dbml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/database%20design/03_collection_trading_module.dbml) | **Thu gom, Thương lượng & Bàn giao** | 7 bảng | `RecyclerPriceList`, `HandoverRequest`, `HandoverRequestItem`, `ChatMessage` (SignalR), `HandoverReceipt`, `TransactionReview`, `TransactionComplaint`. |
| 📄 [`04_payment_service_module.dbml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/database%20design/04_payment_service_module.dbml) | **Gói dịch vụ & Thanh toán VietQR** | 2 bảng | `ServicePackage`, `ServiceOrder` (VietQR động EMVCo & PayOS Webhook đối soát). |
| 📄 [`05_gamification_rewards_module.dbml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/database%20design/05_gamification_rewards_module.dbml) | **Gamification & Phần thưởng** | 7 bảng | `UserPointWallet`, `PointTransactionHistory`, `DailyCheckinLog` (Streak), `VirtualTreeProgress`, `GiftInventory`, `GiftRedemption`, `EcoFact`. |
| 📄 [`06_survey_feedback_module.dbml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/database%20design/06_survey_feedback_module.dbml) | **Khảo sát chất lượng dịch vụ** | 3 bảng | `Survey`, `SurveyQuestion`, `SurveyResponse` (Phục vụ tổng hợp dữ liệu ẩn danh BR-RVW-04/05). |
| 📄 [`07_content_admin_audit_module.dbml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/database%20design/07_content_admin_audit_module.dbml) | **Nội dung, Thông báo & Kiểm toán** | 3 bảng | `BlogPost` (Staff viết, Admin duyệt), `Notification` (Web & Mobile FCM), `AuditLog` (Dấu vết kiểm toán bất biến). |
| 📘 [`Table design.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/database%20design/Table%20design.md) | **Tài liệu đặc tả chi tiết Schema** | - | Bảng diễn giải chi tiết kiểu dữ liệu, ràng buộc, business rules và chiến lược đánh index SQL. |

---

## **2. CÁCH IMPORT VÀ VẼ SƠ ĐỒ TRÊN DBDATAGRAM.IO**

### **Bước 1: Mở trình soạn thảo dbdiagram.io**
1. Mở trình duyệt và truy cập: [https://dbdiagram.io/d](https://dbdiagram.io/d).
2. Tạo một diagram mới (**New Diagram**).

### **Bước 2: Sử dụng các file tương ứng**
- **Để xem tổng thể toàn bộ hệ sinh thái EcoLink:**  
  Mở file [`ecolink_master_schema.dbml`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/database%20design/ecolink_master_schema.dbml) $\rightarrow$ Copy toàn bộ nội dung $\rightarrow$ Dán đè vào khung soạn thảo bên trái của `dbdiagram.io`. Sơ đồ sẽ tự động hiển thị 33 bảng chia theo 7 cụm màu (TableGroup).
- **Để xem riêng từng luồng chi tiết phục vụ báo cáo / slide thuyết trình:**  
  Mở từng file tương ứng từ `01` đến `07` $\rightarrow$ Copy và dán vào `dbdiagram.io`.  
  *(Lưu ý: Các file module đã được thiết kế kèm các bảng phụ Stub để đảm bảo sơ đồ độc lập, hiển thị rõ liên kết ra bên ngoài mà không hề bị báo lỗi cú pháp)*.

### **Bước 3: Xuất tài liệu từ dbdiagram.io**
- Bấm nút **Export** ở thanh công cụ trên cùng:
  - Chọn **Export to PNG / PDF** để chèn vào báo cáo môn học Capstone.
  - Chọn **Export to PostgreSQL** để tự động sinh mã DDL DDL `CREATE TABLE`, `CREATE TYPE`, `ALTER TABLE ADD CONSTRAINT` khi triển khai database thực tế.
