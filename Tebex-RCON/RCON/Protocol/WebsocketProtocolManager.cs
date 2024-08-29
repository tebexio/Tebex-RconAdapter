using System.Net.WebSockets;
using System.Text;

namespace Tebex.RCON.Protocol
{
    public class WebsocketProtocolManager : StdProtocolManager
    {
        private ClientWebSocket _webSocket;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public override bool Connect(string host, int port, string password, bool reconnectOnFail)
        {
            Host = host;
            Port = port;
            Password = password;
            ReconnectOnFail = reconnectOnFail;

            _webSocket = new ClientWebSocket();
            var uri = new Uri($"ws://{host}:{port}/{password}");

            try
            {
                Task connectionTask = _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
                connectionTask.Wait();
                
                if (_webSocket.State == WebSocketState.Open)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Listener?.GetAdapter().LogError($"Error connecting to WebSocket RCON: {ex.Message}");
            }

            return false;
        }

        public override void Write(string data)
        {
            SendCommandAsync(2, data).Wait();
        }

        public override string? Read()
        {
            return ReadResponseAsync().Result;
        }

        private async Task SendCommandAsync(int requestType, string command)
        {
            int requestId = new Random().Next(1, int.MaxValue);
            var payload = new
            {
                Identifier = requestId,
                Message = command,
                Name = "WebRcon"
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            if (Listener != null)
            {
                Listener.GetAdapter().LogDebug($"WebSocket RCON ({Host}:{Port}) -> id: {requestId} | type:{requestType}| length: {buffer.Length} bytes | body: '{command}'");
            }

            await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
        }

        private async Task<string?> ReadResponseAsync()
        {
            var buffer = new byte[4096];
            WebSocketReceiveResult result;

            try
            {
                result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Listener?.GetAdapter().LogError($"Error reading from WebSocket RCON: {ex.Message}");
                return null;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                return null;
            }

            string responseString = Encoding.UTF8.GetString(buffer, 0, result.Count);

            if (Listener != null)
            {
                Listener.GetAdapter().LogDebug($"WebSocket RCON ({Host}:{Port}) <- {responseString} | length: {result.Count}");
            }

            return responseString;
        }

        public override void Close()
        {
            _cancellationTokenSource.Cancel();
            _webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait();
            _webSocket?.Dispose();
        }

        public override string GetProtocolName()
        {
            return "WebsocketRCON";
        }
    }
}
