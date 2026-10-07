# **V. Requirement Appendix**

## **0\. Tổng quan các Module**

| Mã Module | Tên Module | Tác nhân / Hệ thống ngoài chính | Luồng dữ liệu liên quan |
| ----- | ----- | ----- | ----- |
| AUTH | Tài khoản & Xác thực | Guest, User, Recycler, Google | Đăng ký, đăng nhập, đặt lại mật khẩu, OAuth, hồ sơ, đăng xuất |
| CLS | Phân loại AI & Dữ liệu mô hình | User, Staff | Tải ảnh rác, kết quả AI, phản hồi phân loại sai, duyệt ảnh lỗi & gán nhãn, tập dữ liệu kiểm thử |
| SRT | Hướng dẫn phân loại & Danh mục rác | User, Admin | Tra cứu Rule Engine, tìm kiếm rác, quét QR, cập nhật danh mục & hướng dẫn |
| MTH | Hồ sơ Recycler & Kết nối | User, Recycler, Staff | Tìm kiếm theo GPS, danh mục chấp nhận, xác minh Recycler |
| NEG | Thương lượng & Khung giờ thu gom | User, Recycler | Tin nhắn thương lượng, khung giờ thu gom đề xuất trong Handover Request |
| HND | Bàn giao & Giao dịch | User, Recycler | Yêu cầu bàn giao (kèm khung giờ đề xuất), chấp nhận/từ chối, xác nhận biên nhận kèm ảnh |
| PAY | Thanh toán dịch vụ nội bộ | User, Recycler, VietQR | Thanh toán các gói dịch vụ EcoLink (gói tắt quảng cáo, huy hiệu xác minh Tích xanh, v.v.) qua VietQR động |
| DSP | Giải quyết khiếu nại | User, Recycler, Staff | Báo cáo tranh chấp, quyết định của Staff |
| GAM | Gamification & Phần thưởng | User, Recycler, Staff, Admin | Điểm (cả User và Recycler), điểm danh hàng ngày (User/Staff), đổi voucher, Cây ảo, Eco Facts, bảng xếp hạng |
| RVW | Đánh giá, Nhận xét & Khảo sát | User, Recycler, Staff | Đánh giá đối tác (tự xóa hoặc Staff/Admin kiểm duyệt), khảo sát chất lượng dịch vụ định kỳ |
| NTF | Thông báo | Tất cả tác nhân, Gmail | Thông báo qua email và trong ứng dụng |
| ADM | Quản trị, Báo cáo & Duyệt nội dung Blog | Admin, Staff | Tài khoản Staff, trạng thái tài khoản, Dashboard Analytics, xuất báo cáo, duyệt bài Blog |

> 

## **1\. Business Rules**

### **1.1 AUTH – Tài khoản & Xác thực**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-AUTH-01 | Một địa chỉ email chỉ được gắn với một tài khoản duy nhất, trên cả hai vai trò (User/Recycler) và cả hai cách đăng ký (thông thường/Google). | Đăng ký | Ngăn trùng lặp danh tính và mơ hồ về quyền sở hữu lịch sử. | Hệ thống tự động thực hiện mỗi khi đăng ký |
| BR-AUTH-02 | Khi đăng nhập/đăng ký bằng Google, hệ thống đọc claim email\_verified từ ID token/UserInfo của Google. Nếu email\_verified \= true, email được đánh dấu đã xác minh ngay, không cần bước xác minh riêng. Nếu email\_verified \= false, hệ thống yêu cầu luồng xác minh email thông thường trước khi kích hoạt tài khoản. | Đăng ký, kích hoạt | Google báo cáo đáng tin cậy việc email đã được xác minh hay chưa, nên hệ thống có thể dựa vào tín hiệu này thay vì mặc định mọi đăng nhập Google đều đã xác minh sẵn. | Hệ thống tự động thực hiện mỗi khi đăng nhập/đăng ký bằng Google |
| BR-AUTH-03 | Khi đăng nhập thất bại, thông báo không được tiết lộ tên đăng nhập có tồn tại hay không, hay mật khẩu bị sai. | Đăng nhập | Ngăn dò quét tài khoản. | Hệ thống tự động thực hiện mỗi khi đăng nhập thất bại |
| BR-AUTH-04 | Sau {N} lần đăng nhập sai liên tiếp, tài khoản bị khóa tạm thời {M} phút. | Đăng nhập | Giảm thiểu tấn công dò mật khẩu. | Hệ thống tự động thực hiện mỗi khi đăng nhập thất bại |
| BR-AUTH-05 | Tài khoản đã bị vô hiệu hóa hoặc đình chỉ không thể đăng nhập, dù mật khẩu đúng, và mọi phiên đang hoạt động bị hủy ngay lập tức. | Đăng nhập, mọi yêu cầu | Đảm bảo tài khoản bị cấm mất quyền truy cập ngay. | Hệ thống tự động thực hiện mỗi khi đăng nhập và có yêu cầu |
| BR-AUTH-06 | Liên kết đặt lại mật khẩu chỉ dùng được một lần và hết hạn sau {X} phút. Phản hồi giống nhau dù email có tồn tại hay không. Đặt lại mật khẩu sẽ hủy mọi phiên hiện có. | Đặt lại mật khẩu | Ngăn dò quét email và tái sử dụng/đánh cắp token. | Hệ thống tự động thực hiện mỗi khi yêu cầu và sử dụng liên kết |
| BR-AUTH-07 | Đăng nhập Google yêu cầu xác minh OAuth token với Google ở mỗi lần đăng nhập. Tài khoản tạo qua Google gắn với email Google đã xác minh và mặc định có vai trò User. | Google OAuth | Ngăn token giả mạo và leo thang vai trò khi đăng ký. | Hệ thống tự động thực hiện mỗi khi đăng nhập OAuth |
| BR-AUTH-08 | Truy cập được phân quyền theo vai trò. Guest chỉ truy cập được thông tin công khai, mỗi vai trò chỉ truy cập được chức năng và dữ liệu của chính mình. | Mọi trang và yêu cầu | Áp dụng nguyên tắc đặc quyền tối thiểu. | Hệ thống tự động thực hiện mỗi khi có yêu cầu |
| **BR-AUTH-09** | Yêu cầu đặt lại mật khẩu cho cùng một email bị giới hạn {N} lần trong {X} phút, và yêu cầu từ cùng một IP bị giới hạn {M} lần mỗi giờ. Yêu cầu vượt giới hạn bị âm thầm bỏ qua (vẫn hiển thị phản hồi chung như bình thường). | Đặt lại mật khẩu | Ngăn spam/lạm dụng kênh gửi email đặt lại và bảo vệ hạn mức gửi email. | Hệ thống tự động thực hiện mỗi khi có yêu cầu đặt lại |

