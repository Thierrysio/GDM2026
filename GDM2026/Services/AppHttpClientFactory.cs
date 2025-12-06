using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace GDM2026.Services;

/// <summary>
/// Centralise la création d'instances <see cref="HttpClient"/> pour éviter
/// les problèmes spécifiques à Android liés au cast <c>HttpURLConnection</c>.
/// L'utilisation de <see cref="SocketsHttpHandler"/> garantit un pipeline .NET
/// natif plutôt que le pont Java susceptible de lever l'exception
/// « Unable to convert instance of type 'Java.Net.URLConnectionInvoker' to type 'Java.Net.HttpURLConnection' ».
/// </summary>
public static class AppHttpClientFactory
{
    // Partage un même conteneur de cookies entre toutes les instances HttpClient
    // afin de conserver la session (PHPSESSID/REMEMBERME) entre l'authentification
    // et les appels protégés comme /api/mobile/upload.
    private static readonly CookieContainer SharedCookies = new();

    public static Uri? GetValidatedBaseAddress()
    {
        if (string.IsNullOrWhiteSpace(Constantes.BaseApiAddress))
        {
            return null;
        }

        if (!Uri.TryCreate(Constantes.BaseApiAddress, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException($"BaseApiAddress doit être une URL absolue : '{Constantes.BaseApiAddress}'.");
        }

        if (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"BaseApiAddress doit être en http(s), pas en '{baseUri.Scheme}'.");
        }

        return baseUri;
    }

    public static HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            CookieContainer = SharedCookies,
            UseCookies = true
        };

        var client = new HttpClient(handler);
        if (Uri.TryCreate(Constantes.BaseApiAddress, UriKind.Absolute, out var baseUri))
        {
            System.Diagnostics.Debug.WriteLine($"[HTTP] BaseApiAddress = {baseUri}");
            client.BaseAddress = baseUri;
        }

        return client;
    }
}
