# **Báo Cáo Khảo Sát & UC**

# **Báo cáo Khảo sát AI & Danh sách Usecase: Hệ thống hỗ trợ phân loại và kết nối thu mua rác thải tái chế thông minh**

## **1\. Khảo sát Hướng nghiên cứu AI**

### **1.1. Hướng nghiên cứu AI liên quan đã có chưa?**

Việc ứng dụng Thị giác máy tính (Computer Vision) để phân loại rác (Waste Classification) là một hướng nghiên cứu đã có nền tảng rất vững chắc từ năm 2016 đến nay . Quá trình phát triển đã trải qua nhiều giai đoạn, từ Machine Learning cổ điển (SIFT/HOG \+ SVM), qua Học chuyển giao (Transfer Learning với ResNet/AlexNet), và hiện tại đã đạt hiệu suất rất cao bằng các mô hình phát hiện (Detection) và Transformer tiên tiến .

**Các công trình và sản phẩm tiêu biểu:**

> * **Học thuật:** Các nghiên cứu nổi bật như TrashNet (Yang & Thung, 2016\) đã chứng minh sức mạnh của Transfer Learning. Các nghiên cứu sau đó như TACO (2020) hay ZeroWaste (2022) tiếp tục giải quyết bài toán phức tạp hơn trong thực tế như nhận diện rác bị che khuất, biến dạng, trên đường phố hay băng chuyền công nghiệp .  
> * **Thương mại:** Trên thị trường đã có các sản phẩm thương mại hóa thành công ứng dụng AI phân loại rác như: Oscar AI (màn hình tương tác), CleanRobotics/TrashBot (thùng rác thông minh), Bin-e (smart bin cho văn phòng) và AMP Robotics (robot phân loại trên băng chuyền) . Điều này khẳng định tính thực tiễn và nhu cầu lớn của thị trường.

### **1.2. Cách thức xử lý hiện tại như thế nào?**

Các hệ thống hiện tại chủ yếu sử dụng kiến trúc Mạng nơ-ron tích chập (CNN) kết hợp kỹ thuật **Học chuyển giao (Transfer Learning)** trên các mô hình đã huấn luyện trước (Pretrained Models) thay vì huấn luyện từ đầu .

**Lựa chọn Mô hình (Model Selection):**

Việc lựa chọn mô hình cần cân bằng giữa độ chính xác và tài nguyên xử lý. Các kiến trúc CNN phổ biến được so sánh :

> * **ResNet-50:** Mô hình tiêu chuẩn, giải quyết được bài toán Vanishing Gradient nhờ kiến trúc Residual Block .  
> * **MobileNet (như MobileNetV3-Large):** Rất nhẹ, tốc độ cao, lý tưởng để triển khai trực tiếp trên web hoặc thiết bị di động .  
> * **EfficientNet (đặc biệt là B0):** Đạt sự cân bằng tốt nhất giữa kích thước mô hình (chỉ khoảng 5.3M tham số) và độ chính xác (88-92% cho 10-12 lớp rác), rất phù hợp với môi trường tài nguyên hạn chế .

**Chiến lược Dữ liệu (Dataset) và Xử lý:**

> 1. **Nguồn Dữ liệu:** Tận dụng các dataset công khai chất lượng như Kaggle Garbage 12-class (đa dạng, phù hợp nhất) và TrashNet (tốt cho baseline). Để tăng độ chính xác trong thực tế, nhóm cần thu thập thêm ảnh rác tự chụp (khoảng 40% tổng dữ liệu) tại các môi trường mục tiêu (ví dụ: bãi rác địa phương, căn tin) .  
> 2. **Tăng cường dữ liệu (Data Augmentation):** Rất quan trọng để mô hình không bị quá khớp (overfitting) và tăng tính tổng quát. Áp dụng Color Jitter (thay đổi độ sáng/tương phản), Random Rotation (xoay ngẫu nhiên vì rác không có hướng cố định), và Random Erasing (mô phỏng rác bị che khuất) . Có thể sử dụng thư viện như TorchSharp, ImageSharp hoặc ML.NET để hỗ trợ .  
> 3. **Huấn luyện (Fine-tuning):** Sử dụng quy trình 2 pha: Pha 1 đóng băng (freeze) backbone và chỉ train lớp phân loại cuối; Pha 2 mở khóa (unfreeze) với tốc độ học (learning rate) khác nhau cho từng lớp . Cần sử dụng Focal Loss hoặc Class-Balanced Cross-Entropy để xử lý việc mất cân bằng dữ liệu giữa các lớp rác (ví dụ: ảnh chai nhựa luôn nhiều hơn ảnh pin) .

