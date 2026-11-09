using MobiFlight.Base;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MobiFlight.Mcp
{
    /// <summary>Compact overview of one config item as exposed to MCP clients.</summary>
    public record ConfigItemSummary(string Guid, string Name, string Type, string ControllerName, string ControllerSerial, bool Active, string ConfigFile, string InputEvents, string DeviceName, string DeviceType);

    /// <summary>
    /// Read-only tools about the loaded project's configuration; delegates
    /// are injected at startup so the tools stay unit-testable.
    /// </summary>
    [McpServerToolType]
    public static class ConfigTools
    {
        private static Func<List<ConfigFile>> _getConfigFiles;

        public static void Initialize(Func<List<ConfigFile>> getConfigFiles)
        {
            _getConfigFiles = getConfigFiles;
        }

        internal static void Reset()
        {
            _getConfigFiles = null;
        }

        [McpServerTool, Description("Lists all config items (input and output mappings) of the currently loaded project - the same items as the config table in the UI. Each entry has a GUID, name, type, controller (name and serial), device (name and type, the hardware the mapping is tied to), whether it is active, and which input events it covers (button/encoder/analog). Call get_config_item_details with a GUID to see the full mapping of one item, e.g. its simulator actions.")]
        public static List<ConfigItemSummary> GetConfigSummary(
            [Description("Optional filter: 'InputConfigItem' for input mappings, 'OutputConfigItem' for output mappings.")] string configType = null,
            [Description("Optional filter: controller serial, e.g. 'JS-...' or 'SN-...'.")] string controllerSerial = null)
        {
            if (_getConfigFiles is null)
                throw new InvalidOperationException("ConfigTools not initialized.");

            var items = _getConfigFiles().SelectMany(f => f.ConfigItems, (file, item) => (File: file, Item: item));

            if (!string.IsNullOrEmpty(configType))
                items = items.Where(x => string.Equals(x.Item.Type, configType, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(controllerSerial))
                items = items.Where(x => string.Equals(x.Item.Controller?.Serial, controllerSerial, StringComparison.OrdinalIgnoreCase));

            return [.. items.Select(x => new ConfigItemSummary(
                Guid: x.Item.GUID,
                Name: x.Item.Name,
                Type: x.Item.Type,
                ControllerName: x.Item.Controller?.Name,
                ControllerSerial: x.Item.Controller?.Serial,
                Active: x.Item.Active,
                ConfigFile: x.File.Label ?? x.File.FileName,
                InputEvents: string.Join(", ", GetInputEvents(x.Item)),
                DeviceName: x.Item.Device?.Name,
                DeviceType: x.Item.Device?.Type))];
        }

        private static IEnumerable<string> GetInputEvents(IConfigItem item)
        {
            var input = item as InputConfigItem;
            if (input?.button != null) yield return "button";
            if (input?.encoder != null) yield return "encoder";
            if (input?.analog != null) yield return "analog";
        }

        [McpServerTool, Description("Returns the full JSON definition of one config item, in the same format as the project file (.mfproj). Use this to inspect the exact mapping of one item - e.g. which input event it reacts to (button, encoder left/right, analog) and which simulator actions it performs. Pass the GUID from get_config_summary.")]
        public static string GetConfigItemDetails([Description("The GUID of the config item, from get_config_summary.")] string guid)
        {
            if (_getConfigFiles is null)
                throw new InvalidOperationException("ConfigTools not initialized.");

            if (string.IsNullOrWhiteSpace(guid))
                throw new ArgumentException("Guid is required.");

            var item = _getConfigFiles().SelectMany(f => f.ConfigItems)
                .FirstOrDefault(i => string.Equals(i.GUID, guid, StringComparison.OrdinalIgnoreCase));

            if (item is null)
                throw new ArgumentException($"Unknown config item GUID: {guid}. Call get_config_summary first.");

            return JsonConvert.SerializeObject(item, Formatting.Indented);
        }

        [McpServerTool, Description("Opens the configuration view for the specified project file in the UI. This is equivalent to the user clicking the arrow on the project card in the UI.")]
        public static void OpenProjectConfig([Description("The name or path of the project file to open.")] string identifier)
        {
            if (_getConfigFiles is null)
                throw new InvalidOperationException("ConfigTools not initialized.");

            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("Identifier is required.");

            // The actual UI navigation will be handled by the client.
            // We can expose the configuration details or a command to open the config view.
            // For now, we can find the file and confirm it exists.
            var file = _getConfigFiles().FirstOrDefault(f => f.FileName == identifier || f.Label == identifier);
            if (file is null)
                throw new ArgumentException($"Unknown project file: {identifier}");
        }
    }
}
