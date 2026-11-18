using System.Text;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ecolink.Infrastructure.Data.Configurations;

internal static class GamEnumConverters
{
    // HandoverReward <-> Handover_Reward (đúng giá trị trong báo cáo 3.5.2)
    public static readonly ValueConverter<PointActionType, string> ActionType = new(
        v => v == PointActionType.HandoverReward ? "Handover_Reward"
           : v == PointActionType.DailyCheckin ? "Daily_Checkin"
           : v == PointActionType.RedeemGift ? "Redeem_Gift"
           : v == PointActionType.TreeCare ? "Tree_Care"
           : v == PointActionType.LeaderboardBonus ? "Leaderboard_Bonus"
           : "Adjustment",
        s => s == "Handover_Reward" ? PointActionType.HandoverReward
           : s == "Daily_Checkin" ? PointActionType.DailyCheckin
           : s == "Redeem_Gift" ? PointActionType.RedeemGift
           : s == "Tree_Care" ? PointActionType.TreeCare
           : s == "Leaderboard_Bonus" ? PointActionType.LeaderboardBonus
           : PointActionType.Adjustment);

    private static string Underscore(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i])) sb.Append('_');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }
}

public class UserPointWalletConfiguration : IEntityTypeConfiguration<UserPointWallet>
{
    public void Configure(EntityTypeBuilder<UserPointWallet> b)
    {
        b.ToTable("user_point_wallets", t => t.HasCheckConstraint("ck_wallet_balance_nonneg", "current_balance >= 0"));
        b.HasKey(x => x.Id).HasName("pk_user_point_wallets");
        b.Property(x => x.Id).HasColumnName("wallet_id");
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.CurrentBalance).HasColumnName("current_balance").HasDefaultValue(0L).IsRequired();
        b.Property(x => x.TotalEarned).HasColumnName("total_earned").HasDefaultValue(0L).IsRequired();
        b.Property(x => x.TotalSpent).HasColumnName("total_spent").HasDefaultValue(0L).IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()").IsRequired();
        b.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ux_user_point_wallets_user");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_user_point_wallets_users")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PointTransactionConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> b)
    {
        b.ToTable("point_transactions");
        b.HasKey(x => x.Id).HasName("pk_point_transactions");
        b.Property(x => x.Id).HasColumnName("transaction_id");
        b.Property(x => x.WalletId).HasColumnName("wallet_id").IsRequired();
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.Points).HasColumnName("points").IsRequired();
        b.Property(x => x.ActionType).HasColumnName("action_type").HasConversion(GamEnumConverters.ActionType)
            .HasMaxLength(30).IsRequired();
        b.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(50);
        b.Property(x => x.SourceId).HasColumnName("source_id").HasMaxLength(100);
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100);
        b.Property(x => x.Description).HasColumnName("description");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        b.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("ux_point_transactions_idem");
        b.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("ix_point_transactions_user_created");
        b.HasOne<UserPointWallet>().WithMany().HasForeignKey(x => x.WalletId)
            .HasConstraintName("fk_point_transactions_wallet").OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .HasConstraintName("fk_point_transactions_users").OnDelete(DeleteBehavior.Restrict);
    }
}

