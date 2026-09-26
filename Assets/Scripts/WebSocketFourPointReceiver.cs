using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Receives four corner strains from the Python/WebSocket test sender.
/// Unity objects are updated only in Update(), never on the socket thread.
/// </summary>
public class WebSocketFourPointReceiver : MonoBehaviour
{
    [Serializable]
    private class FourPointStrainItem
    {
        public string elementId;
        public float strainP1;
        public float strainP2;
        public float strainP3;
        public float strainP4;
        public float strain;
    }

    [Serializable]
    private class FourPointPacket
    {
        public string type;
        public long timestamp;
        public FourPointStrainItem[] elements;
    }

    [SerializeField]
    private FourPointStressController controller;

    [SerializeField]
    private string serverUrl = "ws://127.0.0.1:8765";

    [SerializeField]
    private bool connectOnStart = true;

    [SerializeField]
    private bool reconnectOnFailure = true;

    [SerializeField]
    private float reconnectSeconds = 2f;

    private readonly ConcurrentQueue<string> pendingMessages =
        new ConcurrentQueue<string>();

    private ClientWebSocket socket;
    private CancellationTokenSource cancellation;
    private Task receiveTask;
    private bool stopping;

    private void Start()
    {
        if (connectOnStart)
        {
            Connect();
        }
    }

    private void Update()
    {
        while (pendingMessages.TryDequeue(out string json))
        {
            ApplyPacket(json);
        }
    }

    [ContextMenu("Connect WebSocket")]
    public void Connect()
    {
        if (receiveTask != null && !receiveTask.IsCompleted)
        {
            return;
        }

        stopping = false;
        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        receiveTask = ReceiveLoopAsync(cancellation.Token);
    }

    [ContextMenu("Disconnect WebSocket")]
    public void Disconnect()
    {
        stopping = true;
        cancellation?.Cancel();

        if (socket != null)
        {
            socket.Dispose();
            socket = null;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !stopping)
        {
            try
            {
                socket?.Dispose();
                socket = new ClientWebSocket();
                await socket.ConnectAsync(new Uri(serverUrl), token);
                UnityEngine.Debug.Log(
                    "Connected to four-point strain server: " + serverUrl);

                byte[] buffer = new byte[8192];
                StringBuilder message = new StringBuilder();

                while (socket.State == WebSocketState.Open &&
                       !token.IsCancellationRequested)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    message.Append(Encoding.UTF8.GetString(
                        buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        pendingMessages.Enqueue(message.ToString());
                        message.Clear();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "WebSocket receive failed: " + exception.Message);
            }

            if (!reconnectOnFailure || stopping || token.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(
                    Mathf.CeilToInt(Mathf.Max(0.1f, reconnectSeconds) * 1000f),
                    token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ApplyPacket(string json)
    {
        if (controller == null)
        {
            UnityEngine.Debug.LogWarning(
                "FourPointStressController is not assigned.");
            return;
        }

        FourPointPacket packet;
        try
        {
            packet = JsonUtility.FromJson<FourPointPacket>(json);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning(
                "Invalid four-point strain JSON: " + exception.Message);
            return;
        }

        if (packet == null || packet.elements == null)
        {
            return;
        }

        foreach (FourPointStrainItem item in packet.elements)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.elementId))
            {
                continue;
            }

            controller.SetElementCornerStrains(
                item.elementId,
                item.strainP1,
                item.strainP2,
                item.strainP3,
                item.strainP4);
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }
}
