using MobiFlight.Base.Serialization.Json;
using MobiFlight.InputConfig;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace MobiFlight.BrowserMessages.Incoming.Converter
{
    public class InputActionConverter : JsonConverter
    {
        public override bool CanWrite => false;
        public override bool CanConvert(Type objectType)
        {
            return typeof(InputAction).IsAssignableFrom(objectType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Never called because CanWrite = false
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            var jsonObject = JObject.Load(reader);
            var shortTypeName = jsonObject["Type"]?.ToString();
            var typeName = $"MobiFlight.InputConfig.{shortTypeName}";

            var type = Type.GetType(typeName);
            if (type == null)
            {
                // Name the valid discriminators so a failed attempt is self-correcting.
                throw new JsonSerializationException($"Unknown input action type: \"{shortTypeName}\". Valid types are: {JsonTypeNames.For(typeof(InputAction))}.");
            }

            var action = Activator.CreateInstance(type) as InputAction;
            serializer.Populate(jsonObject.CreateReader(), action);
            return action;
        }
    }
}