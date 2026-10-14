# ĐẶC TẢ BIỂU ĐỒ CHUYỂN TRẠNG THÁI GIAO DỊCH BÀN GIAO PHẾ LIỆU (STATE TRANSITION DIAGRAM - HANDOVER REQUEST)
**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Thực thể trọng tâm:** `HandoverRequest` / `TradeOrder` (Đơn yêu cầu bàn giao và Giao dịch phế liệu)  
**Tài liệu đối chiếu:** [business_rule_&_system_message.md](file:///c:/Projects/Demo%20Waste%20Classification/documents/business_rule_&_system_message.md), [business_flow_step_description.md](file:///c:/Projects/Demo%20Waste%20Classification/documents/business_flow_step_description.md), [dbml.io](file:///c:/Projects/Demo%20Waste%20Classification/documents/dbml.io)  

---

## 1. BIỂU ĐỒ CHUYỂN TRẠNG THÁI (MERMAID STATE DIAGRAM V2)

```mermaid
stateDiagram-v2
    [*] --> Pending : [User] Tạo yêu cầu bàn giao rác\n(Kèm khung giờ & địa chỉ đề xuất)

    state "Giai đoạn Tiếp nhận & Thương lượng" as InitPhase {
        Pending --> Negotiating : [User / Recycler] Gửi tin nhắn trao đổi qua Chatbox
        Negotiating --> Negotiating : Hai bên thương lượng giá & dời lịch hẹn
        Negotiating --> Accepted : [Recycler] Chốt thỏa thuận & bấm "Chấp nhận"
        Pending --> Accepted : [Recycler] Bấm "Chấp nhận" ngay (Khớp khung giờ)
    }

    state "Giai đoạn Lên lịch & Thực hiện" as ExecutionPhase {
        Accepted --> Scheduled : Hệ thống kích hoạt / Đến khung giờ hẹn đã chốt
        Scheduled --> Completed : [Recycler] Cân đo, tạo Receipt\n+ [User] Quét QR xác nhận biên nhận\n[/Cộng điểm Gamification]
    }

    state "Giai đoạn Tranh chấp & Xử lý Khiếu nại" as DisputePhase {
        Scheduled --> Disputed : [User / Recycler] Gửi khiếu nại tranh chấp\n(Sai lệch cân nặng / chất lượng rác)
        Disputed --> Completed : [Staff] Hòa giải thành công & Duyệt hoàn tất
        Disputed --> Cancelled : [Staff] Hòa giải thất bại & Quyết định hủy đơn
    }

    state "Trạng thái Kết thúc (Terminal States)" as FinalPhase {
        Pending --> Rejected : [Recycler] Bấm "Từ chối" tiếp nhận
        Pending --> Expired : [JOB-03] Quá 24h Recycler không phản hồi
        
        Pending --> Cancelled : [User] Hủy yêu cầu khi chưa ai nhận
        Negotiating --> Cancelled : [User / Recycler] Hủy trong lúc thương lượng
        Accepted --> Cancelled : [User / Recycler] Hủy lịch hẹn\n(Ghi nhận 'Hủy muộn' nếu < X giờ - BR-HND-06)
        
        Completed --> [*]
        Rejected --> [*]
        Cancelled --> [*]
        Expired --> [*]
    }

    classDef active fill:#e3f2fd,stroke:#1565c0,stroke-width:2px;
    classDef success fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px;
    classDef danger fill:#ffebee,stroke:#c62828,stroke-width:2px;
    classDef warning fill:#fff3e0,stroke:#ef6c00,stroke-width:2px;

    class Pending,Negotiating,Accepted,Scheduled active;
    class Completed success;
    class Rejected,Cancelled,Expired danger;
    class Disputed warning;
```

---

## 2. BẢNG MA TRẬN ĐẶC TẢ CHUYỂN TRẠNG THÁI (STATE TRANSITION TABLE)

| STT | Trạng thái Nguồn (From) | Sự kiện Kích hoạt (Trigger / Event) | Tác nhân (Actor) | Điều kiện Kiểm tra (Guard Condition / Business Rules) | Trạng thái Đích (To) | Hành động Hệ thống & Hậu quả (System Actions & Side-effects) |
| :---: | :--- | :--- | :---: | :--- | :--- | :--- |
| **1** | `[*]` | Bấm "Gửi yêu cầu bàn giao" | `User` | Cơ sở thu gom hợp lệ, đang mở cửa và nhận loại rác này (**BR-HND-01**). | **`Pending`** | - Tạo bản ghi `TradeOrder` với trạng thái `Pending`.<br>- Gửi thông báo Push (FCM) đến cơ sở thu gom (**BR-NTF-02**).<br>- Khởi động bộ đếm giờ hết hạn 24 giờ. |
| **2** | **`Pending`** | Gửi tin nhắn thương lượng đầu tiên | `User` / `Recycler` | Đơn hàng vẫn còn trong thời hạn 24 giờ. | **`Negotiating`** | - Chuyển trạng thái sang `Negotiating`.<br>- Khởi tạo phòng Chat bảo mật giữa hai bên (**BR-HND-08**). |
| **3** | **`Pending`** | Bấm "Chấp nhận yêu cầu" | `Recycler` | Cơ sở thu gom đồng ý toàn bộ khối lượng và khung giờ User đề xuất. | **`Accepted`** | - Chốt đơn hàng.<br>- Gửi thông báo đến User thông báo lịch thu gom đã được tiếp nhận. |
| **4** | **`Pending`** | Bấm "Từ chối" | `Recycler` | Bắt buộc nhập lý do từ chối (quá tải, xa, rác không đạt chuẩn). | **`Rejected`** | - Đóng đơn hàng.<br>- Thông báo lý do từ chối cho User để họ tìm cơ sở thu gom khác (**MSG-HND-05**). |
| **5** | **`Pending`** | Hết 24 giờ không phản hồi | `Hệ thống` *(JOB-03)* | Quá 24 giờ kể từ thời điểm tạo đơn (**BR-HND-02**). | **`Expired`** | - Tác vụ nền chuyển trạng thái sang `Expired`.<br>- Gửi thông báo hết hạn cho cả hai bên. |
| **6** | **`Pending`** | Bấm "Hủy yêu cầu" | `User` | Đơn chưa có cơ sở thu gom tiếp nhận. | **`Cancelled`** | - Hủy đơn ngay lập tức.<br>- Đóng tiến trình bàn giao. |
| **7** | **`Negotiating`** | Bấm "Chốt thỏa thuận & Chấp nhận" | `Recycler` | Hai bên đã thống nhất xong khung giờ và đơn giá phế liệu qua chat. | **`Accepted`** | - Lưu khung giờ thu gom chính thức vào hệ thống.<br>- Cập nhật lịch hẹn vào Google Maps/Lịch cá nhân. |
| **8** | **`Negotiating`** | Bấm "Hủy thương lượng" | `User` / `Recycler` | Hai bên không đạt được thỏa thuận về giá hoặc thời gian. | **`Cancelled`** | - Đóng phòng chat thương lượng.<br>- Ghi nhận lý do hủy đơn. |
| **9** | **`Accepted`** | Đến mốc thời gian hẹn thu gom | `Hệ thống` | Thời gian hiện tại khớp với khung giờ hẹn đã chốt. | **`Scheduled`** | - Hệ thống tự động chuyển sang trạng thái `Scheduled`.<br>- Gửi thông báo nhắc lịch hẹn trước {X} giờ (**JOB-09**, **BR-NTF-04**).<br>- Sinh mã VietQR động gắn với đơn hàng để chuẩn bị xác nhận. |
| **10** | **`Accepted`** / **`Scheduled`** | Bấm "Hủy đơn" | `User` / `Recycler` | Đơn chưa tạo biên nhận hoàn tất. | **`Cancelled`** | - Nếu hủy trong vòng {X} giờ trước lịch hẹn: Ghi nhận vi phạm "Hủy muộn" (**BR-HND-06**).<br>- Trừ điểm uy tín nếu hủy muộn không có lý do chính đáng. |
| **11** | **`Scheduled`** | Quét QR & Chốt biên nhận cân đo | `Recycler` & `User` | Recycler nhập số cân + chụp ảnh cân rác; User quét mã QR xác nhận (**BR-HND-04**). | **`Completed`** | - Chốt số tiền thanh toán thực tế (User và Recycler thanh toán tiền mặt/chuyển khoản ngoài đời).<br>- **Phát sự kiện `TradeOrderCompletedEvent` qua Bus:**<br>  + Tự động cộng Điểm Xanh (Green Points) cho cả User & Recycler (**BR-GAM-01**).<br>  + Tích lũy kinh nghiệm tăng cấp Cây ảo.<br>  + Cập nhật Bảng xếp hạng Leaderboard.<br>  + Mở khóa màn hình Đánh giá 1-5 sao (**BR-RVW-01**). |
| **12** | **`Scheduled`** | Gửi đơn "Khiếu nại / Báo cáo" | `User` / `Recycler` | Có bất đồng về khối lượng cân đo, thái độ, hoặc chất lượng phế liệu (**BR-DSP-01**). | **`Disputed`** | - Tạm đóng băng quyền hoàn tất tự động.<br>- Chuyển hồ sơ khiếu nại lên bàn làm việc của Nhân viên vận hành (`Staff`).<br>- Mở luồng cung cấp bằng chứng (ảnh chụp, tin nhắn chat) (**BR-DSP-02**). |
| **13** | **`Disputed`** | Phán quyết: Công nhận giao dịch | `Staff` | Staff kiểm tra bằng chứng và đưa ra kết luận đồng thuận hoàn tất (**BR-DSP-04**). | **`Completed`** | - Cập nhật khối lượng theo phán quyết của Staff.<br>- Kích hoạt cộng điểm Eco-points theo số cân được công nhận.<br>- Gửi thông báo kết quả giải quyết tranh chấp (**BR-DSP-06**). |
| **14** | **`Disputed`** | Phán quyết: Hủy bỏ giao dịch | `Staff` | Gian lận nghiêm trọng, không thể hòa giải hoặc hai bên từ chối nhận rác. | **`Cancelled`** | - Hủy bỏ toàn bộ giao dịch.<br>- Không cộng bất kỳ điểm thưởng nào (**BR-GAM-01**).<br>- Đưa cảnh cáo lên hồ sơ của bên có lỗi vi phạm. |

---

## 3. CÁC NGUYÊN TẮC BẤT BIẾN VÀ BẢO TOÀN DỮ LIỆU (STATE INVARIANTS)

1. **Nguyên tắc Chuyển dịch một chiều (One-Way Progression - `BR-HND-02`):**
   * Vòng đời giao dịch chỉ tịnh tiến theo một chiều:  
     $$\text{Pending} \longrightarrow \text{Negotiating} \longrightarrow \text{Accepted} \longrightarrow \text{Scheduled} \longrightarrow \text{Completed}$$
   * Một khi đơn hàng đã rơi vào các trạng thái kết thúc (**Terminal States** gồm `Completed`, `Cancelled`, `Rejected`, `Expired`), **tuyệt đối không có bất kỳ API nào được phép kích hoạt mở lại đơn hàng**. Bất kỳ nỗ lực quét lại mã QR sẽ bị chặn ngay lập tức với mã lỗi `MSG-HND-03`.
2. **Tính Nguyên tử khi Hoàn tất Giao dịch (Transactional Atomicity & Event-Driven):**
   * Khi chuyển sang trạng thái `Completed`, hành vi đổi trạng thái đơn hàng và ghi nhận biên nhận (`TransactionReceipt`) phải diễn ra trong một Database Transaction duy nhất.
   * Các tác vụ thưởng điểm Gamification, tính toán cấp độ cây ảo, và gửi thông báo FCM được xử lý bất đồng bộ thông qua Event Bus để đảm bảo API phản hồi cho người dùng trong vòng **< 100ms**.
3. **Quyền riêng tư Thông tin liên hệ (`BR-HND-08`):**
   * Ở trạng thái `Pending` và `Negotiating`, số điện thoại thật và địa chỉ chi tiết nhà riêng của User bị che mờ (chỉ hiển thị khu vực phường/quận trên bản đồ).
   * Chỉ khi chuyển sang trạng thái `Accepted`, hai bên mới được quyền xem số điện thoại và địa chỉ chính xác để tiến hành cuộc hẹn thu gom.
