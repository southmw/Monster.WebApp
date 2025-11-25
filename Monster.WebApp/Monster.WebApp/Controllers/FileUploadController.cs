using Microsoft.AspNetCore.Mvc;

namespace Monster.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileUploadController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadController> _logger;

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly string[] AllowedVideoExtensions = { ".mp4", ".webm" };
    private const long MaxImageSize = 10 * 1024 * 1024;  // 10MB
    private const long MaxVideoSize = 50 * 1024 * 1024;  // 50MB

    public FileUploadController(IWebHostEnvironment environment, ILogger<FileUploadController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(52428800)] // 50MB
    public async Task<IActionResult> Upload(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new UploadResponse { Success = false, ErrorMessage = "파일이 없습니다." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var isImage = AllowedImageExtensions.Contains(extension);
            var isVideo = AllowedVideoExtensions.Contains(extension);

            if (!isImage && !isVideo)
            {
                return BadRequest(new UploadResponse
                {
                    Success = false,
                    ErrorMessage = "지원하지 않는 파일 형식입니다. (이미지: jpg, png, gif, webp / 동영상: mp4, webm)"
                });
            }

            var maxSize = isImage ? MaxImageSize : MaxVideoSize;
            if (file.Length > maxSize)
            {
                var maxSizeMB = maxSize / (1024 * 1024);
                return BadRequest(new UploadResponse
                {
                    Success = false,
                    ErrorMessage = $"파일 크기가 너무 큽니다. (최대 {maxSizeMB}MB)"
                });
            }

            // 임시 폴더에 저장
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "temp");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var relativeUrl = $"/uploads/temp/{uniqueFileName}";

            _logger.LogInformation("파일 업로드 완료: {FileName} -> {Url}", file.FileName, relativeUrl);

            return Ok(new UploadResponse
            {
                Success = true,
                Url = relativeUrl,
                FileName = file.FileName,
                IsImage = isImage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "파일 업로드 실패");
            return StatusCode(500, new UploadResponse
            {
                Success = false,
                ErrorMessage = "파일 업로드 중 오류가 발생했습니다."
            });
        }
    }

    public class UploadResponse
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public string? FileName { get; set; }
        public bool IsImage { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
