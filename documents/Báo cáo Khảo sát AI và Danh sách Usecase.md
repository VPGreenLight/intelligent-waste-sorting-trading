**Hệ thống nhận diện loại rác và hướng dẫn phân loại** 

# **1\. Tên đề tài**

### Tiếng Việt: **Hệ thống nhận diện và hỗ trợ phân loại rác bằng hình ảnh**

### Tiếng Anh: **Image-Based Waste Classification and Sorting Guidance System**

# **2\. Lý do chọn đề tài**

Việc phân loại rác tại nguồn thường gặp một vấn đề khá đơn giản nhưng thực tế:

> **Người dùng không chắc một loại rác nên được bỏ vào đâu.**

Ví dụ:

* chai nhựa → thùng nhựa?  
* hộp xốp → rác tái chế hay rác khác?  
* lon nước → kim loại?  
* hộp giấy có dính thức ăn → giấy hay rác khác?  
* pin → không được bỏ chung với rác thông thường.

Nếu chỉ có các thùng:

> **Hữu cơ | Tái chế | Khác**

thì người dùng vẫn phải tự quyết định.

Đề tài xây dựng một hệ thống giúp:

> **chụp/upload ảnh rác → hệ thống nhận diện → đưa ra hướng dẫn phân loại.**

# **3\. Vấn đề mà hệ thống giải quyết**

**Làm thế nào để hỗ trợ người dùng xác định loại rác từ hình ảnh và lựa chọn phương án phân loại phù hợp?**

Có 2 vấn đề chính.

### **Vấn đề 1 — Không biết rác thuộc loại nào**

Người dùng nhìn một vật thể và không biết:

> Đây là nhựa, giấy, kim loại hay rác khác?

### **Vấn đề 2 — Biết vật thể nhưng không biết xử lý thế nào**

Ví dụ:

> “Đây là chai nhựa.”

nhưng vẫn cần biết:

> Có cần rửa không? Có thể tái chế không? Bỏ vào thùng nào?

Do đó hệ thống không chỉ **nhận diện**, mà còn có phần **hướng dẫn phân loại**.

# **4\. Ý tưởng sản phẩm**

Workflow chính:

Người dùng  
    ↓  
Upload / chụp ảnh  
    ↓  
AI phân tích hình ảnh  
    ↓  
Nhận diện loại rác  
    ↓  
Tra cứu quy tắc phân loại  
    ↓  
Hướng dẫn xử lý

**Ví dụ:**

> Người dùng chụp một chai nước.

Hệ thống trả:

Loại vật thể: Chai nhựa  
Nhóm: Nhựa  
Khả năng tái chế: Có  
Hướng dẫn:  
1\. Đổ hết chất lỏng  
2\. Rửa sạch  
3\. Bỏ vào thùng rác tái chế

# **5\. Actor**

## **User**

Có thể:

* nhận diện rác  
* xem hướng dẫn  
* tìm kiếm loại rác  
* xem lịch sử  
* báo cáo kết quả nhận diện sai.

## **Admin**

Có thể:

* quản lý loại rác  
* quản lý hướng dẫn  
* quản lý dữ liệu  
* xem thống kê  
* xử lý báo cáo.

# **6\. Chức năng chính**

## **Module 1 — Đăng ký / đăng nhập**

User:

* Register  
* Login  
* Logout  
* Profile.

# **Module 2 — Nhận diện rác bằng hình ảnh**

Đây là **chức năng AI trung tâm**.

### **Input**

Người dùng:

> Upload ảnh.

**Ví dụ:**

\[Ảnh chai nhựa\]

### **AI xử lý**

Model phân tích hình ảnh.

### **Output**

Kết quả nhận diện

Chai nhựa

Độ tin cậy:  
94%

Có thể trả về top-3:

1\. Plastic bottle   94%  
2\. Glass bottle      4%  
3\. Other             2%

# **Module 3 — Hướng dẫn phân loại**

Sau khi AI nhận diện: Plastic Bottle

hệ thống **không để AI tự viết hướng dẫn**.

Thay vào đó:

AI  
 ↓  
Loại rác  
 ↓  
Database quy tắc  
 ↓  
Hướng dẫn

**Ví dụ:**

Plastic Bottle  
Category: Recyclable  
Bin: Recycling  
Instructions:  
✓ Empty liquid  
✓ Rinse if necessary  
✓ Put into recycling bin

Điều này giúp hệ thống ổn định hơn.

# **Đây là điểm rất quan trọng về mặt thiết kế AI**

Không nên để ChatGPT/LLM quyết định: “Vật này phải bỏ vào thùng nào.”

Vì quy định phân loại rác có thể thay đổi theo:

* địa phương  
* đơn vị xử lý  
* loại rác  
* chính sách.

Tốt hơn là:

### **AI làm:**

> **Image → Object/Waste Category**

### **Database/Rule Engine làm:**

> **Waste Category → Disposal Instruction**

Kiến trúc:

                Image  
                   ↓  
             Vision Model  
                   ↓  
          "Plastic Bottle"  
                   ↓  
             Rule Database  
                   ↓  
        Sorting Recommendation

