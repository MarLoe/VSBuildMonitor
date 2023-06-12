using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace WebMessage.Messages
{

    internal class MessageTypeResolver : DefaultJsonTypeInfoResolver
    {
        private readonly List<JsonDerivedType> _derivedTypes;

        private JsonPolymorphismOptions? _polymorphismOptions;

        internal MessageTypeResolver()
        {
            _derivedTypes = new();
        }

        internal MessageTypeResolver(IEnumerable<JsonDerivedType> derivedTypes) : this()
        {
            _derivedTypes.AddRange(derivedTypes);
        }

        internal MessageTypeResolver(Dictionary<string, Type> derivedTypes)
            : this(derivedTypes.Select(dt => new JsonDerivedType(dt.Value, dt.Key)))
        {
        }

        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var jsonTypeInfo = base.GetTypeInfo(type, options);
            if (jsonTypeInfo.Type == typeof(Message))
            {
                jsonTypeInfo.PolymorphismOptions = _polymorphismOptions ??= CreateOptions(_derivedTypes);
            }
            return jsonTypeInfo;
        }

        public void Add(string typeDiscriminator)
        {
            AddMessage<Message>(typeDiscriminator);
        }

        public void Add<TPayload>(string typeDiscriminator)
        {
            AddMessage<Message<TPayload>>(typeDiscriminator);
        }

        private bool AddMessage<TMessage>(string typeDiscriminator) where TMessage : Message
        {
            lock (_derivedTypes)
            {
                var dt = _derivedTypes.FirstOrDefault(t => typeDiscriminator.Equals(t.TypeDiscriminator));
                if (dt.TypeDiscriminator is not null)
                {
                    // We found an existing type discriminator
                    return dt.DerivedType == typeof(TMessage);
                }

                _polymorphismOptions = null;
                _derivedTypes.Add(new(typeof(TMessage), typeDiscriminator));
                return true;
            }
        }

        public void Remove(string typeDiscriminator)
        {
            lock (_derivedTypes)
            {
                _polymorphismOptions = null;
                _derivedTypes.RemoveAll(d => d.TypeDiscriminator as string == typeDiscriminator);
            }
        }

        private JsonPolymorphismOptions CreateOptions(List<JsonDerivedType> derivedTypes)
        {
            var options = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = nameof(Message.Uri),
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            };
            lock (_derivedTypes)
            {
                foreach (var derivedType in derivedTypes)
                {
                    options.DerivedTypes.Add(derivedType);
                }
            }
            return options;
        }
    }

}

