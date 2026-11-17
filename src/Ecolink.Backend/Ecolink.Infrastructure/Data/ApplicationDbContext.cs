using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Data;

/// <summary>
/// DbContext trung tâm của EcoLink tích hợp PostgreSQL 16 và PostGIS 3.4.
/// Tự động nạp các Fluent API Configurations và Interceptors.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ==========================================
    // MODULE 1: AUTH & ACCOUNTS (01_auth_account_module.dbml)
    // ==========================================
    public DbSet<User> Users => Set<User>();
    public DbSet<RecyclerProfile> RecyclerProfiles => Set<RecyclerProfile>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<RecyclerPrice> RecyclerPrices => Set<RecyclerPrice>();
    public DbSet<HandoverRequest> HandoverRequests => Set<HandoverRequest>();
    public DbSet<HandoverRequestItem> HandoverRequestItems => Set<HandoverRequestItem>();
    public DbSet<HandoverReceipt> HandoverReceipts => Set<HandoverReceipt>();
    public DbSet<StaffPermissionAssignment> StaffPermissionAssignments => Set<StaffPermissionAssignment>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // ==========================================
    // CÁC ENTITIES KHÁC PHỤC VỤ LIÊN KẾT LIÊN PHÂN HỆ
    // ==========================================
    public DbSet<UserEcoProfile> UserEcoProfiles => Set<UserEcoProfile>();
    public DbSet<CollectionPartner> CollectionPartners => Set<CollectionPartner>();
    public DbSet<AcceptedMaterial> AcceptedMaterials => Set<AcceptedMaterial>();
    public DbSet<TradeOrder> TradeOrders => Set<TradeOrder>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<TransactionReceipt> TransactionReceipts => Set<TransactionReceipt>();
    public DbSet<WasteCategory> WasteCategories => Set<WasteCategory>();
    public DbSet<AIFeedbackLog> AIFeedbackLogs => Set<AIFeedbackLog>();

    // ==========================================
    // MODULE M-15: GOVERNANCE, STAFF, ATTENDANCE & AUDIT TRAIL (FE-17, FE-18)
    // ==========================================
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AccountModerationLog> AccountModerationLogs => Set<AccountModerationLog>();
    public DbSet<StaffAttendance> StaffAttendances => Set<StaffAttendance>();

    // ==========================================
    // MODULE M-12: IN-SYSTEM SERVICE PACKAGES & VIETQR PAYMENT (FE-14)
    // ==========================================
    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<PaymentWebhookLog> PaymentWebhookLogs => Set<PaymentWebhookLog>();

    // ==========================================
    // MODULE M-13: AI MISCLASSIFICATION FEEDBACK & DATASET CURATION (FE-15)
    // ==========================================
    public DbSet<AiScanHistory> AiScanHistories => Set<AiScanHistory>();
    public DbSet<AiDatasetItem> AiDatasetItems => Set<AiDatasetItem>();
    public DbSet<AiDatasetVersion> AiDatasetVersions => Set<AiDatasetVersion>();
    public DbSet<AiDatasetVersionItem> AiDatasetVersionItems => Set<AiDatasetVersionItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Kích hoạt PostGIS extension trong PostgreSQL
        modelBuilder.HasPostgresExtension("postgis");

        // Quét tự động tất cả các cấu hình Fluent API trong tầng Infrastructure
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.HasSequence<long>("seq_handover_request_code").StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>("seq_handover_receipt_code").StartsAt(1).IncrementsBy(1);
    }
}
