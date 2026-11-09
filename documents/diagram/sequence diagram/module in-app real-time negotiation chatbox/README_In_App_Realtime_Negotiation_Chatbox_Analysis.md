# PHÂN TÍCH SƠ ĐỒ THIẾT KẾ MODULE: IN-APP REAL-TIME NEGOTIATION CHATBOX (`U-08` & `R-05`)

**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** `NEG` – Thương lượng & Khung giờ thu gom thời gian thực (*Negotiation & Time-slot Alignment*)  
**Use Cases trọng tâm:**
- `U-08: Negotiate via Chatbox (User side)`
- `R-05: Negotiate via Chatbox (Recycler side)`

---

## 1. Danh mục Sơ đồ Thiết kế trong Module

| STT | Loại sơ đồ | Đường dẫn File | Mô tả nội dung |
|:---:|:---:|:---|:---|
| **01** | **Class Diagram** | [`module_in_app_realtime_negotiation_chatbox_clean_architecture_class_diagram_ultra_compact.mmd`](../../class%20diagram/module%20in-app%20real-time%20negotiation%20chatbox/module_in_app_realtime_negotiation_chatbox_clean_architecture_class_diagram_ultra_compact.mmd) | Sơ đồ lớp kiến trúc 4 tầng Clean Architecture (`NegotiationChatController`, `NegotiationChatHub`, `SignalRNegotiationChatNotifier`, `INegotiationChatService`, `NegotiationChatService`, `CloudflareR2StorageService`, `TradeOrder`, `ChatMessage`). |
| **02** | **Sequence Diagram 01** | [`01_u08_r05_join_room_send_realtime_text_image_and_mark_read.mmd`](./01_u08_r05_join_room_send_realtime_text_image_and_mark_read.mmd) | Luồng tham gia phòng chat SignalR cô lập theo đơn (`JoinOrderChatRoom` – `BR-NEG-01`), tải ảnh thực tế phế liệu lên Cloudflare R2, gửi tin nhắn `TEXT`/`IMAGE` thời gian thực (`BR-NEG-02`, `MSG-NEG-02`) và đánh dấu đã đọc (`PUT .../messages/read`). |
| **03** | **Sequence Diagram 02** | [`02_u08_r05_send_counter_proposal_and_supersede_previous_offer.mmd`](./02_u08_r05_send_counter_proposal_and_supersede_previous_offer.mmd) | Luồng gửi đề xuất & phản đề xuất (`POST .../proposals`) về đơn giá, khối lượng và khung giờ thu gom (`BR-NEG-04`), tự động chuyển đề xuất `Pending` trước đó sang `Superseded` (`BR-NEG-03`) và chuyển đơn sang `Negotiating` (`BR-HND-02`, `MSG-NEG-01`). |
| **04** | **Sequence Diagram 03** | [`03_u08_r05_accept_proposal_lock_terms_and_dispute_audit_view.mmd`](./03_u08_r05_accept_proposal_lock_terms_and_dispute_audit_view.mmd) | Luồng đối phương chấp nhận đề xuất mới nhất (`POST .../proposals/{messageId}/respond`), lưu cứng điều khoản và khóa khung giờ thu gom chính thức (`ConfirmedTimeSlot` – `BR-NEG-05`, `MSG-NEG-03`), giữ mở kênh chat `TEXT` sau khi `Accepted` (`BR-NEG-02`) và `Staff` tra cứu bằng chứng khi đơn `Disputed` (`BR-DSP-06`). |

---

## 2. Bảng Đối chiếu Quy tắc Nghiệp vụ (`BR-*`) & Mã Thông báo (`MSG-*`)

| Mã Quy tắc / Thông báo | Nội dung Đặc tả | Thành phần Thực thi trong Sơ đồ |
|:---:|:---|:---|
| **`BR-NEG-01`** | Tin nhắn thương lượng chỉ tồn tại trong phạm vi một yêu cầu bàn giao cụ thể và chỉ hiển thị cho `User` và `Recycler` của yêu cầu đó. Toàn bộ lịch sử chat được lưu trữ vĩnh viễn và không thể xóa để làm bằng chứng đối soát. | `NegotiationChatService.LoadAndAuthorizeOrderContextAsync` & `NegotiationChatHub.JoinOrderChatRoom` |
| **`BR-NEG-02`** | Đưa ra hoặc sửa đổi đề xuất giá/khung giờ chỉ được phép khi đơn ở trạng thái `Pending` hoặc `Negotiating`, bị khóa khi sang `Accepted`. Kênh chat thông thường (`TEXT`/`IMAGE`) vẫn giữ mở sau khi `Accepted` hoặc `Completed`. | `NegotiationChatService.IsProposalAllowed` & `NegotiationChatService.IsGeneralChatAllowed` |
| **`BR-NEG-03`** | Chỉ đề xuất mới nhất có hiệu lực (`Pending`); mỗi phản đề xuất mới ghi đè đề xuất trước đó sang `Superseded`. Khi được chấp nhận, điều khoản thống nhất được lưu cứng vào `TradeOrder`. | `NegotiationChatService.SendProposalAsync` & `NegotiationChatService.RespondToProposalAsync` |
| **`BR-NEG-04`** | `User` đề xuất khung giờ thu gom mong muốn khi tạo đơn; nếu chưa phù hợp, hai bên thương lượng lại khung giờ trực tiếp qua Chatbox. | Trường `ProposedTimeSlot` trong `TradeOrder`, `ChatMessage` và `SendProposalRequest` |
| **`BR-NEG-05`** | Khung giờ thu gom chính thức (`ConfirmedTimeSlot`) được khóa ngay tại thời điểm chốt đề xuất hoặc khi `Recycler` bấm chấp nhận yêu cầu bàn giao. | `NegotiationChatService.RespondToProposalAsync` & `AcceptOrderInitialTermsAsync` |
| **`BR-DSP-06`** | Khi đơn chuyển sang tranh chấp (`Disputed`), nhân viên `Staff`/`Admin` được cấp quyền đọc lịch sử chat bất biến để phân xử. | Nhánh `allowStaffOnDisputed` trong `NegotiationChatService.GetOrderChatThreadAsync` |
| **`MSG-NEG-01`** | *"Yêu cầu trao đổi của bạn đã được gửi đến {facility_name}."* | `NegotiationChatController.SendProposal` |
| **`MSG-NEG-02`** | *"Bạn có tin nhắn mới từ {sender_name}."* | Sự kiện SignalR `ReceiveChatMessage` & `ProposalCreated` |
| **`MSG-NEG-03`** | *"{party_name} đã chấp nhận đề nghị của bạn."* | `NegotiationChatController.RespondToProposal` & `AcceptOrderInitialTerms` |

