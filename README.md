# EcoVision AI - Hệ Thống Phân Loại Rác Thông Minh (YOLOv8 Local Demo)

Ứng dụng nhận diện và phân loại rác thải tự động chạy hoàn toàn offline trên máy tính cá nhân bằng mô hình **YOLOv8 Nano (Classification)**, giao diện Web hiện đại (Dark Mode, Glassmorphism, tích hợp Webcam, Preset thử nghiệm nhanh, Bảng quy tắc phân loại, Gamification & Eco-Tracker).

---

## 🚀 Hướng dẫn Chạy trên Laptop (Sau khi Clone)

### 1. Yêu cầu môi trường
- Đã cài đặt **Python 3.10 hoặc 3.11** trên laptop.
- (Tùy chọn) Git để pull code.

### 2. Các bước cài đặt và khởi chạy (3 bước)

Mở terminal (PowerShell hoặc CMD) tại thư mục dự án trên laptop và chạy:

```powershell
# Bước 1: Tạo môi trường ảo
python -m venv venv

# Bước 2: Kích hoạt môi trường ảo
# Trên Windows PowerShell:
.\venv\Scripts\Activate.ps1
# (Hoặc trên Command Prompt CMD: .\venv\Scripts\activate.bat)

# Bước 3: Cài đặt các thư viện cần thiết (Bản nhẹ CPU)
pip install -r requirements.txt --extra-index-url https://download.pytorch.org/whl/cpu

# Bước 4: Khởi chạy ứng dụng Web Demo
python app.py
```

Sau đó mở trình duyệt và truy cập: **`http://127.0.0.1:8000`**

---

## 📦 Cấu trúc Thư mục

- `app.py`: Server FastAPI phục vụ API AI và giao diện Web.
- `models/best.pt`: Mô hình YOLOv8n đã train trên 6 nhóm rác TrashNet (Accuracy > 91%).
- `data/rules.json`: Cơ sở dữ liệu quy tắc phân loại, màu thùng rác và hướng dẫn xử lý.
- `static/`: Giao diện Web (HTML, CSS, JS).
- `data/trashnet_split/val/`: Bộ ảnh mẫu kiểm thử đại diện cho 6 nhóm rác.
