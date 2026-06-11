using System.Text.RegularExpressions;

namespace Monster.WebApp.Services;

/// <summary>
/// 에디터 이미지 업로드 보조 서비스.
/// 업로드 수신은 FileUploadController(/api/fileupload)가 담당하고,
/// 여기서는 게시글/댓글 저장 시 temp → posts 폴더 이동과 파일 시그니처 검증 로직을 제공한다.
/// </summary>
public class FileUploadService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadService> _logger;

    private const int SignatureHeaderLength = 12;

    // 본문에 삽입된 임시 업로드 미디어(img 등) URL 추출용
    private static readonly Regex TempMediaRegex = new(
        "src=\"(/uploads/temp/[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public FileUploadService(IWebHostEnvironment environment, ILogger<FileUploadService> logger)
    {
        _environment = environment;
        _logger = logger;
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
    /// 본문(HTML) 내 /uploads/temp/ 미디어 파일을 게시글 폴더(/uploads/posts/{postId}/)로 이동하고
    /// 본문의 URL을 치환해 반환한다. 댓글 미디어도 부모 게시글 폴더를 사용한다
    /// (게시글 삭제 시 댓글이 Cascade 삭제되므로 수명 일치).
    /// 이동에 실패한 URL은 원본을 유지한다 (실패는 MoveToPostFolderAsync가 로그).
    /// </summary>
    public async Task<string> MoveContentTempMediaAsync(string content, int postId)
    {
        var tempUrls = TempMediaRegex.Matches(content)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        foreach (var tempUrl in tempUrls)
        {
            var newUrl = await MoveToPostFolderAsync(tempUrl, postId);
            if (newUrl != null)
            {
                content = content.Replace(tempUrl, newUrl);
            }
        }

        return content;
    }

    /// <summary>
    /// 스트림의 첫 바이트(매직넘버)가 확장자와 일치하는지 검증한다.
    /// </summary>
    public static async Task<bool> IsValidFileSignatureAsync(Stream stream, string extension)
    {
        var header = new byte[SignatureHeaderLength];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        return IsValidFileSignature(header, read, extension);
    }

    /// <summary>
    /// 파일 헤더 바이트가 확장자와 일치하는지 검증한다 — 확장자 위조를 통한 악성 파일 업로드 차단.
    /// </summary>
    public static bool IsValidFileSignature(byte[] header, int bytesRead, string extension)
    {
        if (bytesRead < 4)
            return false;

        bool StartsWith(params byte[] sig) => HasPrefix(header, sig);

        return extension switch
        {
            ".jpg" or ".jpeg" => StartsWith(0xFF, 0xD8, 0xFF),
            ".png" => StartsWith(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            ".gif" => StartsWith(0x47, 0x49, 0x46, 0x38), // GIF8
            ".webp" => bytesRead >= 12
                       && HasPrefix(header, new byte[] { 0x52, 0x49, 0x46, 0x46 })       // RIFF
                       && header[8] == 0x57 && header[9] == 0x45                          // WE
                       && header[10] == 0x42 && header[11] == 0x50,                       // BP
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
}