Như vậy AI chỉ giải quyết **bài toán nhận diện**, còn phần hướng dẫn có thể kiểm soát được.

# **Module 4 — Tìm kiếm loại rác**

Ngoài camera/image, người dùng có thể tìm:

"pin"

hoặc:

"chai nhựa"

Hệ thống trả:

Tên:  
Chai nhựa PET  
Nhóm: Nhựa tái chế

Thùng: Recycling

Hướng dẫn:  
...

Điều này rất hữu ích khi người dùng **không có ảnh**.

# **Module 5 — Chi tiết loại rác**

Mỗi loại rác có một trang riêng.

Ví dụ:

## **Chai nhựa**

Category: Plastic  
Recyclable: Yes  
Bin: Recycling

### **Hướng dẫn**

1\. Đổ chất lỏng  
2\. Làm sạch  
3\. Tháo phần không cần thiết nếu có  
4\. Đưa vào thùng tái chế

### **Lưu ý:** Không bỏ chai còn chứa chất lỏng.

# **Module 6 — Lịch sử nhận diện**

User có thể xem:

Lịch sử  
10/09  
Plastic Bottle  
94%

09/09  
Aluminum Can  
91%

08/09  
Cardboard  
96%

Click vào từng kết quả để xem lại hướng dẫn.

# **Module 7 — Báo cáo kết quả sai**

Đây là chức năng tôi khuyên **nên có**.

Ví dụ AI nhận:

> Plastic Bottle — 89%

nhưng thực tế là:

> Glass Bottle.

User có thể chọn:

\[Report incorrect result\]

và chọn:

Correct category:  
Glass

Sau đó:

AI Result:  
Plastic

User Feedback:  
Glass

Admin có thể kiểm tra.

# **Module 8 — Dashboard Admin**

Admin có thể xem:

Total analyses: 2,350

Plastic: 920  
Paper: 540  
Metal: 380  
Glass: 270  
Other: 240

Có thể thống kê:

* loại rác phổ biến  
* số lượt nhận diện  
* tỷ lệ kết quả bị báo sai  
* loại rác AI nhận diện kém.

**Ví dụ:**

Recognition Accuracy  
Plastic       94%  
Paper         91%  
Metal         96%  
Glass         88%  
Other         72%

Điều này rất hữu ích cho phần đánh giá đồ án.

# **7\. AI nên sử dụng thế nào?**

**Không nên tự xây model Computer Vision từ đầu**.

Có thể sử dụng:

* MobileNet  
* ResNet  
* EfficientNet  
* YOLO nếu muốn detection  
* hoặc các pretrained vision model phù hợp.

Sau đó fine-tune trên dataset nhỏ.

Pipeline:

Image  
 ↓  
Preprocessing  
 ↓  
Pretrained Model  
 ↓  
Classification  
 ↓  
Waste Category

# **8\. Phân loại ở mức nào?**

### **Không nên:**

> Nhận diện tất cả mọi loại rác.

Phạm vi quá lớn.

### **Nên giới hạn:**

Ví dụ **8–12 loại**:

1\. Plastic bottle  
2\. Plastic container  
3\. Paper  
4\. Cardboard  
5\. Glass bottle  
6\. Aluminum can  
7\. Food waste  
8\. Battery  
9\. Electronic waste  
10\. Other

Thậm chí **5–7 nhóm rác** là đủ.

# **9\. Dataset**

Có thể sử dụng dataset công khai về waste classification làm nền tảng.

Sau đó nhóm có thể bổ sung một dataset nhỏ của riêng mình.

**Ví dụ:**

Plastic Bottle     	500 images  
Paper               	500  
Cardboard           	400  
Glass               	400  
Metal               	400  
Food                	400  
Other               	500

Tổng khoảng:

> 3.000–4.000 ảnh

đã có thể đủ cho một đồ án nếu sử dụng transfer learning và phạm vi được kiểm soát.

# **10\. Đánh giá AI**

Không thể chỉ nói:

> “Model nhận diện được.”

Cần có đánh giá.

Có thể dùng:

* Accuracy  
* Precision  
* Recall  
* F1-score  
* Confusion Matrix.

**Ví dụ:**

Accuracy: 91.5%  
Precision: 90.8%  
Recall: 89.7%  
F1-score: 90.2%

# **11\. Có thể so sánh nhiều model**

Đây là một phần khá đẹp trong báo cáo.

Ví dụ:

| Model | Accuracy | F1 |
| ----- | ----- | ----- |
| MobileNet | 88.2% | 87.6% |
| ResNet | 91.4% | 90.8% |
| EfficientNet | 93.1% | 92.7% |

Sau đó chọn model tốt nhất để tích hợp vào hệ thống.

Sinh viên vừa có: **nghiên cứu AI**

vừa có: **phát triển Web Application**.

# **12\. Một vấn đề kỹ thuật thực tế**

Không phải ảnh nào cũng rõ.

Ví dụ:

Ảnh 1:  
chai nhựa rõ  
→ 96%

