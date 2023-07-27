using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace WebMessage.Messages
{
    internal class MessageTypeResolver : DefaultJsonTypeInfoResolver
    {
        protected readonly ConcurrentDictionary<string, Type> _types = new();

        protected virtual string TypeDiscriminator { get; } = nameof(Message.Uri);

        internal ConcurrentDictionary<string, Type> Types => _types;

        internal MessageTypeResolver()
        {
            Modifiers.Add(IgnoreTypeDiscriminator);
        }

        internal MessageTypeResolver(string typeDiscriminator, Type type) : this(new Dictionary<string, Type> { [typeDiscriminator] = type })
        {
        }

        internal MessageTypeResolver(IDictionary<string, Type> derivedTypes) : this()
        {
            _types = new(derivedTypes);
        }

        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var jsonTypeInfo = base.GetTypeInfo(type, options);
            if (typeof(Message).IsAssignableFrom(jsonTypeInfo.Type))
            {
                ModifyJsonTypeInfo(type, options, jsonTypeInfo);
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

        public void Remove(string typeDiscriminator)
        {
            _types.TryRemove(typeDiscriminator, out var _);
        }

        protected virtual void AddMessage<TMessage>(string typeDiscriminator) where TMessage : Message
        {
            _types.AddOrUpdate(typeDiscriminator, typeof(TMessage), (_, _) => typeof(TMessage));
        }

        protected virtual void ModifyJsonTypeInfo(Type type, JsonSerializerOptions options, JsonTypeInfo jsonTypeInfo)
        {
            jsonTypeInfo.PolymorphismOptions = CreateOptions(type);
            jsonTypeInfo.OnDeserialized = o =>
            {
                if (o is Message message && _types.ToArray().FirstOrDefault(t => t.Value == o.GetType()) is { Key: string discriminator })
                {
                    SetTypeDiscriminator(message, discriminator);
                }
            };
        }

        protected virtual void SetTypeDiscriminator(Message message, string typeDiscriminator)
        {
            message.Uri = typeDiscriminator;
        }

        private JsonPolymorphismOptions CreateOptions(Type type)
        {
            var options = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = TypeDiscriminator,
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            };

            foreach (var derivedType in _types.ToArray().Where(t => type.IsAssignableFrom(t.Value)))
            {
                options.DerivedTypes.Add(new(derivedType.Value, derivedType.Key));
            }

            return options;
        }

        private void IgnoreTypeDiscriminator(JsonTypeInfo info)
        {
            var propertyInfo = info.Properties.FirstOrDefault(p => p.Name == TypeDiscriminator);
            if (propertyInfo is not null)
            {
                info.Properties.Remove(propertyInfo);
            }
        }

    }

}

