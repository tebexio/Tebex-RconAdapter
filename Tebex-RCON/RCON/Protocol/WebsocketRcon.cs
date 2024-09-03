using System.Net.WebSockets;
using System.Text;
using Tebex.Adapters;

namespace Tebex.RCON.Protocol;

public class WebsocketRcon : RconConnection
{
    private ClientWebSocket _ws;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _stop = false;
    
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
            var uri = new Uri($"ws://{_host}:{_port}/{_password}");
            Task connectionTask = _ws.ConnectAsync(uri, _cancellationTokenSource.Token);
            connectionTask.Wait();
            
            if (_ws.State == WebSocketState.Open)
            {
                new Thread(() =>
                {
                    try
                    {
                        while (!_stop)
                        {
                            var packet = ReadPacket(-1);
                            _adapter.LogInfo(packet.ToString());
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
    
    protected override RconPacket SendPacket(RconPacket.Type requestType, string message)
    {
        var packet = new RconPacket(NextId(), RconPacket.Type.CommandRequest, message);
        var payload = new Payload
        {
            Identifier = packet.Id.ToString(),
            Message = packet.Message,
            Name = "WebRcon"
        };
        string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);

        try
        {
            Task sendTask = _ws.SendAsync(new ArraySegment<byte>(payloadBytes), WebSocketMessageType.Text, true,
                _cancellationTokenSource.Token);
            sendTask.Wait();
            requests.Add(packet.Id, packet);
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

        string responseString = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Payload payload = Newtonsoft.Json.JsonConvert.DeserializeObject<Payload>(responseString);
        var packet = new RconPacket(int.Parse(payload.Identifier), (int)RconPacket.Type.CommandResponse, payload.Message);
        responses.Add(packet.Id, packet);
        return packet;
    }

    public override bool Polls()
    {
        return true;
    }

    protected override bool Reconnect()
    {
        _adapter.LogError(_adapter.Error($"Connection to RCON websocket server lost!"));
        int tries = 0;
        while (true)
        {
            try
            {
                tries++;
                
                _adapter.LogInfo(_adapter.Warn($"Attempting reconnect #{tries}..."));
                _ws = new ClientWebSocket();
                var connectResult = Connect();
                if (connectResult.Item1)
                {
                    _adapter.LogInfo(_adapter.Success("Reconnected."));
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
        if (!responses.ContainsKey(messageId))
        {
            return new Tuple<RconResponse, string>(null, "response not received yet");
        }

        var request = requests[messageId];
        var response = responses[messageId];
        return new Tuple<RconResponse, string>(new RconResponse(request, response), "");
    }
}