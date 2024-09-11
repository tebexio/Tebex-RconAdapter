using System.Net.WebSockets;
using System.Text;
using Tebex.Adapters;

namespace Tebex.RCON.Protocol;

/// <summary>
/// WebsocketRcon is an implementation of Rcon through a Websocket connection.
/// </summary>
public class WebsocketRcon : RconConnection
{
    private ClientWebSocket _ws;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _stop = false;
    
    /// <summary>
    /// Data type for Websocket RCON packets on Rust
    /// </summary>
    public class Payload
    {
        public string Identifier { get; set; }
        public string Message { get; set; }
        public string Name { get; set; }
    }
    
    public WebsocketRcon(TebexRconAdapter adapter, string host, int port, string password) : base(adapter, host, port, password)
    {
        _ws = new ClientWebSocket();
    }

    protected override void CloseConnection()
    {
        base.CloseConnection();
        
        // attempt to cleanly close the websocket connection
        var closeTask = _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        try
        {
            closeTask.Wait();
        }
        catch (Exception e)
        {
            // can be ignored
        }
    }
    
    public override Tuple<bool, string> Connect()
    {
        try
        {
            // Websocket client requires urls like "ws://127.0.0.1:25565/pass"
            var uri = new Uri($"ws://{Host}:{Port}/{Password}");
            Task connectionTask = _ws.ConnectAsync(uri, _cancellationTokenSource.Token);
            connectionTask.Wait();
            
            // Open state indicates that we connected successfully
            if (_ws.State == WebSocketState.Open)
            {
                // Start polling thread
                new Thread(() =>
                {
                    try
                    {
                        while (!_stop)
                        {
                            var packet = ReadPacket(-1);
                            Adapter.LogInfo(packet.ToString());
                        }
                    }
                    catch (Exception e)
                    {
                        //pass if the poller fails to read a packet, usually during reconnect
                    }
                }).Start();
                return new Tuple<bool, string>(true, "");
            }
            return new Tuple<bool, string>(false, "Failed to connect to websocket. Unexpected socket state: " + _ws.State);
        }
        catch (Exception ex)
        {
            return new Tuple<bool, string>(false, ex.Message);
        }
    }
    
    protected override RconPacket SendPacket(RconPacket.Type packetType, string message)
    {
        /*
         * The websocket server expects the RCON packet to be in JSON format. We create a basic RconPacket and
         * translate to the inner Payload data type.
         */
        var packet = new RconPacket(NextId(), RconPacket.Type.CommandRequest, message);
        var payload = new Payload
        {
            Identifier = packet.Id.ToString(),
            Message = packet.Message,
            Name = "WebRcon" // Required by protocol
        };
        string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);

        try
        {
            Task sendTask = _ws.SendAsync(new ArraySegment<byte>(payloadBytes), WebSocketMessageType.Text, true,
                _cancellationTokenSource.Token);
            sendTask.Wait();
            Requests.Add(packet.Id, packet);
        }
        catch (Exception e)
        {
            if (Reconnect())
            {
                return new RconPacket(-1, RconPacket.Type.CommandResponse,
                    "reconnecting"); // dummy packet to exit reconnect logic
            }
            else
            {
                throw new InvalidOperationException("Failed to reconnect.");
            }
        }
        
        return packet;
    }

    protected override RconPacket ReadPacket(int timeoutSeconds)
    {
        var buffer = new byte[4096];
        WebSocketReceiveResult result = null;

        try
        {
            Task<WebSocketReceiveResult> receiveTask =
                _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
            receiveTask.Wait();
            result = receiveTask.Result;
        }
        catch (WebSocketException e)
        {
            if (Reconnect())
            {
                return new RconPacket(-1, RconPacket.Type.CommandResponse, "reconnecting"); //dummy packet to exit reconnect logic
            }
            else
            {
                throw new InvalidOperationException("Failed to reconnect to websocket server.");
            }
        }

        // Websocket payloads will be a Payload in JSON format
        string responseString = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Payload payload = Newtonsoft.Json.JsonConvert.DeserializeObject<Payload>(responseString);
        var packet = new RconPacket(int.Parse(payload.Identifier), (int)RconPacket.Type.CommandResponse, payload.Message);
        Responses.Add(packet.Id, packet);
        return packet;
    }

    public override bool Polls()
    {
        return true;
    }

    protected override bool Reconnect()
    {
        Adapter.LogError(Adapter.Error($"Connection to RCON websocket server lost!"));
        int tries = 0;
        while (true)
        {
            try
            {
                tries++;
                
                Adapter.LogInfo(Adapter.Warn($"Attempting reconnect #{tries}..."));
                _ws = new ClientWebSocket();
                var connectResult = Connect();
                if (connectResult.Item1)
                {
                    Adapter.LogInfo(Adapter.Success("Reconnected."));
                    return true;
                }
                else
                {
                    Thread.Sleep(5000);
                }
            }
            catch (Exception e)
            {
                Thread.Sleep(5000);
                continue;
            }    
        }

        return false;
    }

    public override Tuple<RconResponse, string> ReceiveResponseTo(int messageId, int retries)
    {
        if (!Responses.ContainsKey(messageId))
        {
            return new Tuple<RconResponse, string>(null, "response not received yet");
        }

        var request = Requests[messageId];
        var response = Responses[messageId];
        return new Tuple<RconResponse, string>(new RconResponse(request, response), "");
    }
}