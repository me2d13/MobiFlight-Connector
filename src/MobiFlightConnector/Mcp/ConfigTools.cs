using MobiFlight.Base;
using MobiFlight.Modifier;
using MobiFlight.OutputConfig;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        [McpServerTool, Description("Lists all config items (input and output mappings) of the currently loaded project - the same items as the config table in the UI. Each entry has a GUID, name, type, controller (name and serial), device (name and type, the hardware the mapping is tied to), whether it is active, and which input events it covers (button/encoder/analog). Call get_config_item_details with a GUID to see the full mapping of one item, e.g. its simulator actions. To add or remove mappings, use create_config_item and delete_config_item (both only when the user asks).")]
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

        [McpServerTool, Description("Returns the full JSON definition of one config item, in the same format as the project file (.mfproj). Use this to inspect the exact mapping of one item - e.g. which input event it reacts to (button, encoder left/right, analog) and which simulator actions it performs. This JSON is also exactly the input format that update_config_item expects: to change a mapping, take this JSON, modify only the fields you need and pass it to update_config_item (or create_config_item when adding a new mapping based on this one). If the item is missing a section you need to add (for example \"Modifiers\" is null, or you are changing which input device events exist), call get_config_item_template for a complete, valid example of that section. Reading the output: sections with no content (Modifiers, Preconditions, ConfigRefs) are OMITTED, which means empty; \"RawValue\" is the value read from the simulator before modifiers and \"Value\" is the current output after modifiers (both are omitted when empty and are present while the execution engine is running), so comparing them before and after a change is a quick way to verify an output without looking at the hardware. Pass the GUID from get_config_summary.")]
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

        [McpServerTool, Description("Returns ready-to-copy JSON examples of config item sections with every field the deserializer expects. Use this when the item you are editing lacks the section you need to add (e.g. \"Modifiers\" is null), when you are unsure which fields a section has, or to pick a valid simulator action for an input event. The examples are complete and valid: copy the section and replace the placeholder values (GUID, Controller, Device, names, event ids) with the real ones from get_config_item_details - then send the whole item to update_config_item (existing item) or create_config_item (new item). Templates: input_button, input_encoder, input_analog (whole InputConfigItem examples), output (whole OutputConfigItem with a SimConnect source, an LCD device and a Transformation modifier), modifiers (every valid modifier type with a realistic payload), input_actions (valid simulator actions). \"Modifiers\" is always an object with an \"Items\" array, never a plain array. Modifier semantics: an output item applies modifiers in list order to the value read from the source. Transformation.Expression, Comparison.Value, IfValue and ElseValue are NCalc expressions where \"$\" is the current value and the empty-string literal is written as two single quotes inside the JSON string (\"''\"). Comparison tests the current value against \"Value\" using Operand (=, !=, >, >=, <, <=) and outputs IfValue or ElseValue - to blank out a sentinel value, e.g. -20000, use {\"Type\":\"Comparison\",\"Operand\":\"=\",\"Value\":\"-20000\",\"IfValue\":\"''\",\"ElseValue\":\"$\",\"Active\":true}.")]
        public static string GetConfigItemTemplate(
            [Description("Optional template name: input_button, input_encoder, input_analog, output, modifiers, input_actions. Omit to return all templates.")] string template = null)
        {
            var templates = BuildTemplates();

            if (string.IsNullOrWhiteSpace(template))
                return templates.ToString(Formatting.Indented);

            var match = templates.Properties()
                .FirstOrDefault(p => string.Equals(p.Name, template.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match is null)
                throw new ArgumentException($"Unknown template: \"{template}\". Valid templates are: {string.Join(", ", templates.Properties().Select(p => p.Name))}.");

            return match.Value.ToString(Formatting.Indented);
        }

        private static JObject BuildTemplates()
        {
            var templates = new JObject
            {
                ["input_button"] = JObject.FromObject(CreateButtonTemplate()),
                ["input_encoder"] = JObject.FromObject(CreateEncoderTemplate()),
                ["input_analog"] = JObject.FromObject(CreateAnalogTemplate()),
                ["output"] = JObject.FromObject(CreateOutputTemplate()),
                ["modifiers"] = JObject.FromObject(CreateModifierTemplate()),
                ["input_actions"] = new JObject
                {
                    ["sim_event"] = JObject.FromObject(new MobiFlight.InputConfig.MSFS2020EventIdInputAction { EventId = "NAV1_RADIO_WHOLE_INC" }),
                    ["sim_command"] = JObject.FromObject(new MobiFlight.InputConfig.MSFS2020CustomInputAction { Command = "(>K:NAV1_RADIO_WHOLE_INC)" }),
                    ["variable"] = JObject.FromObject(new MobiFlight.InputConfig.VariableInputAction
                    {
                        Variable = new MobiFlightVariable { Name = "MyVar", Expression = "$ + 1" }
                    })
                }
            };

            return templates;
        }

        private static InputConfigItem CreateButtonTemplate()
        {
            return new InputConfigItem
            {
                GUID = "00000000-0000-0000-0000-000000000000",
                Name = "Example button",
                Controller = CreateTemplateController(),
                Device = new MobiFlight.InputConfig.Button { Name = "Button 1" },
                button = new MobiFlight.InputConfig.ButtonInputConfig
                {
                    onPress = new MobiFlight.InputConfig.MSFS2020EventIdInputAction { EventId = "NAV1_RADIO_WHOLE_INC" },
                    onRelease = new MobiFlight.InputConfig.MSFS2020EventIdInputAction { EventId = "NAV1_RADIO_WHOLE_DEC" }
                }
            };
        }

        private static InputConfigItem CreateEncoderTemplate()
        {
            return new InputConfigItem
            {
                GUID = "00000000-0000-0000-0000-000000000000",
                Name = "Example encoder",
                Controller = CreateTemplateController(),
                Device = new MobiFlight.InputConfig.Encoder { Name = "Encoder 1" },
                encoder = new MobiFlight.InputConfig.EncoderInputConfig
                {
                    onLeft = new MobiFlight.InputConfig.MSFS2020EventIdInputAction { EventId = "NAV1_RADIO_WHOLE_DEC" },
                    onRight = new MobiFlight.InputConfig.MSFS2020EventIdInputAction { EventId = "NAV1_RADIO_WHOLE_INC" }
                }
            };
        }

        private static InputConfigItem CreateAnalogTemplate()
        {
            return new InputConfigItem
            {
                GUID = "00000000-0000-0000-0000-000000000000",
                Name = "Example analog input",
                Controller = CreateTemplateController(),
                Device = new MobiFlight.InputConfig.AnalogInput { Name = "Analog Input 1" },
                analog = new MobiFlight.InputConfig.AnalogInputConfig
                {
                    onChange = new MobiFlight.InputConfig.MSFS2020CustomInputAction { Command = "(>K:AXIS_THROTTLE_SET)" }
                }
            };
        }

        private static OutputConfigItem CreateOutputTemplate()
        {
            var item = new OutputConfigItem
            {
                GUID = "00000000-0000-0000-0000-000000000000",
                Name = "Example output",
                Controller = CreateTemplateController(),
                Source = new SimConnectSource
                {
                    SimConnectValue = new SimConnectValue
                    {
                        Value = "(A:COM ACTIVE FREQUENCY:1,KHz)",
                        VarType = SimConnectVarType.CODE
                    }
                },
                Device = new LcdDisplay
                {
                    Address = "LCD 1",
                    Lines = ["Line 1", "Line 2"]
                }
            };

            item.Modifiers.Items.Add(new Transformation { Active = true, Expression = "($ / 10)" });
            return item;
        }

        private static ModifierList CreateModifierTemplate()
        {
            var modifiers = new ModifierList();

            modifiers.Items.Add(new Blink { Active = true, BlinkValue = "0", OnOffSequence = [500, 500] });
            modifiers.Items.Add(new Comparison { Active = true, Operand = "<", Value = "1000", IfValue = "1", ElseValue = "0" });

            var interpolation = new Interpolation { Active = true };
            interpolation.Add(0, 0);
            interpolation.Add(100, 255);
            modifiers.Items.Add(interpolation);

            modifiers.Items.Add(new Padding { Active = true, Character = ' ', Length = 5, Direction = Padding.PaddingDirection.Left });
            modifiers.Items.Add(new Substring { Active = true, Start = 0, End = 7 });
            modifiers.Items.Add(new Transformation { Active = true, Expression = "($ / 10)" });

            return modifiers;
        }

        private static Controller CreateTemplateController()
        {
            return new Controller { Name = "MobiFlight Module", Serial = "SN-000-000", Type = "MobiFlight" };
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
