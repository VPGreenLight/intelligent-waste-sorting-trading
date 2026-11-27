# ĐẶC TẢ KIỂM THỬ ĐƠN VỊ (UNIT TEST SPECIFICATION)
## PHÂN HỆ M-10 / FE-13: GAMIFICATION, CÂY ẢO & ĐỔI QUÀ THƯỞNG (`GAMIFICATION & REWARDS`)

* **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
* **Phân hệ / Module trong Report 5.1:** `Module 13` (`M-10` / `FE-13`)
* **Dịch vụ & Validator kiểm thử:** `LeaderboardService`, `GamificationService`, `CheckinService`, `PointsService`, `GiftService`, `VirtualTreeService`, `EcoFactService`, `GamificationConfigService`, `ComplaintService` (clawback ví điểm) & `BlogAndGamificationValidators`
* **Use Cases bao phủ:** `U-13`, `U-14`, `U-15`, `S-09`, `S-10`, `A-07`, `A-08`
* **Tệp mã kiểm thử tự động:** [`GamificationAndBlogUnitTests.cs`](../../../src/Ecolink.Backend/Ecolink.UnitTests/Module10_GamificationAndBlog/GamificationAndBlogUnitTests.cs)
* **Ngày thực thi gần nhất:** `2026-11-23`
* **Kết quả thực thi:** **`Round 1`: `16/16 Passed` $\rightarrow$ `Round 2`: `16/16 Passed` (`100%` Pass Rate)**

---

## 1. BẢNG TỔNG HỢP KIỂM THỬ ĐƠN VỊ (SUMMARY TABLE)

| Code Module | `LeaderboardService`, `CheckinService`, `PointsService`, `GiftService`, `VirtualTreeService`, `EcoFactService`, `GamificationConfigService`, `GamificationService`, `ComplaintService`, `Validators` (`M-10`) | | |
| :--- | :---: | :---: | :---: |
| **Number of TCs** | **16** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 16 | 0 | 0 |
| *Round 2* | 16 | 0 | 0 |
| *Round 3* | 0 | 0 | 16 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count** | **7** | **5** | **4** |

---

