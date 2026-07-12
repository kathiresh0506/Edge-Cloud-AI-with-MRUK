using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Game Master Network Client
/// ===========================
/// Connects to the Tactical Hub WebSocket relay (port 8765) and spawns
/// enemies at coordinates received from the phone radar UI. Tries each
/// address in candidateUrls until one connects — required because on the
/// Quest 'localhost' is the headset, not the PC running the hub.
///
/// This script uses the existing AlienWaveManager.SpawnEnemyAt() method
/// so manually spawned enemies behave exactly like auto-spawned ones
/// (full AI, health tracking, score, VFX, death cleanup).
///
/// Setup:
///   1. Create an empty GameObject named "NetworkClient"
///   2. Attach this script
///   3. Drag your AlienWaveManager reference into the Inspector slot
///      (it lives on the GameManager object)
///   4. Run tactical_hub.py on the same PC
///   5. Open the radar UI on your phone
/// </summary>
public class NetworkClient : MonoBehaviour
{
    [Header("WebSocket Connection")]
    [Tooltip("Candidate Tactical Hub URLs, tried in rotation until one connects. On the Quest, 'localhost' is the HEADSET itself — the PC must be reached via its Wi-Fi LAN IP, so those come first.")]
    public string[] candidateUrls =
    {
        "ws://10.79.7.153:8765",   // PC on the shared Wi-Fi network
        "ws://192.168.137.1:8765", // PC when using Windows Mobile Hotspot
        "ws://localhost:8765",     // editor / same-PC testing
    };

    [Tooltip("Currently attempted URL (rotates through candidateUrls on failure).")]
    public string wsUrl = "";

    [Tooltip("Seconds between reconnection attempts.")]
    public float reconnectInterval = 3f;

    private int _urlIndex = 0;

    [Header("Spawn References")]
    [Tooltip("Drag the AlienWaveManager component here (on the GameManager object).")]
    public AlienWaveManager waveManager;

    [Header("Spawn Settings")]
    [Tooltip("Height offset for spawned enemies relative to player Y.")]
    public float spawnHeightOffset = 0f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    // ── Internal ──────────────────────────────────────────
    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
    private bool _isConnecting;
    private float _nextReconnectTime;
    private Transform _playerTransform;

    // ── JSON payload model ────────────────────────────────
    [Serializable]
    private class SpawnPayload
    {
        public string device;
        public string action;
        public string intent;
        public string type;
        public float x;
        public float y;
        public float scale;
    }

    // ══════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════

    void Start()
    {
        // Cache player reference
        Camera mainCam = Camera.main;
        if (mainCam != null)
            _playerTransform = mainCam.transform;

        // Auto-find WaveManager if not assigned
        if (waveManager == null)
            waveManager = FindFirstObjectByType<AlienWaveManager>();

        if (waveManager == null)
            Debug.LogError("[NetworkClient] AlienWaveManager not found! Assign it in the Inspector.");

        // Start WebSocket connection
        ConnectAsync();
    }

    void Update()
    {
        // Process all queued messages on the main thread
        while (_messageQueue.TryDequeue(out string message))
        {
            ProcessMessage(message);
        }

        // Auto-reconnect
        if (_ws == null || _ws.State != WebSocketState.Open)
        {
            if (!_isConnecting && Time.time >= _nextReconnectTime)
            {
                _nextReconnectTime = Time.time + reconnectInterval;
                ConnectAsync();
            }
        }

        // Keep player reference fresh (camera can change in XR)
        if (_playerTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                _playerTransform = mainCam.transform;
        }
    }

    void OnDestroy()
    {
        DisconnectAsync();
    }

    void OnApplicationQuit()
    {
        DisconnectAsync();
    }

    // ══════════════════════════════════════════════════════
    // WebSocket Connection
    // ══════════════════════════════════════════════════════