### **1.2 CLS – Phân loại AI & Dữ liệu mô hình**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-CLS-01 | Chỉ chấp nhận ảnh định dạng được hỗ trợ (JPG/PNG) và dung lượng không quá {X} MB. File không hợp lệ bị từ chối trước khi AI xử lý. | Tải ảnh rác | Bảo vệ tài nguyên hệ thống và chất lượng đầu vào của mô hình. | Hệ thống tự động thực hiện mỗi khi tải ảnh |
| BR-CLS-02 | Kết quả dự đoán của AI chỉ được hiển thị nếu ánh xạ được tới một danh mục đang hoạt động trong danh mục rác. | Kết quả phân loại AI | Ngăn danh mục sai dẫn đến hướng dẫn không chính xác. | Hệ thống tự động thực hiện mỗi khi phân loại |
| BR-CLS-03 | Nếu độ tin cậy của mô hình thấp hơn ngưỡng do Admin cấu hình, kết quả bị gắn cờ "không chắc chắn" và người dùng phải xác nhận hoặc chọn danh mục thủ công. | Kết quả phân loại AI | Tránh trình bày dự đoán độ tin cậy thấp như một sự thật. | Hệ thống tự động thực hiện mỗi khi phân loại |
| BR-CLS-04 | Người dùng phải xác nhận hoặc điều chỉnh danh mục được đề xuất trước khi phân loại được ghi nhận là chính thức. | Xác nhận phân loại | Đảm bảo dữ liệu chất lượng và người dùng hành động dựa trên danh mục đã xác thực. | Hệ thống tự động thực hiện mỗi lần quét |
| BR-CLS-05 | Mỗi báo cáo phân loại sai được liên kết với ảnh gốc, danh mục AI dự đoán và danh mục người dùng đề xuất, và vào hàng đợi duyệt của Staff. | Phản hồi phân loại sai | Tạo vòng phản hồi có thể truy vết để cải thiện mô hình. | Hệ thống tự động thực hiện mỗi khi có báo cáo |
| BR-CLS-06 | Chỉ ảnh đã được Staff duyệt và gán nhãn mới được đưa vào tập dữ liệu huấn luyện/kiểm thử. | Quản lý tập dữ liệu | Ngăn nhãn nhiễu hoặc độc hại làm giảm chất lượng mô hình. | Staff duyệt; hệ thống chặn dữ liệu chưa duyệt |
| BR-CLS-07 | Một ảnh không được xuất hiện đồng thời trong cả tập huấn luyện và tập kiểm thử. Chỉ Staff mới được cập nhật tập kiểm thử. | Tập dữ liệu kiểm thử | Tránh rò rỉ dữ liệu và số liệu độ chính xác thiếu tin cậy. | Hệ thống tự động thực hiện mỗi khi cập nhật |
| BR-CLS-08 | Ảnh rác chỉ được truy cập bởi chủ sở hữu, Recycler trong giao dịch liên quan đang hoạt động, Staff đang duyệt phản hồi, và Admin. | Truy cập ảnh rác | Bảo vệ quyền riêng tư của người dùng. | Hệ thống tự động thực hiện mỗi khi có yêu cầu truy cập |

### 

### **1.3 SRT – Hướng dẫn phân loại & Danh mục rác**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-SRT-01 | Hướng dẫn phân loại chỉ được cung cấp từ các quy tắc đang hoạt động trong Rule Engine, mỗi danh mục có đúng một phiên bản hướng dẫn hoạt động tại một thời điểm. | Tra cứu hướng dẫn phân loại | Đảm bảo hướng dẫn nhất quán, không mơ hồ. | Hệ thống tự động thực hiện mỗi khi tra cứu |
| BR-SRT-02 | Chỉ Admin được tạo, sửa hoặc ngừng sử dụng danh mục và chính sách phân loại. Thay đổi chỉ áp dụng cho lần tra cứu mới. | Danh mục rác, chính sách phân loại | Bảo toàn lịch sử trong khi vẫn cập nhật được hướng dẫn. | Hệ thống tự động thực hiện mỗi khi thay đổi |
| BR-SRT-03 | Hướng dẫn phải có ít nhất một bước xử lý theo thứ tự và một đích đến trước khi công bố. | Công bố hướng dẫn | Ngăn hướng dẫn chưa hoàn chỉnh đến tay người dùng. | Hệ thống tự động thực hiện mỗi khi công bố |
| BR-SRT-04 | Danh mục đã được tham chiếu bởi kết quả phân loại, hồ sơ Recycler hoặc giao dịch chỉ được ngừng sử dụng, không được xóa. | Danh mục rác | Bảo toàn tính toàn vẹn tham chiếu và dữ liệu lịch sử. | Hệ thống tự động thực hiện mỗi khi có yêu cầu xóa |
| BR-SRT-05 | Mã QR quét được phải trỏ tới đích hợp lệ do EcoLink phát hành, điều hướng chỉ giới hạn trong trang nội bộ EcoLink. | Quét QR | Ngăn điều hướng độc hại hoặc ra bên ngoài. | Hệ thống tự động thực hiện mỗi lần quét |

