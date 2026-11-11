using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace MobiFlight.Base.Serialization.Json
{
    public class SourceConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Source).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jsonObject = JObject.Load(reader);
            var shortTypeName = (string)jsonObject["Type"];
            var typeName = $"MobiFlight.Base.{shortTypeName}";

            var type = Type.GetType(typeName);
            if (type == null)
            {
                // Name the valid discriminators so a failed attempt is self-correcting.
                throw new NotSupportedException($"Unknown source type: \"{shortTypeName}\". Valid types are: {JsonTypeNames.For(typeof(Source))}.");
            }

            var source = Activator.CreateInstance(type) as Source;
            serializer.Populate(jsonObject.CreateReader(), source);
            return source;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Use default serialization
            throw new NotImplementedException();
        }

        public override bool CanWrite => false;
    }
}
