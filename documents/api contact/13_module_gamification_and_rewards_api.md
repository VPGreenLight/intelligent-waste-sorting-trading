# ĐẶC TẢ HỢP ĐỒNG API CHI TIẾT (API CONTRACT SPECIFICATION)
## PHÂN HỆ M-10 / FE-13: GAMIFICATION, CÂY ẢO & ĐỔI QUÀ THƯỞNG (`GAMIFICATION & REWARDS`)

* **Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform (`SEP490_G141`)
* **Phiên bản tài liệu:** `v2.7.0` (Cập nhật đồng bộ với mã nguồn `Ecolink.WebApi` / `.NET 10` & PostgreSQL 16)
* **Use Cases bao phủ:**
  * `U-13`: Daily Check-in (*Điểm danh hàng ngày theo múi giờ ICT UTC+7, chuỗi Streak & kẹp trần phi giao dịch*)
  * `U-14`: Redeem Points for Gifts (*Đổi quà nguyên tử trừ tồn kho + trừ ví điểm, E-Voucher sinh mã & hạn dùng, quà hiện vật giao nhận*)
  * `U-15`: Care, Harvest & Upgrade Virtual Tree (*Chăm sóc cây tưới/bón phân với độ ẩm hao hụt `-2%/giờ`, thu hoạch quả chín & nâng cấp mở khóa Eco Fact — chỉ `Citizen`/`User`*)
  * `S-09`: Manage Eco Fact Content (*Staff `[MANAGE_INVENTORY]` biên tập, bật/tắt trạng thái & xóa mềm `INACTIVE` nếu đã có người mở khóa*)
  * `S-10`: Manage Gift Inventory (*Staff `[MANAGE_INVENTORY]` upload ảnh quà, nhập kho `restock`, nhập hàng loạt mã E-Voucher, đóng/mở đổi quà & xử lý Fulfill / Cancel hoàn điểm*)
  * `A-07`: Manage Gamification Config (*Admin cấu hình / reset tỷ lệ điểm/kg, trần ngày, thông số Streak & bảng cấp Cây Ảo*)
  * `A-08`: Configure Leaderboard Cycles (*Admin quản lý, sửa, kích hoạt chu kỳ bảng xếp hạng Tuần/Tháng/Quý & chốt trao thưởng Top 3*)
* **Controllers liên quan:** `GamificationController` (`3`), `UserGamificationController` (`8`), `PointsAndRewardsController` (`8`), `StaffGamificationController` (`17`), `AdminGamificationController` (`8`)
* **Phạm vi Endpoint:** `44` Canonical REST APIs (`STT 077` – `STT 120`, tổng `63` route bindings gồm `19` compatibility aliases) trong [`00_api_inventory_and_endpoint_statistics.md`](./00_api_inventory_and_endpoint_statistics.md)
* **Tiêu chuẩn phản hồi:** Chuẩn hóa theo `ApiResponse<T>` trong [`00_api_overview_and_response_contract.md`](./00_api_overview_and_response_contract.md) (`camelCase`, trạng thái chuỗi).

---

## 1. TỔNG QUAN KIẾN TRÚC & MA TRẬN API PHÂN HỆ `M-10`

### 1.1. Nguyên tắc Kiến trúc Cốt lõi (`BR-GAM-01` – `BR-GAM-09`)

1. **Sổ cái Idempotent & Số dư Không Âm (`BR-GAM-01`, `BR-GAM-03`):**
   * Mỗi người dùng có `1` ví duy nhất `user_point_wallets` (`user_id` unique) và sổ cái bất biến `point_transactions` gắn ràng buộc `ux_point_transactions_idem` chống cộng/trừ trùng (`HANDOVER:{receiptId}:USER`, `CHECKIN:{userId}:{date}`, `REDEEM:{redemptionId}`, `TREE:{userId}:L{level}`, `TREE_HARVEST:{userId}:{harvestNo}`, `LEADERBOARD:{cycleId}:RANK:{rank}:{userId}`, `REFUND:{redemptionId}`).
   * Mọi thao tác trừ điểm trong `PointsService.TryDebitAsync` dùng cập nhật có điều kiện `WHERE current_balance >= points` trong PostgreSQL transaction.
