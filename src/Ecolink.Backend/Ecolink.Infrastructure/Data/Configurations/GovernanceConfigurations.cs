using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecolink.Infrastructure.Data.Configurations;

/// <summary>
/// Cấu hình bảng audit_logs (Append-Only Immutable Audit Trail - UC A-02, BR-ADM-05).
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id).HasName("pk_audit_logs");

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(a => a.ActorEmail).HasColumnName("actor_email").HasMaxLength(255);
        builder.Property(a => a.ActorRole).HasColumnName("actor_role").HasMaxLength(30);
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(100);
        builder.Property(a => a.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
        builder.Property(a => a.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
        builder.Property(a => a.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(a => a.CreatedAt).HasDatabaseName("idx_audit_logs_created_at");
        builder.HasIndex(a => new { a.ActorUserId, a.Action, a.CreatedAt }).HasDatabaseName("idx_audit_logs_actor_action");
        builder.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("idx_audit_logs_entity");

        builder.HasOne(a => a.ActorUser)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.ActorUserId)
            .HasConstraintName("fk_audit_logs_users")
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}

/// <summary>
/// Cấu hình bảng account_moderation_logs (UC S-06, S-07, A-03 - BR-ADM-03, BR-ADM-04).
/// </summary>
public class AccountModerationLogConfiguration : IEntityTypeConfiguration<AccountModerationLog>
{
    public void Configure(EntityTypeBuilder<AccountModerationLog> builder)
    {
        builder.ToTable("account_moderation_logs");
        builder.HasKey(m => m.Id).HasName("pk_account_moderation_logs");

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TargetUserId).HasColumnName("target_user_id").IsRequired();
        builder.Property(m => m.PerformedBy).HasColumnName("performed_by").IsRequired();
        builder.Property(m => m.ActionType).HasColumnName("action_type").HasMaxLength(30).IsRequired();
        builder.Property(m => m.PreviousStatus).HasColumnName("previous_status").HasMaxLength(30).IsRequired();
        builder.Property(m => m.NewStatus).HasColumnName("new_status").HasMaxLength(30).IsRequired();
        builder.Property(m => m.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(m => m.RelatedComplaintId).HasColumnName("related_complaint_id");
        builder.Property(m => m.RevokedTokensCount).HasColumnName("revoked_tokens_count").HasDefaultValue(0).IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(m => new { m.TargetUserId, m.CreatedAt })
            .HasDatabaseName("idx_account_moderation_logs_target_created_at");

        builder.HasOne(m => m.TargetUser)
            .WithMany(u => u.ModerationLogsReceived)
            .HasForeignKey(m => m.TargetUserId)
            .HasConstraintName("fk_account_moderation_logs_target_users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.PerformedByUser)
            .WithMany(u => u.ModerationLogsPerformed)
            .HasForeignKey(m => m.PerformedBy)
            .HasConstraintName("fk_account_moderation_logs_actor_users")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Cấu hình bảng staff_attendances (UC S-11 - BR-GAM-09).
/// </summary>
public class StaffAttendanceConfiguration : IEntityTypeConfiguration<StaffAttendance>
{
    public void Configure(EntityTypeBuilder<StaffAttendance> builder)
    {
        builder.ToTable("staff_attendances");
        builder.HasKey(a => a.Id).HasName("pk_staff_attendances");

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.StaffId).HasColumnName("staff_id").IsRequired();
        builder.Property(a => a.WorkDate).HasColumnName("work_date").HasColumnType("date").IsRequired();
        builder.Property(a => a.CheckInAt).HasColumnName("check_in_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(a => a.CheckOutAt).HasColumnName("check_out_at");
        builder.Property(a => a.WorkedMinutes).HasColumnName("worked_minutes");
        builder.Property(a => a.StreakDay).HasColumnName("streak_day").HasDefaultValue(1).IsRequired();
        builder.Property(a => a.WorkPointsEarned).HasColumnName("work_points_earned").HasDefaultValue(10).IsRequired();
        builder.Property(a => a.TasksCompletedSnapshot).HasColumnName("tasks_completed_snapshot").HasDefaultValue(0).IsRequired();
        builder.Property(a => a.ShiftNote).HasColumnName("shift_note").HasMaxLength(500);

        // Audit fields
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(a => new { a.StaffId, a.WorkDate })
            .IsUnique()
            .HasDatabaseName("uq_staff_attendances_staff_work_date");

        builder.HasIndex(a => a.WorkDate)
            .HasDatabaseName("idx_staff_attendances_work_date");

        builder.HasOne(a => a.StaffProfile)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.StaffId)
            .HasConstraintName("fk_staff_attendances_staff_profiles")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
