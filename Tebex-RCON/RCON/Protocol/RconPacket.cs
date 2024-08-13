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
        private int _size;

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

        public int Size
        {
            get => _size;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Size), "Size cannot be negative.");
                }
                _size = value;
            }
        }

        public RconPacket(int id, int responseType, string message, int size)
        {
            Id = id;
            PacketType = (Type)responseType;
            Message = message;
            Size = size;
        }

        public override string ToString()
        {
            return $"id: {this._id} | type: {this.PacketType} | message: {this._message} | size: {this._size} bytes";
        }
    }
}