2. **Múi giờ ICT (UTC+7) & Trần Phi Giao dịch (`BR-GAM-02`, `BR-GAM-09`):**
   * `GamTime.Today()` quy đổi ngày check-in và giới hạn chăm cây theo UTC+7. Điểm cộng từ `DailyCheckin` và `VirtualTreeHarvest` bị kẹp theo phần còn lại của `DailyNonTransactionCap`; nếu dư địa bằng `0`, hệ thống ném `BusinessRuleException("BR-GAM-02", "MSG-GAM-05")`.
3. **Đổi quà Nguyên tử (`BR-GAM-04`, `BR-GAM-05`):**
   * `GiftService.RedeemAsync` gộp trừ tồn kho có điều kiện (`status == 'ACTIVE' AND stock_quantity >= qty`), tạo `GiftRedemption`, và trừ ví điểm `TryDebitAsync` trong cùng `1` DB Transaction. Thất bại ở bước nào sẽ rollback toàn bộ bước còn lại.
4. **Phân quyền Hạt mịn & Tách biệt Vai trò (`BR-GAM-07`, `BR-GAM-08`, `BR-ADM-02`):**
   * **Cả `Citizen` (`User`) và `Recycler` (`Collector`):** được dùng ví điểm, đổi quà (`U-14`), điểm danh hằng ngày (`U-13`) và xếp hạng trên Bảng xếp hạng (`STT 079`, `BR-GAM-08`).
   * **Chỉ `Citizen` (`User`):** được nuôi/thu hoạch Cây Ảo (`U-15`) và mở khóa Eco Fact (`BR-GAM-08`).
   * **Staff quản trị kho quà & Eco Fact (`S-09`, `S-10`):** bắt buộc qua `[HasPermission(StaffPermissions.ManageInventory)]` (hoặc `Admin`).

### 1.2. Bảng Quy chiếu Nhanh Toàn bộ `44` Canonical Endpoints (`STT 077` – `STT 120`, `63` Bindings)

