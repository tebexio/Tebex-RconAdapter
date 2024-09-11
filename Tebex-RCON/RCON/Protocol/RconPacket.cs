namespace Tebex.RCON.Protocol
{
    /// <summary>
    /// RconPacket is the data type to contain a standard RCON message.
    /// </summary>
    public class RconPacket
    {
        /// <summary>
        /// The type of RCON packet, sent as part of the standard RCON protocol
        /// </summary>
        public enum Type
        {
            CommandResponse = 0,
            CommandRequest = 2,
            LoginRequest = 3
        }

        private int _id = 0;
        private Type _type = Type.CommandRequest;
        private string _message = "";

        /// <summary>
        /// An identifier for this packet. Server responses normally contain the same packet ID as the sent packet.
        /// Packets not associated with a request typically use ID -1. For example some games may provide
        /// their server log to connected RCON clients, and may use a negative ID. 
        /// </summary>
        public int Id
        {
            get => _id;
            set => _id = value;
        }
        
        /// <summary>
        /// <see cref="Type"/>
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public Type PacketType
        {
            get => _type;
            set
            {
                if (!Enum.IsDefined(typeof(Type), value))
                {
                    throw new ArgumentException($"Invalid RCON packet type: {value}");
                }
                _type = value;
            }
        }

        /// <summary>
        /// The message associated with this packet. For command packets this is the command to execute.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public string Message
        {
            get => _message;
            set => _message = value ?? throw new ArgumentNullException(nameof(Message));
        }

        public RconPacket(int id, Type packetType, string message)
        {
            Id = id;
            PacketType = packetType;
            Message = message;
        }

        public override string ToString()
        {
            // Don't show RCON password if we write the packet to log
            if (PacketType == Type.LoginRequest)
            {
                return $"id: {this._id} | type: {this.PacketType} | msg: **password**";
            }
            
            return $"id: {this._id} | type: {this.PacketType} | msg: {this._message}";
        }
    }
}