### 

### **1.4 MTH – Hồ sơ Recycler & Kết nối**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-MTH-01 | Recycler phải được Staff phê duyệt trước khi xuất hiện trong tìm kiếm hoặc nhận yêu cầu bàn giao. | Đăng ký Recycler | Đảm bảo tính hợp pháp và an toàn của đối tác. | Hệ thống tự động thực hiện mỗi khi tìm kiếm |
| BR-MTH-02 | Khi từ chối xác minh phải ghi rõ lý do, Recycler có thể nộp lại hồ sơ đã chỉnh sửa. | Xác minh Recycler | Đảm bảo minh bạch và có đường khắc phục. | Hệ thống tự động thực hiện mỗi khi Staff ra quyết định |
| BR-MTH-03 | Hồ sơ Recycler chỉ được công khai khi có ít nhất một loại rác chấp nhận, điều kiện thu gom và vị trí hợp lệ. | Công khai hồ sơ Recycler | Ngăn các hồ sơ không sử dụng được. | Hệ thống tự động thực hiện mỗi khi công khai |
| BR-MTH-04 | Kết quả tìm kiếm chỉ gồm Recycler đã duyệt, đang hoạt động, trong bán kính tìm kiếm và nhận đúng loại rác, mặc định sắp theo khoảng cách. | Tìm kiếm Recycler (PostGIS) | Đảm bảo kết quả phù hợp và có thể hành động được. | Hệ thống tự động thực hiện mỗi khi tìm kiếm |
| BR-MTH-05 | Tọa độ GPS của người dùng chỉ dùng để tính khoảng cách, không hiển thị cho Recycler. Địa chỉ chính xác chỉ chia sẻ sau khi yêu cầu được chấp nhận. | Dữ liệu vị trí | Bảo vệ quyền riêng tư vị trí. | Hệ thống tự động thực hiện mỗi khi tìm kiếm/xem hồ sơ |
| BR-MTH-06 | Recycler bị đình chỉ/vô hiệu hóa bị ẩn khỏi tìm kiếm ngay, nhưng giao dịch đang hoạt động vẫn hiển thị với cả hai bên. | Hiển thị Recycler | Loại bỏ đối tác rủi ro nhanh mà không gián đoạn giao dịch đang diễn ra. | Hệ thống tự động thực hiện mỗi khi thay đổi trạng thái |
| BR-MTH-07 | Việc gỡ một danh mục khỏi hồ sơ Recycler không ảnh hưởng đến yêu cầu bàn giao đã được chấp nhận. | Cập nhật hồ sơ Recycler | Bảo vệ các thỏa thuận đã chốt. | Hệ thống tự động thực hiện mỗi khi cập nhật |

### 

### **1.5 NEG – Thương lượng & Khung giờ thu gom**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-NEG-01 | Tin nhắn thương lượng chỉ tồn tại trong một yêu cầu bàn giao cụ thể và chỉ hiển thị với User, Recycler của yêu cầu đó (và Staff được phân công khi có tranh chấp). **Toàn bộ lịch sử tin nhắn được lưu lại, không bị xóa**, để làm bằng chứng nếu xảy ra tranh chấp. | Tin nhắn thương lượng | Giữ bí mật nội dung thương lượng, đồng thời có bằng chứng đối chiếu khi cần. | Hệ thống tự động thực hiện mỗi khi gửi và truy vấn tin nhắn |
| BR-NEG-02 | Việc đưa ra hoặc thay đổi đề nghị về giá/khối lượng chỉ được thực hiện khi yêu cầu ở trạng thái Pending hoặc Negotiating, và bị khóa khi đã Accepted, Rejected, Cancelled hoặc Expired. **Riêng kênh chat vẫn mở cho cả hai bên kể cả sau khi bàn giao đã Completed**, để họ có thể tiếp tục liên lạc. | Đề nghị thương lượng và chat | Ngăn thay đổi điều khoản đã thống nhất, nhưng vẫn cho phép hai bên giữ liên lạc sau giao dịch vì đóng chat không mang lại lợi ích bảo mật. | Hệ thống tự động thực hiện mỗi khi gửi đề nghị/tin nhắn |
| BR-NEG-03 | Chỉ đề nghị mới nhất có hiệu lực, mỗi đề nghị phản hồi thay thế đề nghị trước đó. Khi chấp nhận, các điều khoản đã thống nhất được lưu thành bản chụp không thể thay đổi. | Đề nghị và đề nghị phản hồi | Loại bỏ mơ hồ về những gì đã thỏa thuận. | Hệ thống tự động thực hiện mỗi khi gửi đề nghị/chấp nhận |
| **BR-NEG-04** | User đề xuất khung giờ thu gom mong muốn **ngay trong Yêu cầu bàn giao** khi tạo yêu cầu. Nếu khung giờ đó không phù hợp với Recycler, hai bên thương lượng lại qua Chatbox cho đến khi thống nhất. | Đề xuất khung giờ thu gom | Đồng bộ với luồng Handover một chiều, bỏ hẳn module đặt lịch hẹn rời. | Hệ thống tự động thực hiện khi tạo yêu cầu |
| **BR-NEG-05** | Khung giờ thu gom chính thức được chốt tại đúng thời điểm Recycler bấm **"Chấp nhận" (Accept)** Yêu cầu bàn giao của User — không cần bước xác nhận lịch riêng biệt nào khác. | Xác nhận khung giờ thu gom | Đồng bộ việc chốt lịch với trạng thái Accepted của chính Handover Request. | Hệ thống tự động thực hiện khi Recycler chấp nhận yêu cầu |