| STT | Phương thức | Tuyến đường Chuẩn (`Canonical Route`) | Alias Route Tương thích | Controller / Action | Phân quyền (`RBAC`) |
|:---:|:---:|:---|:---|:---|:---|
| **077** | `GET` | `/api/v1/gamification/profile/me` | — | `GamificationController.GetMyProfile` | `Authorize` |
| **078** | `GET` | `/api/v1/gamification/profile/{userId:guid}` | — | `GamificationController.GetProfile` | `Authorize` |
| **079** | `GET` | `/api/v1/gamification/leaderboard` | — | `GamificationController.GetLeaderboard` | `AllowAnonymous` |
| **080** | `GET` | `/api/v1/gamification/check-in/status` | `GET /api/v1/check-in` | `UserGamificationController.CheckinStatus` | `User,Citizen,Recycler,Collector` |
| **081** | `POST` | `/api/v1/gamification/check-in` | `POST /api/v1/check-in` | `UserGamificationController.CheckIn` | `User,Citizen,Recycler,Collector` |
| **082** | `GET` | `/api/v1/gamification/tree/me` | `GET /api/v1/virtual-tree` | `UserGamificationController.Tree` | `User,Citizen` (`403` Recycler) |
| **083** | `POST` | `/api/v1/gamification/tree/care` | `POST /api/v1/virtual-tree/care` | `UserGamificationController.Care` | `User,Citizen` (`403` Recycler) |
| **084** | `POST` | `/api/v1/gamification/tree/harvest` | `POST /api/v1/virtual-tree/harvest` | `UserGamificationController.Harvest` | `User,Citizen` (`403` Recycler) |
| **085** | `POST` | `/api/v1/gamification/tree/upgrade` | `POST /api/v1/virtual-tree/water` | `UserGamificationController.Water` | `User,Citizen` (`403` Recycler) |
| **086** | `GET` | `/api/v1/gamification/eco-facts/me` | `GET /api/v1/eco-facts` | `UserGamificationController.Facts` | `User,Citizen` (`403` Recycler) |
| **087** | `GET` | `/api/v1/gamification/eco-facts/{id:guid}` | `GET /api/v1/eco-facts/{id:guid}` | `UserGamificationController.Fact` | `User,Citizen` (`403` Recycler) |
| **088** | `GET` | `/api/v1/gamification/points/wallet` | `GET /api/v1/points/wallet` | `PointsAndRewardsController.Wallet` | `User,Citizen,Recycler,Collector` |
| **089** | `GET` | `/api/v1/gamification/points/ledger` | `GET /api/v1/points/transactions` | `PointsAndRewardsController.Transactions` | `User,Citizen,Recycler,Collector` |
| **090** | `GET` | `/api/v1/gamification/gifts` | `GET /api/v1/gifts` | `PointsAndRewardsController.Gifts` | `User,Citizen,Recycler,Collector` |
| **091** | `GET` | `/api/v1/gamification/gifts/{id:guid}` | `GET /api/v1/gifts/{id:guid}` | `PointsAndRewardsController.Gift` | `User,Citizen,Recycler,Collector` |
| **092** | `POST` | `/api/v1/gifts/{id:guid}/redeem` | — | `PointsAndRewardsController.Redeem` | `User,Citizen,Recycler,Collector` |
| **093** | `POST` | `/api/v1/gamification/redemptions` | — | `PointsAndRewardsController.RedeemFromBody` | `User,Citizen,Recycler,Collector` |
| **094** | `GET` | `/api/v1/gamification/redemptions/me` | `GET /api/v1/gift-redemptions` | `PointsAndRewardsController.MyRedemptions` | `User,Citizen,Recycler,Collector` |
| **095** | `GET` | `/api/v1/gamification/redemptions/{id:guid}` | `GET /api/v1/gift-redemptions/{id:guid}` | `PointsAndRewardsController.MyRedemption` | `User,Citizen,Recycler,Collector` |
| **096** | `POST` | `/api/v1/staff/gifts/upload-image` | — | `StaffGamificationController.UploadGiftImage` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **097** | `GET` | `/api/v1/staff/gifts` | — | `StaffGamificationController.Gifts` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **098** | `GET` | `/api/v1/staff/gifts/{id:guid}` | — | `StaffGamificationController.Gift` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **099** | `POST` | `/api/v1/staff/gifts` | — | `StaffGamificationController.CreateGift` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **100** | `PUT` | `/api/v1/staff/gifts/{id:guid}` | — | `StaffGamificationController.UpdateGift` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **101** | `POST` | `/api/v1/staff/gifts/{id:guid}/restock` | — | `StaffGamificationController.Restock` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **102** | `POST` | `/api/v1/staff/gifts/{id:guid}/vouchers/import` | — | `StaffGamificationController.ImportVouchers` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **103** | `PATCH` | `/api/v1/staff/gifts/{id:guid}/status` | `POST /api/v1/staff/gifts/{id:guid}/status` | `StaffGamificationController.GiftStatus` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **104** | `GET` | `/api/v1/staff/gifts/{id:guid}/redemptions` | — | `StaffGamificationController.GiftRedemptions` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **105** | `GET` | `/api/v1/staff/redemptions` | — | `StaffGamificationController.AllRedemptions` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **106** | `PATCH` | `/api/v1/staff/redemptions/{id:guid}/fulfill` | `POST /api/v1/staff/redemptions/{id:guid}/fulfill`<br>`POST /api/v1/staff/gift-redemptions/{id:guid}/fulfill` | `StaffGamificationController.Fulfill` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **107** | `GET` | `/api/v1/staff/eco-facts` | — | `StaffGamificationController.Facts` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **108** | `GET` | `/api/v1/staff/eco-facts/{id:guid}` | — | `StaffGamificationController.Fact` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **109** | `POST` | `/api/v1/staff/eco-facts` | — | `StaffGamificationController.CreateFact` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **110** | `PUT` | `/api/v1/staff/eco-facts/{id:guid}` | — | `StaffGamificationController.UpdateFact` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **111** | `PATCH` | `/api/v1/staff/eco-facts/{id:guid}/status` | `POST /api/v1/staff/eco-facts/{id:guid}/status` | `StaffGamificationController.FactStatus` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **112** | `DELETE` | `/api/v1/staff/eco-facts/{id:guid}` | — | `StaffGamificationController.DeleteFact` | `Staff [MANAGE_INVENTORY]`, `Admin` |
| **113** | `GET` | `/api/v1/admin/gamification/config` | — | `AdminGamificationController.Get` | `Admin` |
| **114** | `PUT` | `/api/v1/admin/gamification/config` | — | `AdminGamificationController.Update` | `Admin` |
| **115** | `POST` | `/api/v1/admin/gamification/config/reset` | — | `AdminGamificationController.Reset` | `Admin` |
| **116** | `GET` | `/api/v1/admin/gamification/leaderboard-cycles` | — | `AdminGamificationController.GetCycles` | `Admin` |
| **117** | `POST` | `/api/v1/admin/gamification/leaderboard-cycles` | — | `AdminGamificationController.CreateCycle` | `Admin` |
| **118** | `PUT` | `/api/v1/admin/gamification/leaderboard-cycles/{cycleId:guid}` | — | `AdminGamificationController.UpdateCycle` | `Admin` |
| **119** | `PATCH` | `/api/v1/admin/gamification/leaderboard-cycles/{cycleId:guid}/activate` | `POST .../leaderboard-cycles/{cycleId:guid}/activate` | `AdminGamificationController.ActivateCycle` | `Admin` |
| **120** | `POST` | `/api/v1/admin/gamification/leaderboard-cycles/{cycleId:guid}/finalize` | — | `AdminGamificationController.FinalizeCycle` | `Admin` |

