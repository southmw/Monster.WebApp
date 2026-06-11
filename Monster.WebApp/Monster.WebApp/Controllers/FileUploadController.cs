using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monster.WebApp.Services;

namespace Monster.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // 인증된 사용자만 업로드 허용 (미인증 익명 업로드 차단)
public class FileUploadController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadController> _logger;

    // 에디터 이미지 업로드 전용 (동영상은 YouTube/Vimeo 링크 임베드 방식이라 파일 업로드 미지원)
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxImageSize = 10 * 1024 * 1024;  // 10MB

    public FileUploadController(IWebHostEnvironment environment, ILogger<FileUploadController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(10_485_760)] // 10MB (이미지 전용)
    public async Task<IActionResult> Upload(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new UploadResponse { Success = false, ErrorMessage = "파일이 없습니다." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension))
            {
                return BadRequest(new UploadResponse
                {
                    Success = false,
                    ErrorMessage = "지원하지 않는 파일 형식입니다. (이미지: jpg, png, gif, webp)"
                });
            }

            if (file.Length > MaxImageSize)
            {
                return BadRequest(new UploadResponse
                {
                    Success = false,
                    ErrorMessage = $"파일 크기가 너무 큽니다. (최대 {MaxImageSize / (1024 * 1024)}MB)"
                });
            }

            // 파일 시그니처(매직넘버) 검증 — 확장자 위조를 통한 악성 파일 업로드 차단
            // (검증 로직은 FileUploadService와 공유)
            bool validSignature;
            await using (var signatureStream = file.OpenReadStream())
            {
                validSignature = await FileUploadService.IsValidFileSignatureAsync(signatureStream, extension);
            }

            if (!validSignature)
            {
                _logger.LogWarning("파일 시그니처 불일치로 업로드 거부: {FileName}", file.FileName);
                return BadRequest(new UploadResponse
                {
                    Success = false,
                    ErrorMessage = "파일 내용이 확장자와 일치하지 않습니다."
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
                FileName = file.FileName
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
        public string? ErrorMessage { get; set; }
    }
}
