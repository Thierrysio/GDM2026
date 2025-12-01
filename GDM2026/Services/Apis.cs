
using GDM2026;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GDM2026.Services
{
    public interface IApis : IDisposable
    {
        Task<List<T>> GetListAsync<T>(string relativeUrl, CancellationToken ct = default);
        Task<TOut> GetAsync<TOut>(string relativeUrl, CancellationToken ct = default);
        Task<TResponse> PostAsync<TRequest, TResponse>(string relativeUrl, TRequest body, CancellationToken ct = default);
        Task<bool> PostBoolAsync<TRequest>(string relativeUrl, TRequest body, CancellationToken ct = default);

        void SetBearerToken(string token);
    }

    public class Apis : IApis
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerSettings _json;
        private readonly Uri _configuredBaseUri;

        public Apis(HttpClient httpClient = null)
        {
            _http = httpClient ?? new HttpClient();

            if (!string.IsNullOrWhiteSpace(Constantes.BaseApiAddress)
                && Uri.TryCreate(Constantes.BaseApiAddress.Trim(), UriKind.Absolute, out var parsedBase))
            {
                _configuredBaseUri = EnsureTrailingSlash(parsedBase);
                if (_http.BaseAddress == null)
                    _http.BaseAddress = _configuredBaseUri;
            }

            if (_http.Timeout == Timeout.InfiniteTimeSpan)
                _http.Timeout = TimeSpan.FromSeconds(30);

            if (!_http.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
                _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd("GDM2026/1.0 (+maui)"))
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("GDM2026/1.0");

            _json = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTime,
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Converters = { new IsoDateTimeConverter { DateTimeFormat = "yyyy-MM-dd HH:mm:ss" } }
            };
        }

        public void Dispose() => _http?.Dispose();

        public void SetBearerToken(string token)
        {
            _http.DefaultRequestHeaders.Authorization =
                string.IsNullOrWhiteSpace(token) ? null : new AuthenticationHeaderValue("Bearer", token);
        }

        // ---------- GET : renvoie List<T> ----------
        public async Task<List<T>> GetListAsync<T>(string relativeUrl, CancellationToken ct = default)
        {
            using var reqCts = LinkedCts(ct, TimeSpan.FromSeconds(30));
            var requestUri = BuildUri(relativeUrl);
            using var resp = await _http.GetAsync(requestUri, reqCts.Token).ConfigureAwait(false);
            await EnsureSuccess(resp, requestUri.ToString()).ConfigureAwait(false);

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<T>>(json, _json) ?? new List<T>();
        }

        // ---------- GET : renvoie un TOut ----------
        public async Task<TOut> GetAsync<TOut>(string relativeUrl, CancellationToken ct = default)
        {
            using var reqCts = LinkedCts(ct, TimeSpan.FromSeconds(30));
            var requestUri = BuildUri(relativeUrl);
            using var resp = await _http.GetAsync(requestUri, reqCts.Token).ConfigureAwait(false);
            await EnsureSuccess(resp, requestUri.ToString()).ConfigureAwait(false);

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<TOut>(json, _json);
        }

        // ---------- POST : renvoie un type de réponse ----------
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string relativeUrl, TRequest body, CancellationToken ct = default)
        {
            var payload = JsonConvert.SerializeObject(body, _json);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var reqCts = LinkedCts(ct, TimeSpan.FromSeconds(30));

            var requestUri = BuildUri(relativeUrl);
            using var resp = await _http.PostAsync(requestUri, content, reqCts.Token).ConfigureAwait(false);
            await EnsureSuccess(resp, requestUri.ToString(), payload).ConfigureAwait(false);

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<TResponse>(json, _json);
        }

        // ---------- POST : bool succès/échec ----------
        public async Task<bool> PostBoolAsync<TRequest>(string relativeUrl, TRequest body, CancellationToken ct = default)
        {
            var payload = JsonConvert.SerializeObject(body, _json);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var reqCts = LinkedCts(ct, TimeSpan.FromSeconds(30));

            var requestUri = BuildUri(relativeUrl);
            using var resp = await _http.PostAsync(requestUri, content, reqCts.Token).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode) return true;

            await EnsureSuccess(resp, requestUri.ToString(), payload).ConfigureAwait(false);
            return false; // n’est jamais atteint si EnsureSuccess lève
        }

        // ========== Helpers ==========
        private Uri BuildUri(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("relativeUrl cannot be null or empty", nameof(path));

            path = path.Trim();
            var parts = path.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Allow callers to supply a base URL and relative path in a single argument, e.g.
            // "https://dantecmarket.com" and "/api/mobile/GetFindUser" => "https://dantecmarket.com/api/mobile/GetFindUser".
            if (parts.Length >= 2 && Uri.TryCreate(parts[0], UriKind.Absolute, out var suppliedBase))
            {
                var relativePath = string.Join("/", parts.Skip(1).Select(p => p.Trim('/')));
                return new Uri(EnsureTrailingSlash(suppliedBase), relativePath);
            }

            if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            {
                return absolute;
            }

            if (_http.BaseAddress != null)
            {
                return new Uri(_http.BaseAddress, path);
            }

            if (_configuredBaseUri != null)
            {
                return new Uri(_configuredBaseUri, path);
            }

            throw new InvalidOperationException("BaseAddress must be configured to call relative URLs.");
        }

        private static Uri EnsureTrailingSlash(Uri uri)
            => uri?.ToString().EndsWith("/") == true ? uri : new Uri(uri + "/");

        private static async Task EnsureSuccess(HttpResponseMessage response, string path, string payload = null)
        {
            if (response.IsSuccessStatusCode) return;

            string body = null;
            try { body = await response.Content.ReadAsStringAsync().ConfigureAwait(false); } catch { }

            var msg = $"API error {(int)response.StatusCode} {response.ReasonPhrase} on '{path}'. " +
                      (payload == null ? "" : $"Payload: {Trim(payload)} ") +
                      (string.IsNullOrWhiteSpace(body) ? "" : $"Body: {Trim(body)}");

            throw new HttpRequestException(msg);
        }

        private static string Trim(string s, int max = 600)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";

        private static CancellationTokenSource LinkedCts(CancellationToken external, TimeSpan perRequestTimeout)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(external);
            cts.CancelAfter(perRequestTimeout);
            return cts;
        }
    }
}
