using MobiFlight.Modifier;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace MobiFlight.BrowserMessages.Incoming.Converter
{
    public class ModifierBaseConverter : JsonConverter<ModifierBase>
    {
        public override void WriteJson(JsonWriter writer, ModifierBase value, JsonSerializer serializer)
        {
            var type = value.GetType();
            writer.WriteStartObject();
            writer.WritePropertyName("Type");
            writer.WriteValue(type.Name); // Write the type discriminator
            foreach (var property in type.GetProperties())
            {
                if (property.CanRead)
                {
                    writer.WritePropertyName(property.Name);
                    serializer.Serialize(writer, property.GetValue(value));
                }
            }
            writer.WriteEndObject();
        }

        public override ModifierBase ReadJson(JsonReader reader, Type objectType, ModifierBase existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var typeName = $"MobiFlight.Modifier.{jsonObject["Type"]?.ToString()}";

            var type = Type.GetType(typeName);
            if (type == null)
            {
                // List the valid discriminators so a failed attempt is self-correcting
                // (e.g. for MCP clients editing configs: the name is the class name,
                // "Transformation", not "Transform").
                var validTypes = typeof(ModifierBase).Assembly.GetTypes()
                    .Where(t => typeof(ModifierBase).IsAssignableFrom(t) && !t.IsAbstract && t.IsPublic)
                    .Select(t => t.Name)
                    .OrderBy(n => n);
                throw new JsonSerializationException($"Unknown modifier type: \"{jsonObject["Type"]?.ToString()}\". Valid types are: {string.Join(", ", validTypes)}.");
            }

            // "Type" is the discriminator written by WriteJson, not a settable member of the
            // modifier classes - drop it before Populate so strict deserializers
            // (MissingMemberHandling.Error, used by the MCP update tool) do not reject it.
            jsonObject.Remove("Type");

            var modifier = Activator.CreateInstance(type) as ModifierBase;
            serializer.Populate(jsonObject.CreateReader(), modifier);
            return modifier;
        }
    }
}