nhưng:

Ảnh 2:  
chai bị che một phần  
→ 61%

hoặc:

Ảnh 3:  
nhiều vật thể cùng lúc  
→ 43%

Do đó hệ thống nên có:

Confidence \< 60%  
        ↓  
Không chắc chắn  
        ↓  
"Vui lòng chụp lại ảnh rõ hơn"

Đây là một chức năng nhỏ nhưng rất hợp lý.

# **13\. Những chức năng KHÔNG nên làm**

Để tránh vượt quá khả năng nhóm, tôi sẽ bỏ:

 \- nhận diện mọi loại rác  
 \- tự động điều khiển robot phân loại rác  
 \- camera realtime phức tạp  
 \-  IoT sensor  
 \- tự động phân loại vật lý  
 \- dự đoán tác động môi trường phức tạp  
 \- tích hợp hệ thống thu gom rác thành phố  
 \- chatbot về môi trường.

Đây là **web \+ image classification \+ sorting guidance** là đủ.

# **14\. MVP**

### **User**

1. Register/Login  
2. Upload image  
3. Nhận diện rác  
4. Xem confidence  
5. Xem hướng dẫn  
6. Tìm kiếm loại rác  
7. Xem lịch sử  
8. Báo cáo kết quả sai

### **AI**

1. Dataset  
2. Preprocessing  
3. Transfer learning  
4. Classification  
5. Evaluation  
6. Model deployment

### **Admin**

1. Quản lý loại rác  
2. Quản lý hướng dẫn  
3. Xem feedback  
4. Dashboard.

# **15\. Tiến độ 4 tháng**

### **Tháng 1 — Phân tích \+ dữ liệu**

* Requirement  
* UI/UX  
* Database  
* Dataset  
* Data preprocessing  
* Baseline model.

### **Tháng 2 — Xây Web**

* Authentication  
* Waste category  
* Upload image  
* Search  
* Admin.

### **Tháng 3 — AI**

* Fine-tuning  
* Model evaluation  
* Model comparison  
* API AI  
* Integration.

### **Tháng 4 — Hoàn thiện**

* Sorting guide  
* History  
* Feedback  
* Dashboard  
* Testing  
* Deployment  
* Report.

# **16\. Điểm mạnh của đề tài**

Đề tài này có một ưu điểm rất rõ:

> **AI có nhiệm vụ cụ thể và nhìn thấy được.**

Người dùng:

**Ảnh rác**

↓

**AI nhận diện**

↓

**Loại rác**

↓

**Hướng dẫn**

↓

**Hành động**

Ví dụ demo chỉ cần vài giây:

> 📷 Chụp chai nhựa  
>  ↓  
>  🧠 Plastic Bottle — 94%  
>  ↓  
>  ♻️ Recyclable  
>  ↓  
>  “Đổ hết nước → làm sạch → bỏ vào thùng tái chế”

Rất dễ trình bày trước hội đồng.

# **17\. Một điểm cần đặc biệt lưu ý**

Nếu chỉ làm:

> **Ảnh → Plastic / Paper / Metal**

thì **đề tài hơi đơn giản và dễ bị đánh giá là “train một model rồi làm giao diện”.**

Để thành **đồ án CNTT hoàn chỉnh**, nên giữ cấu trúc:

> **Recognition \+ Knowledge Base \+ User Workflow**

Tức là:

                IMAGE  
                   ↓  
              AI CLASSIFIER  
                   ↓  
             WASTE CATEGORY  
                   ↓  
          ┌─────────────────┐  
          │ Knowledge Base  │  
          └─────────────────┘  
                   ↓  
        Sorting Instructions  
                   ↓  
         User Decision / Action  
                   ↓  
              Feedback

Phần **Knowledge Base \+ hướng dẫn \+ feedback \+ lịch sử \+ admin** chính là thứ biến nó từ một bài toán AI đơn lẻ thành **một sản phẩm web hoàn chỉnh**.

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

# **Tài Liệu Tham Khảo**

[Intelligent Waste Classification System Using Deep Learning Convolutional Neural Network](https://drive.google.com/file/d/1CZQ1LoDOYVzYbhA31zLQ4c9SgUm6YKhr/view?usp=sharing)  
[Waste Classification for Sustainable Development Using Image Recognition with Deep Learning Neural Network Models](https://drive.google.com/file/d/13BUjzhUetastGsxKfaxoM-V4rR0J_-Lt/view?usp=sharing)

# **Bản Demo về AI**

[TrashNet Computer Vision Dataset](https://universe.roboflow.com/trash-recognition-fixed/trashnet-blkh7)  
[Model Online Demonstration](https://colab.research.google.com/drive/1TT-YAao0txXLzBlrU8dA4Y-DyVq-kjxz?usp=sharing)  
Tên tiếng Việt: Hệ thống hỗ trợ phân loại và kết nối thu mua rác thải tái chế thông minh  
Tên tiếng Anh: Intelligent Recyclable Waste Classification and Trade Matching System.   
Tên viết tắt: EcoLink