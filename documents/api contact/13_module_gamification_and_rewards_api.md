# ĐẶC TẢ HỢP ĐỒNG API CHI TIẾT (API CONTRACT SPECIFICATION)
## PHÂN HỆ M-10 / FE-13: GAMIFICATION, CÂY ẢO & ĐỔI QUÀ THƯỞNG (`GAMIFICATION & REWARDS`)

* **Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform (`SEP490_G141`)
* **Phiên bản tài liệu:** `v1.0.0` (Cập nhật đồng bộ với mã nguồn `Ecolink.WebApi` / `.NET 10` & PostgreSQL 16)
* **Use Cases bao phủ:**
  * `U-13`: Daily Check-in (*Điểm danh hàng ngày theo múi giờ ICT UTC+7, chuỗi Streak & kẹp trần phi giao dịch*)
  * `U-14`: Redeem Points for Gifts (*Đổi quà nguyên tử trừ tồn kho + trừ ví điểm, E-Voucher sinh mã & hạn dùng, quà hiện vật giao nhận*)
  * `U-15`: Upgrade Virtual Tree (*Tưới / nâng cấp Cây Ảo một chiều & mở khóa Eco Fact — chỉ `Citizen`/`User`*)
  * `S-09`: Manage Eco Fact Content (*Staff `[MANAGE_INVENTORY]` biên tập & xóa mềm `INACTIVE` nếu đã có người mở khóa*)
  * `S-10`: Manage Gift Inventory (*Staff `[MANAGE_INVENTORY]` nhập kho, nhập hàng loạt mã E-Voucher, đóng/mở đổi quà & xử lý Fulfill / Cancel hoàn điểm*)
  * `A-07`: Manage Gamification Config (*Admin cấu hình tỷ lệ điểm/kg theo loại rác, trần ngày, thông số Streak & bảng cấp Cây Ảo*)
  * `A-08`: Configure Leaderboard Cycles (*Admin quản lý chu kỳ bảng xếp hạng Tuần/Tháng/Quý & chốt trao thưởng Top 3*)
* **Controllers liên quan:** `GamificationController`, `UserGamificationController`, `PointsAndRewardsController`, `StaffGamificationController`, `AdminGamificationController`
* **Phạm vi Endpoint:** `27` REST APIs (`STT 076` – `STT 102`) trong [`00_api_inventory_and_endpoint_statistics.md`](./00_api_inventory_and_endpoint_statistics.md) + các route hỗ trợ bổ sung (`/profile/me`, `/gifts/{id}`, `/redemptions/{id}`, `/eco-facts/{id}`, `/config/reset`, `/leaderboard-cycles/{id}`, `/activate`).
* **Tiêu chuẩn phản hồi:** Chuẩn hóa theo `ApiResponse<T>` trong [`00_api_overview_and_response_contract.md`](./00_api_overview_and_response_contract.md) (`camelCase`, trạng thái chuỗi).

---

## 1. TỔNG QUAN KIẾN TRÚC & MA TRẬN API PHÂN HỆ `M-10`

### 1.1. Nguyên tắc Kiến trúc Cốt lõi (`BR-GAM-01` – `BR-GAM-09`)

1. **Sổ cái Idempotent & Số dư Không Âm (`BR-GAM-01`, `BR-GAM-03`):**
   * Mỗi người dùng có `1` ví duy nhất `user_point_wallets` (`user_id` unique) và sổ cái bất biến `point_transactions` gắn ràng buộc `ux_point_transactions_idem` chống cộng/trừ trùng (`HANDOVER:{receiptId}:USER`, `CHECKIN:{userId}:{date}`, `REDEEM:{redemptionId}`, `TREE:{userId}:L{level}`, `LEADERBOARD:{cycleId}:RANK:{rank}:{userId}`, `REFUND:{redemptionId}`).
   * Mọi thao tác trừ điểm trong `PointsService.TryDebitAsync` dùng cập nhật có điều kiện `WHERE current_balance >= points` trong PostgreSQL transaction (với nhánh kiểm thử `!_db.Database.IsRelational()` giữ nguyên tính bất biến không âm).
2. **Múi giờ ICT (UTC+7) & Trần Phi Giao dịch (`BR-GAM-02`, `BR-GAM-09`):**
   * `GamTime.Today()` quy đổi ngày check-in và giới hạn chăm cây theo UTC+7. Điểm cộng từ `DailyCheckin` bị kẹp theo phần còn lại của `DailyNonTransactionCap`; nếu dư địa bằng `0`, hệ thống ném `BusinessRuleException("BR-GAM-02", "MSG-GAM-05")`.
