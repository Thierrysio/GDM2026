using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace GDM2026.Services;

public record ImageUploadResult(string FileName, string RelativeUrl);

public class ImageUploadService
{
    private readonly HttpClient _httpClient;
    private readonly Uri _uploadUri;

    public ImageUploadService(HttpClient httpClient = null)
    {
        _httpClient = httpClient ?? AppHttpClientFactory.Create();

        var baseUri = _httpClient.BaseAddress ?? AppHttpClientFactory.GetValidatedBaseAddress();
        if (baseUri == null)
        {
            throw new InvalidOperationException("Aucune BaseAddress n'est configurée pour l'API.");
        }

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = baseUri;
        }

        _uploadUri = new Uri(baseUri, "/api/mobile/upload");
    }

    public void SetBearerToken(string token)
    {
        SetAuthorization(string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token));
    }

    public void SetBasicAuthentication(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || password is null)
        {
            SetAuthorization(null);
            return;
        }

        var rawCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        SetAuthorization(new AuthenticationHeaderValue("Basic", rawCredentials));
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
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
        await using var fileStream = File.OpenRead(filePath);
        return await UploadAsync(fileStream, fileName, folder, ct).ConfigureAwait(false);
    }

    public async Task<ImageUploadResult> UploadAsync(Stream fileStream, string fileName, string folder = "images", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var relativeFolder = string.IsNullOrWhiteSpace(folder) ? "images" : folder.Trim('/');

        using var formData = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(fileName));
        formData.Add(streamContent, "file", fileName);
        formData.Add(new StringContent(relativeFolder), "folder");

        using var response = await _httpClient.PostAsync(_uploadUri, formData, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var reason = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase
                : $"{response.ReasonPhrase}: {body}";

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var authHeader = _httpClient.DefaultRequestHeaders.Authorization;
                var authInfo = authHeader is null
                    ? "aucun en-tête Authorization"
                    : $"Authorization {authHeader.Scheme} présent";

                reason = string.IsNullOrWhiteSpace(reason)
                    ? authInfo
                    : $"{reason} ({authInfo}; base={_httpClient.BaseAddress})";
            }

            throw new HttpRequestException(
                $"Le serveur a répondu {(int)response.StatusCode} {reason}",
                null,
                response.StatusCode);
        }

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

    private void SetAuthorization(AuthenticationHeaderValue? header)
    {
        _httpClient.DefaultRequestHeaders.Authorization = header;
        AppHttpClientFactory.SetAuthorization(header);
    }

    private static string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