## 2. CHI TIẾT 16 UNIT TEST CASES (`UTC001` – `UTC016`)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- | :---: | :---: | :---: | :--- |
| **UTC001** | Admin tồn tại; chưa có chu kỳ trùng | `CreateCycleAsync("Tuần 1", IsActive=true)` $\rightarrow$ `CreateCycleAsync("Tháng 11", IsActive=true)` | Chỉ có `1` chu kỳ `IsActive=true` (chu kỳ mới), chu kỳ cũ tắt về `false`, ghi `2` bản ghi `AuditLog` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC001_CreateCycle_WithIsActiveTrue_ShouldDeactivatePreviousActiveCycle]` `A-08` duy nhất 1 kỳ active |
| **UTC002** | DbContext sạch | `StartsAt=now, EndsAt=now.AddHours(-1)` | Ném `BusinessRuleException ("BR-GAM-07", "MSG-AUTH-02")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC002_CreateCycle_WhenEndsAtBeforeStartsAt_ShouldThrowMSGAuth02]` Chặn mốc ngày ngược |
| **UTC003** | DbContext sạch | `PeriodType="Yearly"` & `Top1=100 < Top2=200` | Cả 2 trường hợp đều ném `BusinessRuleException ("MSG-AUTH-02")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC003_CreateCycle_WhenPeriodTypeOrBonusOrderInvalid_ShouldThrowMSGAuth02]` Kiểm định chu kỳ & thưởng |
| **UTC004** | Có `2` chu kỳ (Tuần active, Tháng inactive) | `ActivateCycleAsync(adminId, month.Id)` | Chu kỳ Tháng bật `IsActive=true`, Tuần về `false`, ghi `LEADERBOARD_CYCLE_ACTIVATE` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC004_ActivateCycle_ShouldSwitchActiveCycleAndWriteAuditLog]` Chuyển chu kỳ |
| **UTC005** | Có `2` Citizen & `1` Recycler; Citizen 1 kiếm `200` tiêu `180`, Citizen 2 kiếm `80` + thưởng `LeaderboardBonus` | `GetLeaderboardViewAsync(10, "Weekly", null, second.Id)` | Chỉ đưa Citizen vào BXH (loại Recycler); Hạng 1 là người tiêu điểm (`PeriodPoints=200`), không cộng dồn `LeaderboardBonus` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC005_GetLeaderboardView_ShouldRankByPeriodEarnedWithoutRedeemPenalty]` `BR-GAM-07`, `BR-GAM-08` |
| **UTC006** | Citizen `120đ`, Recycler `80đ` ở cả `UserEcoProfile` & `UserPointWallet`; biên nhận `+50đ` | `ResolveComplaintAsync("CancelDisputedOrder")` | `PointsRevoked=true`, ví Citizen còn `70`, Recycler còn `30`, ghi `2` bút toán `-50 ComplaintClawback` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC006_ComplaintService_CancelDisputedOrder_ShouldClawBackBothEcoProfileAndPointWallet]` Đồng bộ clawback `M-11` |
| **UTC007** | Citizen chỉ còn `15đ` trong ví nhưng cần thu hồi `50đ` | `ResolveComplaintAsync("CancelDisputedOrder")` | Ví kẹp sàn `CurrentBalance=0` (`>= 0`), bút toán thu hồi chỉ ghi `-15đ` | Boundary | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC007_ComplaintService_CancelDisputedOrder_WhenWalletLowerThanRevoke_ShouldClampToZero]` `BR-GAM-03` Floor 0 |
| **UTC008** | User đã check-in hôm nay | Gọi `CheckInAsync(user.Id)` lần 2 trong ngày | Ném `BusinessRuleException ("BR-GAM-09", "MSG-GEN-01")` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC008_CheckinService_DuplicateSameDay_ShouldThrowBRGam09MSGGen01]` Chặn check-in trùng |
| **UTC009** | `DailyNonTransactionCap = 50`; U1 đã nhận `47đ`, U2 đã nhận `50đ` | `CheckInAsync(uClamp)` & `CheckInAsync(uFull)` | U1 chỉ nhận `3đ` (`LimitedByDailyCap=true`); U2 chạm `0` dư địa ném `MSG-GAM-05` | Boundary | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC009_CheckinService_WhenDailyCapRemainingTight_ShouldClampOrThrowMSGGam05]` `BR-GAM-02` Daily Cap |
| **UTC010** | User có `500đ`; có `1` quà Voucher (`150đ`) và `1` quà Physical (`100đ`) | `RedeemAsync` Voucher (`qty=1`) & Physical (`qty=2` + địa chỉ) | Voucher trả mã `ECO-...` (`Redeemed`), Physical ở `Pending`, trừ đúng `350đ` & tồn kho | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC010_GiftService_RedeemVoucherAndPhysical_ShouldDebitWalletDecrementStockAndSetStatus]` `U-14`, `BR-GAM-04` |
| **UTC011** | User có `20đ`; quà A giá `200đ` tồn `5`; quà B hết hàng tồn `0` | `RedeemAsync(expensive)` & `RedeemAsync(emptyStock)` | Ném `MSG-GAM-03` (giữ nguyên tồn kho `5`) và `MSG-GAM-04` | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC011_GiftService_RedeemAsync_WhenInsufficientBalanceOrOutOfStock_ShouldThrowProperMsgCode]` `BR-GAM-03`, `BR-GAM-04` |
| **UTC012** | User có `250đ`, cây cấp `1`; Staff đã tạo EcoFact cấp `2` | `WaterAndUpgradeAsync(user.Id)` | Trừ `100đ`, nâng lên cấp `2`, mở khóa EcoFact cấp `2` | Normal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC012_VirtualTreeService_WaterAndUpgradeAsync_ShouldUpgradeLevelDebitPointsAndUnlockFacts]` `U-15`, `BR-GAM-06` |
| **UTC013** | Đã có User đạt cây cấp `2` (chưa ai đạt cấp `4`) | `DeleteAsync` trên Fact cấp `2` và Fact cấp `4` | Fact cấp `2` chuyển `INACTIVE` (giữ lịch sử mở khóa); Fact cấp `4` xóa cứng | Boundary | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC013_EcoFactService_DeleteAsync_ShouldSoftDeactivateIfUnlockedAndHardDeleteOtherwise]` `S-09`, `BR-GAM-06` |
| **UTC014** | Có `EcoFact ACTIVE` gắn `UnlockTreeLevel = 4` | `UpdateAsync` cắt `TreeLevels` xuống chỉ còn `2` cấp | Ném `BusinessRuleException ("BR-GAM-06", "MSG-AUTH-02")` bảo vệ cấp đang có Fact | Abnormal | Passed | 2026-11-19 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC014_GamificationConfigService_WhenRemovingLevelWithActiveEcoFact_ShouldThrowBRGam06]` `A-07` khóa xóa cấp đang dùng |
| **UTC015** | User có cây cấp `2` với `HydrationPercent = 75`, `LastHydrationAt = UtcNow - 40h` (suy giảm `-2%/giờ` -> `0%`) | `GetAsync(user.Id)`, `CareAsync("fertilize")` khi héo, `CareAsync("water")` x3 (`0%->35%->70%->100%`), reset `DailyWaterCount = 0` và `CareAsync("water")` ở `100%` | Cây giữ nguyên cấp `2` (`BR-GAM-06`), `IsWilted = true`; chặn bón phân khi héo (`MSG-GEN-01`); tưới lần 1 hồi sinh `35%` (`+10 EXP`); chạm ngưỡng bão hòa `>= 95%` bị chặn `MSG-GEN-01` | Boundary | Passed | 2026-11-23 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC015_VirtualTreeService_WaterRevivesWiltedTree_AndRespectsSaturationGuard]` `U-15` suy giảm độ ẩm, hồi sinh cây héo & chặn tưới bão hòa (`>= 95%`) |
| **UTC016** | User có cây cấp `2` (`MaxCareExp = 100, HarvestReward = 20`), `HydrationPercent = 25` | `CareAsync("fertilize")` khi độ ẩm `< 30%`; chuỗi `water` (`60%`) $\rightarrow$ `fertilize` (`+35 EXP`) $\rightarrow$ `water` (`85%`) $\rightarrow$ `fertilize` (`90 EXP`) $\rightarrow$ `water` (`100 EXP`) $\rightarrow$ `HarvestAsync` | Đất khô `25% < 30%` bị chặn (`MSG-GEN-01`); `60%` vào vùng ẩm vàng (`InOptimalHydrationZone=true`); đạt `100 EXP` bật `HarvestReady=true`; thu hoạch cộng `+20đ` ví (`TotalHarvested=1, CareExp=0`) | Normal | Passed | 2026-11-23 | Hoàng Thắng Nguyễn | Passed | Pending | N/A | `[UTC016_VirtualTreeService_FertilizeOptimalZoneDryGuardAndHarvest_ShouldAwardWalletPointsUnderCap]` `U-15` chặn bón đất khô `< 30%`, vùng ẩm vàng `60–90%` & thu hoạch |