3. **Đổi quà Nguyên tử (`BR-GAM-04`, `BR-GAM-05`):**
   * `GiftService.RedeemAsync` gộp trừ tồn kho có điều kiện (`status == 'ACTIVE' AND stock_quantity >= qty`), tạo `GiftRedemption`, và trừ ví điểm `TryDebitAsync` trong cùng `1` DB Transaction. Thất bại ở bước nào sẽ rollback toàn bộ bước còn lại.
4. **Phân quyền Hạt mịn & Tách biệt Vai trò (`BR-GAM-07`, `BR-GAM-08`, `BR-ADM-02`):**
   * **Cả `Citizen` (`User`) và `Recycler` (`Collector`):** được dùng ví điểm, đổi quà (`U-14`), điểm danh hằng ngày (`U-13`) và xếp hạng trên Bảng xếp hạng (`STT 077`, `BR-GAM-08`).
   * **Chỉ `Citizen` (`User`):** được nuôi Cây Ảo (`U-15`) và mở khóa Eco Fact (`BR-GAM-08`).
   * **Staff quản trị kho quà & Eco Fact (`S-09`, `S-10`):** bắt buộc qua `[HasPermission(StaffPermissions.ManageInventory)]` (hoặc `Admin`).

### 1.2. Bảng Quy chiếu Nhanh `27` Endpoints (`STT 076` – `STT 102`) & Alias Route Tương thích

