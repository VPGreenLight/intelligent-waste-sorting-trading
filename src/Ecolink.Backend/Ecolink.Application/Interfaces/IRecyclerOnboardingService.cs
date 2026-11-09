using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

public interface IRecyclerOnboardingService
{
    /// <summary>
    /// U-17: Công dân gửi hồ sơ xin nâng cấp lên Recycler (hoặc nộp lại sau khi bị từ chối)
    /// </summary>
    Task<RecyclerApplicationDetailDto> SubmitUpgradeApplicationAsync(
        Guid userId,
        ApplyRecyclerUpgradeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// U-17: Công dân xem trạng thái hồ sơ đăng ký Recycler của chính mình
    /// </summary>
    Task<RecyclerApplicationDetailDto> GetMyApplicationAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// S-04: Staff lấy danh sách hồ sơ đăng ký Recycler có bộ lọc và phân trang
    /// </summary>
    Task<PagedResult<RecyclerApplicationDetailDto>> GetApplicationsForStaffAsync(
        RecyclerApplicationFilterRequest filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// S-04: Staff xem chi tiết một hồ sơ theo UserId
    /// </summary>
    Task<RecyclerApplicationDetailDto> GetApplicationByUserIdAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// S-04: Staff phê duyệt (Approve) hoặc từ chối (Reject) hồ sơ đăng ký Recycler
    /// </summary>
    Task<RecyclerApplicationDetailDto> VerifyApplicationAsync(
        Guid staffUserId,
        Guid targetUserId,
        VerifyRecyclerApplicationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IFileStorageService
{
    /// <summary>
    /// Tải chứng từ pháp lý hoặc ảnh chụp cơ sở lên Cloudflare R2 Object Storage
    /// </summary>
    Task<RecyclerDocumentUploadResponseDto> UploadRecyclerDocumentAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        string folderPrefix = "recycler-kyb",
        CancellationToken cancellationToken = default);
}

