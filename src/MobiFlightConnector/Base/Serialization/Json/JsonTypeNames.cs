using System;
using System.Linq;

namespace MobiFlight.Base.Serialization.Json
{
    /// <summary>
    /// Builds the "valid types are: ..." suffix used by the polymorphic JSON converters.
    /// The discriminator in project files is the short class name (e.g. "Transformation",
    /// not "Transform"), so a rejected payload can name the exact strings that would work.
    /// </summary>
    internal static class JsonTypeNames
    {
        public static string For(Type baseType)
        {
            var names = baseType.Assembly.GetTypes()
                .Where(t => t != baseType && baseType.IsAssignableFrom(t) && !t.IsAbstract && t.IsPublic)
                .Select(t => t.Name)
                .OrderBy(n => n, StringComparer.Ordinal);

            return string.Join(", ", names);
        }
    }
}
