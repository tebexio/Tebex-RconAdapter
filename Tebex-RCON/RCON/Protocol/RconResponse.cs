namespace Tebex.RCON.Protocol;

/// <summary>
/// RconResponse is a paired RconPacket request and its associated response from the server.
/// </summary>
public class RconResponse
{
    /// <summary>
    /// The RCON request sent to the server
    /// </summary>
    private RconPacket _request;
    
    /// <summary>
    /// The RCON response received from the server
    /// </summary>
    private RconPacket _response;

    /// <summary>
    /// Creates an empty RconResponse that should be used only to return from errored functions
    /// </summary>
    public RconResponse()
    {
        _request = null;
        _response = null;
    }
    
    /// <summary>
    /// Creates a new RconPacket pair.
    /// </summary>
    /// <param name="request">The original RCON request</param>
    /// <param name="response">The RCON response</param>
    public RconResponse(RconPacket request, RconPacket response)
    {
        _request = request;
        _response = response;
    }
    
    public RconPacket Request
    {
        get => _request;
        set => _request = value;
    }
    
    public RconPacket Response
    {
        get => _response;
        set => _response = value;
    }
}