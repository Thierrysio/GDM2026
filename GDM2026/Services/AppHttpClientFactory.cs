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
    public static HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };

        var client = new HttpClient(handler);
        if (Uri.TryCreate(Constantes.BaseApiAddress, UriKind.Absolute, out var baseUri))
        {
            client.BaseAddress = baseUri;
        }

        return client;
    }
}
