### **1\. Ý kiến của Thầy Kiên (Focus vào Nghiệp vụ, Tài liệu, BF, UC & Slide)**

Thầy Kiên góp ý rất chi tiết về sự đồng bộ tài liệu, tính thực tế của nghiệp vụ và cách trình bày. Các vấn đề cần sửa đổi bao gồm:

**Về Business Flow (Luồng nghiệp vụ) & Mô hình kinh doanh:**

* **Đổi tên và cấu trúc lại BF:** 3 luồng chính đang bị rời rạc. Cần đổi tên các BF sao cho bám sát chức năng thực tế của phần mềm, không nên đặt tên quá phụ thuộc hoặc lạm dụng từ khóa "AI".  
* **Tính liên kết (Input \- Output):** Input và Output của các luồng nghiệp vụ cần được nối (link) trực tiếp với nhau, mô tả rõ ràng sự luân chuyển dữ liệu từ luồng này sang luồng khác.  
* **Làm rõ Mô hình kinh doanh (Business Model):** Cần trình bày ngắn gọn lại. Giảm bớt các "yếu tố phi thực tế" trong nghiệp vụ.  
* **Xử lý rủi ro nghiệp vụ (Edge Cases):** Cần làm rõ bài toán: *"Nếu phân loại rác xong mà không có ai mua thì xử lý thế nào?"*. Cần làm cho ứng dụng/mô hình kinh doanh trở nên hấp dẫn, thu hút người dùng hơn. Làm rõ luồng: Phân loại rác $\rightarrow$ Bán $\rightarrow$ Tạo đề nghị bán $\rightarrow$ Thêm điểm thưởng khuyến khích bảo vệ môi trường.

**Về Use Case (UC) & ERD:**

* **Đồng bộ tài liệu (Consistency):** Cần "matching" chi tiết các Use Case từ Report 1, 2 đến Report 3 để đảm bảo tính đồng bộ (không bị vênh râu giữa các báo cáo).  
* **Chất lượng hình ảnh:** Hình ảnh Use Case Diagram đang bị mờ, cần xuất lại ảnh nét và sắp xếp dễ nhìn hơn.  
* **Sửa lỗi Conceptual ERD:** Thiết kế mức khái niệm đang chứa các thực thể vật lý (database-level) không liên quan/không cần thiết như auditlog, bảng verify. Sơ đồ đang bị rối.  
* **Chuẩn hóa định danh:** Chuẩn hóa lại cách đặt tên trong biểu đồ (đang bị lộn xộn giữa danh động từ "V-ing" và "động từ nguyên thể").

**Về Bố cục Slide thuyết trình:**

* **Cấu trúc logic:** Bố cục phần trình bày hiện tại chưa hợp lý. Phải sắp xếp lại theo luồng: **Context (Bối cảnh) $\rightarrow$ Business Flow (Luồng nghiệp vụ) $\rightarrow$ Use Case**.  
* **Cách dẫn dắt vấn đề:** Bám sát công thức: *Nhu cầu thực tế là gì? $\rightarrow$ Giải pháp hiện tại trên thị trường đang có là gì? $\rightarrow$ Phát hiện khoảng trống (Gap) $\rightarrow$ Đưa ra giải pháp của nhóm*.  
* **Phần 3 của Slide:** Đang bị ngược logic và đang lạm dụng từ "AI" quá nhiều. Cần tinh chỉnh lại để bài toán và luồng quấn hút, chỉn chu và thực tế hơn.

### **2\. Ý kiến của Thầy Giang (Focus vào Kỹ thuật AI & Luồng xử lý AI)**

Thầy Giang đánh giá hệ thống dưới góc độ quy trình kỹ thuật, vòng đời của mô hình AI và cách xử lý ngoại lệ.

**Về Luồng vận hành AI (AI Lifecycle Workflow):**

* **Thiết kế quy trình thay vì liệt kê:** Các vấn đề kỹ thuật hiện tại đang chỉ gạch đầu dòng liệt kê. Cần chuyển hóa các tài liệu này thành các sơ đồ quy trình (Workflow/Process) thể hiện rõ ràng.  
* **Bổ sung Use Case / Luồng Retrain AI:** Thầy "chưa nhìn thấy được việc train AI qua Use Case". Phải vẽ bổ sung luồng nghiệp vụ tạo Data Loop: *Người dùng cung cấp ảnh $\rightarrow$ Staff sơ chế và gán nhãn $\rightarrow$ Submit dữ liệu cho Developer để train lại*.  
* **Cơ chế Retrain:** Phải làm rõ thuật toán và luồng là hệ thống sẽ **train lại tự động** (auto) hay **train theo từng đợt** (batch/manual).

**Về Thông số & Xử lý ngoại lệ (Exception Handling):**

* **Bổ sung thông số kỹ thuật:** Trong thiết kế cần đưa ra các thông số tham khảo cụ thể (Ví dụ: Train mất bao lâu? Update model lên server mất bao lâu?...).  
* **Kịch bản dự phòng (Backup Plan):** Giải pháp xử lý thế nào khi hệ thống nhận diện ảnh không rõ (mờ, tối, vật thể lạ)? Đã có phương án backup chưa? (Gợi ý: Đưa flow Confidence \< 60% yêu cầu user tự chọn bằng tay vào trình bày thật rõ).

### **3\. Ý kiến của Cô Lan**

* Cô Lan không có comment (Chấp thuận hoặc đồng tình với các ý kiến của 2 thầy)