### **1.3. Tính khả thi đối với nhóm**

Đề tài này **rất khả thi** cho một nhóm sinh viên thực hiện trong 4 tháng, với điều kiện kiểm soát chặt chẽ phạm vi :

> * **Giới hạn phân loại:** Tập trung vào 8–12 loại rác phổ biến (nhựa, giấy, kim loại, thủy tinh, hữu cơ, điện tử...) thay vì cố gắng nhận diện mọi loại rác .  
> * **Kiến trúc an toàn:** AI chỉ đảm nhiệm phần nhận diện hình ảnh (Vision). Kết quả từ AI sẽ được ánh xạ qua Cơ sở dữ liệu Quy tắc (Rule Database) tĩnh để xuất ra hướng dẫn phân loại. Không để AI tự sinh nội dung hướng dẫn để tránh sai lệch chính sách .  
> * **Kiểm soát dữ liệu ngoài phân phối (Out-of-Distribution):** Trong thực tế, AI có thể cố ép một vật thể không phải rác (vd: chiếc lá, xe đạp) vào các nhãn đã học. Nhóm cần xử lý bằng cách đặt **ngưỡng tin cậy (Confidence threshold)** hợp lý. Nếu độ tin cậy thấp (ví dụ \< 60%), hệ thống sẽ báo không chắc chắn và yêu cầu người dùng xác nhận hoặc chụp lại ảnh .  
> * **Đánh giá toàn diện:** Không chỉ dùng Accuracy, việc đánh giá mô hình phải dựa trên F1-score, Precision, Recall và đặc biệt là Ma trận nhầm lẫn (Confusion Matrix) để phân tích chi tiết xem mô hình hay nhầm lẫn giữa các loại rác nào (ví dụ: giấy phẳng vs. bìa carton) .  
> * **Giải pháp Offline Demo:** Mặc dù hệ thống chính gọi API Model Online, nhưng để phòng trường hợp rủi ro mạng khi demo, nhóm nên chuẩn bị một phiên bản **Model chạy Offline**. Giải pháp lý tưởng là sử dụng **MobileNet hoặc YOLO nhẹ** đã được convert sang định dạng TensorFlow.js (để chạy trực tiếp trên trình duyệt) hoặc tflite (nếu demo bằng app di động/desktop). Mô hình này sẽ được nạp sẵn vào bundle của ứng dụng, đảm bảo tính năng nhận diện cốt lõi vẫn hoạt động mượt mà không cần internet.

&nbsp;

## **2\. Tổng hợp chi tiết danh sách dự kiến các Medium Usecase**

Tập trung vào luồng giá trị: **Nhận diện → Hướng dẫn phân loại → Kết nối thu mua/Bàn giao → Tích điểm & Đánh giá**&nbsp;

Dưới đây là danh sách 24 Usecase Medium dự kiến, phân bổ cho 3 nhóm Actor: User, Admin và Đối tác Thu mua.

