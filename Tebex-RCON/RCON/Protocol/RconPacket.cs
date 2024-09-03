using System;

namespace Tebex.RCON.Protocol
{
    public class RconPacket
    {
        public enum Type
        {
            CommandResponse = 0,
            CommandRequest = 2,
            LoginRequest = 3
        }

        private int _id;
        private Type _type;
        private string _message;

        public int Id
        {
            get => _id;
            set => _id = value;
        }
        
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

        public string Message
        {
            get => _message;
            set => _message = value ?? throw new ArgumentNullException(nameof(Message));
        }

        public RconPacket(int id, Type responseType, string message)
        {
            Id = id;
            PacketType = responseType;
            Message = message;
        }

        public override string ToString()
        {
            if (this.PacketType == Type.LoginRequest)
            {
                return $"id: {this._id} | type: {this.PacketType} | msg: **password**";
            }
            else
            {
                return $"id: {this._id} | type: {this.PacketType} | msg: {this._message}";    
            }
            
        }
    }
}