### **1.6 HND – Bàn giao & Giao dịch**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-HND-01 | Yêu cầu bàn giao chỉ được gửi tới Recycler đã duyệt, đang hoạt động và có nhận danh mục rác trong yêu cầu. | Tạo yêu cầu bàn giao | Ngăn các yêu cầu không thể thực hiện. | Hệ thống tự động thực hiện mỗi khi gửi yêu cầu |
| BR-HND-02 | Thay đổi trạng thái bàn giao chỉ theo một chiều: Pending → (Negotiating) → Accepted → Scheduled → Completed, hoặc kết thúc ở Rejected/Cancelled/Expired/Disputed. Không trạng thái nào được quay lại. | Trạng thái bàn giao | Duy trì toàn vẹn dữ liệu và dấu vết kiểm toán. | Hệ thống tự động thực hiện mỗi khi thay đổi trạng thái |
| BR-HND-03 | Recycler chỉ được xem và phản hồi các yêu cầu gửi đến chính họ. | Truy cập yêu cầu bàn giao | Đảm bảo công bằng và riêng tư giữa các Recycler. | Hệ thống tự động thực hiện mỗi khi truy vấn |
| BR-HND-04 | Bàn giao chỉ Completed khi Recycler xác nhận kèm ảnh và User xác nhận biên nhận. | Hoàn tất bàn giao | Có bằng chứng và sự đồng thuận của cả hai bên. | Hệ thống tự động thực hiện mỗi khi hoàn tất |
| BR-HND-05 | Quyết toán dựa trên khối lượng thực tế Recycler xác nhận và đơn giá đã thống nhất. Nếu lệch quá {X}%, User có thể chấp nhận hoặc khiếu nại trước khi quyết toán. | Quyết toán | Bảo vệ User khỏi điều chỉnh không công bằng. | Hệ thống tự động thực hiện khi xác nhận |
| BR-HND-06 | Mỗi bên có thể hủy cho đến khi Completed. Hủy trong vòng {X} giờ trước lịch hẹn đã xác nhận được ghi là hủy muộn. | Hủy bàn giao | Hạn chế bỏ hẹn nhưng vẫn cho phép hủy. | Hệ thống tự động thực hiện mỗi khi hủy |
| BR-HND-07 | Sau khi Completed, giao dịch không thể sửa hoặc xóa, chỉ có thể xem. | Bản ghi giao dịch | Bảo toàn lịch sử đáng tin cậy phục vụ báo cáo và khiếu nại. | Hệ thống tự động thực hiện sau khi hoàn tất |
| BR-HND-08 | Thông tin liên hệ đầy đủ chỉ hiển thị sau khi yêu cầu được chấp nhận. | Hiển thị thông tin liên hệ | Bảo vệ quyền riêng tư cho đến khi hai bên cam kết. | Hệ thống tự động thực hiện mỗi khi xem hồ sơ/yêu cầu |

### 

### **1.7 PAY – Thanh toán dịch vụ nội bộ**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-PAY-01 | Khoản thanh toán dịch vụ nội bộ (như mua gói tắt quảng cáo, huy hiệu Tích xanh xác minh, hoặc các tính năng nâng cao) của User/Recycler được thực hiện qua mã VietQR động gắn với mã đơn hàng riêng, và chỉ được ghi nhận thành công sau khi hệ thống xác minh thanh toán hợp lệ (đúng số tiền và mã tham chiếu). | Thanh toán dịch vụ EcoLink | Đảm bảo việc nâng cấp gói/tính năng nội bộ chính xác, tự động và minh bạch. | Hệ thống tự động thực hiện mỗi khi nhận webhook |
| BR-PAY-02 | Sự kiện thanh toán các gói dịch vụ nội bộ được xử lý theo tính idempotent, đảm bảo mỗi giao dịch thanh toán qua VietQR chỉ được ghi nhận và kích hoạt dịch vụ đúng một lần. | Thanh toán VietQR nội bộ | Ngăn xử lý trùng. | Hệ thống tự động thực hiện mỗi khi nhận webhook |

### 

### **1.8 DSP – Giải quyết khiếu nại**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-DSP-01 | Chỉ User hoặc Recycler là bên tham gia giao dịch mới được khiếu nại, trong vòng {N} ngày sau khi hoàn tất, tối đa một khiếu nại đang mở cho mỗi giao dịch. | Tạo khiếu nại | Ngăn lạm dụng và khiếu nại trùng lặp. | Hệ thống tự động thực hiện mỗi khi gửi khiếu nại |
| BR-DSP-02 | Khiếu nại phải có phân loại lý do và mô tả. Ảnh minh chứng là tùy chọn. | Tạo khiếu nại | Cung cấp đủ thông tin để Staff quyết định. | Hệ thống tự động thực hiện mỗi khi gửi |
| BR-DSP-04 | Chỉ Staff được giải quyết khiếu nại, mỗi quyết định phải ghi kết quả (VD: User được hoàn toàn phần/toàn bộ từ Recycler, bác bỏ) và lý do. Do thanh toán diễn ra trực tiếp giữa hai bên, kết quả là một khuyến nghị ràng buộc được thông báo cho cả hai — không phải lệnh chuyển tiền tự động. | Giải quyết khiếu nại | Đảm bảo quyết định có trách nhiệm và giải thích được, dù không còn tiền giữ trên nền tảng. | Hệ thống tự động ghi nhận quyết định do Staff đưa ra |
| BR-DSP-06 | Dữ liệu khiếu nại (tin nhắn, ảnh, chi tiết giao dịch) chỉ được truy cập bởi hai bên và Staff được phân công. | Truy cập dữ liệu khiếu nại | Bảo vệ tính bảo mật. | Hệ thống tự động thực hiện mỗi khi có yêu cầu |

