# TÀI LIỆU ĐẶC TẢ USE CASE (USE CASE DESCRIPTION)
## PHÂN HỆ RECYCLER (ĐỐI TÁC THU MUA) & STAFF (NHÂN VIÊN VẬN HÀNH)

**Dự án:** EcoLink – Hệ thống hỗ trợ phân loại và kết nối thu mua rác thải tái chế thông minh  
**Mã dự án:** SEP490-G141  
**Cấp độ tài liệu:** Yêu cầu người dùng (User Requirements Specification - Level 2)  
**Tiêu chuẩn định dạng:** Tuân thủ [Quy chuẩn viết Use Case Description.md](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/Quy%20chu%E1%BA%A9n%20vi%E1%BA%BFt%20Use%20Case%20Description.md) và tài liệu quy tắc [BR & MSG.md](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/BR%20&%20MSG.md)

---

## MỤC LỤC
1. [Nguyên tắc thiết kế tài liệu User Requirement](#1-nguyên-tắc-thiết-kế-tài-liệu-user-requirement)
2. [Bảng tổng hợp Use Case phân hệ Recycler và Staff](#2-bảng-tổng-hợp-use-case-phân-hệ-recycler-và-staff)
3. [Đặc tả Use Case Phân hệ RECYCLER (Đối tác thu mua)](#3-đặc-tả-use-case-phân-hệ-recycler-đối-tác-thu-mua)
   - [UC-R01: Quản lý Danh mục & Bảng giá thu mua (Manage Accepted Waste & Pricing)](#uc-r01-manage-accepted-waste--pricing)
   - [UC-R02: Bật/Tắt trạng thái hoạt động trên bản đồ (Toggle Active Status)](#uc-r02-toggle-active-status)
   - [UC-R03: Tiếp nhận / Từ chối yêu cầu thu gom (Accept / Reject Handover Request)](#uc-r03-accept--reject-handover-request)
   - [UC-R04: Hủy lịch hẹn thu gom (Cancel Collection Appointment)](#uc-r04-cancel-collection-appointment)
   - [UC-R05: Trao đổi qua Khung chat (Negotiate via Chatbox)](#uc-r05-negotiate-via-chatbox)
   - [UC-R06: Tạo Biên nhận bàn giao phế liệu (Create Handover Receipt)](#uc-r06-create-handover-receipt)
   - [UC-R07: Thanh toán gói dịch vụ nội bộ qua VietQR (Purchase In-System Services via VietQR)](#uc-r07-purchase-in-system-services-via-vietqr)
   - [UC-R08: Quên / Đặt lại mật khẩu (Reset Password)](#uc-r08-reset-password)
4. [Đặc tả Use Case Phân hệ STAFF (Nhân viên vận hành)](#4-đặc-tả-use-case-phân-hệ-staff-nhân-viên-vận-hành)
   - [UC-S01: Đánh giá ảnh AI nhận diện sai (Review Misclassified Images)](#uc-s01-review-misclassified-images)
   - [UC-S02: Gán nhãn dữ liệu hình ảnh (Label Image Data)](#uc-s02-label-image-data)
   - [UC-S03: Cập nhật Tập dữ liệu kiểm chứng (Update Verified Dataset)](#uc-s03-update-verified-dataset)
   - [UC-S04: Thẩm định hồ sơ Đối tác thu mua (Verify Recycler Application)](#uc-s04-verify-recycler-application)
   - [UC-S05: Xử lý khiếu nại giao dịch (Handle Transaction Complaints)](#uc-s05-handle-transaction-complaints)
   - [UC-S06: Quản lý Tài khoản Người dùng & Đối tác (Manage User & Buyer Accounts)](#uc-s06-manage-user--buyer-accounts)
   - [UC-S07: Soạn thảo bài đăng Blog truyền thông (Create Blog Post Draft)](#uc-s07-create-blog-post-draft)
   - [UC-S08: Quản lý Nội dung Kiến thức Eco Fact (Manage Eco Fact Content)](#uc-s08-manage-eco-fact-content)
   - [UC-S09: Quản lý Kho Quà tặng & Voucher (Manage Gift Inventory)](#uc-s09-manage-gift-inventory)
   - [UC-S10: Điểm danh ca làm việc nhân viên (Staff Daily Check-in)](#uc-s10-staff-daily-check-in)
5. [Ma trận phân quyền tác nhân (Role-Permission Matrix)](#5-ma-trận-phân-quyền-tác-nhân-role-permission-matrix)

---

## 1. NGUYÊN TẮC THIẾT KẾ TÀI LIỆU USER REQUIREMENT

1. **Ngôn ngữ thuần nghiệp vụ (Non-Technical Language):** 
   - Tuyệt đối không sử dụng các từ ngữ kỹ thuật lập trình backend (như database query, REST endpoint, JSON, Webhook signature, state machine internals). 
   - Thanh toán tiền thu mua phế liệu giữa Người dân (User) và Đối tác thu mua (Recycler) được thực hiện trực tiếp giữa hai bên (tiền mặt hoặc chuyển khoản thỏa thuận ngoài hệ thống). Hệ thống không can thiệp giữ tiền hay giải ngân tự động chi trả rác thải.
   - Cổng thanh toán VietQR chỉ được sử dụng duy nhất cho các dịch vụ nội bộ trả phí của nền tảng EcoLink (mua gói tắt quảng cáo, huy hiệu Tích xanh xác minh cơ sở uy tín, gói nâng cao vị trí hiển thị).
2. **Luồng thao tác Actor + Hành động xoay quanh Frontend:**
   - Không sử dụng ký tự mũi tên (`→`) trong mô tả luồng để tránh hiểu lầm thành bước mới; thay vào đó sử dụng các từ ngữ liên kết liền mạch như: *sau đó, đồng thời, và, tiếp tục*.
   - Mỗi bước `NF-xx` thể hiện rõ hành vi người dùng tương tác với các thành phần giao diện (nút bấm, bảng danh mục, hộp thoại, bộ lọc) và phản hồi tương ứng của hệ thống.
3. **Quy chuẩn thông báo (System Message):**
   - Tại các luồng rẽ nhánh, luồng ngoại lệ hoặc các bước hiển thị phản hồi, hệ thống chỉ dẫn chiếu theo mã định danh thông báo chuẩn `MSG-[Module]-[ID]` theo đúng tài liệu [BR & MSG.md](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/BR%20&%20MSG.md), không diễn giải lại nội dung chuỗi ký tự thông báo.
4. **Quy chuẩn Business Rule:**
   - Các quy tắc nghiệp vụ bắt buộc phải gắn tiền tố định danh Module theo định dạng `BR-[Module]-[ID]` và phản ánh chính xác nội dung quy tắc đã được tài liệu hóa.

---

## 2. BẢNG TỔNG HỢP USE CASE PHÂN HỆ RECYCLER VÀ STAFF

| ID | Tên Use Case (EN) | Tên Use Case (VI) | Primary Actor | Secondary Actor | Mục đích chính |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **UC-R01** | Manage Accepted Waste & Pricing | Quản lý Danh mục & Bảng giá thu mua | Recycler | | Thiết lập loại phế liệu chấp nhận thu mua, đơn giá tham khảo và điều kiện thu gom |
| **UC-R02** | Toggle Active Status | Bật/Tắt trạng thái hoạt động | Recycler | Bản đồ số | Bật để hiện diện trên bản đồ tìm kiếm hoặc tắt để tạm ngừng nhận yêu cầu mới |
| **UC-R03** | Accept / Reject Handover Request | Tiếp nhận / Từ chối yêu cầu thu gom | Recycler | User | Tiếp nhận để chốt khung giờ hẹn thu gom hoặc từ chối kèm lý do |
| **UC-R04** | Cancel Collection Appointment | Hủy lịch hẹn thu gom | Recycler | User | Hủy lịch hẹn đã nhận trong trường hợp phát sinh bất khả kháng kèm lý do |
| **UC-R05** | Negotiate via Chatbox | Trao đổi qua Khung chat | Recycler | User | Thương lượng thời gian thu gom thực tế hoặc thỏa thuận lại chi tiết phế liệu |
| **UC-R06** | Create Handover Receipt | Tạo Biên nhận bàn giao phế liệu | Recycler | User | Xác nhận khối lượng thực tế và chụp ảnh hoàn tất bàn giao để tích điểm |
| **UC-R07** | Purchase In-System Services via VietQR | Thanh toán gói dịch vụ nội bộ qua VietQR | Recycler | VietQR | Thanh toán phí mua huy hiệu Tích xanh xác minh hoặc gói dịch vụ qua VietQR động |
| **UC-R08** | Reset Password | Quên / Đặt lại mật khẩu | Recycler | Dịch vụ Email | Khôi phục mật khẩu thông qua liên kết hoặc mã xác thực gửi về hòm thư điện tử |
| **UC-S01** | Review Misclassified Images | Đánh giá ảnh AI nhận diện sai | Staff | | Kiểm tra các hình ảnh rác bị người dùng báo cáo nhận diện không đúng |
| **UC-S02** | Label Image Data | Gán nhãn dữ liệu hình ảnh | Staff | | Điều chỉnh và gắn nhãn phân loại chính xác cho hình ảnh phế liệu |
| **UC-S03** | Update Verified Dataset | Cập nhật Tập dữ liệu kiểm chứng | Staff | | Đóng gói ảnh đã gán nhãn chuẩn vào tập dữ liệu phục vụ huấn luyện AI |
| **UC-S04** | Verify Recycler Application | Thẩm định hồ sơ Đối tác thu mua | Staff | Recycler | Thẩm tra giấy phép và thông tin cơ sở để phê duyệt tài khoản hoạt động |
| **UC-S05** | Handle Transaction Complaints | Xử lý khiếu nại giao dịch | Staff | User, Recycler | Tiếp nhận bằng chứng, hòa giải và đưa ra kết luận xử lý tranh chấp giữa hai bên |
| **UC-S06** | Manage User & Buyer Accounts | Quản lý Tài khoản Người dùng & Đối tác | Staff | | Tra cứu hồ sơ chi tiết và khóa hoặc mở khóa tài khoản có hành vi vi phạm |
| **UC-S07** | Create Blog Post Draft | Soạn thảo bài đăng Blog truyền thông | Staff | Admin | Viết bài chia sẻ kiến thức phân loại rác và gửi Admin phê duyệt xuất bản |
| **UC-S08** | Manage Eco Fact Content | Quản lý Nội dung Kiến thức Eco Fact | Staff | | Thêm, sửa, xóa nội dung thẻ sự thật môi trường và cài đặt mốc mở khóa |
| **UC-S09** | Manage Gift Inventory | Quản lý Kho Quà tặng & Voucher | Staff | | Thiết lập danh mục quà tặng đổi điểm, số lượng tồn kho và ngừng áp dụng |
| **UC-S10** | Staff Daily Check-in | Điểm danh ca làm việc nhân viên | Staff | | Xác nhận bắt đầu và kết thúc ca làm việc hàng ngày của nhân viên vận hành |

---

## 3. ĐẶC TẢ USE CASE PHÂN HỆ RECYCLER (ĐỐI TÁC THU MUA)

### UC-R01: Manage Accepted Waste & Pricing
##### **UC-R01 - Manage Accepted Waste & Pricing (Quản lý Danh mục & Bảng giá thu mua)**

| Primary Actor(s): | Recycler (Đối tác thu mua) | Secondary Actor(s): |  |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép cơ sở thu mua quản lý danh mục các loại phế liệu mà cơ sở tiếp nhận, thiết lập bảng đơn giá công khai trên mỗi kilôgam (VNĐ/kg) và cài đặt điều kiện khối lượng tối thiểu để nhận gom tận nơi. | | |
| **Preconditions:** | 1. Đối tác thu mua đã đăng nhập vào hệ thống và tài khoản đang ở trạng thái đã được kích hoạt.<br>2. Đối tác đang ở màn hình Bảng giá & Danh mục thu mua. | | |
| **Postconditions:** | - **Thành công:** Bảng danh mục phế liệu chấp nhận thu mua và đơn giá của cơ sở được cập nhật thành công, hiển thị công khai trên hồ sơ của cơ sở cho người dân tham khảo.<br>- **Thất bại:** Dữ liệu bảng giá cũ giữ nguyên, hệ thống hiển thị thông báo lỗi để cơ sở điều chỉnh. | | |
| **Normal Flow:** | **Xem danh mục và bảng giá hiện tại**<br>NF-01: Đối tác chọn mục "Bảng giá thu mua" trên thanh điều hướng.<br>NF-02: Hệ thống hiển thị bảng danh sách các loại phế liệu mà cơ sở đang thu mua, bao gồm: Tên loại rác, Phân nhóm (Nhựa, Giấy, Kim loại...), Đơn giá tham khảo (VNĐ/kg), Khối lượng tối thiểu nhận gom tận nơi, và Trạng thái.<br><br>**Thiết lập thêm loại phế liệu mới**<br>NF-03: Đối tác nhấn nút "Thêm loại phế liệu".<br>NF-04: Hệ thống hiển thị biểu mẫu bao gồm danh sách chọn loại rác chuẩn hệ thống, ô nhập đơn giá (VNĐ/kg) và ô nhập khối lượng gom tối thiểu (kg).<br>NF-05: Đối tác chọn loại phế liệu từ danh sách, nhập đơn giá và khối lượng tối thiểu, sau đó nhấn "Lưu áp dụng".<br>NF-06: Hệ thống kiểm tra tính hợp lệ của dữ liệu, sau đó lưu loại phế liệu vào hồ sơ bảng giá của cơ sở, đồng thời làm mới danh sách hiển thị.<br><br>**Cập nhật đơn giá hoặc tạm ngừng thu mua**<br>NF-03 (từ NF-02): Đối tác nhấn biểu tượng "Chỉnh sửa" tại dòng phế liệu mong muốn.<br>NF-04: Hệ thống mở hộp thoại cho phép nhập lại đơn giá hoặc gạt công tắc tạm ngừng thu mua.<br>NF-05: Đối tác thay đổi thông tin đơn giá, sau đó nhấn "Cập nhật".<br>NF-06: Hệ thống ghi nhận mức giá mới và cập nhật lại bảng danh mục của cơ sở. | | |
| **Alternative Flow:** | AF-01: Tại NF-02, Đối tác sử dụng ô tìm kiếm nhanh tên loại phế liệu hoặc lọc theo nhóm chất liệu; hệ thống tự động lọc các dòng tương ứng trên bảng. | | |
| **Extension Flow:** | ETF-01: Tại NF-04, nếu Đối tác nhấn nút "Hủy bỏ" hoặc nhấp ra ngoài hộp thoại, hệ thống đóng biểu mẫu và giữ nguyên toàn bộ thông tin cũ. | | |
| **Exceptions Flow:** | EF-01: Tại NF-05, nếu Đối tác để trống các trường bắt buộc, hệ thống hiển thị thông báo MSG-AUTH-02.<br>EF-02: Tại NF-05, nếu hồ sơ chưa đủ điều kiện công khai do thiếu thông tin loại rác hoặc vị trí, hệ thống hiển thị thông báo MSG-MTH-06.<br>EF-03: Tại NF-06, nếu phiên làm việc của đối tác đã hết hạn, hệ thống trả về thông báo MSG-NTF-01 và chuyển hướng về trang đăng nhập. | | |
| **Business Rule** | BR-MTH-01: Recycler phải được Staff phê duyệt trước khi xuất hiện trong tìm kiếm hoặc nhận yêu cầu bàn giao.<br>BR-MTH-03: Hồ sơ Recycler chỉ được công khai khi có ít nhất một loại rác chấp nhận, điều kiện thu gom và vị trí hợp lệ.<br>BR-MTH-07: Việc gỡ một danh mục khỏi hồ sơ Recycler không ảnh hưởng đến yêu cầu bàn giao đã được chấp nhận. | | |

---

### UC-R02: Toggle Active Status
##### **UC-R02 - Toggle Active Status (Bật/Tắt trạng thái hoạt động trên bản đồ)**

| Primary Actor(s): | Recycler (Đối tác thu mua) | Secondary Actor(s): | Bản đồ số (Map Service) |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép Đối tác thu mua chuyển đổi trạng thái sẵn sàng làm việc (Đang hoạt động / Tạm nghỉ). Khi bật hoạt động, cơ sở sẽ xuất hiện trong kết quả tìm kiếm trên bản đồ của Người dân lân cận; khi tắt, cơ sở sẽ tạm ẩn khỏi bản đồ và không nhận thêm yêu cầu bàn giao mới. | | |
| **Preconditions:** | 1. Đối tác đã đăng nhập thành công vào ứng dụng.<br>2. Đối tác đang ở màn hình Bảng điều khiển chính (Dashboard). | | |
| **Postconditions:** | - **Bật hoạt động:** Cơ sở hiển thị trạng thái sẵn sàng nhận yêu cầu trên bản đồ định vị của Người dân trong bán kính hoạt động.<br>- **Tắt hoạt động:** Cơ sở chuyển sang trạng thái tạm nghỉ trên bản đồ và hệ thống tạm dừng nhận yêu cầu mới.<br>- **Thất bại:** Trạng thái công tắc giữ nguyên như trước khi thao tác. | | |
| **Normal Flow:** | NF-01: Đối tác quan sát nút công tắc gạt trạng thái sẵn sàng tiếp nhận yêu cầu ở góc trên giao diện chính.<br>NF-02: Đối tác gạt công tắc chuyển đổi giữa trạng thái tạm nghỉ và đang hoạt động.<br>NF-03: Hệ thống hiển thị hộp thoại xác nhận yêu cầu chuyển đổi trạng thái tiếp nhận thu gom.<br>NF-04: Đối tác nhấn "Đồng ý".<br>NF-05: Hệ thống ghi nhận trạng thái mới của cơ sở, sau đó cập nhật hiển thị vị trí cơ sở trên bản đồ số, đồng thời hiển thị trạng thái hoạt động tương ứng trên màn hình. | | |
| **Alternative Flow:** |  | | |
| **Extension Flow:** | ETF-01: Tại NF-04, nếu Đối tác chọn "Hủy bỏ", hệ thống giữ nguyên vị trí công tắc trạng thái trước đó và đóng hộp thoại xác nhận. | | |
| **Exceptions Flow:** | EF-01: Tại NF-02, nếu cơ sở chưa thiết lập bất kỳ loại rác thu mua nào hoặc thiếu vị trí hợp lệ, hệ thống trả về thông báo MSG-MTH-06 và không cho phép bật hoạt động.<br>EF-02: Tại NF-05, nếu tài khoản của cơ sở đã bị vô hiệu hóa hoặc đình chỉ, hệ thống trả về thông báo MSG-AUTH-04 và tự động hủy phiên đăng nhập. | | |
| **Business Rule** | BR-MTH-01: Recycler phải được Staff phê duyệt trước khi xuất hiện trong tìm kiếm hoặc nhận yêu cầu bàn giao.<br>BR-MTH-03: Hồ sơ Recycler chỉ được công khai khi có ít nhất một loại rác chấp nhận, điều kiện thu gom và vị trí hợp lệ.<br>BR-MTH-06: Recycler bị đình chỉ/vô hiệu hóa bị ẩn khỏi tìm kiếm ngay, nhưng giao dịch đang hoạt động vẫn hiển thị với cả hai bên. | | |

---

### UC-R03: Accept / Reject Handover Request
##### **UC-R03 - Accept / Reject Handover Request (Tiếp nhận / Từ chối yêu cầu thu gom)**

| Primary Actor(s): | Recycler (Đối tác thu mua) | Secondary Actor(s): | User (Người tạo yêu cầu) |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép Đối tác thu mua xem danh sách các yêu cầu bàn giao phế liệu do người dân gửi đến cơ sở kèm khung giờ hẹn mong muốn, từ đó quyết định tiếp nhận đơn để chốt khung giờ thu gom hoặc từ chối yêu cầu. | | |
| **Preconditions:** | 1. Đối tác đã đăng nhập và đang mở mục Quản lý yêu cầu bàn giao.<br>2. Có ít nhất một yêu cầu bàn giao rác đang ở trạng thái chờ tiếp nhận (Pending). | | |
| **Postconditions:** | - **Nếu tiếp nhận:** Yêu cầu chuyển sang trạng thái đã chấp nhận (Accepted). Khung giờ thu gom chính thức được chốt, thông tin liên hệ được hiển thị và người dân nhận được thông báo.<br>- **Nếu từ chối:** Yêu cầu chuyển sang trạng thái bị từ chối (Rejected), người dân nhận được thông báo để chọn cơ sở khác. | | |
| **Normal Flow:** | **Tiếp nhận yêu cầu thu gom**<br>NF-01: Đối tác chọn mục "Yêu cầu bàn giao" trên thanh quản lý.<br>NF-02: Hệ thống hiển thị danh sách các yêu cầu gồm: Tên người gửi, Khoảng cách (km), Danh mục rác, Khối lượng ước tính, Khung giờ hẹn đề xuất và Ảnh chụp đính kèm.<br>NF-03: Đối tác nhấn vào một yêu cầu để xem thông tin chi tiết.<br>NF-04: Đối tác nhấn nút "Tiếp nhận".<br>NF-05: Hệ thống cập nhật trạng thái yêu cầu sang trạng thái đã chấp nhận (Accepted), sau đó chốt khung giờ hẹn thu gom, hiển thị thông tin liên hệ đầy đủ của người dân, đồng thời gửi thông báo MSG-HND-02 đến người dân.<br><br>**Từ chối yêu cầu thu gom**<br>NF-04 (từ NF-03): Đối tác nhấn nút "Từ chối".<br>NF-05: Hệ thống hiển thị hộp thoại yêu cầu chọn lý do từ chối.<br>NF-06: Đối tác chọn lý do từ chối phù hợp, sau đó nhấn "Xác nhận từ chối".<br>NF-07: Hệ thống cập nhật trạng thái yêu cầu sang bị từ chối (Rejected), đồng thời gửi thông báo cập nhật trạng thái đến người dân. | | |
| **Alternative Flow:** | AF-01: Tại NF-03, nếu khung giờ đề xuất chưa thuận tiện, Đối tác nhấn nút "Nhắn tin" để mở khung chat trao đổi lại với người dân trước khi đưa ra quyết định tiếp nhận (kết nối sang UC-R05). | | |
| **Extension Flow:** | ETF-01: Tại NF-05 (khi từ chối), nếu Đối tác nhấn nút "Hủy", hệ thống đóng hộp thoại chọn lý do và giữ nguyên yêu cầu ở trạng thái chờ tiếp nhận. | | |
| **Exceptions Flow:** | EF-01: Tại NF-04, nếu yêu cầu đã bị người dân hủy trước đó, hệ thống trả về thông báo MSG-HND-05 và làm mới lại danh sách.<br>EF-02: Tại NF-06, nếu Đối tác để trống phần lý do bắt buộc, hệ thống hiển thị thông báo MSG-AUTH-02. | | |
| **Business Rule** | BR-NEG-05: Khung giờ thu gom chính thức được chốt tại đúng thời điểm Recycler bấm "Chấp nhận" (Accept) Yêu cầu bàn giao của User — không cần bước xác nhận lịch riêng biệt nào khác.<br>BR-HND-02: Thay đổi trạng thái bàn giao chỉ theo một chiều: Pending → (Negotiating) → Accepted → Scheduled → Completed, hoặc kết thúc ở Rejected/Cancelled/Expired/Disputed. Không trạng thái nào được quay lại.<br>BR-HND-03: Recycler chỉ được xem và phản hồi các yêu cầu gửi đến chính họ.<br>BR-HND-08: Thông tin liên hệ đầy đủ chỉ hiển thị sau khi yêu cầu được chấp nhận. | | |

---

### UC-R04: Cancel Collection Appointment
##### **UC-R04 - Cancel Collection Appointment (Hủy lịch hẹn thu gom)**

| Primary Actor(s): | Recycler (Đối tác thu mua) | Secondary Actor(s): | User (Người dân có lịch hẹn) |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép Đối tác thu mua hủy một lịch hẹn thu gom đã chấp nhận trước đó khi xảy ra sự cố đột xuất hoặc lý do bất khả kháng, có ghi nhận lý do rõ ràng trên hệ thống. | | |
| **Preconditions:** | 1. Đối tác đã đăng nhập vào hệ thống.<br>2. Yêu cầu bàn giao đang ở trạng thái đã chấp nhận (Accepted hoặc Scheduled) và chưa bắt đầu xác nhận biên nhận. | | |
| **Postconditions:** | - **Thành công:** Yêu cầu chuyển sang trạng thái đã hủy (Cancelled), người dân nhận được thông báo hủy và lý do tương ứng.<br>- **Thất bại:** Lịch hẹn giữ nguyên trạng thái cũ. | | |
| **Normal Flow:** | NF-01: Đối tác vào mục "Lịch trình thu gom", tìm đến yêu cầu đã chấp nhận cần hủy và nhấn chọn xem chi tiết.<br>NF-02: Đối tác nhấn vào nút "Hủy lịch hẹn" trên giao diện chi tiết.<br>NF-03: Hệ thống hiển thị hộp thoại cảnh báo việc hủy lịch hẹn và yêu cầu chọn lý do hủy.<br>NF-04: Đối tác chọn lý do hủy phù hợp, sau đó nhấn "Xác nhận hủy".<br>NF-05: Hệ thống cập nhật trạng thái yêu cầu sang đã hủy (Cancelled), sau đó gửi thông báo MSG-HND-05 đến người dân, đồng thời hiển thị thông báo trạng thái cập nhật trên màn hình của cơ sở. | | |
| **Alternative Flow:** |  | | |
| **Extension Flow:** | ETF-01: Tại NF-04, nếu Đối tác chọn "Quay lại" hoặc đóng hộp thoại, hệ thống giữ nguyên lịch hẹn thu gom. | | |
| **Exceptions Flow:** | EF-01: Tại NF-04, nếu việc hủy diễn ra cận kề trong khoảng thời gian quy định trước giờ hẹn đã xác nhận, hệ thống hiển thị thông báo MSG-HND-06 để cảnh báo trước khi đối tác đưa ra quyết định.<br>EF-02: Tại NF-04, nếu Đối tác để trống trường lý do hủy bắt buộc, hệ thống hiển thị thông báo MSG-AUTH-02. | | |
| **Business Rule** | BR-HND-02: Thay đổi trạng thái bàn giao chỉ theo một chiều: Pending → (Negotiating) → Accepted → Scheduled → Completed, hoặc kết thúc ở Rejected/Cancelled/Expired/Disputed. Không trạng thái nào được quay lại.<br>BR-HND-06: Mỗi bên có thể hủy cho đến khi Completed. Hủy trong vòng {X} giờ trước lịch hẹn đã xác nhận được ghi là hủy muộn. | | |

---

### UC-R05: Negotiate via Chatbox
##### **UC-R05 - Negotiate via Chatbox (Trao đổi qua Khung chat)**

| Primary Actor(s): | Recycler (Đối tác thu mua) | Secondary Actor(s): | User (Người dân) |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép Đối tác thu mua gửi tin nhắn văn bản và hình ảnh trực tiếp với Người dân trong phạm vi một yêu cầu bàn giao cụ thể để làm rõ loại rác, khối lượng hoặc thống nhất lại khung giờ thu gom thuận tiện cho cả hai bên. | | |
| **Preconditions:** | 1. Đối tác đã đăng nhập và đang mở một yêu cầu bàn giao cụ thể.<br>2. Phiên chat của yêu cầu bàn giao đang hoạt động. | | |
| **Postconditions:** | Tin nhắn và hình ảnh trao đổi được gửi đến người dân theo thời gian thực và được lưu trữ toàn bộ để làm căn cứ đối chiếu nếu phát sinh khiếu nại. | | |
| **Normal Flow:** | NF-01: Trên giao diện chi tiết yêu cầu bàn giao, Đối tác nhấn vào biểu tượng "Nhắn tin".<br>NF-02: Hệ thống mở cửa sổ khung chat riêng của yêu cầu đó, hiển thị thông tin tóm tắt yêu cầu ở phần đầu và lịch sử tin nhắn trước đó (nếu có).<br>NF-03: Đối tác nhập nội dung tin nhắn hoặc đính kèm hình ảnh làm rõ thông tin phế liệu.<br>NF-04: Đối tác nhấn nút "Gửi".<br>NF-05: Hệ thống hiển thị tin nhắn mới trên màn hình chat, sau đó gửi thông báo MSG-NEG-02 tới ứng dụng của Người dân. | | |
| **Alternative Flow:** | AF-01: Tại NF-03, Đối tác đưa ra đề nghị điều chỉnh về đơn giá hoặc số lượng phế liệu; hệ thống gửi đề nghị mới và cập nhật thông báo MSG-NEG-03 khi người dân đồng ý. | | |
| **Extension Flow:** | ETF-01: Đối tác nhấn biểu tượng đóng khung chat; toàn bộ nội dung trò chuyện được tự động lưu lại để có thể mở lại bất cứ lúc nào. | | |
| **Exceptions Flow:** | EF-01: Tại NF-03, nếu nội dung tin nhắn vượt quá độ dài tối đa cho phép, hệ thống trả về thông báo MSG-NTF-03.<br>EF-02: Tại NF-03, nếu tệp ảnh đính kèm không đúng định dạng hỗ trợ hoặc vượt quá dung lượng tối đa, hệ thống hiển thị thông báo MSG-CLS-01 hoặc MSG-CLS-02. | | |
| **Business Rule** | BR-NEG-01: Tin nhắn thương lượng chỉ tồn tại trong một yêu cầu bàn giao cụ thể và chỉ hiển thị với User, Recycler của yêu cầu đó (và Staff được phân công khi có tranh chấp). Toàn bộ lịch sử tin nhắn được lưu lại, không bị xóa, để làm bằng chứng nếu xảy ra tranh chấp.<br>BR-NEG-02: Việc đưa ra hoặc thay đổi đề nghị về giá/khối lượng chỉ được thực hiện khi yêu cầu ở trạng thái Pending hoặc Negotiating, và bị khóa khi đã Accepted, Rejected, Cancelled hoặc Expired. Riêng kênh chat vẫn mở cho cả hai bên kể cả sau khi bàn giao đã Completed, để họ có thể tiếp tục liên lạc.<br>BR-NEG-03: Chỉ đề nghị mới nhất có hiệu lực, mỗi đề nghị phản hồi thay thế đề nghị trước đó. Khi chấp nhận, các điều khoản đã thống nhất được lưu thành bản chụp không thể thay đổi.<br>BR-NEG-04: User đề xuất khung giờ thu gom mong muốn ngay trong Yêu cầu bàn giao khi tạo yêu cầu. Nếu khung giờ đó không phù hợp với Recycler, hai bên thương lượng lại qua Chatbox cho đến khi thống nhất. | | |

---

### UC-R06: Create Handover Receipt
##### **UC-R06 - Create Handover Receipt (Tạo Biên nhận bàn giao phế liệu)**

| Primary Actor(s): | Recycler (Đối tác thu mua) | Secondary Actor(s): | User (Người dân bàn giao) |
| :---- | :---- | :---- | :---- |
| **Description:** | Khi có mặt thu gom thực tế tại điểm hẹn, Đối tác thu mua tiến hành cân đo thực tế các loại phế liệu, nhập khối lượng vào hệ thống và chụp ảnh minh chứng bàn giao để xác nhận hoàn tất giao dịch. Hai bên tự thanh toán tiền mua bán phế liệu trực tiếp với nhau ngoài hệ thống. | | |
| **Preconditions:** | 1. Đối tác đang ở địa điểm bàn giao và yêu cầu đang ở trạng thái đã chấp nhận (Accepted hoặc Scheduled).<br>2. Hai bên đã thực hiện cân phế liệu thực tế và thống nhất thanh toán tiền trực tiếp với nhau. | | |
| **Postconditions:** | - **Thành công:** Biên nhận bàn giao được ghi nhận thành công, yêu cầu chuyển trạng thái sang hoàn tất (Completed), hệ thống ghi nhận điểm thưởng Gamification cho cả hai bên.<br>- **Thất bại:** Yêu cầu giữ nguyên trạng thái cũ, không ghi nhận hoàn tất và không cộng điểm. | | |
| **Normal Flow:** | NF-01: Đối tác mở giao diện yêu cầu thu gom tương ứng, sau đó nhấn nút "Tạo biên nhận bàn giao".<br>NF-02: Hệ thống hiển thị biểu mẫu biên nhận gồm các loại phế liệu kèm ô nhập khối lượng thực tế.<br>NF-03: Đối tác nhập số kilôgam thực tế đã cân của từng loại phế liệu, sau đó nhấn nút chụp ảnh để tải lên ảnh chụp phế liệu thực tế hoặc phiếu cân xác nhận.<br>NF-04: Hệ thống tự động tính toán tổng khối lượng và giá trị tham khảo dựa trên đơn giá đã thỏa thuận, đồng thời hiển thị bản xem trước biên nhận.<br>NF-05: Đối tác kiểm tra lại số liệu cùng Người dân, sau đó nhấn nút "Xác nhận hoàn tất bàn giao".<br>NF-06: Hệ thống cập nhật trạng thái yêu cầu sang hoàn tất (Completed), sau đó gửi thông báo MSG-HND-03 đến cả hai bên, đồng thời tự động cộng điểm thưởng cho User và Recycler kèm thông báo MSG-GAM-01. | | |
| **Alternative Flow:** | AF-01: Tại NF-03, nếu có phát sinh thêm loại phế liệu ngoài danh sách ban đầu, Đối tác chọn thêm loại rác mới từ danh mục chấp nhận và nhập khối lượng cân tương ứng. | | |
| **Extension Flow:** | ETF-01: Tại NF-05, nếu hai bên chưa thống nhất về khối lượng, Đối tác nhấn nút "Hủy tạo biên nhận", hệ thống đóng biểu mẫu và giữ nguyên trạng thái yêu cầu để hai bên tiếp tục trao đổi. | | |
| **Exceptions Flow:** | EF-01: Tại NF-04, nếu khối lượng phế liệu thực tế chênh lệch vượt quá tỷ lệ quy định so với ước tính ban đầu, hệ thống hiển thị cảnh báo MSG-HND-04 để hai bên kiểm tra lại trước khi xác nhận.<br>EF-02: Tại NF-03, nếu Đối tác để trống khối lượng hoặc chưa đính kèm ảnh minh chứng bắt buộc, hệ thống hiển thị thông báo MSG-AUTH-02. | | |
| **Business Rule** | BR-HND-04: Bàn giao chỉ Completed khi Recycler xác nhận kèm ảnh và User xác nhận biên nhận.<br>BR-HND-05: Quyết toán dựa trên khối lượng thực tế Recycler xác nhận và đơn giá đã thống nhất. Nếu lệch quá {X}%, User có thể chấp nhận hoặc khiếu nại trước khi quyết toán.<br>BR-HND-07: Sau khi Completed, giao dịch không thể sửa hoặc xóa, chỉ có thể xem.<br>BR-GAM-01: Điểm chỉ được cộng cho các sự kiện đã xác minh, như bàn giao Completed (dựa trên danh mục và khối lượng đã xác nhận) — cộng cho cả User và Recycler đã hoàn tất giao dịch — và chỉ một lần cho mỗi sự kiện với mỗi vai trò. Không cộng điểm cho giao dịch hủy/tranh chấp; điểm bị thu hồi nếu khiếu nại đảo ngược giao dịch. | | |

---

### UC-R07: Purchase In-System Services via VietQR
##### **UC-R07 - Purchase In-System Services via VietQR (Thanh toán gói dịch vụ nội bộ qua VietQR)**

| Primary Actor(s): | Recycler (Đối tác thu mua) | Secondary Actor(s): | Dịch vụ VietQR |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép Đối tác thu mua đăng ký mua các gói dịch vụ gia tăng nội bộ của nền tảng EcoLink (như huy hiệu Tích xanh xác minh cơ sở uy tín, gói đẩy vị trí hiển thị ưu tiên trên bản đồ, hoặc gói tính năng nâng cao) bằng cách quét mã VietQR động được tạo riêng cho từng đơn hàng. | | |
| **Preconditions:** | 1. Đối tác thu mua đã đăng nhập vào hệ thống quản lý cơ sở.<br>2. Đối tác đang ở màn hình Gói dịch vụ & Nâng cấp tài khoản. | | |
| **Postconditions:** | - **Thành công:** Gói dịch vụ được kích hoạt thành công cho cơ sở (ví dụ hiển thị huy hiệu Tích xanh xác minh trên hồ sơ), thông báo xác nhận thanh toán được gửi tới đối tác.<br>- **Thất bại:** Gói dịch vụ chưa được kích hoạt, không có thay đổi quyền lợi trên tài khoản. | | |
| **Normal Flow:** | NF-01: Đối tác chọn mục "Gói dịch vụ EcoLink" trên thanh điều hướng.<br>NF-02: Hệ thống hiển thị danh mục các gói dịch vụ có sẵn, bao gồm: Tên gói (Ví dụ: Huy hiệu Tích xanh xác minh cơ sở, Gói hiển thị nổi bật), Quyền lợi, Thời hạn hiệu lực và Mức phí niêm yết.<br>NF-03: Đối tác chọn gói dịch vụ mong muốn, sau đó nhấn nút "Đăng ký thanh toán".<br>NF-04: Hệ thống tạo đơn hàng dịch vụ nội bộ, sau đó tạo và hiển thị mã VietQR động chứa chính xác số tiền và mã tham chiếu giao dịch tương ứng với thông báo MSG-PAY-01.<br>NF-05: Đối tác mở ứng dụng ngân hàng di động, thực hiện quét mã VietQR trên màn hình và hoàn tất chuyển khoản theo đúng thông tin hiển thị.<br>NF-06: Hệ thống xác thực thanh toán hợp lệ đúng số tiền và nội dung tham chiếu, sau đó tự động kích hoạt tính năng của gói dịch vụ trên tài khoản của cơ sở, đồng thời gửi thông báo MSG-PAY-02 đến màn hình của Đối tác. | | |
| **Alternative Flow:** |  | | |
| **Extension Flow:** | ETF-01: Tại NF-04, nếu Đối tác nhấn nút "Hủy thanh toán", hệ thống đóng màn hình mã QR và hủy đơn hàng dịch vụ chưa thanh toán. | | |
| **Exceptions Flow:** | EF-01: Tại NF-05, nếu quá thời hạn hiệu lực của mã thanh toán mà chưa nhận được xác nhận hợp lệ, hệ thống trả về thông báo MSG-PAY-03 và cho phép tạo lại mã mới.<br>EF-02: Tại NF-06, nếu số tiền thanh toán không khớp với mã tham chiếu đơn hàng, hệ thống ghi nhận trạng thái chờ đối soát và thông báo đối tác liên hệ bộ phận hỗ trợ. | | |
| **Business Rule** | BR-PAY-01: Khoản thanh toán dịch vụ nội bộ (như mua gói tắt quảng cáo, huy hiệu Tích xanh xác minh, hoặc các tính năng nâng cao) của User/Recycler được thực hiện qua mã VietQR động gắn với mã đơn hàng riêng, và chỉ được ghi nhận thành công sau khi hệ thống xác minh thanh toán hợp lệ (đúng số tiền và mã tham chiếu).<br>BR-PAY-02: Sự kiện thanh toán các gói dịch vụ nội bộ được xử lý theo tính idempotent, đảm bảo mỗi giao dịch thanh toán qua VietQR chỉ được ghi nhận và kích hoạt dịch vụ đúng một lần. | | |

---

### UC-R08: Reset Password
##### **UC-R08 - Reset Password (Quên / Đặt lại mật khẩu)**

| Primary Actor(s): | Recycler (Đối tác thu mua) | Secondary Actor(s): | Dịch vụ Email |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép Đối tác thu mua khôi phục lại mật khẩu đăng nhập tài khoản khi bị quên thông qua liên kết đặt lại mật khẩu bảo mật gửi về địa chỉ email đã đăng ký. | | |
| **Preconditions:** | 1. Đối tác có tài khoản trên hệ thống nhưng không nhớ mật khẩu đăng nhập.<br>2. Đối tác đang ở màn hình Đăng nhập của ứng dụng. | | |
| **Postconditions:** | - **Thành công:** Mật khẩu mới được cập nhật, mật khẩu cũ và mọi phiên đăng nhập cũ bị hủy bỏ hoàn toàn.<br>- **Thất bại:** Mật khẩu tài khoản không thay đổi, tài khoản giữ nguyên trạng thái. | | |
| **Normal Flow:** | NF-01: Tại màn hình Đăng nhập, Đối tác chọn liên kết "Quên mật khẩu?".<br>NF-02: Hệ thống hiển thị biểu mẫu yêu cầu nhập địa chỉ Email đăng ký tài khoản.<br>NF-03: Đối tác nhập địa chỉ email của mình, sau đó nhấn "Gửi yêu cầu".<br>NF-04: Hệ thống ghi nhận yêu cầu và gửi liên kết đặt lại mật khẩu dùng một lần về email của người dùng nếu email tồn tại, đồng thời hiển thị thông báo MSG-AUTH-05 trên giao diện.<br>NF-05: Đối tác mở hòm thư email, nhấn vào liên kết đặt lại mật khẩu trong thư để mở trang thiết lập mật khẩu mới.<br>NF-06: Đối tác nhập Mật khẩu mới và Nhập lại mật khẩu mới, sau đó nhấn "Xác nhận đặt lại mật khẩu".<br>NF-07: Hệ thống kiểm tra liên kết hợp lệ, sau đó lưu mật khẩu mới, đồng thời hủy bỏ mọi phiên đăng nhập hiện có và điều hướng Đối tác về trang Đăng nhập. | | |
| **Alternative Flow:** |  | | |
| **Extension Flow:** | ETF-01: Tại màn hình nhập email, Đối tác nhấn "Quay lại Đăng nhập" để hủy thao tác quên mật khẩu. | | |
| **Exceptions Flow:** | EF-01: Tại NF-03, nếu Đối tác để trống ô email, hệ thống hiển thị thông báo MSG-AUTH-02.<br>EF-02: Tại NF-03, nếu có quá nhiều yêu cầu đặt lại mật khẩu được gửi liên tục cho cùng một email hoặc IP, hệ thống áp dụng giới hạn và hiển thị thông báo MSG-AUTH-07.<br>EF-03: Tại NF-05, nếu liên kết đặt lại đã hết hạn sử dụng hoặc đã được dùng trước đó, hệ thống hiển thị thông báo lỗi MSG-AUTH-06. | | |
| **Business Rule** | BR-AUTH-06: Liên kết đặt lại mật khẩu chỉ dùng được một lần và hết hạn sau {X} phút. Phản hồi giống nhau dù email có tồn tại hay không. Đặt lại mật khẩu sẽ hủy mọi phiên hiện có.<br>BR-AUTH-09: Yêu cầu đặt lại mật khẩu cho cùng một email bị giới hạn {N} lần trong {X} phút, và yêu cầu từ cùng một IP bị giới hạn {M} lần mỗi giờ. Yêu cầu vượt giới hạn bị âm thầm bỏ qua (vẫn hiển thị phản hồi chung như bình thường). | | |

---

## 4. ĐẶC TẢ USE CASE PHÂN HỆ STAFF (NHÂN VIÊN VẬN HÀNH)

### UC-S01: Review Misclassified Images
##### **UC-S01 - Review Misclassified Images (Đánh giá ảnh AI nhận diện sai)**

| Primary Actor(s): | Staff (Nhân viên vận hành) | Secondary Actor(s): |  |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép nhân viên vận hành kiểm tra và đánh giá danh sách các hình ảnh rác do người dùng gửi báo cáo nhận diện sai hoặc các ảnh có độ tin cậy thấp, nhằm xác định phân loại thực tế của vật thể trong ảnh. | | |
| **Preconditions:** | 1. Nhân viên đã đăng nhập vào hệ thống quản trị Back-office với quyền `Staff`.<br>2. Nhân viên đang ở phân hệ Quản lý Dữ liệu AI. | | |
| **Postconditions:** | Báo cáo ảnh được chuyển sang trạng thái đã đánh giá, sẵn sàng chuyển giao cho bước gán nhãn dữ liệu chuẩn thức. | | |
| **Normal Flow:** | NF-01: Nhân viên chọn mục "Hàng đợi ảnh báo cáo phân loại" trên thanh menu bên trái.<br>NF-02: Hệ thống hiển thị danh sách các ảnh cần đánh giá gồm: Ảnh thu nhỏ, Dự đoán của AI, Độ tin cậy (%), Danh mục người dùng đề xuất, Ngày báo cáo và Trạng thái.<br>NF-03: Nhân viên nhấn chọn vào một dòng ảnh để mở màn hình đánh giá chi tiết.<br>NF-04: Hệ thống hiển thị ảnh kích thước đầy đủ cùng các công cụ phóng to, thu nhỏ và thông tin ghi chú liên quan.<br>NF-05: Nhân viên kiểm tra kỹ vật thể trong ảnh, sau đó chọn hành động xác nhận chuyển ảnh sang bước gán nhãn hoặc bác bỏ báo cáo nếu dự đoán ban đầu của AI là chính xác.<br>NF-06: Hệ thống cập nhật trạng thái xử lý của ảnh, sau đó hiển thị thông báo cập nhật thành công và tự động chuyển sang ảnh tiếp theo trong hàng đợi. | | |
| **Alternative Flow:** | AF-01: Tại NF-02, Nhân viên lọc danh sách theo mức độ tin cậy của AI hoặc theo nhóm danh mục phế liệu để đánh giá theo từng chuyên đề. | | |
| **Extension Flow:** | ETF-01: Tại NF-04, nếu Nhân viên nhấn nút "Bỏ qua", hệ thống giữ nguyên trạng thái ảnh trong hàng đợi và chuyển sang hiển thị ảnh kế tiếp. | | |
| **Exceptions Flow:** | EF-01: Tại NF-04, nếu ảnh chụp bị mờ nhòe hoàn toàn không thể nhận diện được vật thể, Nhân viên chọn hành động hủy bỏ ảnh; hệ thống đánh dấu loại trừ ảnh khỏi tập huấn luyện. | | |
| **Business Rule** | BR-CLS-05: Mỗi báo cáo phân loại sai được liên kết với ảnh gốc, danh mục AI dự đoán và danh mục người dùng đề xuất, và vào hàng đợi duyệt của Staff.<br>BR-CLS-06: Chỉ ảnh đã được Staff duyệt và gán nhãn mới được đưa vào tập dữ liệu huấn luyện/kiểm thử.<br>BR-CLS-08: Ảnh rác chỉ được truy cập bởi chủ sở hữu, Recycler trong giao dịch liên quan đang hoạt động, Staff đang duyệt phản hồi, và Admin. | | |

---

### UC-S02: Label Image Data
##### **UC-S02 - Label Image Data (Gán nhãn dữ liệu hình ảnh)**

| Primary Actor(s): | Staff (Nhân viên vận hành) | Secondary Actor(s): |  |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép nhân viên vận hành lựa chọn nhãn danh mục rác chuẩn xác từ bảng danh mục hệ thống để gán vào hình ảnh đã được thẩm định, tạo nguồn dữ liệu được gán nhãn phục vụ huấn luyện mô hình. | | |
| **Preconditions:** | 1. Nhân viên đang mở một hình ảnh ở trạng thái đã đánh giá và sẵn sàng gán nhãn.<br>2. Danh mục rác chuẩn của hệ thống đang hoạt động bình thường. | | |
| **Postconditions:** | Bức ảnh được gắn nhãn danh mục chuẩn, chuyển trạng thái sang đã gán nhãn chuẩn và sẵn sàng đưa vào tập dữ liệu kiểm chứng. | | |
| **Normal Flow:** | NF-01: Trên màn hình chi tiết ảnh, Nhân viên nhấn nút "Gán nhãn chuẩn".<br>NF-02: Hệ thống hiển thị bảng danh sách các danh mục rác chuẩn đang hoạt động của hệ thống.<br>NF-03: Nhân viên tìm kiếm và chọn đúng nhãn danh mục tương ứng với vật thể trong ảnh, sau đó nhấn nút "Lưu nhãn".<br>NF-04: Hệ thống ghi nhận nhãn chuẩn cho bức ảnh, sau đó cập nhật trạng thái dữ liệu thành đã chuẩn hóa và hiển thị thông báo hoàn thành thao tác. | | |
| **Alternative Flow:** |  | | |
| **Extension Flow:** | ETF-01: Tại NF-02, nếu Nhân viên nhấn nút "Hủy", hệ thống đóng bảng danh mục và không thay đổi dữ liệu hiện tại của ảnh. | | |
| **Exceptions Flow:** | EF-01: Tại NF-03, nếu Nhân viên nhấn nút Lưu mà chưa chọn danh mục nào, hệ thống hiển thị thông báo MSG-AUTH-02. | | |
| **Business Rule** | BR-CLS-06: Chỉ ảnh đã được Staff duyệt và gán nhãn mới được đưa vào tập dữ liệu huấn luyện/kiểm thử.<br>BR-CLS-07: Một ảnh không được xuất hiện đồng thời trong cả tập huấn luyện và tập kiểm thử. Chỉ Staff mới được cập nhật tập kiểm thử. | | |

---

### UC-S03: Update Verified Dataset
##### **UC-S03 - Update Verified Dataset (Cập nhật Tập dữ liệu kiểm chứng)**

| Primary Actor(s): | Staff (Nhân viên vận hành) | Secondary Actor(s): |  |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép nhân viên vận hành tổng hợp các hình ảnh đã được thẩm định và gán nhãn chuẩn để đóng gói bổ sung vào Tập dữ liệu kiểm chứng (Verified Dataset), phục vụ công tác đánh giá và huấn luyện định kỳ cho mô hình AI. | | |
| **Preconditions:** | 1. Nhân viên đã đăng nhập với vai trò Staff và đang ở mục Quản lý tập dữ liệu.<br>2. Có các ảnh mới ở trạng thái đã chuẩn hóa sẵn sàng đóng gói. | | |
| **Postconditions:** | Tập dữ liệu kiểm chứng được nâng cấp phiên bản mới chứa các mẫu ảnh đã chuẩn hóa, đồng thời tạo bản ghi lịch sử phục vụ theo dõi. | | |
| **Normal Flow:** | NF-01: Nhân viên chọn mục "Tập dữ liệu kiểm chứng" trên menu quản trị.<br>NF-02: Hệ thống hiển thị thông tin phiên bản dữ liệu hiện tại, thống kê số lượng mẫu ảnh theo từng danh mục và số lượng ảnh mới đang chờ đóng gói.<br>NF-03: Nhân viên nhấn nút "Cập nhật tập dữ liệu".<br>NF-04: Hệ thống hiển thị hộp thoại xác nhận kèm thống kê phân bổ số lượng ảnh mới theo từng nhãn danh mục.<br>NF-05: Nhân viên kiểm tra số liệu phân bổ, nhập tên ghi chú đợt cập nhật, sau đó nhấn "Xác nhận cập nhật".<br>NF-06: Hệ thống bổ sung các ảnh mới vào kho dữ liệu kiểm chứng chính thức, sau đó tạo bản ghi lịch sử phiên bản mới và thông báo cập nhật thành công. | | |
| **Alternative Flow:** | AF-01: Tại NF-02, Nhân viên nhấn nút "Xuất báo cáo dữ liệu" để tải xuống bảng thống kê phân bổ số lượng mẫu ảnh dưới dạng file bảng tính theo thông báo MSG-ADM-02. | | |
| **Extension Flow:** | ETF-01: Tại NF-04, nếu Nhân viên chọn "Hủy bỏ", hệ thống đóng hộp thoại xác nhận và giữ nguyên danh sách ảnh chờ. | | |
| **Exceptions Flow:** | EF-01: Tại NF-05, nếu Nhân viên để trống nội dung ghi chú bắt buộc, hệ thống hiển thị thông báo MSG-AUTH-02. | | |
| **Business Rule** | BR-CLS-06: Chỉ ảnh đã được Staff duyệt và gán nhãn mới được đưa vào tập dữ liệu huấn luyện/kiểm thử.<br>BR-CLS-07: Một ảnh không được xuất hiện đồng thời trong cả tập huấn luyện và tập kiểm thử. Chỉ Staff mới được cập nhật tập kiểm thử. | | |

---

### UC-S04: Verify Recycler Application
##### **UC-S04 - Verify Recycler Application (Thẩm định hồ sơ Đối tác thu mua)**

| Primary Actor(s): | Staff (Nhân viên vận hành) | Secondary Actor(s): | Recycler (Đối tác nộp hồ sơ) |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép nhân viên vận hành thẩm định các hồ sơ đăng ký của cơ sở thu mua hoặc người thu gom phế liệu (bao gồm kiểm tra giấy tờ tùy thân/giấy phép, địa chỉ cơ sở và ảnh thực tế) để phê duyệt kích hoạt tài khoản hoặc từ chối kèm lý do rõ ràng. | | |
| **Preconditions:** | 1. Nhân viên đã đăng nhập và đang ở phân hệ Quản lý Đối tác thu mua.<br>2. Có hồ sơ đối tác ở trạng thái chờ thẩm định (Pending Verification). | | |
| **Postconditions:** | - **Nếu Duyệt:** Tài khoản đối tác chuyển sang trạng thái đã xác minh (Verified), cơ sở có quyền thiết lập bảng giá và xuất hiện trên bản đồ tìm kiếm kèm thông báo MSG-MTH-04 gửi đến đối tác.<br>- **Nếu Từ chối:** Hồ sơ chuyển trạng thái từ chối (Rejected), đối tác nhận được thông báo kèm lý do theo mẫu MSG-MTH-05. | | |
| **Normal Flow:** | **Kiểm tra và Phê duyệt hồ sơ**<br>NF-01: Nhân viên chọn tab "Hồ sơ chờ thẩm định" trong danh sách Đối tác.<br>NF-02: Hệ thống hiển thị danh sách hồ sơ với các thông tin tóm tắt: Tên cơ sở, Số điện thoại, Địa chỉ hoạt động, và Ngày nộp hồ sơ.<br>NF-03: Nhân viên nhấn chọn một hồ sơ để xem chi tiết tài liệu đính kèm.<br>NF-04: Hệ thống hiển thị chi tiết hồ sơ gồm: Ảnh giấy tờ đăng ký hoặc CCCD, Ảnh cơ sở kho bãi, Địa chỉ định vị trên bản đồ số và Thông tin người đại diện.<br>NF-05: Sau khi đối chiếu xác minh thông tin hợp lệ, Nhân viên nhấn nút "Phê duyệt hồ sơ".<br>NF-06: Hệ thống hiển thị hộp thoại xác nhận, sau đó Nhân viên nhấn "Đồng ý".<br>NF-07: Hệ thống cập nhật trạng thái tài khoản đối tác thành đã duyệt, đồng thời gửi thông báo MSG-MTH-04 đến đối tác và làm mới danh sách chờ.<br><br>**Từ chối hoặc yêu cầu bổ sung hồ sơ**<br>NF-05 (từ NF-04): Nếu hồ sơ chưa đạt yêu cầu, Nhân viên nhấn nút "Từ chối hồ sơ".<br>NF-06: Hệ thống hiển thị biểu mẫu yêu cầu chọn và nhập lý do từ chối cụ thể.<br>NF-07: Nhân viên chọn lý do và nhập hướng dẫn bổ sung, sau đó nhấn "Gửi phản hồi".<br>NF-08: Hệ thống cập nhật trạng thái hồ sơ sang từ chối, đồng thời gửi thông báo MSG-MTH-05 đến đối tác và đóng chi tiết hồ sơ. | | |
| **Alternative Flow:** |  | | |
| **Extension Flow:** | ETF-01: Tại NF-06, nếu Nhân viên nhấn "Hủy bỏ", hệ thống đóng hộp thoại và giữ nguyên trạng thái hồ sơ đang chờ duyệt. | | |
| **Exceptions Flow:** | EF-01: Tại NF-07 (khi từ chối), nếu Nhân viên để trống nội dung giải thích lý do, hệ thống trả về thông báo MSG-AUTH-02 và ngăn chặn việc gửi phản hồi. | | |
| **Business Rule** | BR-MTH-01: Recycler phải được Staff phê duyệt trước khi xuất hiện trong tìm kiếm hoặc nhận yêu cầu bàn giao.<br>BR-MTH-02: Khi từ chối xác minh phải ghi rõ lý do, Recycler có thể nộp lại hồ sơ đã chỉnh sửa.<br>BR-ADM-02: Phân tách vai trò: Admin phụ trách chính sách, danh mục, cấu hình bảng xếp hạng, tài khoản Staff, báo cáo. Staff phụ trách khiếu nại, gán nhãn, xác minh, kho phần thưởng, cấu hình khảo sát, trạng thái tài khoản.<br>BR-ADM-05: Mọi thao tác của Admin/Staff về cấu hình, trạng thái tài khoản, xác minh và quyết định khiếu nại đều được ghi vào nhật ký kiểm toán bất biến. | | |

---

### UC-S05: Handle Transaction Complaints
##### **UC-S05 - Handle Transaction Complaints (Xử lý khiếu nại giao dịch)**

| Primary Actor(s): | Staff (Nhân viên vận hành) | Secondary Actor(s): | User, Recycler |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép nhân viên vận hành tiếp nhận và xử lý các khiếu nại phát sinh từ Người dân hoặc Cơ sở thu mua liên quan đến giao dịch bàn giao rác (như chênh lệch khối lượng thực tế, không đến thu gom dù đã hẹn, hành vi không đúng chuẩn mực); nhân viên đóng vai trò trung gian đối soát bằng chứng và ban hành khuyến nghị giải quyết ràng buộc cho hai bên. | | |
| **Preconditions:** | 1. Nhân viên đã đăng nhập và đang mở phân hệ Quản lý Khiếu nại.<br>2. Có đơn khiếu nại đang ở trạng thái chờ giải quyết. | | |
| **Postconditions:** | Khiếu nại được đóng với kết luận xử lý rõ ràng, thông báo MSG-DSP-02 được gửi đến các bên liên quan để thực hiện theo phán quyết. | | |
| **Normal Flow:** | NF-01: Nhân viên chọn mục "Khiếu nại giao dịch" trên thanh menu.<br>NF-02: Hệ thống hiển thị danh sách đơn khiếu nại gồm: Mã khiếu nại, Mã giao dịch, Bên gửi khiếu nại, Phân loại vấn đề và Thời gian gửi.<br>NF-03: Nhân viên nhấn chọn một khiếu nại để mở hồ sơ giải quyết.<br>NF-04: Hệ thống hiển thị thông tin chi tiết: Nội dung khiếu nại, Ảnh chụp bằng chứng đính kèm, Chi tiết biên nhận bàn giao, và Toàn bộ lịch sử tin nhắn trò chuyện giữa hai bên.<br>NF-05: Nhân viên đối soát các chứng cứ khách quan, sau đó chọn kết luận xử lý (Ví dụ: Chấp nhận khiếu nại và yêu cầu bên vi phạm bồi hoàn trực tiếp; hoặc Bác bỏ khiếu nại do không có căn cứ).<br>NF-06: Nhân viên nhập tóm tắt nội dung phán quyết giải thích rõ lý do, sau đó nhấn "Ban hành kết luận & Đóng khiếu nại".<br>NF-07: Hệ thống cập nhật trạng thái khiếu nại thành đã giải quyết, đồng thời gửi thông báo kết quả giải quyết MSG-DSP-02 đến cả hai bên tham gia giao dịch. | | |
| **Alternative Flow:** | AF-01: Tại NF-05, nếu chứng cứ từ hai bên chưa đầy đủ, Nhân viên cập nhật trạng thái đơn thành "Đang xác minh bổ sung" để yêu cầu các bên cung cấp thêm hình ảnh đối soát. | | |
| **Extension Flow:** | ETF-01: Nhân viên nhấn nút "Quay lại danh sách", hệ thống đóng hồ sơ chi tiết và giữ nguyên trạng thái khiếu nại hiện tại. | | |
| **Exceptions Flow:** | EF-01: Tại NF-06, nếu Nhân viên để trống nội dung kết luận xử lý bắt buộc, hệ thống hiển thị thông báo MSG-AUTH-02. | | |
| **Business Rule** | BR-DSP-01: Chỉ User hoặc Recycler là bên tham gia giao dịch mới được khiếu nại, trong vòng {N} ngày sau khi hoàn tất, tối đa một khiếu nại đang mở cho mỗi giao dịch.<br>BR-DSP-02: Khiếu nại phải có phân loại lý do và mô tả. Ảnh minh chứng là tùy chọn.<br>BR-DSP-04: Chỉ Staff được giải quyết khiếu nại, mỗi quyết định phải ghi kết quả (VD: User được hoàn toàn phần/toàn bộ từ Recycler, bác bỏ) và lý do. Do thanh toán diễn ra trực tiếp giữa hai bên, kết quả là một khuyến nghị ràng buộc được thông báo cho cả hai — không phải lệnh chuyển tiền tự động.<br>BR-DSP-06: Dữ liệu khiếu nại (tin nhắn, ảnh, chi tiết giao dịch) chỉ được truy cập bởi hai bên và Staff được phân công. | | |

---

### UC-S06: Manage User & Buyer Accounts
##### **UC-S06 - Manage User & Buyer Accounts (Quản lý Tài khoản Người dùng & Đối tác)**

| Primary Actor(s): | Staff (Nhân viên vận hành) | Secondary Actor(s): |  |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép nhân viên vận hành tra cứu danh sách, xem chi tiết thông tin và lịch sử hoạt động của tài khoản Người dân (User) hoặc Đối tác thu mua (Buyer); thực hiện đổi trạng thái tài khoản (khóa tạm thời hoặc mở khóa tài khoản vi phạm chính sách) kèm theo lý do cụ thể. | | |
| **Preconditions:** | 1. Nhân viên đã đăng nhập và được phân quyền quản lý trạng thái tài khoản thành viên.<br>2. Nhân viên đang ở phân hệ Quản lý Người dùng. | | |
| **Postconditions:** | - **Nếu khóa tài khoản:** Trạng thái tài khoản chuyển sang bị đình chỉ, mọi phiên đăng nhập của tài khoản bị hủy ngay lập tức.<br>- **Nếu mở khóa:** Tài khoản khôi phục quyền truy cập bình thường.<br>- **Thất bại:** Trạng thái tài khoản được giữ nguyên. | | |
| **Normal Flow:** | **Tra cứu và xem hồ sơ tài khoản**<br>NF-01: Nhân viên chọn mục "Quản lý Tài khoản" trên menu.<br>NF-02: Hệ thống hiển thị bảng danh sách các tài khoản kèm các bộ lọc: Vai trò (Người dân / Đối tác thu mua), Trạng thái, và Ngày tham gia.<br>NF-03: Nhân viên nhập số điện thoại hoặc email để tìm kiếm tài khoản cụ thể, sau đó nhấn "Xem chi tiết".<br>NF-04: Hệ thống hiển thị chi tiết hồ sơ: Thông tin cá nhân, Lịch sử bàn giao và các báo cáo khiếu nại liên quan.<br><br>**Đổi trạng thái tài khoản vi phạm (Khóa tài khoản)**<br>NF-05: Khi phát hiện tài khoản có hành vi vi phạm quy chuẩn, Nhân viên nhấn nút "Khóa tài khoản".<br>NF-06: Hệ thống hiển thị hộp thoại yêu cầu chọn thời hạn khóa và nhập lý do khóa bắt buộc.<br>NF-07: Nhân viên chọn thời hạn và nhập lý do vi phạm, sau đó nhấn "Xác nhận khóa".<br>NF-08: Hệ thống cập nhật trạng thái tài khoản thành bị khóa, sau đó hủy ngay mọi phiên đăng nhập đang hoạt động của người dùng đó, đồng thời hiển thị thông báo MSG-ADM-01 trên giao diện của Staff.<br><br>**Mở khóa tài khoản**<br>NF-05 (đối với tài khoản đang bị khóa): Nhân viên nhấn nút "Mở khóa tài khoản".<br>NF-06: Hệ thống hiển thị hộp thoại yêu cầu nhập lý do mở khóa.<br>NF-07: Nhân viên nhập lý do mở lại quyền truy cập, sau đó nhấn "Xác nhận".<br>NF-08: Hệ thống khôi phục trạng thái hoạt động cho tài khoản và hiển thị thông báo MSG-ADM-01. | | |
| **Alternative Flow:** | AF-01: Tại NF-02, Nhân viên lọc danh sách các tài khoản có lịch sử bị khiếu nại để rà soát rủi ro định kỳ. | | |
| **Extension Flow:** | ETF-01: Tại NF-06, nếu Nhân viên chọn "Hủy bỏ", hệ thống đóng hộp thoại và giữ nguyên trạng thái tài khoản hiện tại. | | |
| **Exceptions Flow:** | EF-01: Tại NF-07, nếu Nhân viên không nhập lý do thay đổi trạng thái bắt buộc, hệ thống hiển thị thông báo MSG-AUTH-02.<br>EF-02: Tại NF-05, nếu Nhân viên thao tác trên tài khoản thuộc vai trò Admin hoặc Staff khác, hệ thống ngăn chặn và hiển thị thông báo MSG-ADM-04. | | |
| **Business Rule** | BR-AUTH-05: Tài khoản đã bị vô hiệu hóa hoặc đình chỉ không thể đăng nhập, dù mật khẩu đúng, và mọi phiên đang hoạt động bị hủy ngay lập tức.<br>BR-ADM-02: Phân tách vai trò: Admin phụ trách chính sách, danh mục, cấu hình bảng xếp hạng, tài khoản Staff, báo cáo. Staff phụ trách khiếu nại, gán nhãn, xác minh, kho phần thưởng, cấu hình khảo sát, trạng thái tài khoản.<br>BR-ADM-03: Staff chỉ được đổi trạng thái tài khoản User/Recycler khi có ghi lý do, không được đổi tài khoản Admin hay Staff khác.<br>BR-ADM-04: Tài khoản đã có lịch sử giao dịch chỉ được vô hiệu hóa, không được xóa.<br>BR-ADM-05: Mọi thao tác của Admin/Staff về cấu hình, trạng thái tài khoản, xác minh và quyết định khiếu nại đều được ghi vào nhật ký kiểm toán bất biến. | | |

---

### UC-S07: Create Blog Post Draft
##### **UC-S07 - Create Blog Post Draft (Soạn thảo bài đăng Blog truyền thông)**

| Primary Actor(s): | Staff (Nhân viên vận hành) | Secondary Actor(s): | Admin (Người phê duyệt) |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép nhân viên vận hành soạn thảo các bài viết chia sẻ kiến thức sống xanh, mẹo phân loại rác hoặc tin tức môi trường bằng trình soạn thảo nội dung trực quan, sau đó lưu bản nháp hoặc gửi lên Quản trị viên (Admin) phê duyệt xuất bản. | | |
| **Preconditions:** | 1. Nhân viên đã đăng nhập và đang ở phân hệ Quản lý Tin tức / Blog. | | |
| **Postconditions:** | - **Lưu nháp:** Bài viết được lưu ở trạng thái bản nháp (Draft), chỉ hiển thị trong nội bộ hệ thống quản trị.<br>- **Gửi duyệt:** Bài viết chuyển trạng thái sang chờ duyệt (Pending_Approval) kèm thông báo MSG-ADM-06, xuất hiện trong hàng đợi duyệt của Admin.<br>- **Thất bại:** Dữ liệu soạn thảo chưa lưu không được cập nhật lên hệ thống. | | |
| **Normal Flow:** | NF-01: Nhân viên chọn mục "Bài viết Blog" trên menu, sau đó nhấn nút "Tạo bài viết mới".<br>NF-02: Hệ thống hiển thị giao diện soạn thảo gồm: Ô nhập Tiêu đề, Chọn chuyên mục, Tải ảnh bìa đại diện, Trình soạn thảo văn bản và ô Tóm tắt nội dung.<br>NF-03: Nhân viên nhập tiêu đề, chọn ảnh bìa và soạn thảo nội dung bài viết.<br>NF-04: Nhân viên nhấn nút "Xem trước" để kiểm tra giao diện bài viết hiển thị trên thiết bị người dùng.<br>NF-05: Sau khi kiểm tra hoàn chỉnh, Nhân viên nhấn nút "Gửi duyệt xuất bản".<br>NF-06: Hệ thống kiểm tra các trường bắt buộc, sau đó lưu bài viết với trạng thái chờ duyệt (Pending_Approval) và hiển thị thông báo MSG-ADM-06. | | |
| **Alternative Flow:** | AF-01: Tại NF-05, nếu bài viết chưa hoàn tất, Nhân viên nhấn nút "Lưu bản nháp"; hệ thống lưu nội dung hiện tại ở trạng thái Draft để tiếp tục hoàn thiện sau. | | |
| **Extension Flow:** | ETF-01: Nhân viên nhấn nút "Hủy bỏ"; hệ thống đóng giao diện soạn thảo sau khi người dùng xác nhận thoát. | | |
| **Exceptions Flow:** | EF-01: Tại NF-06, nếu bài viết để trống tiêu đề, thiếu ảnh bìa hoặc thiếu nội dung bắt buộc, hệ thống hiển thị thông báo MSG-AUTH-02.<br>EF-02: Tại NF-03, nếu tải lên ảnh bìa vượt quá dung lượng tối đa cho phép, hệ thống trả về thông báo MSG-CLS-02. | | |
| **Business Rule** | BR-ADM-08: Bài đăng Blog do Staff biên soạn phải ở trạng thái Draft hoặc Pending_Approval. Bài viết chỉ được công khai trên ứng dụng sau khi Admin phê duyệt (Approved). | | |

---

### UC-S08: Manage Eco Fact Content
##### **UC-S08 - Manage Eco Fact Content (Quản lý Nội dung Kiến thức Eco Fact)**

| Primary Actor(s): | Staff (Nhân viên vận hành) | Secondary Actor(s): |  |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép nhân viên vận hành quản lý kho nội dung các mẩu kiến thức ngắn về môi trường (Eco Fact) hiển thị trong tính năng Cây ảo; nhân viên có thể tạo mới mẩu kiến thức, cập nhật nội dung và thiết lập cấp độ Cây ảo tương ứng cần đạt để người dùng mở khóa kiến thức. | | |
| **Preconditions:** | 1. Nhân viên đã đăng nhập và đang mở mục Quản lý Nội dung Gamification. | | |
| **Postconditions:** | Mẩu kiến thức Eco Fact mới được lưu vào hệ thống, được liên kết với cấp độ Cây ảo tương ứng để Người dân mở khóa trong quá trình tích lũy điểm thưởng. | | |
| **Normal Flow:** | **Xem danh sách Eco Fact**<br>NF-01: Nhân viên chọn mục "Kho kiến thức Eco Fact" trên thanh menu.<br>NF-02: Hệ thống hiển thị bảng danh sách các thẻ Eco Fact hiện có, gồm: Mã thẻ, Tiêu đề sự thật môi trường, Cấp độ Cây ảo yêu cầu mở khóa, Ngày tạo và Trạng thái.<br><br>**Thêm thẻ Eco Fact mới**<br>NF-03: Nhân viên nhấn nút "Thêm Eco Fact mới".<br>NF-04: Hệ thống hiển thị biểu mẫu bao gồm: Tiêu đề kiến thức, Nội dung mô tả ngắn, Tải ảnh minh họa và Hộp chọn Cấp độ mở khóa.<br>NF-05: Nhân viên điền đầy đủ thông tin và chọn cấp độ tương ứng, sau đó nhấn "Lưu thẻ kiến thức".<br>NF-06: Hệ thống kiểm tra dữ liệu, sau đó lưu thẻ Eco Fact mới vào hệ thống và làm mới bảng danh sách.<br><br>**Chỉnh sửa thẻ Eco Fact**<br>NF-03 (từ NF-02): Nhân viên nhấn biểu tượng "Sửa" trên một dòng Eco Fact.<br>NF-04: Hệ thống mở biểu mẫu chứa sẵn thông tin hiện tại.<br>NF-05: Nhân viên chỉnh sửa nội dung hoặc thay đổi cấp độ mở khóa, sau đó nhấn "Lưu thay đổi".<br>NF-06: Hệ thống ghi nhận nội dung mới và cập nhật tức thì trên hệ thống. | | |
| **Alternative Flow:** | AF-01: Tại NF-02, Nhân viên lọc danh sách theo từng cấp độ Cây ảo để rà soát phân bổ số lượng thẻ kiến thức giữa các cấp. | | |
| **Extension Flow:** | ETF-01: Tại NF-04, nếu Nhân viên chọn "Hủy bỏ", hệ thống đóng biểu mẫu và hủy thao tác soạn thảo hiện tại. | | |
| **Exceptions Flow:** | EF-01: Tại NF-05, nếu nội dung nhập vào vượt quá độ dài tối đa cho phép của thẻ hiển thị, hệ thống hiển thị thông báo MSG-NTF-03.<br>EF-02: Tại NF-05, nếu các trường bắt buộc bị để trống, hệ thống hiển thị thông báo MSG-AUTH-02. | | |
| **Business Rule** | BR-GAM-06: Giai đoạn phát triển Cây ảo chỉ tăng theo một chiều. Chăm sóc giới hạn {N} lần/ngày, Eco Fact đã mở khóa vẫn được giữ mở.<br>BR-GAM-08: Việc kiếm điểm và đổi điểm lấy voucher áp dụng cho cả vai trò User và Recycler. Chăm sóc Cây ảo, Eco Facts và bảng xếp hạng đóng góp vẫn chỉ dành riêng cho vai trò User. | | |

---

### UC-S09: Manage Gift Inventory
##### **UC-S09 - Manage Gift Inventory (Quản lý Kho Quà tặng & Voucher)**

| Primary Actor(s): | Staff (Nhân viên vận hành) | Secondary Actor(s): |  |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép nhân viên vận hành quản lý danh mục các phần quà đổi điểm trong hệ thống (bao gồm quà tặng hiện vật và mã giảm giá Voucher); thiết lập số điểm EcoPoint cần để đổi, cập nhật số lượng tồn kho và tạm ngừng áp dụng khi quà đã hết. | | |
| **Preconditions:** | 1. Nhân viên đã đăng nhập và đang ở phân hệ Quản lý Đổi quà & Voucher. | | |
| **Postconditions:** | Danh mục quà tặng và số lượng tồn kho được cập nhật chính xác, hiển thị trên giao diện Đổi quà của Người dân; quà hết tồn kho sẽ tự động chuyển sang trạng thái tạm hết. | | |
| **Normal Flow:** | **Xem danh mục quà và kiểm tra số lượng tồn kho**<br>NF-01: Nhân viên chọn mục "Kho Quà tặng & Voucher" trên thanh điều hướng.<br>NF-02: Hệ thống hiển thị bảng danh sách quà tặng gồm: Tên quà, Loại hình (Hiện vật hoặc Voucher), Số điểm quy đổi, Số lượng tồn kho, Số lượng đã đổi và Trạng thái.<br><br>**Tạo mới món quà hoặc danh sách mã Voucher**<br>NF-03: Nhân viên nhấn nút "Thêm quà tặng mới".<br>NF-04: Hệ thống hiển thị biểu mẫu bao gồm: Tên quà tặng, Loại quà, Ảnh đại diện, Số điểm quy đổi và Số lượng nhập kho ban đầu.<br>NF-05: Nhân viên điền thông tin chi tiết và đặt số điểm đổi tương ứng, sau đó nhấn "Lưu quà tặng".<br>NF-06: Hệ thống kiểm tra dữ liệu, sau đó lưu món quà vào danh mục đổi quà và làm mới bảng danh sách.<br><br>**Cập nhật số lượng tồn kho hoặc tạm ngừng đổi**<br>NF-03 (từ NF-02): Nhân viên nhấn "Sửa" hoặc "Nhập kho" tại một phần quà cụ thể.<br>NF-04: Hệ thống mở hộp thoại cho phép bổ sung số lượng tồn kho hoặc gạt công tắc ngừng áp dụng.<br>NF-05: Nhân viên nhập số lượng bổ sung, sau đó nhấn "Cập nhật kho".<br>NF-06: Hệ thống cập nhật lại số lượng tồn khả dụng và hiển thị số liệu mới trên hệ thống. | | |
| **Alternative Flow:** | AF-01: Tại NF-02, Nhân viên nhấn nút "Xem lịch sử đổi quà" để theo dõi danh sách người dùng đã đổi quà kèm thông tin nhận quà tương ứng. | | |
| **Extension Flow:** | ETF-01: Tại NF-04, nếu Nhân viên nhấn "Hủy bỏ", hệ thống đóng hộp thoại và giữ nguyên số lượng kho hiện tại. | | |
| **Exceptions Flow:** | EF-01: Tại NF-05, nếu các trường thông tin bắt buộc bị để trống, hệ thống hiển thị thông báo MSG-AUTH-02.<br>EF-02: Khi một phần quà có số lượng tồn kho giảm về 0, hệ thống tự động trả về trạng thái hiển thị thông báo MSG-GAM-04 khi có người dùng cố gắng đổi phần thưởng đó. | | |
| **Business Rule** | BR-GAM-03: Số dư điểm không được âm. Đổi thưởng trừ điểm và cấp voucher trong một thao tác nguyên tử.<br>BR-GAM-04: Phần thưởng chỉ đổi được khi đang hoạt động và còn hàng, mỗi lượt đổi trừ tồn kho một cách nguyên tử.<br>BR-GAM-05: Voucher chỉ dùng một lần, gắn với tài khoản đã đổi, và hết hạn theo ngày hết hạn. | | |

---

### UC-S10: Staff Daily Check-in
##### **UC-S10 - Staff Daily Check-in (Điểm danh ca làm việc nhân viên)**

| Primary Actor(s): | Staff (Nhân viên vận hành) | Secondary Actor(s): |  |
| :---- | :---- | :---- | :---- |
| **Description:** | Cho phép nhân viên vận hành thực hiện thao tác điểm danh trực tuyến khi bắt đầu và kết thúc ca làm việc hàng ngày trên hệ thống quản trị, nhằm ghi nhận thời gian trực hỗ trợ và tích lũy chuỗi ngày làm việc chuyên cần. | | |
| **Preconditions:** | 1. Nhân viên đã đăng nhập thành công vào tài khoản quản trị Back-office của mình.<br>2. Nhân viên đang ở màn hình Bảng điều khiển nhân viên (Staff Dashboard). | | |
| **Postconditions:** | Thời gian bắt đầu vào ca hoặc kết thúc ca làm việc được hệ thống ghi nhận chính xác theo thời gian thực và cộng điểm thưởng chuyên cần theo chuỗi ngày liên tục. | | |
| **Normal Flow:** | NF-01: Khi đăng nhập vào đầu ca làm việc, Nhân viên quan sát tiện ích "Điểm danh ca làm việc" hiển thị trên màn hình bảng điều khiển.<br>NF-02: Hệ thống hiển thị thời gian hiện tại và nút "Điểm danh vào ca".<br>NF-03: Nhân viên nhấn nút "Điểm danh vào ca".<br>NF-04: Hệ thống ghi nhận giờ vào ca thực tế, sau đó cập nhật trạng thái làm việc thành đang trực ca, đồng thời gửi thông báo điểm danh thành công MSG-GAM-08 và chuyển nút bấm thành "Điểm danh tan ca".<br>NF-05: Vào cuối ngày khi kết thúc giờ làm việc, Nhân viên nhấn nút "Điểm danh tan ca".<br>NF-06: Hệ thống ghi nhận giờ tan ca, sau đó tính toán tổng thời gian làm việc trong ngày và cập nhật hoàn thành ca làm việc. | | |
| **Alternative Flow:** | AF-01: Nhân viên có thể nhấn vào liên kết "Xem lịch sử điểm danh" để kiểm tra bảng tổng hợp số giờ làm việc và chuỗi ngày chuyên cần của mình trong tháng. | | |
| **Extension Flow:** |  | | |
| **Exceptions Flow:** | EF-01: Tại NF-03, nếu Nhân viên đã thực hiện điểm danh vào ca trước đó trong ngày, hệ thống chỉ cho phép thực hiện thao tác tan ca và hiển thị thông báo MSG-GAM-05 nếu cố tình thực hiện thao tác nhận điểm trùng lặp. | | |
| **Business Rule** | BR-GAM-09: Mỗi tài khoản User/Staff chỉ được điểm danh 1 lượt/ngày. Điểm thưởng cộng theo chuỗi ngày liên tục (Streak) — tăng dần theo số ngày điểm danh liên tiếp — và tự động reset về Ngày 1 nếu đứt chuỗi (bỏ lỡ một ngày). | | |

---

## 5. MA TRẬN PHÂN QUYỀN TÁC NHÂN (ROLE-PERMISSION MATRIX)

| Mã Use Case | Tên Use Case | Module | Guest | User | Recycler | Staff | Admin |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **UC-R01** | Manage Accepted Waste & Pricing | MTH | - | - | **Chính (Primary)** | - | Xem / Giám sát |
| **UC-R02** | Toggle Active Status | MTH | - | - | **Chính (Primary)** | - | Khóa cưỡng chế |
| **UC-R03** | Accept / Reject Handover Request | HND / NEG | - | Thụ động (Tạo yêu cầu) | **Chính (Primary)** | Đối chiếu khi có tranh chấp | - |
| **UC-R04** | Cancel Collection Appointment | HND | - | Thụ động (Nhận tin) | **Chính (Primary)** | Giám sát tỷ lệ hủy | - |
| **UC-R05** | Negotiate via Chatbox | NEG | - | Tương tác 2 chiều | **Chính (Primary)** | Xem bằng chứng giải quyết khiếu nại | - |
| **UC-R06** | Create Handover Receipt | HND / GAM | - | Thụ động (Xác nhận nhận tiền trực tiếp) | **Chính (Primary)** | Căn cứ giải quyết khiếu nại | - |
| **UC-R07** | Purchase In-System Services via VietQR | PAY | - | Mua dịch vụ User | **Chính (Primary)** | - | Cấu hình biểu phí dịch vụ |
| **UC-R08** | Reset Password | AUTH | - | - | **Chính (Primary)** | - | - |
| **UC-S01** | Review Misclassified Images | CLS | - | Đóng góp ảnh lỗi | - | **Chính (Primary)** | Xem báo cáo AI |
| **UC-S02** | Label Image Data | CLS | - | - | - | **Chính (Primary)** | - |
| **UC-S03** | Update Verified Dataset | CLS | - | - | - | **Chính (Primary)** | Xuất tập kiểm chứng |
| **UC-S04** | Verify Recycler Application | MTH / ADM | - | - | Thụ động (Nộp hồ sơ) | **Chính (Primary)** | Quyết định phúc quyết |
| **UC-S05** | Handle Transaction Complaints | DSP | - | Nộp khiếu nại | Nộp khiếu nại | **Chính (Primary)** | Thẩm quyền giải quyết cấp cao |
| **UC-S06** | Manage User & Buyer Accounts | ADM / AUTH | - | Đối tượng quản lý | Đối tượng quản lý | **Chính (Primary)** | Toàn quyền quản trị |
| **UC-S07** | Create Blog Post Draft | ADM | - | Đọc bài công khai | - | **Chính (Primary)** | Phê duyệt xuất bản (Approved) |
| **UC-S08** | Manage Eco Fact Content | GAM | - | Thụ động (Đọc thẻ) | - | **Chính (Primary)** | Cấu hình mốc level Cây |
| **UC-S09** | Manage Gift Inventory | GAM | - | Thụ động (Đổi quà) | Thụ động (Đổi quà) | **Chính (Primary)** | Duyệt danh mục quà |
| **UC-S10** | Staff Daily Check-in | GAM | - | Điểm danh User | - | **Chính (Primary)** | Giám sát chuyên cần |

---

*Tài liệu được cập nhật và chuẩn hóa toàn diện theo Quy chuẩn User Requirement và Phụ lục Business Rules & System Messages của Đồ án Capstone SEP490-G141.*