| STT | Phương thức | Tuyến đường Chuẩn (`Canonical Route`) | Alias Route Tương thích | Controller / Action | Phân quyền (`RBAC`) |
|:---:|:---:|:---|:---|:---|:---|
| **076** | `GET` | `/api/v1/gamification/profile/{userId:guid}`<br>*(+ `/profile/me`)* | — | `GamificationController.GetProfile` / `GetMyProfile` | `Authorize` |
| **077** | `GET` | `/api/v1/gamification/leaderboard` | — | `GamificationController.GetLeaderboard` | `AllowAnonymous` |
| **078** | `GET` | `/api/v1/gamification/check-in/status` | `/api/v1/check-in` | `UserGamificationController.CheckinStatus` | `User,Citizen,Recycler,Collector` |
| **079** | `POST` | `/api/v1/gamification/check-in` | `/api/v1/check-in` | `UserGamificationController.CheckIn` | `User,Citizen,Recycler,Collector` |
| **080** | `GET` | `/api/v1/gamification/points/ledger`<br>*(+ `/points/wallet`)* | `/api/v1/points/transactions`<br>*(+ `/api/v1/points/wallet`)* | `PointsAndRewardsController.Transactions` / `Wallet` | `User,Citizen,Recycler,Collector` |
| **081** | `GET` | `/api/v1/gamification/gifts`<br>*(+ `/gifts/{id:guid}`)* | `/api/v1/gifts`<br>*(+ `/api/v1/gifts/{id:guid}`)* | `PointsAndRewardsController.Gifts` / `Gift` | `User,Citizen,Recycler,Collector` |
| **082** | `POST` | `/api/v1/gamification/redemptions` | `/api/v1/gifts/{id:guid}/redeem` | `PointsAndRewardsController.RedeemFromBody` / `Redeem` | `User,Citizen,Recycler,Collector` |
| **083** | `GET` | `/api/v1/gamification/redemptions/me`<br>*(+ `/redemptions/{id:guid}`)* | `/api/v1/gift-redemptions`<br>*(+ `/api/v1/gift-redemptions/{id:guid}`)* | `PointsAndRewardsController.MyRedemptions` / `MyRedemption` | `User,Citizen,Recycler,Collector` |
| **084** | `GET` | `/api/v1/gamification/tree/me` | `/api/v1/virtual-tree` | `UserGamificationController.Tree` | `User,Citizen` (`403` Recycler) |
| **085** | `POST` | `/api/v1/gamification/tree/upgrade` | `/api/v1/virtual-tree/water` | `UserGamificationController.Water` | `User,Citizen` (`403` Recycler) |
| **086** | `GET` | `/api/v1/gamification/eco-facts/me`<br>*(+ `/eco-facts/{id:guid}`)* | `/api/v1/eco-facts`<br>*(+ `/api/v1/eco-facts/{id:guid}`)* | `UserGamificationController.Facts` / `Fact` | `User,Citizen` (`403` Recycler) |
| **087** | `GET` | `/api/v1/staff/eco-facts`<br>*(+ `/{id:guid}`)* | — | `StaffGamificationController.Facts` / `Fact` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **088** | `POST` | `/api/v1/staff/eco-facts` | — | `StaffGamificationController.CreateFact` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **089** | `PUT` | `/api/v1/staff/eco-facts/{id:guid}` | — | `StaffGamificationController.UpdateFact` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **090** | `DELETE` | `/api/v1/staff/eco-facts/{id:guid}` | — | `StaffGamificationController.DeleteFact` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **091** | `GET` | `/api/v1/staff/gifts`<br>*(+ `/{id:guid}`, `/{id}/redemptions`)* | — | `StaffGamificationController.Gifts` / `Gift` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **092** | `POST` | `/api/v1/staff/gifts` | — | `StaffGamificationController.CreateGift` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **093** | `PUT` | `/api/v1/staff/gifts/{id:guid}`<br>*(+ `/{id:guid}/restock`)* | — | `StaffGamificationController.UpdateGift` / `Restock` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **094** | `PATCH` | `/api/v1/staff/gifts/{id:guid}/status` | `POST /api/v1/staff/gifts/{id:guid}/status` | `StaffGamificationController.GiftStatus` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **095** | `POST` | `/api/v1/staff/gifts/{id:guid}/vouchers/import` | — | `StaffGamificationController.ImportVouchers` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **096** | `GET` | `/api/v1/staff/redemptions` | — | `StaffGamificationController.AllRedemptions` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **097** | `PATCH` | `/api/v1/staff/redemptions/{id:guid}/fulfill` | `POST /api/v1/staff/redemptions/{id:guid}/fulfill`<br>`POST /api/v1/staff/gift-redemptions/{id:guid}/fulfill` | `StaffGamificationController.Fulfill` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **098** | `GET` | `/api/v1/admin/gamification/config` | — | `AdminGamificationController.Get` | `Admin` |
| **099** | `PUT` | `/api/v1/admin/gamification/config`<br>*(+ `POST /config/reset`)* | — | `AdminGamificationController.Update` / `Reset` | `Admin` |
| **100** | `GET` | `/api/v1/admin/gamification/leaderboard-cycles` | — | `AdminGamificationController.GetCycles` | `Admin` |
| **101** | `POST` | `/api/v1/admin/gamification/leaderboard-cycles`<br>*(+ `PUT /{cycleId}`, `PATCH\|POST /{cycleId}/activate`)* | — | `AdminGamificationController.CreateCycle` / `UpdateCycle` / `ActivateCycle` | `Admin` |
| **102** | `POST` | `/api/v1/admin/gamification/leaderboard-cycles/{cycleId:guid}/finalize` | — | `AdminGamificationController.FinalizeCycle` | `Admin` |

---

## 2. ĐẶC TẢ CHI TIẾT CÁC ENDPOINTS (`STT 076` – `STT 102`)

### 2.1. Nhóm Hồ sơ Sống Xanh, Bảng xếp hạng & Điểm danh (`U-13` — `STT 076–079`)

#### `[STT 076]` `GET /api/v1/gamification/profile/me` & `GET /api/v1/gamification/profile/{userId:guid}`
* **Mô tả:** Tổng hợp hồ sơ Sống Xanh trực tiếp từ dữ liệu thật (`UserPointWallets`, `VirtualTreeProgresses`, `HandoverReceipts`, `AiScanHistories`, `DailyCheckinLogs`). Không sử dụng bộ đệm mock tĩnh khi `db != null`.
* **Response `200 OK` (`EcoProfileDto`):** `userId`, `currentGreenPoints`, `totalLifetimePoints`, `treeLevel`, `treeStageTitle`, `treeExperience`, `experienceToNextLevel`, `totalWasteScanned`, `totalRecycledKg`, `currentStreak`, `co2SavedKg`.

