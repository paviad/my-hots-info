using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyReplayLibrary.Obs;

/// <summary>Where OBS's WebSocket server listens, and its password when it requires one.</summary>
public record ObsConnection(Uri Uri, string? Password) {
    /// <summary>
    /// Reads the connection from OBS's own obs-websocket settings, so the password never has to be
    /// copied anywhere. Null when the settings are missing or the server isn't enabled.
    /// </summary>
    public static ObsConnection? FromObsConfig() {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "obs-studio", "plugin_config", "obs-websocket", "config.json");
        if (!File.Exists(path)) {
            return null;
        }

        var config = JsonNode.Parse(File.ReadAllText(path));
        if (config?["server_enabled"]?.GetValue<bool>() != true) {
            return null;
        }

        var port = config["server_port"]?.GetValue<int>() ?? 4455;
        var password = config["auth_required"]?.GetValue<bool>() == true
            ? config["server_password"]?.GetValue<string>()
            : null;

        return new(new($"ws://127.0.0.1:{port}"), password);
    }
}

public class ObsRequestException(string requestType, int code, string? comment)
    : Exception($"OBS request {requestType} failed with code {code}: {comment}") {
    public int Code { get; } = code;
}

/// <summary>
/// Just enough of the obs-websocket v5 protocol to send requests and receive events.
/// </summary>
public sealed class ObsClient : IAsyncDisposable {
    /// <summary>The <c>Outputs</c> event subscription, which carries <c>RecordStateChanged</c>.</summary>
    public const int OutputEvents = 1 << 6;

    private const int OpHello = 0;
    private const int OpIdentify = 1;
    private const int OpIdentified = 2;
    private const int OpEvent = 5;
    private const int OpRequest = 6;
    private const int OpRequestResponse = 7;

    private readonly ClientWebSocket _ws = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Task _receiveLoop = Task.CompletedTask;

    private ObsClient() {
    }

    /// <summary>Raised from the receive loop with the event type and its <c>eventData</c>.</summary>
    public event Action<string, JsonElement>? EventReceived;

    public static async Task<ObsClient> ConnectAsync(ObsConnection connection, int eventSubscriptions,
        CancellationToken cancellationToken) {
        var client = new ObsClient();
        try {
            await client._ws.ConnectAsync(connection.Uri, cancellationToken);

            var hello = await client.ReceiveAsync(cancellationToken);
            if (hello.GetProperty("op").GetInt32() != OpHello) {
                throw new InvalidOperationException("OBS didn't start with Hello");
            }

            var helloData = hello.GetProperty("d");
            var identify = new JsonObject {
                ["rpcVersion"] = 1,
                ["eventSubscriptions"] = eventSubscriptions,
            };
            if (helloData.TryGetProperty("authentication", out var auth)) {
                identify["authentication"] = Authenticate(connection.Password ?? "",
                    auth.GetProperty("salt").GetString()!, auth.GetProperty("challenge").GetString()!);
            }

            await client.SendAsync(OpIdentify, identify, cancellationToken);

            var identified = await client.ReceiveAsync(cancellationToken);
            if (identified.GetProperty("op").GetInt32() != OpIdentified) {
                throw new InvalidOperationException("OBS didn't accept the identification");
            }

            client._receiveLoop = client.ReceiveLoop();
            return client;
        }
        catch {
            await client.DisposeAsync();
            throw;
        }
    }

    /// <summary>Sends a request and returns its <c>responseData</c> (an undefined element when there is none).</summary>
    public async Task<JsonElement> RequestAsync(string requestType, JsonObject? requestData,
        CancellationToken cancellationToken) {
        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = tcs;
        try {
            var payload = new JsonObject {
                ["requestType"] = requestType,
                ["requestId"] = requestId,
            };
            if (requestData is not null) {
                payload["requestData"] = requestData;
            }

            await SendAsync(OpRequest, payload, cancellationToken);
            var response = await tcs.Task.WaitAsync(cancellationToken);

            var status = response.GetProperty("requestStatus");
            if (!status.GetProperty("result").GetBoolean()) {
                var comment = status.TryGetProperty("comment", out var c) ? c.GetString() : null;
                throw new ObsRequestException(requestType, status.GetProperty("code").GetInt32(), comment);
            }

            return response.TryGetProperty("responseData", out var data) ? data : default;
        }
        finally {
            _pending.TryRemove(requestId, out _);
        }
    }

    public async ValueTask DisposeAsync() {
        await _cts.CancelAsync();
        if (_ws.State == WebSocketState.Open) {
            try {
                using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, closeTimeout.Token);
            }
            catch {
                /* closing is best effort */
            }
        }

        try {
            await _receiveLoop;
        }
        catch {
            /* already reported through the pending requests */
        }

        _ws.Dispose();
        _cts.Dispose();
        _sendLock.Dispose();
    }

    private static string Authenticate(string password, string salt, string challenge) {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    private async Task ReceiveLoop() {
        try {
            while (!_cts.IsCancellationRequested) {
                var message = await ReceiveAsync(_cts.Token);
                var data = message.GetProperty("d");
                switch (message.GetProperty("op").GetInt32()) {
                    case OpRequestResponse:
                        if (_pending.TryGetValue(data.GetProperty("requestId").GetString()!, out var tcs)) {
                            tcs.TrySetResult(data);
                        }

                        break;
                    case OpEvent:
                        var eventData = data.TryGetProperty("eventData", out var ed) ? ed : default;
                        EventReceived?.Invoke(data.GetProperty("eventType").GetString()!, eventData);
                        break;
                }
            }
        }
        catch (Exception x) {
            foreach (var tcs in _pending.Values) {
                tcs.TrySetException(x);
            }
        }
    }

    private async Task SendAsync(int op, JsonObject data, CancellationToken cancellationToken) {
        var bytes = Encoding.UTF8.GetBytes(new JsonObject { ["op"] = op, ["d"] = data }.ToJsonString());
        await _sendLock.WaitAsync(cancellationToken);
        try {
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally {
            _sendLock.Release();
        }
    }

    private async Task<JsonElement> ReceiveAsync(CancellationToken cancellationToken) {
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do {
            result = await _ws.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) {
                throw new WebSocketException("OBS closed the connection");
            }

            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        using var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }
}