---

## 2. ĐẶC TẢ CHI TIẾT CÁC ENDPOINTS (`STT 077` – `STT 120`)

### 2.1. Nhóm Hồ sơ Sống Xanh, Bảng xếp hạng & Điểm danh (`U-13` — `STT 077–081`)

#### `[STT 077 & 078]` `GET /api/v1/gamification/profile/me` & `GET /api/v1/gamification/profile/{userId:guid}`
* **Mô tả:** Tổng hợp hồ sơ Sống Xanh trực tiếp từ dữ liệu thật (`UserPointWallets`, `VirtualTreeProgresses`, `HandoverReceipts`, `AiScanHistories`, `DailyCheckinLogs`). Không sử dụng bộ đệm mock tĩnh khi `db != null`.
* **Response `200 OK` (`EcoProfileDto`):** `userId`, `currentGreenPoints`, `totalLifetimePoints`, `treeLevel`, `treeStageTitle`, `treeExperience`, `experienceToNextLevel`, `totalWasteScanned`, `totalRecycledKg`, `currentStreak`, `co2SavedKg`.

#### `[STT 079]` `GET /api/v1/gamification/leaderboard`
* **Query Parameters:** `top` (`1..100`, mặc định `10`), `period` (`Weekly|Monthly|Quarterly`), `cycleId` (`Guid?`), `role` (`all|user|recycler`).
* **Response `200 OK` (`LeaderboardViewDto`):** Gồm `activeCycle: LeaderboardCycleDto?`, `items: LeaderboardEntryDto[]` (xếp hạng cả `User` và `Recycler` đang `Active`, điểm tính theo `TotalEarned` / điểm kiếm được trong chu kỳ, đổi quà không làm tụt hạng `BR-GAM-07`), `myEntry: LeaderboardEntryDto?`, và `recentFinalizedCycles: LeaderboardCycleDto[]`.

