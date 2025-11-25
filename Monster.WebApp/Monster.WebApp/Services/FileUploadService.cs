using Microsoft.AspNetCore.Components.Forms;

namespace Monster.WebApp.Services;

public class FileUploadService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadService> _logger;

    // 허용된 파일 확장자
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly string[] AllowedVideoExtensions = { ".mp4", ".webm" };

    // 최대 파일 크기 (바이트)
    private const long MaxImageSize = 10 * 1024 * 1024;  // 10MB
    private const long MaxVideoSize = 50 * 1024 * 1024;  // 50MB

    public FileUploadService(IWebHostEnvironment environment, ILogger<FileUploadService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// 파일을 업로드하고 URL을 반환합니다.
    /// </summary>
    public async Task<FileUploadResult> UploadFileAsync(IBrowserFile file, int? postId = null)
    {
        try
        {
            var extension = Path.GetExtension(file.Name).ToLowerInvariant();
            var fileType = GetFileType(extension);

            if (fileType == FileType.Unknown)
            {
                return new FileUploadResult
                {
                    Success = false,
                    ErrorMessage = "지원하지 않는 파일 형식입니다. (이미지: jpg, png, gif, webp / 동영상: mp4, webm)"
                };
            }

            var maxSize = fileType == FileType.Image ? MaxImageSize : MaxVideoSize;
            if (file.Size > maxSize)
            {
                var maxSizeMB = maxSize / (1024 * 1024);
                return new FileUploadResult
                {
                    Success = false,
                    ErrorMessage = $"파일 크기가 너무 큽니다. (최대 {maxSizeMB}MB)"
                };
            }

            // 저장 경로 생성
            var uploadsFolder = postId.HasValue
                ? Path.Combine(_environment.WebRootPath, "uploads", "posts", postId.Value.ToString())
                : Path.Combine(_environment.WebRootPath, "uploads", "temp");

            Directory.CreateDirectory(uploadsFolder);

            // 고유한 파일명 생성
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // 파일 저장
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.OpenReadStream(maxSize).CopyToAsync(stream);

            // URL 생성
            var relativeUrl = postId.HasValue
                ? $"/uploads/posts/{postId.Value}/{uniqueFileName}"
                : $"/uploads/temp/{uniqueFileName}";

            _logger.LogInformation("파일 업로드 완료: {FileName} -> {Url}", file.Name, relativeUrl);

            return new FileUploadResult
            {
                Success = true,
                Url = relativeUrl,
                FileName = file.Name,
                StoredFileName = uniqueFileName,
                FileType = fileType,
                FileSize = file.Size
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "파일 업로드 실패: {FileName}", file.Name);
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = "파일 업로드 중 오류가 발생했습니다."
            };
        }
    }

    /// <summary>
    /// 임시 파일을 게시글 폴더로 이동합니다.
    /// </summary>
    public async Task<string?> MoveToPostFolderAsync(string tempUrl, int postId)
    {
        try
        {
            var fileName = Path.GetFileName(tempUrl);
            var tempPath = Path.Combine(_environment.WebRootPath, "uploads", "temp", fileName);

            if (!File.Exists(tempPath))
            {
                _logger.LogWarning("임시 파일을 찾을 수 없음: {Path}", tempPath);
                return null;
            }

            var postFolder = Path.Combine(_environment.WebRootPath, "uploads", "posts", postId.ToString());
            Directory.CreateDirectory(postFolder);

            var newPath = Path.Combine(postFolder, fileName);
            File.Move(tempPath, newPath, true);

            return $"/uploads/posts/{postId}/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "파일 이동 실패: {TempUrl} -> PostId {PostId}", tempUrl, postId);
            return null;
        }
    }

    /// <summary>
    /// 파일을 삭제합니다.
    /// </summary>
    public bool DeleteFile(string fileUrl)
    {
        try
        {
            var filePath = Path.Combine(_environment.WebRootPath, fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("파일 삭제 완료: {Path}", filePath);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "파일 삭제 실패: {Url}", fileUrl);
            return false;
        }
    }

    private static FileType GetFileType(string extension)
    {
        if (AllowedImageExtensions.Contains(extension))
            return FileType.Image;
        if (AllowedVideoExtensions.Contains(extension))
            return FileType.Video;
        return FileType.Unknown;
    }
}

public class FileUploadResult
{
    public bool Success { get; set; }
    public string? Url { get; set; }
    public string? FileName { get; set; }
    public string? StoredFileName { get; set; }
    public FileType FileType { get; set; }
    public long FileSize { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum FileType
{
    Unknown,
    Image,
    Video
}
