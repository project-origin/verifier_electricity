using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectOrigin.Electricity.Exceptions;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

public class JsonPublicKeyConverter : JsonConverter<IPublicKey>
{
    public override IPublicKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var bytes = reader.GetBytesFromBase64();
        var text = Encoding.UTF8.GetString(bytes);
        try
        {
            return Algorithms.Ed25519.ImportPublicKeyText(text);
        }
        catch (FormatException ex)
        {
            throw new InvalidConfigurationException("Invalid public key format.", ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, IPublicKey value, JsonSerializerOptions options)
    {
        if (value is IPublicKey publicKey
             && publicKey is not null)
        {
            var text = publicKey.ExportPkixText();
            var bytes = Encoding.UTF8.GetBytes(text);
            writer.WriteBase64StringValue(bytes);
        }
        else
        {
            writer.WriteBase64StringValue([]);
        }
    }
}