#### `[STT 080]` & `[STT 081]` `GET /api/v1/gamification/check-in/status` & `POST /api/v1/gamification/check-in`
* **Logic Check-in:** Cộng `checkinBasePoints + min((streak - 1) * streakBonusIncrement, streakBonusCap)`, kẹp theo phần còn lại của `dailyNonTransactionCap` hôm nay ICT.
* **Responses:**
  * `GET 200 OK`: `CheckinStatusDto` (`checkedInToday`, `currentStreak`, `nextStreak`, `streakWillReset`, `todayRewardPreview`, `dailyCapRemaining`, `walletBalance`, `calendar[7]`).
  * `POST 200 OK` (`MSG-GAM-08`): `CheckinResultDto` (`pointsAwarded`, `streakCount`, `walletBalance`, `limitedByDailyCap`).
  * `POST 400 Bad Request`: `MSG-GEN-01` nếu đã điểm danh trong ngày ICT; `MSG-GAM-05` nếu đã chạm trần điểm ngày.

### 2.2. Nhóm Ví điểm & Đổi quà (`U-14` — `STT 088–095`)

#### `[STT 088 & 089]` `GET /api/v1/gamification/points/wallet` & `GET /api/v1/gamification/points/ledger`
* **Query Parameters (`ledger`):** `from` (`DateTime?`), `to` (`DateTime?`), `actionType` (`Handover_Reward|Daily_Checkin|Redeem_Gift|Tree_Care|Leaderboard_Bonus|Adjustment`), `page`, `pageSize` (`1..50`).
* **Response `200 OK`:** `WalletDto` cho `/wallet`, hoặc `GamPagedResult<PointTransactionDto>` cho `/ledger`.

#### `[STT 090]` – `[STT 095]` Cửa hàng quà & Đổi thưởng nguyên tử
* **Catalog (`090–091`):** `GET /api/v1/gamification/gifts` (`?type=Physical|Voucher&sort=points_asc|points_desc|newest&search=`) và `GET /api/v1/gamification/gifts/{id:guid}`.
* **`POST /api/v1/gamification/redemptions` (`093`) / `POST /api/v1/gifts/{id:guid}/redeem` (`092`):**
  ```json
  {
    "giftId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "quantity": 1,
    "deliveryAddress": "Số 1 Đại Cồ Việt, Hai Bà Trưng, Hà Nội"
  }
  ```
* **Validation & Responses:**
  * Voucher chỉ cho phép `quantity == 1` (`BR-GAM-05`), tự sinh mã `ECO-XXXX-XXXX-XXXX` và hạn dùng `now + voucherValidityDays`, trạng thái `Redeemed`.
  * Physical bắt buộc có `deliveryAddress`, trạng thái `Pending` chờ Staff xử lý (`GET /redemptions/me` `094`, `GET /redemptions/{id}` `095`).
  * `201 Created` (`MSG-GAM-02`): `RedeemResultDto { redemption, walletBalance }`.
  * `400 Bad Request`: `MSG-GAM-04` (hết hàng / `INACTIVE`), `MSG-GAM-03` (không đủ số dư điểm, rollback tồn kho), `MSG-AUTH-02` (thiếu địa chỉ cho quà hiện vật).

### 2.3. Nhóm Cây Ảo, Chăm sóc, Thu hoạch & Eco Fact (`U-15`, `S-09` — `STT 082–087`, `107–112`)