### 

### 

### **1.9 GAM – Gamification & Phần thưởng**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-GAM-01 | Điểm chỉ được cộng cho các sự kiện đã xác minh, như bàn giao Completed (dựa trên danh mục và khối lượng đã xác nhận) — **cộng cho cả User và Recycler đã hoàn tất giao dịch** — và chỉ một lần cho mỗi sự kiện với mỗi vai trò. Không cộng điểm cho giao dịch hủy/tranh chấp; điểm bị thu hồi nếu khiếu nại đảo ngược giao dịch. | Cộng điểm | Ngăn điểm không xứng đáng hoặc bị cộng trùng. | Hệ thống tự động thực hiện mỗi khi có sự kiện đủ điều kiện |
| BR-GAM-02 | Điểm kiếm được mỗi ngày từ hành động không phải giao dịch bị giới hạn tối đa {N} cho mỗi User/Recycler. | Cộng điểm | Ngăn cày điểm. | Hệ thống tự động thực hiện mỗi khi cộng điểm |
| BR-GAM-03 | Số dư điểm không được âm. Đổi thưởng trừ điểm và cấp voucher trong một thao tác nguyên tử. | Đổi điểm | Đảm bảo nhất quán giữa điểm và voucher. | Hệ thống tự động thực hiện mỗi khi đổi thưởng |
| BR-GAM-04 | Phần thưởng chỉ đổi được khi đang hoạt động và còn hàng, mỗi lượt đổi trừ tồn kho một cách nguyên tử. | Kho phần thưởng | Ngăn đổi vượt số lượng. | Hệ thống tự động thực hiện mỗi khi đổi thưởng |
| BR-GAM-05 | Voucher chỉ dùng một lần, gắn với tài khoản đã đổi, và hết hạn theo ngày hết hạn. | Voucher | Ngăn bán lại, dùng lại và voucher quá hạn. | Hệ thống tự động thực hiện mỗi khi dùng voucher |
| BR-GAM-06 | Giai đoạn phát triển Cây ảo chỉ tăng theo một chiều. Chăm sóc giới hạn {N} lần/ngày, Eco Fact đã mở khóa vẫn được giữ mở. | Cây ảo, Eco Facts | Giữ tiến trình công bằng và nhất quán. | Hệ thống tự động thực hiện mỗi khi chăm sóc/mở khóa |
| BR-GAM-07 | Bảng xếp hạng chỉ tính giao dịch Completed và không tranh chấp theo tiêu chí do Admin cấu hình, chỉ hiển thị tên hiển thị. | Bảng xếp hạng | Đảm bảo xếp hạng công bằng và bảo vệ quyền riêng tư. | Hệ thống tự động thực hiện mỗi khi tính toán |
| **BR-GAM-08** | Việc kiếm điểm và đổi điểm lấy voucher áp dụng cho **cả vai trò User và Recycler**. Chăm sóc Cây ảo, Eco Facts và bảng xếp hạng đóng góp vẫn chỉ dành riêng cho vai trò User. | Truy cập gamification | Ghi nhận Recycler đóng góp vào giao dịch thành công, trong khi vẫn giữ các tính năng hướng đến người tiêu dùng cho riêng User. | Hệ thống tự động thực hiện mỗi khi có yêu cầu |
| **BR-GAM-09**  | Mỗi tài khoản User/Staff chỉ được điểm danh **1 lượt/ngày**. Điểm thưởng cộng theo **chuỗi ngày liên tục (Streak)** — tăng dần theo số ngày điểm danh liên tiếp — và tự động **reset về Ngày 1** nếu đứt chuỗi (bỏ lỡ một ngày). | Điểm danh hàng ngày | Khuyến khích thói quen tương tác đều đặn mỗi ngày (bổ sung cho Use Case U-15 & S-11). | Hệ thống tự động thực hiện mỗi khi điểm danh |

### 

### **1.10 RVW – Đánh giá, Nhận xét & Khảo sát**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-RVW-01 | Chỉ bên tham gia giao dịch Completed mới được đánh giá, mỗi bên một lần cho mỗi giao dịch, trong vòng {N} ngày sau khi hoàn tất. | Gửi đánh giá | Ngăn đánh giá giả, trùng lặp hoặc mang tính trả đũa. | Hệ thống tự động thực hiện mỗi khi gửi đánh giá |
| BR-RVW-02 | Đánh giá từ 1–5 sao kèm nhận xét tùy chọn. Sau khi gửi, không thể sửa, nhưng tác giả có thể xóa toàn bộ đánh giá (xem BR-RVW-03). | Nội dung đánh giá | Giữ nội dung đã đăng đáng tin cậy, đồng thời cho tác giả quyền thu hồi. | Hệ thống tự động thực hiện mỗi khi có yêu cầu sửa/xóa |
| **BR-RVW-03** | **Tác giả của đánh giá (User hoặc Recycler, tùy ai gửi) có thể tự xóa đánh giá của mình** vào bất kỳ lúc nào; việc xóa là vĩnh viễn, không thể khôi phục. **Ngoại lệ:** Staff/Admin có quyền **Ẩn/Xóa** một đánh giá nếu đánh giá đó **bị báo cáo và được xác định vi phạm tiêu chuẩn cộng đồng** (VD: ngôn từ xúc phạm, ngụy tạo thông tin). Ngoài tác giả và Staff/Admin trong trường hợp trên, không bên nào khác — kể cả bên được đánh giá — được xóa hay ẩn đánh giá. Điểm đánh giá trung bình hiển thị được tính lại ngay sau khi xóa/ẩn. | Xóa/ẩn đánh giá & hiển thị | Cho tác giả quyền kiểm soát nội dung của mình, đồng thời cho Staff/Admin công cụ kiểm duyệt nội dung độc hại, mà không để bên được đánh giá tự ý che giấu phản hồi bất lợi nhưng hợp lệ. | Hệ thống tự động thực hiện mỗi khi xóa/tính lại; Staff/Admin thực hiện ẩn/xóa khi có báo cáo vi phạm |
| BR-RVW-04 | Khảo sát gửi theo lịch Staff cấu hình, chỉ tới tài khoản có ít nhất một giao dịch Completed trong kỳ, mỗi người nhận trả lời một lần cho mỗi khảo sát. | Khảo sát định kỳ | Nhắm đúng đối tượng liên quan và tránh trùng lặp. | Hệ thống tự động thực hiện mỗi chu kỳ khảo sát |
| BR-RVW-05 | Kết quả khảo sát hiển thị cho Staff/Admin được tổng hợp, không xác định từng người trả lời. | Phân tích khảo sát | Khuyến khích phản hồi trung thực. | Hệ thống tự động thực hiện mỗi khi tạo báo cáo |

