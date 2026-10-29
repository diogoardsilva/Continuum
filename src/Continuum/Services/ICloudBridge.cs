using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Continuum.Services;

public sealed record BridgeStatus(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("delivery")] string? Delivery)
{
    public bool IsOk => Status == "ok";
    public bool NeedsCode => Status == "code_required";
}

public sealed record ICloudPhoto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("filename")] string? FileName,
    [property: JsonPropertyName("created")] DateTimeOffset? Created,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("versions")] List<string> Versions);

/// <summary>
/// Talks to Continuum.iCloud.exe (bridge/continuum_icloud.py), which signs in
/// to iCloud.com and serves contacts and photos on a local, token-protected port.
/// </summary>
public sealed class ICloudBridge : IAsyncDisposable
{
    const string ExeName = "Continuum.iCloud.exe";

    readonly Process _process;
    readonly HttpClient _http;

    ICloudBridge(Process process, int port, string token)
    {
        _process = process;
        _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromMinutes(3) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static bool IsAvailable => File.Exists(Path.Combine(AppContext.BaseDirectory, ExeName));

    public static async Task<ICloudBridge> StartAsync(CancellationToken ct = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, ExeName), "serve --port 0")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.Environment["CONTINUUM_BRIDGE_TOKEN"] = token;
        var process = Process.Start(start) ?? throw new InvalidOperationException("Não foi possível arrancar o " + ExeName);

        // The bridge prints {"ready": true, "port": N} once it listens.
        var line = await process.StandardOutput.ReadLineAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(60), ct);
        using var ready = JsonDocument.Parse(line ?? throw new InvalidOperationException("O " + ExeName + " terminou ao arrancar."));
        return new ICloudBridge(process, ready.RootElement.GetProperty("port").GetInt32(), token);
    }

    public Task<BridgeStatus> ResumeAsync(string appleId) => PostAsync("resume", new { apple_id = appleId });

    public Task<BridgeStatus> LoginAsync(string appleId, string password) =>
        PostAsync("login", new { apple_id = appleId, password });

    public Task<BridgeStatus> SubmitCodeAsync(string code) => PostAsync("code", new { code });

    public async Task<List<ICloudPhoto>> GetPhotosAsync(int offset, int limit, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<ICloudPhoto>>($"photos?offset={offset}&limit={limit}", ct) ?? new();

    public async Task<byte[]?> GetPhotoAsync(string id, string version, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"photos/{Uri.EscapeDataString(id)}?version={version}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(ct) : null;
    }

    async Task<BridgeStatus> PostAsync(string path, object body)
    {
        using var response = await _http.PostAsJsonAsync(path, body);
        var text = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            return JsonSerializer.Deserialize<BridgeStatus>(text) ?? new BridgeStatus("error", "Resposta vazia.", null, null);
        }
        using var error = JsonDocument.Parse(text);
        var message = error.RootElement.TryGetProperty("error", out var e) ? e.GetString() : response.ReasonPhrase;
        return new BridgeStatus("error", message, null, null);
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        try { if (!_process.HasExited) _process.Kill(); } catch (InvalidOperationException) { }
        _process.Dispose();
        return ValueTask.CompletedTask;
    }
}