* **`GET /api/v1/gamification/tree/me` (`STT 082`):** Chỉ cho phép `User,Citizen` (`403` với `Recycler`). Trả về `VirtualTreeDto` gồm:
  * Độ ẩm & trạng thái sinh học: `hydrationPercent` (hao hụt `-2%/giờ` qua `HydrationDecayPerHour = 2.0`), `isWilted` (`true` khi `hydrationPercent == 0`), `inOptimalHydrationZone` (`true` khi `60 <= hydrationPercent <= 90`), `canWater` (`dailyWaterCount < dailyLimit && hydrationPercent < 95`), `canFertilize` (`hydrationPercent >= 30 && !harvestReady && careExp < maxCareExp && (dailyFertilizeCount < dailyFertilizeLimit || fertilizerPacks > 0)`).
  * Chỉ số chăm sóc & thu hoạch: `careExp`, `maxCareExp` (`= 80 + (treeLevel - 1) * 20`), `harvestReady`, `harvestRewardPreview` (`= 10 + treeLevel * 5`), `fertilizerPacks` (túi phân hữu cơ dự trữ), `totalHarvested`, `lastCareEffect`.
  * Lộ trình nâng cấp: `level`, `levelTitle`, `maxLevel`, `isMaxLevel`, `nextLevel`, `nextLevelTitle`, `upgradeCost`, `walletBalance`, `progressPercent`, `canUpgrade`, `levels[]`, `unlockedFactCount`, `totalFactCount`.
* **`POST /api/v1/gamification/tree/care` (`STT 083` — `CareTreeRequest { actionType: "water" | "fertilize" }`):**
  * `"water"` (mặc định nếu bỏ trống): chặn nếu đạt `dailyWaterLimit` (`TreeLimit`) hoặc độ ẩm `>= 95%`; cộng `+35%` độ ẩm (tối đa `100%`) và `+10 EXP` (hồi sinh nếu cây đang héo `0%`, không tụt cấp).
  * `"fertilize"`: chặn nếu cây héo (`0%`), độ ẩm `< 30%` (xót rễ), hoặc đã `harvestReady`. Dùng lượt miễn phí ngày (`dailyFertilizeLimit = min(2, TreeLimit)`) trước, hết lượt miễn phí tự động tiêu thụ `1` `fertilizerPack`; độ ẩm vùng vàng `60–90%` nhận **x1.4 (`+35 EXP`)**, ngoài vùng vàng **`+25 EXP`**, đồng thời giảm `-10%` độ ẩm.
* **`POST /api/v1/gamification/tree/harvest` (`STT 084`):** Yêu cầu `hydration > 0` và `harvestReady || careExp >= maxCareExp`. Cộng `harvestRewardPreview = 10 + level*5` Điểm Xanh idempotent (`TREE_HARVEST:{userId}:{harvestNo}`), kẹp trần `BR-GAM-02` `DailyNonTransactionCap` (nếu dư địa ngày `<= 0` trả `400 MSG-GAM-05` và giữ nguyên quả chín sang hôm sau), reset `careExp = 0`, `harvestReady = false`, tăng `totalHarvested`. Trả về `HarvestTreeResultDto { tree, pointsAwarded, limitedByDailyCap }` (`MSG-GAM-06`).
* **`POST /api/v1/gamification/tree/upgrade` (`STT 085`):** Kiểm tra chưa đạt `MaxLevel`, chưa vượt `dailyTreeCareLimit`, trừ số dư ví đúng bằng `next.RequiredPoints`, tăng cấp một chiều và trả về `WaterTreeResultDto` kèm danh sách `unlockedFacts` (`MSG-GAM-06`).
* **Tra cứu Eco Fact Citizen (`STT 086–087`):** `GET /api/v1/gamification/eco-facts/me?includeLocked=false` và `GET /api/v1/gamification/eco-facts/{id:guid}`.
* **Quản trị Eco Fact Staff (`STT 107–112`, `[MANAGE_INVENTORY]`):** `GET /staff/eco-facts`, `GET /staff/eco-facts/{id}`, `POST /staff/eco-facts`, `PUT /staff/eco-facts/{id}` (tiêu đề `1..200`, nội dung `1..300`, `unlockTreeLevel` $\in [1..\text{MaxLevel}]$), `PATCH|POST /staff/eco-facts/{id}/status`, `DELETE /staff/eco-facts/{id}` (chuyển `INACTIVE` nếu đã có người mở khóa theo `BR-GAM-06`).

