# **Báo cáo Đề xuất Triển khai AI & Cấu trúc Dự án (Dành cho Local/Offline)**

&nbsp;

## **1\. Khảo sát & Đề xuất Model AI chạy Offline**

Để đáp ứng yêu cầu một hệ thống nhận diện rác thải hoạt động độc lập (offline) chạy trên Local (Web hoặc Desktop), không phụ thuộc vào internet, đồng thời đảm bảo tốc độ phản hồi nhanh để demo ngay trong tối nay, dưới đây là những đề xuất cụ thể:

### **1.1. Lựa chọn Model: YOLOv8 (phiên bản nano/small) hoặc MobileNetV2**

Các nghiên cứu hiện tại và nhiều dự án mã nguồn mở (ví dụ như ứng dụng TrashBestie) đều chỉ ra rằng **YOLOv8** mang lại sự cân bằng hoàn hảo giữa tốc độ và độ chính xác \[cite: google\]. Tuy nhiên, để chạy offline trực tiếp trên trình duyệt Web (Client-side) thông qua **TensorFlow.js (tfjs)**, bạn nên xem xét:

> * **YOLOv8n (Nano) đã convert sang tfjs:** Rất nhẹ, phát hiện nhiều vật thể cùng lúc, nhận diện chính xác kể cả với các góc chụp khó.  
> * **MobileNetV2:** Đây là mạng classification cực nhẹ, chỉ yêu cầu vài MB bộ nhớ, hoàn toàn có thể chạy mượt mà ngay cả trên thiết bị phần cứng yếu mà không cần GPU chuyên dụng.

### **1.2. Đề xuất Dataset**

Vì thời gian gấp rút, bạn không nên tự train từ đầu mà hãy tận dụng các Dataset có sẵn, đã được gán nhãn sẵn cho YOLO format trên các nền tảng mở:

> * **Roboflow Universe:** Tìm kiếm các dataset như "Garbage Classification YOLOv8". Roboflow cho phép export thẳng ra định dạng YOLOv8 hoặc TensorFlow.js (có sẵn cấu trúc thư mục /train, /val, /test) \[cite: google\].  
> * **Kaggle Dataset:** Sử dụng "TrashNet" hoặc các dataset 5-6 class cơ bản (Plastic, Paper, Glass, Metal, Organic, Trash) để demo nhanh sự khác biệt giữa các luồng xử lý.

### **1.3. Kiến trúc hệ thống chạy Offline**

Thay vì mô hình Client \-\> Server (API) \-\> AI Model, ta sẽ đẩy Model về phía Client (tích hợp thẳng vào ứng dụng \- Bundle). Luồng xử lý như sau:

> 1. Người dùng tải ảnh/mở webcam trên giao diện Web (React/HTML tĩnh).  
> 2. Thư viện **TensorFlow.js (đã được import dạng thẻ script hoặc npm install)** sẽ load file Model tĩnh (các file model.json và các tệp .bin trọng số) trực tiếp từ thư mục /public hoặc /assets của chính trang web đó.  
> 3. Quá trình Inference (suy luận) diễn ra hoàn toàn bằng CPU/WebGL của máy tính cá nhân.  
> 4. Kết quả (Bounding box hoặc Tên nhãn \+ Confidence Score) được JavaScript bắt lấy và đối chiếu với **Database Quy tắc tĩnh (dạng file JSON nội bộ)** để render ra hướng dẫn xử lý rác.

**Công cụ cần thiết (Tech Stack):** Frontend: ReactJS hoặc HTML/JS thuần; AI Engine: @tensorflow/tfjs; Để convert model (nếu cần): ultralytics (Python) export ra tfjs.

&nbsp;

## **2\. Tổng hợp chi tiết danh sách dự kiến các Medium Usecase**

Dựa trên các ý kiến đóng góp độc đáo của thành viên và phân tích kỹ lưỡng \[cite: 11, 12\], dưới đây là danh sách 12 Usecase (chức năng) Medium, bao gồm luồng Người dùng (có yếu tố Eco-Tracker, Gamification) \[cite: google\] và luồng Quản trị (kiểm soát AI theo yêu cầu của Thầy) \[cite: 11\].