    private async void ConnectAsync()
    {
        if (_isConnecting) return;
        _isConnecting = true;

        try
        {
            // Clean up previous socket
            if (_ws != null)
            {
                try { _ws.Dispose(); } catch { }
                _ws = null;
            }

            // Rotate through the candidate URLs: each failed attempt moves on
            // to the next address, so the right one is found automatically
            // whether we're in the editor (localhost) or on the Quest (LAN IP).
            if (candidateUrls != null && candidateUrls.Length > 0)
                wsUrl = candidateUrls[_urlIndex % candidateUrls.Length];

            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();

            if (showDebugLogs)
                Debug.Log($"[NetworkClient] Connecting to {wsUrl}...");

            await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);

            if (showDebugLogs)
                Debug.Log($"[NetworkClient] \u2705 Connected to Tactical Hub at {wsUrl}!");

            // Start background receive loop
            _ = ReceiveLoop(_cts.Token);
        }
        catch (Exception ex)
        {
            // Try the next candidate address on the following attempt
            _urlIndex++;

            if (showDebugLogs)
                Debug.LogWarning($"[NetworkClient] Connection to {wsUrl} failed: {ex.Message} \u2014 will try next candidate.");
        }
        finally
        {
            _isConnecting = false;
        }
    }

    private async void DisconnectAsync()
    {
        try
        {
            _cts?.Cancel();

            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client shutting down",
                    CancellationToken.None
                );
            }
        }
        catch { }
        finally
        {
            try { _ws?.Dispose(); } catch { }
            _ws = null;
        }
    }

    // ══════════════════════════════════════════════════════
    // Background Receive Loop
    // ══════════════════════════════════════════════════════

    private async Task ReceiveLoop(CancellationToken token)
    {
        byte[] buffer = new byte[4096];

        try
        {
            while (_ws != null && _ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                WebSocketReceiveResult result = await _ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    token
                );

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (showDebugLogs)
                        Debug.Log("[NetworkClient] Server closed connection.");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _messageQueue.Enqueue(message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (WebSocketException ex)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[NetworkClient] WebSocket error: {ex.Message}");
        }
        catch (Exception ex)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[NetworkClient] Receive error: {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════════════
    // Main-Thread Message Processing
    // ══════════════════════════════════════════════════════

    private void ProcessMessage(string rawJson)
    {
        if (showDebugLogs)
            Debug.Log($"[NetworkClient] Received: {rawJson}");

        SpawnPayload payload;
        try
        {
            payload = JsonUtility.FromJson<SpawnPayload>(rawJson);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkClient] JSON parse error: {ex.Message}");
            return;
        }

        // Only process spawn commands from the phone
        if (payload == null || payload.action != "spawn" || payload.device != "phone")
            return;

        // ── Calculate world position ──────────────────────
        if (_playerTransform == null)
        {
            Debug.LogWarning("[NetworkClient] No player transform — cannot spawn.");
            return;
        }

        // Phone radar: X = left/right, Y = forward/backward (depth)
        // Unity:       X = left/right, Z = forward/backward
        Vector3 playerPos = _playerTransform.position;
        Vector3 playerForward = _playerTransform.forward;
        Vector3 playerRight = _playerTransform.right;

        // Flatten to horizontal plane
        playerForward.y = 0f;
        playerForward.Normalize();
        playerRight.y = 0f;
        playerRight.Normalize();

        // Map phone coordinates to world offset relative to player facing direction
        // Phone X → player's right axis
        // Phone Y → player's forward axis (negative Y = forward on radar)
        Vector3 worldOffset = playerRight * payload.x + playerForward * (-payload.y);

        Vector3 spawnPos = playerPos + worldOffset;
        spawnPos.y = playerPos.y + spawnHeightOffset;

        // ── Determine enemy type ─────────────────────────
        bool isBoss = payload.type != null &&
                      payload.type.Equals("boss", StringComparison.OrdinalIgnoreCase);

        // ── Spawn via AlienWaveManager ────────────────────
        if (waveManager != null)
        {
            waveManager.SpawnEnemyAt(spawnPos, isBoss);

            if (showDebugLogs)
            {
                string typeLabel = isBoss ? "BOSS" : payload.type;
                Debug.Log(
                    $"[NetworkClient] \U0001f47e Spawned {typeLabel} at " +
                    $"({spawnPos.x:F1}, {spawnPos.y:F1}, {spawnPos.z:F1}) " +
                    $"[{payload.x:F1}m right, {payload.y:F1}m deep, zoom {payload.scale:F1}x]"
                );
            }
        }
        else
        {
            Debug.LogError("[NetworkClient] AlienWaveManager is null — cannot spawn!");
        }
    }
}
