using System.Net.Http.Headers;
using System.Text.Json;

namespace GDM2026.Services;

public record ImageUploadResult(string FileName, string RelativeUrl);

public class ImageUploadService
{
    private readonly HttpClient _httpClient;

    public ImageUploadService(HttpClient httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (_httpClient.BaseAddress == null && Uri.TryCreate(Constantes.BaseApiAddress, UriKind.Absolute, out var baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }
    }

    public async Task<ImageUploadResult> UploadAsync(string filePath, string folder = "images", CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Le fichier à envoyer est introuvable.", filePath);
        }

        var fileName = Path.GetFileName(filePath);
        var relativeFolder = string.IsNullOrWhiteSpace(folder) ? "images" : folder.Trim('/');

        using var formData = new MultipartFormDataContent();
        await using var fileStream = File.OpenRead(filePath);
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        formData.Add(streamContent, "file", fileName);
        formData.Add(new StringContent(relativeFolder), "folder");

        using var response = await _httpClient.PostAsync("/api/upload", formData, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var uploadedFileName = TryExtractField(body, "fileName") ?? TryExtractField(body, "filename") ?? fileName;
        var relativeUrl = TryExtractField(body, "path")
            ?? TryExtractField(body, "url")
            ?? $"{relativeFolder}/{uploadedFileName}".Replace("\\", "/");

        return new ImageUploadResult(uploadedFileName, relativeUrl);
    }

    private static string TryExtractField(string json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(propertyName, out var value))
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore parsing errors; the caller will fallback to defaults.
        }

        return null;
    }
}