| STT | Tên Usecase (Chức năng) | Actor (Tác nhân) | Mô tả chi tiết luồng xử lý&nbsp;&nbsp; |
| :---- | :---- | :---- | :---- |
| **Nhóm 1: Nhận diện hình ảnh cục bộ (Offline Vision)** |  |  |  |
| 1 | **Nhận diện rác bằng hình ảnh (Offline)** | User | Người dùng chọn ảnh từ máy. Model TFJS phân tích ngay trên trình duyệt mà không cần mạng. Trả về kết quả loại rác và độ tự tin (Confidence). |
| 2 | **Kiểm soát ngoại lai (Out-of-Distribution Handling)** | System | Hệ thống chặn các kết quả có Confidence \< 60% hoặc các vật thể không thuộc database, hiện thông báo "Vui lòng chụp lại hình ảnh" thay vì đoán mò. |
| 3 | **Tra cứu hướng dẫn phân loại (Rule-based)** | User | Sau khi nhận diện thành công, hệ thống mapping nhãn rác với file dữ liệu JSON/LocalDB để hiển thị cách xử lý (rửa sạch, bỏ thùng tái chế, v.v.). |
| 4 | **Báo cáo kết quả nhận diện sai (Feedback)** | User | Nếu AI dự đoán sai, User có quyền chọn lại nhãn đúng và gửi báo cáo (chức năng này sẽ được đồng bộ lên Server khi có mạng). |
| **Nhóm 2: Tương tác, Eco Tracker & Gamification** |  |  |  |
| 5 | **Lưu danh mục rác yêu thích (Bookmark)** | User | Người dùng đánh dấu các loại rác thường vứt (ví dụ: vỏ hộp sữa, chai nhựa) để tra cứu lại hướng dẫn nhanh mà không cần chụp ảnh. |
| 6 | **Theo dõi chỉ số sống xanh (User Eco Tracker)** | User | Thống kê tổng số lượng rác đã nhận diện và phân loại đúng trong tuần/tháng. Hiển thị dạng biểu đồ cá nhân. |
| 7 | **Hệ thống Tích điểm (Gamification \- Điểm xanh)** | System | Cộng "Điểm Xanh" (hoặc huy hiệu) khi User quét rác thành công nhiều ngày liên tiếp. Điểm này có thể dùng để mở khóa các "Eco Fact" (Kiến thức môi trường). |
| 8 | **Đề xuất Điểm Thu mua gần nhất** | User | Gợi ý các bãi phế liệu hoặc thùng rác thông minh dựa trên vị trí địa lý của người dùng (nếu có kết nối mạng). |
| **Nhóm 3: Quản trị Hệ thống, Dữ liệu & AI Control** |  |  |  |
| 9 | **Quản lý Danh mục rác & Quy tắc tĩnh** | Admin | Thêm, sửa, xóa các nhóm rác và cập nhật nội dung hướng dẫn để đảm bảo cơ sở dữ liệu (Rule Engine) luôn chính xác, tách bạch khỏi AI. |
| 10 | **Xem Thống kê Dashboard (Analytics)** | Admin | Theo dõi biểu đồ tổng lượt quét, tỷ lệ rác nhựa vs rác hữu cơ, và thống kê các loại rác AI nhận diện kém (dựa trên Feedback). |
| 11 | **Quản lý Dữ liệu kiểm chứng (Verified Dataset)** | Admin | Tiếp nhận Feedback báo lỗi từ người dùng, lọc bỏ các ảnh rác/spam, và đánh nhãn lại ảnh đúng để chuẩn bị cho việc train lại model trong tương lai. |
| 12 | **Thiết lập Cấu hình Gamification** | Admin | Quản lý và điều chỉnh các mốc điểm xanh, mức độ phần thưởng, thêm mới các "Kiến thức sống xanh" (Eco Fact) để tăng tương tác với User. |

&nbsp;