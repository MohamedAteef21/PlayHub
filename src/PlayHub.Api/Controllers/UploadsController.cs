using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PlayHub.Api.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize]
public class UploadsController : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private readonly IWebHostEnvironment _env;

    public UploadsController(IWebHostEnvironment env) => _env = env;

    /// <summary>Upload an optional payment receipt image (bank transfer / wallet).</summary>
    [HttpPost("payment-proof")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadPaymentProof(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { message = "File must be 5 MB or smaller." });

        var contentType = file.ContentType?.Trim() ?? string.Empty;
        if (!AllowedContentTypes.Contains(contentType))
            return BadRequest(new { message = "Only image files are allowed (jpg, png, webp, gif)." });

        var ext = contentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(webRoot);
        }

        var folder = Path.Combine(webRoot, "uploads", "payment-proofs");
        Directory.CreateDirectory(folder);

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        var url = $"/uploads/payment-proofs/{fileName}";
        return Ok(new { url, fileName, contentType });
    }
}