public class DailyCheckinLogConfiguration : IEntityTypeConfiguration<DailyCheckinLog>
{
    public void Configure(EntityTypeBuilder<DailyCheckinLog> b)
    {
        b.ToTable("daily_checkin_logs");
        b.HasKey(x => x.Id).HasName("pk_daily_checkin_logs");
        b.Property(x => x.Id).HasColumnName("checkin_id");
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.CheckinDate).HasColumnName("checkin_date").HasColumnType("date").IsRequired();
        b.Property(x => x.StreakCount).HasColumnName("streak_count").HasDefaultValue(1).IsRequired();
        b.Property(x => x.PointsAwarded).HasColumnName("points_awarded").HasDefaultValue(0L).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        b.HasIndex(x => new { x.UserId, x.CheckinDate }).IsUnique().HasDatabaseName("ux_daily_checkin_user_date");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_daily_checkin_users")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VirtualTreeProgressConfiguration : IEntityTypeConfiguration<VirtualTreeProgress>
{
    public void Configure(EntityTypeBuilder<VirtualTreeProgress> b)
    {
        b.ToTable("virtual_tree_progress", t => t.HasCheckConstraint("ck_tree_level_min", "tree_level >= 1"));
        b.HasKey(x => x.Id).HasName("pk_virtual_tree_progress");
        b.Property(x => x.Id).HasColumnName("progress_id");
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.TreeLevel).HasColumnName("tree_level").HasDefaultValue(1).IsRequired();
        b.Property(x => x.GrowthPoints).HasColumnName("growth_points").HasDefaultValue(0L).IsRequired();
        b.Property(x => x.TotalWatered).HasColumnName("total_watered").HasDefaultValue(0).IsRequired();
        b.Property(x => x.DailyWaterCount).HasColumnName("daily_water_count").HasDefaultValue(0).IsRequired();
        b.Property(x => x.LastWaterDate).HasColumnName("last_water_date").HasColumnType("date");
        b.Property(x => x.LastWateredAt).HasColumnName("last_watered_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()").IsRequired();
        b.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ux_virtual_tree_user");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_virtual_tree_users")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GiftConfiguration : IEntityTypeConfiguration<Gift>
{
    public void Configure(EntityTypeBuilder<Gift> b)
    {
        b.ToTable("gift_inventory", t =>
        {
            t.HasCheckConstraint("ck_gift_stock_nonneg", "stock_quantity >= 0");
            t.HasCheckConstraint("ck_gift_points_nonneg", "points_required >= 0");
        });
        b.HasKey(x => x.Id).HasName("pk_gift_inventory");
        b.Property(x => x.Id).HasColumnName("gift_id");
        b.Property(x => x.Name).HasColumnName("gift_name").HasMaxLength(150).IsRequired();
        b.Property(x => x.Type).HasColumnName("gift_type").HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(GiftType.Physical).IsRequired();
        b.Property(x => x.Description).HasColumnName("description");
        b.Property(x => x.ImageUrl).HasColumnName("image_url");
        b.Property(x => x.PointsRequired).HasColumnName("points_required").IsRequired();
        b.Property(x => x.StockQuantity).HasColumnName("stock_quantity").HasDefaultValue(0).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("ACTIVE").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()").IsRequired();
        b.HasIndex(x => new { x.Status, x.Type }).HasDatabaseName("ix_gift_inventory_status_type");
    }
}

public class GiftRedemptionConfiguration : IEntityTypeConfiguration<GiftRedemption>
{
    public void Configure(EntityTypeBuilder<GiftRedemption> b)
    {
        b.ToTable("gift_redemptions");
        b.HasKey(x => x.Id).HasName("pk_gift_redemptions");
        b.Property(x => x.Id).HasColumnName("redemption_id");
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.GiftId).HasColumnName("gift_id").IsRequired();
        b.Property(x => x.PointsSpent).HasColumnName("points_spent").IsRequired();
        b.Property(x => x.Quantity).HasColumnName("quantity").HasDefaultValue(1).IsRequired();
        b.Property(x => x.RedemptionCode).HasColumnName("redemption_code").HasMaxLength(50).IsRequired();
        b.Property(x => x.VoucherCode).HasColumnName("voucher_code").HasMaxLength(100);
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(RedemptionStatus.Pending).IsRequired();
        b.Property(x => x.DeliveryAddress).HasColumnName("delivery_address");
        b.Property(x => x.RedeemedAt).HasColumnName("redeemed_at").HasDefaultValueSql("NOW()").IsRequired();
        b.Property(x => x.FulfilledAt).HasColumnName("fulfilled_at");
        b.HasIndex(x => x.RedemptionCode).IsUnique().HasDatabaseName("ux_gift_redemptions_code");
        b.HasIndex(x => x.VoucherCode).IsUnique().HasDatabaseName("ux_gift_redemptions_voucher");
        b.HasIndex(x => new { x.UserId, x.RedeemedAt }).HasDatabaseName("ix_gift_redemptions_user");
        b.HasIndex(x => new { x.GiftId, x.RedeemedAt }).HasDatabaseName("ix_gift_redemptions_gift");
        b.HasOne(x => x.Gift).WithMany().HasForeignKey(x => x.GiftId).HasConstraintName("fk_gift_redemptions_gift")
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_gift_redemptions_users")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class EcoFactConfiguration : IEntityTypeConfiguration<EcoFact>
{
    public void Configure(EntityTypeBuilder<EcoFact> b)
    {
        b.ToTable("eco_facts", t => t.HasCheckConstraint("ck_eco_fact_level_min", "unlock_tree_level >= 1"));
        b.HasKey(x => x.Id).HasName("pk_eco_facts");
        b.Property(x => x.Id).HasColumnName("fact_id");
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        b.Property(x => x.Content).HasColumnName("content").IsRequired();
        b.Property(x => x.SourceUrl).HasColumnName("source_url");
        b.Property(x => x.ImageUrl).HasColumnName("image_url");
        b.Property(x => x.UnlockTreeLevel).HasColumnName("unlock_tree_level").HasDefaultValue(1).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("ACTIVE").IsRequired();
        b.Property(x => x.CreatedByStaffId).HasColumnName("created_by_staff_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()").IsRequired();
        b.HasIndex(x => new { x.Status, x.UnlockTreeLevel }).HasDatabaseName("ix_eco_facts_status_level");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByStaffId).HasConstraintName("fk_eco_facts_staff")
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class GamificationConfigConfiguration : IEntityTypeConfiguration<GamificationConfig>
{
    public void Configure(EntityTypeBuilder<GamificationConfig> b)
    {
        b.ToTable("gamification_config", t => t.HasCheckConstraint("ck_gamification_config_single", "id = 1"));
        b.HasKey(x => x.Id).HasName("pk_gamification_config");
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.DailyNonTransactionCap).HasColumnName("daily_non_transaction_cap").IsRequired();
        b.Property(x => x.CheckinBasePoints).HasColumnName("checkin_base_points").IsRequired();
        b.Property(x => x.StreakBonusIncrement).HasColumnName("streak_bonus_increment").IsRequired();
        b.Property(x => x.StreakBonusCap).HasColumnName("streak_bonus_cap").IsRequired();
        b.Property(x => x.DailyTreeCareLimit).HasColumnName("daily_tree_care_limit").IsRequired();
        b.Property(x => x.VoucherValidityDays).HasColumnName("voucher_validity_days").IsRequired();
        b.Property(x => x.DefaultPointsPerKg).HasColumnName("default_points_per_kg").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()").IsRequired();
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}

public class PointRateRuleConfiguration : IEntityTypeConfiguration<PointRateRule>
{
    public void Configure(EntityTypeBuilder<PointRateRule> b)
    {
        b.ToTable("point_rate_rules", t => t.HasCheckConstraint("ck_point_rate_nonneg", "points_per_kg >= 0"));
        b.HasKey(x => x.Id).HasName("pk_point_rate_rules");
        b.Property(x => x.Id).HasColumnName("rule_id");
        b.Property(x => x.WasteCategoryCode).HasColumnName("waste_category_code").HasMaxLength(50).IsRequired();
        b.Property(x => x.PointsPerKg).HasColumnName("points_per_kg").IsRequired();
        b.HasIndex(x => x.WasteCategoryCode).IsUnique().HasDatabaseName("ux_point_rate_rules_category");
    }
}

public class TreeLevelThresholdConfiguration : IEntityTypeConfiguration<TreeLevelThreshold>
{
    public void Configure(EntityTypeBuilder<TreeLevelThreshold> b)
    {
        b.ToTable("tree_level_thresholds", t => t.HasCheckConstraint("ck_tree_threshold_nonneg", "required_points >= 0"));
        b.HasKey(x => x.Level).HasName("pk_tree_level_thresholds");
        b.Property(x => x.Level).HasColumnName("level").ValueGeneratedNever();
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(100).IsRequired();
        b.Property(x => x.RequiredPoints).HasColumnName("required_points").IsRequired();
    }
}

public class LeaderboardCycleConfiguration : IEntityTypeConfiguration<LeaderboardCycle>
{
    public void Configure(EntityTypeBuilder<LeaderboardCycle> b)
    {
        b.ToTable("leaderboard_cycles", t =>
        {
            t.HasCheckConstraint("chk_leaderboard_cycles_window", "ends_at > starts_at");
            t.HasCheckConstraint("chk_leaderboard_cycles_bonus_nonneg",
                "top1_bonus_points >= 0 AND top2_bonus_points >= 0 AND top3_bonus_points >= 0");
        });
        b.HasKey(x => x.Id).HasName("pk_leaderboard_cycles");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(x => x.PeriodType).HasColumnName("period_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.StartsAt).HasColumnName("starts_at").IsRequired();
        b.Property(x => x.EndsAt).HasColumnName("ends_at").IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(false).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(LeaderboardCycleStatus.Active).IsRequired();
        b.Property(x => x.Top1BonusPoints).HasColumnName("top1_bonus_points").HasDefaultValue(500L).IsRequired();
        b.Property(x => x.Top2BonusPoints).HasColumnName("top2_bonus_points").HasDefaultValue(300L).IsRequired();
        b.Property(x => x.Top3BonusPoints).HasColumnName("top3_bonus_points").HasDefaultValue(150L).IsRequired();
        b.Property(x => x.WinnersSnapshotJson).HasColumnName("winners_snapshot").HasColumnType("jsonb");
        b.Property(x => x.CreatedByAdminId).HasColumnName("created_by_admin_id");
        b.Property(x => x.FinalizedByAdminId).HasColumnName("finalized_by_admin_id");
        b.Property(x => x.FinalizedAt).HasColumnName("finalized_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()").IsRequired();

        b.HasIndex(x => x.IsActive)
            .IsUnique()
            .HasDatabaseName("uq_leaderboard_cycles_single_active")
            .HasFilter("is_active = TRUE");
        b.HasIndex(x => new { x.Status, x.StartsAt, x.EndsAt })
            .HasDatabaseName("idx_leaderboard_cycles_status_dates");

        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CreatedByAdminId)
            .HasConstraintName("fk_leaderboard_cycles_created_by")
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne<User>().WithMany()
            .HasForeignKey(x => x.FinalizedByAdminId)
            .HasConstraintName("fk_leaderboard_cycles_finalized_by")
            .OnDelete(DeleteBehavior.SetNull);
    }
}