| STT | Tên Usecase (Chức năng) | Actor (Tác nhân) | Mô tả chi tiết luồng xử lý&nbsp;&nbsp; |
| :---- | :---- | :---- | :---- |
| **Nhóm 1: Nhận diện & Hướng dẫn (AI & Knowledge Base)** |  |  |  |
| 1 | **Nhận diện rác qua ảnh** | User | Gửi ảnh lên hệ thống. AI xử lý, kiểm tra độ tin cậy và trả về Top-3 dự đoán. Nếu độ tin cậy thấp, yêu cầu User chụp lại hoặc chọn thủ công. |
| 2 | **Tra cứu hướng dẫn theo quy tắc (Rule Engine)** | User | Từ kết quả nhận diện, hệ thống ánh xạ loại rác với chính sách đang áp dụng tại khu vực để xuất ra hướng dẫn chi tiết (rửa sạch, tháo nhãn, bỏ đúng thùng). |
| 3 | **Tìm kiếm & Xem chi tiết loại rác** | User | Tra cứu bằng từ khóa văn bản khi không có ảnh. Hệ thống trả về chi tiết thông tin, mức độ độc hại và quy tắc xử lý tương ứng. |
| 4 | **Báo cáo AI nhận diện sai (Feedback)** | User | Gửi báo cáo khi AI dự đoán sai, cung cấp nhãn đúng (Correct Label) để hỗ trợ quá trình cải thiện Dataset và Model sau này. |
| **Nhóm 2: Kết nối Thu mua & Bàn giao** |  |  |  |
| 5 | **Tìm điểm thu mua phù hợp theo vị trí (GPS)** | User | Lọc và sắp xếp các cơ sở thu mua gần nhất có tiếp nhận loại vật liệu vừa được nhận diện, ưu tiên các điểm đang mở cửa. |
| 6 | **Công bố danh mục & điều kiện tiếp nhận** | Đối tác | Cơ sở thu mua thiết lập danh sách các loại rác chấp nhận, giá tham khảo, số lượng tối thiểu và khung giờ hoạt động để hiển thị trên bản đồ. |
| 7 | **Tạo yêu cầu bàn giao rác** | User | Khai báo khối lượng ước tính và chọn thời gian dự kiến mang rác đến cơ sở đã chọn. |
| 8 | **Tiếp nhận/Từ chối yêu cầu bàn giao** | Đối tác | Xem danh sách yêu cầu chờ xử lý, chấp nhận hoặc từ chối (có lý do) dựa trên khả năng tiếp nhận thực tế của cơ sở. |
| 9 | **Xác nhận Biên nhận bàn giao (QR Scan)** | Cả hai | Đối tác nhập khối lượng thực nhận, đơn giá và quét mã QR của User để tạo biên nhận. User kiểm tra trên app, nếu đồng ý thì hệ thống chốt giao dịch. |
| **Nhóm 3: Tích điểm, Đổi thưởng & Gamification** |  |  |  |
| 10 | **Xử lý cấp điểm thưởng giao dịch** | System | Dựa trên biên nhận hoàn tất, hệ thống tự động cộng điểm "đóng góp xanh" (hoặc Mầm cây) cho User, độc lập với tiền thanh toán mua bán vật liệu. |
| 11 | **Đổi điểm lấy quà/voucher** | User | Kiểm tra số dư điểm, đối chiếu kho quà tặng và thực hiện trừ điểm để nhận mã ưu đãi. |
| 12 | **Quản lý Cây ảo & Mở khóa Eco Fact** | User | Dùng "Mầm cây" kiếm được để nâng cấp "Cây ảo". Khi đạt cấp độ nhất định, hệ thống tự động mở khóa các kiến thức môi trường (Eco Fact). |
| 13 | **Đánh giá Đối tác sau bàn giao** | User | Chỉ khi có giao dịch hoàn tất, User mới được đánh giá (sao & nhận xét) về tính minh bạch, thái độ và giá cả của cơ sở thu mua. |
| **Nhóm 4: Quản trị Hệ thống & Dữ liệu (Admin)** |  |  |  |
| 14 | **Quản lý Danh mục Rác & Hướng dẫn** | Admin | Thêm, sửa, gom nhóm các loại rác (Waste Category) và cập nhật nội dung hướng dẫn tĩnh để đảm bảo dữ liệu Knowledge Base chính xác. |
| 15 | **Thiết lập Chính sách Phân loại (Sorting Policy)** | Admin | Tạo và cấu hình các quy tắc xử lý (Rule) linh hoạt thay đổi theo từng khu vực địa lý hoặc theo tổ chức cụ thể. |
| 16 | **Thẩm định Hồ sơ Đối tác Thu mua** | Admin | Kiểm tra thông tin đăng ký của cơ sở (người thu mua cá nhân hoặc cửa hàng), phê duyệt để cấp quyền xuất hiện trên bản đồ tìm kiếm. |
| 17 | **Xử lý Feedback Báo cáo Nhận diện Sai** | Admin | Review hình ảnh bị nhận diện sai do User gửi, xác nhận nhãn chuẩn (Correct Label) để đưa vào tập dữ liệu chờ duyệt. |
| 18 | **Quản lý Tập dữ liệu Kiểm chứng (Curate Verified Dataset)** | Admin | Chuyển các ảnh phản hồi đã xác minh thành mẫu dữ liệu sạch, bổ sung vào Dataset Version để chuẩn bị huấn luyện lại mô hình. |
| 19 | **Quản lý Vòng đời Mô hình AI (Model Lifecycle)** | Admin | Lưu vết các phiên bản mô hình, đánh giá chỉ số (Accuracy, F1-score), so sánh hiệu suất và kích hoạt (Deploy) phiên bản tốt nhất ra môi trường Production. |
| 20 | **Thiết lập Cấu hình Gamification & Phần thưởng** | Admin | Định nghĩa mức quy đổi vật liệu ra điểm, cấu hình ngân sách quà tặng, số điểm cần để nâng cấp cây và quản lý ngân hàng câu hỏi Eco Fact. |
| 21 | **Giải quyết Khiếu nại Giao dịch** | Admin | Tiếp nhận tranh chấp giữa User và Đối tác về khối lượng/giá cả, đối chiếu chứng từ biên nhận để ra quyết định điều chỉnh lịch sử điểm. |
| 22 | **Xem Thống kê Hệ thống (Dashboard Analytics)** | Admin | Theo dõi tỷ lệ phân loại thành công, đo lường độ chính xác của AI theo từng nhóm rác, và thống kê tổng lượng thu gom từ các đối tác. |
| 23 | **Quản lý Bảng xếp hạng Đóng góp (Leaderboard)** | Admin | Hệ thống tự động tính toán tổng khối lượng đóng góp hợp lệ theo kỳ (tháng/quý) để xếp hạng User và xếp hạng Chất lượng/Khối lượng cho Đối tác. |
| 24 | **Xuất Báo cáo Dữ liệu (Export Report)** | Admin | Trích xuất các báo cáo dưới dạng Excel/CSV về hoạt động thu gom, dữ liệu nhận diện sai phục vụ cho việc nộp báo cáo đồ án và đánh giá dự án. |

&nbsp;

# **Link Tài liệu tìm hiểu và Demo**

# **Tài Liệu Tham Khảo**

[Intelligent Waste Classification System Using Deep Learning Convolutional Neural Network](https://drive.google.com/file/d/1CZQ1LoDOYVzYbhA31zLQ4c9SgUm6YKhr/view?usp=sharing)

[Waste Classification for Sustainable Development Using Image Recognition with Deep Learning Neural Network Models](https://drive.google.com/file/d/13BUjzhUetastGsxKfaxoM-V4rR0J_-Lt/view?usp=sharing)

&nbsp;

# **Bản Demo về AI**

[TrashNet Computer Vision Dataset](https://universe.roboflow.com/trash-recognition-fixed/trashnet-blkh7)

[Model Online Demonstration](https://colab.research.google.com/drive/1TT-YAao0txXLzBlrU8dA4Y-DyVq-kjxz?usp=sharing)

# **Đặt tên Dự án**

Tên tiếng Việt: Hệ thống hỗ trợ phân loại và kết nối thu mua rác thải tái chế thông minh

Tên tiếng Anh: Intelligent Recyclable Waste

Classification and Trade Matching System.&nbsp;

Tên viết tắt: EcoLink