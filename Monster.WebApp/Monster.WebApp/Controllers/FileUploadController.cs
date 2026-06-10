using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Monster.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // 인증된 사용자만 업로드 허용 (미인증 익명 업로드 차단)
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

            // 파일 시그니처(매직넘버) 검증 — 확장자 위조를 통한 악성 파일 업로드 차단
            if (!await IsValidFileSignatureAsync(file, extension))
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

    /// <summary>
    /// 파일의 첫 바이트(매직넘버)가 확장자와 일치하는지 검증한다.
    /// </summary>
    private static async Task<bool> IsValidFileSignatureAsync(IFormFile file, string extension)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (read < 4)
            return false;

        bool StartsWith(params byte[] sig) => HasPrefix(header, sig);

        return extension switch
        {
            ".jpg" or ".jpeg" => StartsWith(0xFF, 0xD8, 0xFF),
            ".png" => StartsWith(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            ".gif" => StartsWith(0x47, 0x49, 0x46, 0x38), // GIF8
            ".webp" => read >= 12
                       && HasPrefix(header, new byte[] { 0x52, 0x49, 0x46, 0x46 })       // RIFF
                       && header[8] == 0x57 && header[9] == 0x45                          // WE
                       && header[10] == 0x42 && header[11] == 0x50,                       // BP
            ".mp4" => read >= 8 && header[4] == 0x66 && header[5] == 0x74
                      && header[6] == 0x79 && header[7] == 0x70,                          // ftyp
            ".webm" => StartsWith(0x1A, 0x45, 0xDF, 0xA3),
            _ => false
        };
    }

    private static bool HasPrefix(byte[] data, byte[] prefix)
    {
        if (data.Length < prefix.Length)
            return false;
        for (var i = 0; i < prefix.Length; i++)
        {
            if (data[i] != prefix[i])
                return false;
        }
        return true;
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