### 

### **1.11 NTF – Thông báo**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-NTF-01 | Email bảo mật và giao dịch (xác minh, đặt lại mật khẩu, thanh toán, khiếu nại) không thể bị người dùng tắt. | Thông báo email | Đảm bảo thông tin quan trọng luôn đến được người dùng. | Hệ thống tự động thực hiện mỗi khi gửi |
| BR-NTF-02 | Mọi thay đổi trạng thái của bàn giao, lịch hẹn, khiếu nại hoặc chi trả đều thông báo cho tất cả các bên liên quan. | Thông báo trạng thái | Giữ cho mọi bên được cập nhật. | Hệ thống tự động thực hiện mỗi khi thay đổi trạng thái |
| BR-NTF-03 | Thông báo chỉ gửi tới email đã xác minh của tài khoản, đổi email yêu cầu xác minh lại. | Gửi email | Ngăn thông báo gửi nhầm người. | Hệ thống tự động thực hiện mỗi khi gửi |
| BR-NTF-04 | Nhắc lịch hẹn được gửi trước giờ hẹn {X} giờ. | Nhắc lịch hẹn | Giảm tình trạng bỏ hẹn. | Hệ thống tự động thực hiện (tác vụ theo lịch) |
| BR-NTF-05 | Thông báo không được chứa dữ liệu nhạy cảm (thông tin liên hệ đầy đủ, thông tin ngân hàng, token), trừ liên kết xác minh/đặt lại dùng một lần. | Nội dung thông báo | Giảm rò rỉ dữ liệu qua email. | Hệ thống tự động thực hiện mỗi khi tạo thông báo |
| BR-NTF-06 | Email gửi thất bại được ghi log và thử lại tối đa {N} lần, không bao giờ chặn hoặc hoàn tác thao tác nghiệp vụ. | Gửi email | Giữ các luồng cốt lõi ổn định. | Hệ thống tự động thực hiện mỗi khi gửi thất bại |

### 

### **1.12 ADM – Quản trị & Báo cáo**

| BR-ID | Nội dung quy tắc | Áp dụng cho | Lý do | Được thực thi bởi |
| ----- | ----- | ----- | ----- | ----- |
| BR-ADM-01 | Tài khoản Staff và Admin chỉ do Admin tạo, không tự đăng ký, Staff không được tạo/sửa tài khoản Admin. | Tài khoản Staff/Admin | Ngăn truy cập đặc quyền trái phép. | Hệ thống tự động thực hiện mỗi khi tạo tài khoản |
| BR-ADM-02 | Phân tách vai trò: Admin phụ trách chính sách, danh mục, cấu hình bảng xếp hạng, tài khoản Staff, báo cáo. Staff phụ trách khiếu nại, gán nhãn, xác minh, kho phần thưởng, cấu hình khảo sát, trạng thái tài khoản. | Phân quyền vai trò | Áp dụng phân tách nhiệm vụ. | Hệ thống tự động thực hiện mỗi khi có thao tác |
| BR-ADM-03 | Staff chỉ được đổi trạng thái tài khoản User/Recycler khi có ghi lý do, không được đổi tài khoản Admin hay Staff khác. | Trạng thái tài khoản | Đảm bảo việc kiểm duyệt có trách nhiệm giải trình. | Hệ thống tự động thực hiện mỗi khi đổi trạng thái |
| BR-ADM-04 | Tài khoản đã có lịch sử giao dịch chỉ được vô hiệu hóa, không được xóa. | Xóa tài khoản | Bảo toàn lịch sử tài chính và kiểm toán. | Hệ thống tự động thực hiện mỗi khi có yêu cầu xóa |
| BR-ADM-05 | Mọi thao tác của Admin/Staff về cấu hình, trạng thái tài khoản, xác minh và quyết định khiếu nại đều được ghi vào nhật ký kiểm toán bất biến. | Dấu vết kiểm toán | Đảm bảo trách nhiệm giải trình và khả năng truy vết. | Hệ thống tự động thực hiện mỗi khi có thao tác |
| BR-ADM-06 | Báo cáo xuất mặc định chứa dữ liệu tổng hợp/ẩn danh, không gồm thông tin liên hệ và ngân hàng cá nhân, mọi lần xuất đều được ghi log. | Xuất báo cáo dữ liệu | Bảo vệ dữ liệu cá nhân. | Hệ thống tự động thực hiện mỗi lần xuất |
| BR-ADM-07 | Dashboard Analytics chỉ tính từ dữ liệu đã hoàn tất và đã xác nhận, ở chế độ chỉ đọc. | Dashboard Analytics | Đảm bảo số liệu đáng tin cậy. | Hệ thống tự động thực hiện mỗi khi truy vấn |
| **BR-ADM-08** | Bài đăng Blog do Staff biên soạn phải ở trạng thái **Draft** hoặc **Pending\_Approval**. Bài viết chỉ được công khai trên ứng dụng sau khi **Admin phê duyệt (Approved)**. | Duyệt nội dung Blog | Đảm bảo nội dung được kiểm duyệt trước khi đến tay người dùng (bổ sung cho Use Case S-08 & A-09). | Hệ thống tự động chặn trạng thái công khai; Admin ra quyết định phê duyệt |

