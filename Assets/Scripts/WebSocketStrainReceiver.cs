using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class WebSocketStrainReceiver : MonoBehaviour
{
    [Serializable]
    public class StrainItem
    {
        public string elementId;
        public float strain;
    }

    [Serializable]
    public class StrainPacket
    {
        public string type;
        public long timestamp;
        public StrainItem[] elements;
    }

    [Serializable]
    public class SingleStrainMessage
    {
        public string elementId;
        public float strain;
    }

    [Header("References")]
    [SerializeField]
    private BeamElementColorController colorController;

    [Header("WebSocket")]
    [SerializeField]
    private string serverUrl =
        "ws://127.0.0.1:8765";

    [SerializeField]
    private bool connectOnStart = true;

    [SerializeField]
    private bool autoReconnect = true;

    [SerializeField]
    private float reconnectDelaySeconds = 3.0f;

    [SerializeField]
    private float keepAliveSeconds = 20.0f;

    [SerializeField]
    private int receiveBufferSize = 8192;

    [SerializeField]
    private int maxMessageBytes = 1024 * 1024;

    private readonly ConcurrentQueue<string>
        pendingMessages =
        new ConcurrentQueue<string>();

    private readonly CancellationTokenSource
        lifetimeCancellation =
        new CancellationTokenSource();

    private ClientWebSocket socket;
    private bool connectionStarted;

    private void Start()
    {
        if (colorController == null)
        {
            UnityEngine.Debug.LogError(
                "Color Controller is not assigned.");
        }

        // 让梁生成器先完成生成，再扫描子梁。
        Invoke(
            nameof(RefreshColorController),
            0.2f);

        if (connectOnStart)
        {
            StartWebSocket();
        }
    }

    private void Update()
    {
        // WebSocket 接收线程只负责把 JSON 放入队列。
        // Unity 对象和材质只能在主线程中修改。
        string latestMessage = null;

        while (pendingMessages.TryDequeue(
                   out string message))
        {
            // 只处理最新一帧，避免网络数据堆积导致显示延迟。
            latestMessage = message;
        }

        if (!string.IsNullOrEmpty(latestMessage))
        {
            ProcessMessage(latestMessage);
        }
    }

    [ContextMenu("Start WebSocket")]
    public void StartWebSocket()
    {
        if (connectionStarted)
        {
            UnityEngine.Debug.LogWarning(
                "WebSocket connection has already started.");

            return;
        }

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            UnityEngine.Debug.LogError(
                "WebSocket server URL is empty.");

            return;
        }

        connectionStarted = true;

        _ = ConnectLoopAsync();
    }

    [ContextMenu("Refresh Color Controller")]
    public void RefreshColorController()
    {
        if (colorController == null)
        {
            UnityEngine.Debug.LogError(
                "Color Controller is not assigned.");

            return;
        }

        colorController.RefreshElements();

        UnityEngine.Debug.Log(
            "Color controller refreshed. Elements: " +
            colorController.GetElementCount());
    }

    private async Task ConnectLoopAsync()
    {
        while (!lifetimeCancellation.IsCancellationRequested)
        {
            ClientWebSocket currentSocket = null;

            try
            {
                currentSocket =
                    new ClientWebSocket();

                currentSocket.Options.KeepAliveInterval =
                    TimeSpan.FromSeconds(
                        Mathf.Max(
                            keepAliveSeconds,
                            1.0f));

                socket =
                    currentSocket;

                UnityEngine.Debug.Log(
                    "Connecting to WebSocket: " +
                    serverUrl);

                await currentSocket.ConnectAsync(
                    new Uri(serverUrl),
                    lifetimeCancellation.Token);

                UnityEngine.Debug.Log(
                    "WebSocket connected.");

                await ReceiveLoopAsync(
                    currentSocket,
                    lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                if (!lifetimeCancellation.IsCancellationRequested)
                {
                    UnityEngine.Debug.LogWarning(
                        "WebSocket error: " +
                        exception.Message);
                }
            }
            finally
            {
                if (socket == currentSocket)
                {
                    socket = null;
                }

                if (currentSocket != null)
                {
                    currentSocket.Dispose();
                }
            }

            if (!autoReconnect ||
                lifetimeCancellation.IsCancellationRequested)
            {
                break;
            }

            try
            {
                UnityEngine.Debug.Log(
                    "WebSocket disconnected. Reconnecting...");

                await Task.Delay(
                    TimeSpan.FromSeconds(
                        Mathf.Max(
                            reconnectDelaySeconds,
                            0.1f)),
                    lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        connectionStarted = false;
    }

    private async Task ReceiveLoopAsync(
        ClientWebSocket currentSocket,
        CancellationToken cancellationToken)
    {
        byte[] buffer =
            new byte[
                Mathf.Max(
                    receiveBufferSize,
                    1024)];

        List<byte> messageBytes =
            new List<byte>();

        bool discardCurrentMessage = false;

        while (
            currentSocket.State ==
            WebSocketState.Open &&
            !cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result =
                await currentSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

            if (result.MessageType ==
                WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType !=
                WebSocketMessageType.Text)
            {
                continue;
            }

            if (
                messageBytes.Count +
                result.Count >
                maxMessageBytes)
            {
                discardCurrentMessage = true;
            }

            if (!discardCurrentMessage)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    messageBytes.Add(
                        buffer[i]);
                }
            }

            if (result.EndOfMessage)
            {
                if (!discardCurrentMessage)
                {
                    string json =
                        Encoding.UTF8.GetString(
                            messageBytes.ToArray());

                    pendingMessages.Enqueue(json);
                }

                messageBytes.Clear();
                discardCurrentMessage = false;
            }
        }
    }

    private void ProcessMessage(
        string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            StrainPacket packet =
                JsonUtility.FromJson<StrainPacket>(
                    json);

            if (packet != null &&
                packet.elements != null &&
                packet.elements.Length > 0)
            {
                ApplyPacket(packet);
                return;
            }

            SingleStrainMessage single =
                JsonUtility.FromJson<
                    SingleStrainMessage>(
                    json);

            if (single != null &&
                !string.IsNullOrWhiteSpace(
                    single.elementId))
            {
                ApplySingleResult(
                    single.elementId,
                    single.strain);

                return;
            }

            UnityEngine.Debug.LogWarning(
                "Unsupported strain message: " +
                json);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError(
                "Failed to parse WebSocket JSON: " +
                exception.Message +
                "\nMessage: " +
                json);
        }
    }

    private void ApplyPacket(
        StrainPacket packet)
    {
        if (colorController == null)
        {
            return;
        }

        foreach (StrainItem item in packet.elements)
        {
            if (item == null ||
                string.IsNullOrWhiteSpace(
                    item.elementId))
            {
                continue;
            }

            ApplySingleResult(
                item.elementId,
                item.strain);
        }
    }

    private void ApplySingleResult(
        string elementId,
        float strain)
    {
        if (colorController == null)
        {
            return;
        }

        // 如果生成器刚刚生成完单元，先重新扫描一次。
        if (!colorController.ContainsElement(
                elementId))
        {
            colorController.RefreshElements();
        }

        colorController.SetElementStrain(
            elementId,
            strain);
    }

    private void OnDestroy()
    {
        lifetimeCancellation.Cancel();

        if (socket != null)
        {
            socket.Dispose();
            socket = null;
        }
    }
}