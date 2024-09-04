using Google.Protobuf;

namespace ProjectOrigin.Electricity.Interfaces;

public interface IProtoDeserializer
{
    IMessage Deserialize(string type, ByteString content);
}