### 2.4. Nhóm Quản trị Kho quà & Xử lý Đổi thưởng Staff (`S-10` — `STT 096–106`)

* **`POST /api/v1/staff/gifts/upload-image` (`STT 096`):** `multipart/form-data` (`file`), định dạng `.jpg/.jpeg/.png/.webp`, tối đa `5 MB`, tải lên thư mục R2 `gamification-gifts`, trả `RecyclerDocumentUploadResponseDto`.
* **Quản lý kho quà (`STT 097–104`):** `GET /staff/gifts` (danh sách), `GET /staff/gifts/{id}` (chi tiết 1 quà), `POST /staff/gifts` (`201 Created`), `PUT /staff/gifts/{id}` (sửa thông tin quà), `POST /staff/gifts/{id}/restock` (`RestockGiftRequest { quantity: 1..1,000,000 }` cộng tồn kho), `POST /staff/gifts/{id}/vouchers/import` (`GiftType.Voucher`, khử trùng `codes` hoặc `quantity` $\in [1..100000]$), `PATCH|POST /staff/gifts/{id}/status` (`ACTIVE|INACTIVE`), `GET /staff/gifts/{id}/redemptions` (lọc lịch sử đổi theo quà).
* **`GET /api/v1/staff/redemptions` (`STT 105`) & `PATCH /api/v1/staff/redemptions/{id:guid}/fulfill` (`STT 106`):**
  * `action = "Fulfill"` (mặc định): chuyển đơn `Pending` hoặc `Redeemed` (chưa hết hạn) sang `Delivered` kèm `fulfilledAt`.
  * `action = "Cancel"`: chuyển sang `Cancelled`, cộng hoàn lại `stock_quantity` và gọi `PointsService.CreditAsync(..., idempotencyKey: "REFUND:{id}")` hoàn lại toàn bộ điểm đã tiêu.

### 2.5. Nhóm Cấu hình Hệ thống & Chu kỳ Bảng xếp hạng Admin (`A-07`, `A-08` — `STT 113–120`)

* **Cấu hình Gamification (`STT 113–115`):** `GET /api/v1/admin/gamification/config`, `PUT /api/v1/admin/gamification/config` (cấp cây liên tục từ cấp `1` `RequiredPoints = 0`, tăng dần `1..10` cấp, cấm bỏ cấp còn `EcoFact ACTIVE` theo `BR-GAM-06`, ghi `AuditLog`), và **`POST /api/v1/admin/gamification/config/reset`** (`STT 115`: khôi phục tham số mặc định và ghi `AuditLog`).
* **Chu kỳ Bảng xếp hạng (`STT 116–120`):**
  * `GET /leaderboard-cycles` (`116`) & `POST /leaderboard-cycles` (`117` — yêu cầu `endsAt > startsAt`, `top1 >= top2 >= top3`, tự tắt chu kỳ cũ nếu `isActive = true`).
  * `PUT /leaderboard-cycles/{cycleId:guid}` (`118` — sửa chu kỳ chưa `Finalized`).
  * `PATCH|POST /leaderboard-cycles/{cycleId:guid}/activate` (`119` — chuyển chu kỳ đang áp dụng, đảm bảo partial unique index `uq_leaderboard_cycles_single_active`).
  * `POST /leaderboard-cycles/{cycleId:guid}/finalize` (`120` — trao điểm `LeaderboardBonus` idempotent cho Top 1/2/3, lưu `winnersSnapshotJson`, đặt `Finalized` & ghi `AuditLog`).

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
