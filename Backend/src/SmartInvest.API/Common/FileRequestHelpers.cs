using SmartInvest.Application.DTOs;

namespace SmartInvest.API.Common;

/// <summary>تحويل ملفات الطلب (IFormFile) إلى DTOs طبقة الـ Application + أنواع MIME للتحميل.</summary>
public static class FileRequestHelpers
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    public static async Task<FileUploadDto> ToUploadDtoAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        return new FileUploadDto
        {
            FileName = Path.GetFileName(file.FileName),
            FileExtension = Path.GetExtension(file.FileName),
            FileSize = file.Length,
            Content = stream.ToArray(),
        };
    }

    public static string GetContentType(string fileExtension) =>
        ContentTypes.TryGetValue(fileExtension ?? string.Empty, out var contentType)
            ? contentType
            : "application/octet-stream";
}