#### `[STT 077]` `GET /api/v1/gamification/leaderboard`
* **Query Parameters:** `top` (`1..100`, mặc định `10`), `period` (`Weekly|Monthly|Quarterly`), `cycleId` (`Guid?`), `role` (`all|user|recycler`).
* **Response `200 OK` (`LeaderboardViewDto`):** Gồm `activeCycle: LeaderboardCycleDto?`, `items: LeaderboardEntryDto[]` (xếp hạng cả `User` và `Recycler` đang `Active`, điểm tính theo `TotalEarned` / điểm kiếm được trong chu kỳ, đổi quà không làm tụt hạng `BR-GAM-07`), `myEntry: LeaderboardEntryDto?`, và `recentFinalizedCycles: LeaderboardCycleDto[]`.

#### `[STT 078]` & `[STT 079]` `GET /api/v1/gamification/check-in/status` & `POST /api/v1/gamification/check-in`
* **Logic Check-in:** Cộng `checkinBasePoints + min((streak - 1) * streakBonusIncrement, streakBonusCap)`, kẹp theo phần còn lại của `dailyNonTransactionCap` hôm nay ICT.
* **Responses:**
  * `GET 200 OK`: `CheckinStatusDto` (`checkedInToday`, `currentStreak`, `nextStreak`, `streakWillReset`, `todayRewardPreview`, `dailyCapRemaining`, `walletBalance`, `calendar[7]`).
  * `POST 200 OK` (`MSG-GAM-08`): `CheckinResultDto` (`pointsAwarded`, `streakCount`, `walletBalance`, `limitedByDailyCap`).
  * `POST 400 Bad Request`: `MSG-GEN-01` nếu đã điểm danh trong ngày ICT; `MSG-GAM-05` nếu đã chạm trần điểm ngày.

### 2.2. Nhóm Ví điểm & Đổi quà (`U-14` — `STT 080–083`)

#### `[STT 080]` `GET /api/v1/gamification/points/ledger` *(và `GET /api/v1/gamification/points/wallet`)*
* **Query Parameters (`ledger`):** `from` (`DateTime?`), `to` (`DateTime?`), `actionType` (`Handover_Reward|Daily_Checkin|Redeem_Gift|Tree_Care|Leaderboard_Bonus|Adjustment`), `page`, `pageSize` (`1..50`).
* **Response `200 OK`:** `GamPagedResult<PointTransactionDto>` (hoặc `WalletDto` cho `/wallet`).

#### `[STT 081]` – `[STT 083]` Cửa hàng quà & Đổi thưởng nguyên tử
* **`POST /api/v1/gamification/redemptions` (`CreateRedemptionRequest`):**
  ```json
  {
    "giftId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "quantity": 1,
    "deliveryAddress": "Số 1 Đại Cồ Việt, Hai Bà Trưng, Hà Nội"
  }
  ```
* **Validation & Responses:**
  * Voucher chỉ cho phép `quantity == 1` (`BR-GAM-05`), tự sinh mã `ECO-XXXX-XXXX-XXXX` và hạn dùng `now + voucherValidityDays`, trạng thái `Redeemed`.
  * Physical bắt buộc có `deliveryAddress`, trạng thái `Pending` chờ Staff xử lý.
  * `201 Created` (`MSG-GAM-02`): `RedeemResultDto { redemption, walletBalance }`.
  * `400 Bad Request`: `MSG-GAM-04` (hết hàng / `INACTIVE`), `MSG-GAM-03` (không đủ số dư điểm, rollback tồn kho), `MSG-AUTH-02` (thiếu địa chỉ cho quà hiện vật).

### 2.3. Nhóm Cây Ảo & Eco Fact (`U-15`, `S-09` — `STT 084–090`)

* **`POST /api/v1/gamification/tree/upgrade` (`STT 085`):** Chỉ cho phép `User,Citizen` (`403` với `Recycler`). Kiểm tra chưa đạt `MaxLevel`, chưa vượt `dailyTreeCareLimit` lượt/ngày ICT, trừ số dư ví đúng bằng `next.RequiredPoints`, tăng cấp một chiều và trả về `WaterTreeResultDto` kèm danh sách `unlockedFacts` (`MSG-GAM-06`).
* **`POST/PUT /api/v1/staff/eco-facts` (`STT 088–089`):** Tiêu đề `1..200` ký tự, nội dung `1..300` ký tự (`MSG-NTF-03` nếu vượt), `unlockTreeLevel` $\in [1..\text{MaxLevel}]$.
* **`DELETE /api/v1/staff/eco-facts/{id:guid}` (`STT 090`):** Nếu đã có ít nhất `1` người dùng đạt `tree_level >= unlock_tree_level`, chuyển `status = INACTIVE` thay vì xóa cứng để bảo toàn lịch sử đã mở khóa (`BR-GAM-06`).

