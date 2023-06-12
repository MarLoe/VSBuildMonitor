using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BuildMonitor.Messages
{
    internal class MessageResponseTypeResolver : DefaultJsonTypeInfoResolver
    {
        private readonly JsonPolymorphismOptions _polymorphismOptions;

        internal MessageResponseTypeResolver()
        {
            _polymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = nameof(Message.Uri),
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            };
        }

        public void AddResponse<TResponse>(string uri)
        {
            if (_polymorphismOptions.DerivedTypes.Any(t => uri.Equals(t.TypeDiscriminator)))
            {
                return;
            }
            _polymorphismOptions.DerivedTypes.Add(new(typeof(Message<TResponse>), uri));
        }

        public void RemoveResponse(string uri)
        {
            var derivedType = _polymorphismOptions.DerivedTypes.FirstOrDefault(d => d.TypeDiscriminator as string == uri);
            //_polymorphismOptions.DerivedTypes.Remove(derivedType);
        }

        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var jsonTypeInfo = base.GetTypeInfo(type, options);
            //if (typeof(Message).IsAssignableFrom(jsonTypeInfo.Type))
            if (jsonTypeInfo.Type == typeof(Message))
            {
                jsonTypeInfo.PolymorphismOptions = _polymorphismOptions;
            }
            return jsonTypeInfo;
        }
    }

}

