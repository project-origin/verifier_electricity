using System;
using System.Text;
using ProjectOrigin.Electricity.Exceptions;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ProjectOrigin.Electricity.Converters;

public class YamlPublicKeyConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(IPublicKey) || type.IsAssignableTo(typeof(IPublicKey));
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var base64 = parser.Consume<Scalar>().Value;
        var bytes = Convert.FromBase64String(base64);
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

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is IPublicKey publicKey)
        {
            var text = publicKey.ExportPkixText();
            var bytes = Encoding.UTF8.GetBytes(text);
            var base64 = Convert.ToBase64String(bytes);

            emitter.Emit(new Scalar(base64));
        }
        else
        {
            emitter.Emit(new Scalar(string.Empty));
        }
    }
}

