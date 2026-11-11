using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MobiFlight.Mcp
{
    /// <summary>
    /// Describes which JSON paths differ between two representations of a config item.
    /// Used to turn update_config_item's "success" into "success + what changed", so an
    /// agent can confirm that the fields it meant to touch are the fields that moved.
    /// </summary>
    internal static class JsonDiff
    {
        /// <summary>Runtime-only values the UI rewrites continuously; not part of the mapping.</summary>
        private static readonly HashSet<string> IgnoredRootProperties =
            new(StringComparer.Ordinal) { "Status", "Value", "RawValue" };

        public static List<string> ChangedPaths(JToken before, JToken after)
        {
            var changes = new List<string>();

            if (before is JObject b && after is JObject a)
            {
                foreach (var name in PropertyNames(b, a))
                {
                    if (IgnoredRootProperties.Contains(name)) continue;
                    Compare(b[name], a[name], name, changes);
                }
            }
            else
            {
                Compare(before, after, "", changes);
            }

            return changes;
        }

        private static void Compare(JToken before, JToken after, string path, List<string> changes)
        {
            if (JToken.DeepEquals(before, after)) return;

            if (before is JObject b && after is JObject a)
            {
                foreach (var name in PropertyNames(b, a))
                    Compare(b[name], a[name], $"{path}.{name}", changes);
                return;
            }

            if (before is JArray beforeArray && after is JArray afterArray)
            {
                if (beforeArray.Count != afterArray.Count)
                {
                    changes.Add($"{path} ({beforeArray.Count} -> {afterArray.Count} items)");
                    return;
                }

                for (var i = 0; i < beforeArray.Count; i++)
                    Compare(beforeArray[i], afterArray[i], $"{path}[{i}]", changes);
                return;
            }

            changes.Add(path);
        }

        private static IEnumerable<string> PropertyNames(JObject before, JObject after)
        {
            return before.Properties().Select(p => p.Name)
                .Union(after.Properties().Select(p => p.Name))
                .OrderBy(n => n, StringComparer.Ordinal);
        }
    }
}