## **2\. System Messages**

### **AUTH**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-AUTH-01 | In line | Sai tên đăng nhập/mật khẩu | *Sai tên đăng nhập hoặc mật khẩu. Vui lòng kiểm tra lại.* |
| MSG-AUTH-02 | Màu đỏ, dưới ô nhập | Trường bắt buộc để trống | *Trường có dấu \* là bắt buộc.* |
| MSG-AUTH-03 | In line | Tài khoản tạm khóa | *Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau {M} phút.* |
| MSG-AUTH-04 | In line | Tài khoản đã bị vô hiệu hóa/đình chỉ | *Tài khoản này hiện không còn hoạt động. Vui lòng liên hệ bộ phận hỗ trợ.* |
| MSG-AUTH-05 | Toast | Đã gửi liên kết đặt lại mật khẩu | *Nếu email này tồn tại, một liên kết đặt lại mật khẩu đã được gửi.* |
| MSG-AUTH-06 | In line | Liên kết đặt lại không hợp lệ/hết hạn | *Liên kết này không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.* |
| MSG-AUTH-07 | In line | Yêu cầu đặt lại quá nhiều lần | *Bạn đã yêu cầu đặt lại quá nhiều lần. Vui lòng thử lại sau.* |
| MSG-AUTH-08 | Toast | Đăng ký thành công | *Tạo tài khoản thành công\! Vui lòng kiểm tra email để xác minh tài khoản.* |
| MSG-AUTH-09 | Toast | Email đã được xác minh | *Email của bạn đã được xác minh.* |

### **CLS**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-CLS-01 | Màu đỏ, dưới khung tải ảnh | File không đúng định dạng | *Định dạng file không được hỗ trợ. Vui lòng tải lên ảnh JPG hoặc PNG.* |
| MSG-CLS-02 | Màu đỏ, dưới khung tải ảnh | File vượt dung lượng tối đa | *File vượt quá dung lượng tối đa {max\_size\_mb} MB.* |
| MSG-CLS-03 | Toast | Phân loại thành công | *Nhận diện rác thành công.* |
| MSG-CLS-04 | Toast (cảnh báo) | Kết quả có độ tin cậy thấp | *Chúng tôi chưa chắc chắn hoàn toàn về vật phẩm này — vui lòng xác nhận hoặc chọn danh mục thủ công.* |
| MSG-CLS-05 | In line | AI xử lý thất bại | *Không thể xử lý ảnh này. Vui lòng thử lại với ảnh rõ hơn.* |
| MSG-CLS-06 | Toast | Đã gửi báo cáo phân loại sai | *Cảm ơn bạn\! Phản hồi của bạn đã được gửi để xem xét.* |

### **SRT**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-SRT-01 | In line | Không tìm thấy kết quả | *Không tìm thấy danh mục rác phù hợp.* |
| MSG-SRT-02 | In line | Mã QR không hợp lệ | *Mã QR này không hợp lệ hoặc đã hết hạn.* |
| MSG-SRT-03 | Toast (Admin) | Lưu danh mục thành công | *Lưu danh mục rác thành công.* |
| MSG-SRT-04 | Toast (Admin) | Công bố hướng dẫn thành công | *Công bố hướng dẫn phân loại thành công.* |

### **MTH**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-MTH-01 | In line | Không tìm thấy Recycler | *Không tìm thấy cơ sở thu mua rác gần bạn.* |
| MSG-MTH-02 | In line | Từ chối quyền vị trí | *Cần quyền truy cập vị trí để tìm cơ sở thu mua gần bạn. Vui lòng bật trong phần cài đặt.* |
| MSG-MTH-03 | Toast | Hồ sơ Recycler chờ duyệt | *Hồ sơ cơ sở của bạn đang được xem xét và sẽ xuất hiện trong tìm kiếm sau khi được duyệt.* |
| MSG-MTH-04 | Toast/Email | Recycler được duyệt | *Hồ sơ cơ sở của bạn đã được duyệt và hiện hiển thị với người dùng.* |
| MSG-MTH-05 | In line | Recycler bị từ chối | *Hồ sơ cơ sở của bạn chưa được duyệt: {reason}. Vui lòng cập nhật và gửi lại.* |
| MSG-MTH-06 | Màu đỏ | Hồ sơ chưa đủ điều kiện công khai | *Vui lòng thêm ít nhất một loại rác chấp nhận và một vị trí hợp lệ trước khi công khai.* |

### **NEG**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-NEG-01 | Toast | Gửi yêu cầu bàn giao/thương lượng | *Yêu cầu trao đổi của bạn đã được gửi đến {facility\_name}.* |
| MSG-NEG-02 | Trong ứng dụng | Có tin nhắn mới | *Bạn có tin nhắn mới từ {sender\_name}.* |
| MSG-NEG-03 | Toast | Đề nghị được chấp nhận | *{party\_name} đã chấp nhận đề nghị của bạn.* |
| **MSG-NEG-04** | Toast | Recycler nhận yêu cầu bàn giao mới kèm khung giờ đề xuất | *Bạn có yêu cầu bàn giao mới kèm khung giờ đề xuất từ {user\_name}.* |