### 2.4. Nhóm Quản trị Kho quà & Xử lý Đổi thưởng Staff (`S-10` — `STT 091–097`)

* **`POST /api/v1/staff/gifts/{id:guid}/vouchers/import` (`STT 095`):** Chỉ áp dụng cho `GiftType.Voucher`, khử trùng lặp `codes` (`OrdinalIgnoreCase`, mỗi mã `<= 80` ký tự) hoặc dùng `quantity` $\in [1..100000]$, cộng dồn vào `stock_quantity`.
* **`PATCH /api/v1/staff/redemptions/{id:guid}/fulfill` (`STT 097`):**
  * `action = "Fulfill"` (mặc định): chuyển đơn `Pending` hoặc `Redeemed` (chưa hết hạn) sang `Delivered` kèm `fulfilledAt`.
  * `action = "Cancel"`: chuyển sang `Cancelled`, cộng hoàn lại `stock_quantity` và gọi `PointsService.CreditAsync(..., idempotencyKey: "REFUND:{id}")` hoàn lại toàn bộ điểm đã tiêu.

### 2.5. Nhóm Cấu hình Hệ thống & Chu kỳ Bảng xếp hạng Admin (`A-07`, `A-08` — `STT 098–102`)

* **`PUT /api/v1/admin/gamification/config` (`STT 099`):** Validation bảng cấp cây bắt buộc liên tục từ cấp `1` (`RequiredPoints = 0`), ngưỡng tăng dần nghiêm ngặt (`1..10` cấp) và cấm bỏ bớt cấp nếu còn `EcoFact ACTIVE` gắn với cấp đó (`BR-GAM-06`). Tự động ghi `AuditLog` (`GAMIFICATION_CONFIG_UPDATE`).
* **`POST /api/v1/admin/gamification/leaderboard-cycles` (`STT 101`):** Yêu cầu `endsAt > startsAt`, giải thưởng không âm giảm dần (`top1 >= top2 >= top3`). Nếu `isActive = true`, tự động tắt chu kỳ đang bật trước đó để bảo toàn partial unique index `uq_leaderboard_cycles_single_active`.
* **`POST /api/v1/admin/gamification/leaderboard-cycles/{cycleId:guid}/finalize` (`STT 102`):** Tính thứ hạng trong chu kỳ, cộng điểm `LeaderboardBonus` idempotent cho Top 1/2/3 (`Points > 0`), lưu `winnersSnapshotJson`, chuyển trạng thái `Finalized` (`IsActive = false`) và ghi `AuditLog` (`LEADERBOARD_CYCLE_FINALIZE`).

---

## 3. LƯỢC ĐỒ CSDL & MÃ THÔNG BÁO CHUẨN HÓA (`M-10`)

| Bảng CSDL PostgreSQL | Vai trò Nghiệp vụ | Ràng buộc / Chỉ mục Chính |
|:---|:---|:---|
| `user_point_wallets` | Ví điểm 1-1 (`User` & `Recycler`) | `UNIQUE (user_id)`, `CHECK (current_balance >= 0)` |
| `point_transactions` | Sổ cái biến động điểm bất biến | `UNIQUE (idempotency_key)` (`ux_point_transactions_idem`) |
| `daily_checkin_logs` | Nhật ký check-in theo ngày ICT | `UNIQUE (user_id, checkin_date)` (`ux_daily_checkin_user_date`) |
| `virtual_tree_progress` | Tiến độ Cây Ảo 1-1 (`User`) | `UNIQUE (user_id)`, `tree_level >= 1` |
| `gift_inventory` | Danh mục quà `Physical` / `Voucher` | `stock_quantity >= 0`, `status IN ('ACTIVE','INACTIVE')` |
| `gift_redemptions` | Lịch sử đổi quà & mã voucher | `UNIQUE (redemption_code)`, `UNIQUE (voucher_code)` |
| `eco_facts` | Thư viện kiến thức gắn cấp cây | Index `(status, unlock_tree_level)` |
| `gamification_config`, `point_rate_rules`, `tree_level_thresholds` | Tham số vận hành `A-07` | `id = 1`, `UNIQUE (waste_category_code)`, PK `level` |
| `leaderboard_cycles` | Chu kỳ bảng xếp hạng `A-08` | Partial Unique `uq_leaderboard_cycles_single_active (is_active = TRUE)` |
