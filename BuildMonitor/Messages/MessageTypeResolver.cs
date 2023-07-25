using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace WebMessage.Messages
{
    internal class RequestTypeResolver : MessageTypeResolver
    {
        internal RequestTypeResolver()
        {
        }

        internal RequestTypeResolver(string typeDiscriminator, Type type) : this(new Dictionary<string, Type> { [typeDiscriminator] = type })
        {
        }

        internal RequestTypeResolver(IDictionary<string, Type> derivedTypes) : base(derivedTypes)
        {
        }

        protected override string TypeDiscriminator => nameof(Message.Uri);

        protected override void SetTypeDiscriminator(Message message, string typeDiscriminator)
        {
            message.Uri = typeDiscriminator;
        }
    }

    internal class ResponseTypeResolver : MessageTypeResolver
    {
        internal ResponseTypeResolver()
        {
        }

        internal ResponseTypeResolver(string typeDiscriminator, Type type) : this(new Dictionary<string, Type> { [typeDiscriminator] = type })
        {
        }

        internal ResponseTypeResolver(IDictionary<string, Type> derivedTypes) : base(derivedTypes)
        {
        }

        protected override string TypeDiscriminator => nameof(Message.Id);

        protected override List<string> IgnoredProperties => new() { nameof(Message.Uri) };

        protected override void SetTypeDiscriminator(Message message, string typeDiscriminator)
        {
            message.Id = typeDiscriminator;
        }
    }

    internal abstract class MessageTypeResolver : DefaultJsonTypeInfoResolver
    {
        //protected readonly List<JsonDerivedType> _derivedTypes;

        protected readonly ConcurrentDictionary<string, Type> _types = new();

        protected abstract string TypeDiscriminator { get; }

        protected virtual List<string> IgnoredProperties => new() { };

        internal ConcurrentDictionary<string, Type> Types => _types;

        internal MessageTypeResolver()
        {
            //_derivedTypes = new();
            Modifiers.Add(IgnoreProperties);
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
            //lock (_derivedTypes)
            //{
            //    _derivedTypes.RemoveAll(d => d.TypeDiscriminator as string == typeDiscriminator);
            //}
        }

        protected void AddMessage<TMessage>(string typeDiscriminator) where TMessage : Message
        {
            _types.AddOrUpdate(typeDiscriminator, typeof(TMessage), (_, _) => typeof(TMessage));
            //lock (_derivedTypes)
            //{
            //    var dt = _derivedTypes.FirstOrDefault(t => typeDiscriminator.Equals(t.TypeDiscriminator));
            //    if (dt.TypeDiscriminator is not null)
            //    {
            //        // We found an existing type discriminator
            //        return dt.DerivedType == typeof(TMessage);
            //    }

            //    _derivedTypes.Add(new(typeof(TMessage), typeDiscriminator));
            //    return true;
            //}
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

        protected abstract void SetTypeDiscriminator(Message message, string typeDiscriminator);

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

            //lock (_derivedTypes)
            //{
            //    foreach (var derivedType in _derivedTypes.Where(t => type.IsAssignableFrom(t.DerivedType)))
            //    {
            //        options.DerivedTypes.Add(derivedType);
            //    }
            //}

            return options;
        }

        private void IgnoreProperties(JsonTypeInfo info)
        {
            foreach (var ignoredProperty in IgnoredProperties.Append(TypeDiscriminator))
            {
                var propertyInfo = info.Properties.FirstOrDefault(p => p.Name == ignoredProperty);
                if (propertyInfo is not null)
                {
                    info.Properties.Remove(propertyInfo);
                }
            }
        }

    }

}