*(Các mã MSG-NEG-05, 06, 07 phiên bản trước — về xác nhận/hết hạn/dời lịch hẹn độc lập — đã được loại bỏ vì lịch hẹn không còn là luồng riêng, xem BR-NEG-04/05.)*

### **HND**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-HND-01 | Toast | Gửi yêu cầu bàn giao | *Yêu cầu bàn giao của bạn đã được gửi.* |
| MSG-HND-02 | Toast | Yêu cầu bàn giao được chấp nhận | *Yêu cầu bàn giao của bạn đã được chấp nhận.* |
| MSG-HND-03 | Toast | Bàn giao hoàn tất | *Giao dịch \#{transaction\_id} đã được đánh dấu hoàn tất.* |
| MSG-HND-04 | In line | Khối lượng lệch so với thỏa thuận | *Khối lượng xác nhận lệch hơn {X}% so với thỏa thuận. Vui lòng xem lại trước khi xác nhận.* |
| MSG-HND-05 | Toast | Bàn giao bị hủy | *Bàn giao này đã bị hủy.* |
| MSG-HND-06 | In line | Hủy muộn | *Việc hủy này diễn ra trong vòng {X} giờ trước lịch hẹn đã xác nhận và sẽ được ghi nhận là hủy muộn.* |

### **PAY**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-PAY-01 | In line | Tạo mã QR thanh toán | *Quét mã VietQR này để hoàn tất thanh toán dịch vụ EcoLink.* |
| MSG-PAY-02 | Toast | Nhận thanh toán thành công | *Thanh toán dịch vụ của bạn đã được xác nhận thành công.* |
| MSG-PAY-03 | In line | Mã thanh toán hết hạn | *Mã thanh toán này đã hết hạn. Vui lòng tạo mã mới.* |

### **DSP**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-DSP-01 | Toast | Đã gửi khiếu nại | *Khiếu nại của bạn đã được gửi và đang được xem xét.* |
| MSG-DSP-02 | Toast | Khiếu nại đã giải quyết | *Khiếu nại của bạn đã được giải quyết. {outcome\_summary}* |
| MSG-DSP-03 | In line | Hết thời hạn khiếu nại | *Bạn không thể khiếu nại giao dịch này nữa.* |
| MSG-DSP-04 | In line | Đã có khiếu nại đang mở | *Đã có một khiếu nại đang mở cho giao dịch này.* |

### **GAM**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-GAM-01 | Toast | Nhận được điểm | *Bạn vừa nhận được {points} điểm\!* |
| MSG-GAM-02 | Toast | Đổi voucher thành công | *Đổi voucher thành công.* |
| MSG-GAM-03 | In line | Không đủ điểm | *Bạn không đủ điểm để đổi phần thưởng này.* |
| MSG-GAM-04 | In line | Phần thưởng đã hết | *Phần thưởng này hiện không còn nữa.* |
| MSG-GAM-05 | In line | Đạt giới hạn điểm trong ngày | *Bạn đã đạt giới hạn điểm trong ngày cho hành động này.* |
| MSG-GAM-06 | Toast | Cây ảo lên cấp | *Cây ảo của bạn đã lớn thêm một giai đoạn mới\!* |
| MSG-GAM-07 | Toast | Mở khóa Eco Fact | *Bạn vừa mở khóa một Eco Fact mới\!* |
| MSG-GAM-08 | Toast | Điểm danh hàng ngày thành công | *Điểm danh thành công\! Bạn nhận được {points} điểm (Chuỗi {streak\_days} ngày).* |

### **RVW**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-RVW-01 | Toast | Gửi đánh giá thành công | *Cảm ơn bạn đã đánh giá\!* |
| MSG-RVW-02 | In line | Chưa có giao dịch hoàn tất | *Bạn chỉ có thể đánh giá cơ sở thu mua sau khi hoàn tất giao dịch với họ.* |
| MSG-RVW-03 | Toast | Đã xóa đánh giá | *Đánh giá của bạn đã được xóa.* |
| MSG-RVW-04 | Email/thông báo | Mời tham gia khảo sát | *Chúng tôi rất mong nhận được phản hồi của bạn — hãy tham gia khảo sát ngắn về trải nghiệm gần đây.* |
| MSG-RVW-05 | In line | Đã hoàn thành khảo sát | *Bạn đã hoàn thành khảo sát này rồi.* |

### **NTF**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-NTF-01 | Toast | Phiên đăng nhập hết hạn | *Phiên làm việc của bạn đã hết hạn. Vui lòng đăng nhập lại.* |
| MSG-NTF-02 | Nội bộ (Staff/Admin) | Gửi email thất bại | *Không thể gửi email tới {email\_address}.* |
| MSG-NTF-03 | Màu đỏ, dưới ô nhập | Vượt độ dài tối đa | *Vượt quá độ dài tối đa {max\_length}.* |

### **ADM**

| Mã | Loại | Ngữ cảnh | Nội dung |
| ----- | ----- | ----- | ----- |
| MSG-ADM-01 | Toast (Staff) | Cập nhật trạng thái tài khoản | *Cập nhật trạng thái tài khoản thành công.* |
| MSG-ADM-02 | Toast (Admin) | Xuất báo cáo | *Xuất báo cáo thành công.* |
| MSG-ADM-03 | Toast (Admin) | Tạo tài khoản Staff | *Tạo tài khoản nhân viên thành công.* |
| MSG-ADM-04 | In line | Thao tác không được phép | *Bạn không có quyền thực hiện thao tác này.* |
| MSG-ADM-05  | Toast (Admin) | Duyệt bài viết Blog | *Bài viết "{blog\_title}" đã được duyệt và công khai.* |
| MSG-ADM-06  | Toast (Staff) | Gửi bài Blog để duyệt | *Bài viết đã được gửi cho Admin chờ kiểm duyệt.* |

