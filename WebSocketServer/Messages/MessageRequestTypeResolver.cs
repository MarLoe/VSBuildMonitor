using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace com.lobger.WebSocketServer.Messages
{
    internal class MessageRequestTypeResolver : DefaultJsonTypeInfoResolver
    {
        private readonly List<JsonDerivedType> _derivedTypes;

        internal MessageRequestTypeResolver(IEnumerable<JsonDerivedType> derivedTypes)
        {
            _derivedTypes = derivedTypes.ToList();
        }

        internal MessageRequestTypeResolver(Dictionary<string, Type> derivedTypes)
            : this(derivedTypes.Select(dt => new JsonDerivedType(dt.Value, dt.Key)))
        {
        }

        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var jsonTypeInfo = base.GetTypeInfo(type, options);
            if (jsonTypeInfo.Type == typeof(Message))
            {
                jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = nameof(Message.Uri),
                    IgnoreUnrecognizedTypeDiscriminators = true,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                };
                foreach (var derivedType in _derivedTypes)
                {
                    jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(derivedType);
                }
            }

            return jsonTypeInfo;
        }
    }

}

