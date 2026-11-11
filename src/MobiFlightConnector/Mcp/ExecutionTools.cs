using MobiFlight.Base;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Linq;

namespace MobiFlight.Mcp
{
    /// <summary>
    /// Mutation and execution tools: change a config item's mapping like the UI does, trigger a
    /// synthetic input event, and save the project. Delegates are injected at startup so the tools
    /// stay unit-testable.
    /// </summary>
    [McpServerToolType]
    public static class ExecutionTools
    {
        private static Func<string, ConfigItem> _findConfigItem;
        private static Func<ConfigItem, bool> _updateConfigItem;
        private static Func<ConfigItem, bool> _addConfigItem;
        private static Func<string, bool> _deleteConfigItem;
        private static Func<bool> _isExecutionStarted;
        private static Action<InputEventArgs> _triggerInputEvent;
        private static Func<(string Error, string FilePath)> _saveProject;

        public static void Initialize(
            Func<string, ConfigItem> findConfigItem,
            Func<ConfigItem, bool> updateConfigItem,
            Func<ConfigItem, bool> addConfigItem,
            Func<string, bool> deleteConfigItem,
            Func<bool> isExecutionStarted,
            Action<InputEventArgs> triggerInputEvent,
            Func<(string Error, string FilePath)> saveProject)
        {
            _findConfigItem = findConfigItem;
            _updateConfigItem = updateConfigItem;
            _addConfigItem = addConfigItem;
            _deleteConfigItem = deleteConfigItem;
            _isExecutionStarted = isExecutionStarted;
            _triggerInputEvent = triggerInputEvent;
            _saveProject = saveProject;
        }

        internal static void Reset()
        {
            _findConfigItem = null;
            _updateConfigItem = null;
            _addConfigItem = null;
            _deleteConfigItem = null;
            _isExecutionStarted = null;
            _triggerInputEvent = null;
            _saveProject = null;
        }

        [McpServerTool, Description("Replaces one config item of the loaded project with the given definition - the same in-memory update the MobiFlight UI performs when editing an item. The change is applied in memory only: it appears in the UI immediately, but the project file on disk is NOT touched, so the user can validate the change first. IMPORTANT: build itemJson by taking the full JSON that get_config_item_details returned for this GUID and changing only the fields you want - keep every other field and the exact structure unchanged, because the whole item is replaced and nested sections have non-obvious shapes. Do not construct the JSON from scratch. \"Type\" must be present and match the existing item type; the \"GUID\" must match the guid parameter (filled in automatically if missing). \"Modifiers\" is an object holding an \"Items\" array, never a plain array, and null means no modifiers; to add one, e.g. a value transformation, use \"Modifiers\": {\"Items\": [{\"Type\": \"Transformation\", \"Active\": true, \"Expression\": \"($ / 10)\"}]} - valid modifier Type values: Blink, Comparison, Interpolation, Padding, Substring, Transformation. Unknown or misspelled fields are REJECTED (the error names the offending property) instead of being silently dropped - type every field name exactly as get_config_item_details returned it. Call get_config_item_template when the item lacks a section you need to add (e.g. \"Modifiers\" is null) or you need a valid simulator action example. The update is applied synchronously: validation or schema errors are returned as a failure here, a success means the item was actually replaced - the UI table updates immediately and the success message lists which fields changed. Do NOT call save_project automatically after an update: saving is the user's decision. After a successful update, tell the user that the change is applied in the UI but NOT saved to the project file yet, and call save_project only when the user explicitly asks to save.")]
        public static ProjectOperationResult UpdateConfigItem(
            [Description("The GUID of the config item to replace, from get_config_summary.")] string guid,
            [Description("The full config item JSON in project-file format, as returned by get_config_item_details, with the desired modifications.")] string itemJson)
        {
            if (_findConfigItem is null || _updateConfigItem is null)
                throw new InvalidOperationException("ExecutionTools not initialized.");

            if (string.IsNullOrWhiteSpace(guid))
                return new ProjectOperationResult(false, "Guid is required.");

            if (string.IsNullOrWhiteSpace(itemJson))
                return new ProjectOperationResult(false, "Item JSON is required.");

            var existing = _findConfigItem(guid);
            if (existing is null)
                return new ProjectOperationResult(false, $"Unknown config item GUID: {guid}. Call get_config_summary first.");

            JObject item;
            try
            {
                item = JObject.Parse(itemJson);
            }
            catch (JsonReaderException ex)
            {
                return new ProjectOperationResult(false, $"Item JSON is not valid JSON: {ex.Message}");
            }

            var jsonType = (string)item["Type"];
            if (string.IsNullOrEmpty(jsonType))
                return new ProjectOperationResult(false, "Item JSON must contain the \"Type\" field (\"InputConfigItem\" or \"OutputConfigItem\"), as returned by get_config_item_details.");

            if (!string.Equals(jsonType, existing.Type, StringComparison.Ordinal))
                return new ProjectOperationResult(false, $"Item JSON \"Type\" is \"{jsonType}\" but the existing item is a \"{existing.Type}\" - the type cannot be changed.");

            var jsonGuid = (string)item["GUID"];
            if (string.IsNullOrEmpty(jsonGuid))
            {
                item["GUID"] = guid;
            }
            else if (!string.Equals(jsonGuid, guid, StringComparison.OrdinalIgnoreCase))
            {
                return new ProjectOperationResult(false, $"Item JSON GUID \"{jsonGuid}\" does not match the guid parameter \"{guid}\".");
            }

            if (!TryDeserializeConfigItem(item, out var newItem, out var schemaError))
                return new ProjectOperationResult(false, schemaError);

            // Apply synchronously on the UI thread (same handler the frontend command runs);
            // exceptions propagate back from form.Invoke, and a false return means the item's
            // GUID vanished (e.g. project was switched meanwhile).
            try
            {
                if (!_updateConfigItem(newItem))
                    return new ProjectOperationResult(false, $"Config item {guid} no longer exists - was the project reloaded?");
            }
            catch (Exception ex)
            {
                Log.Instance.log($"MCP update_config_item failed for {guid}: {ex.Message}", LogSeverity.Error);
                return new ProjectOperationResult(false, $"Applying the update failed: {ex.Message}");
            }

            // Turn "success" into "success + diff": the agent (and the user reading the
            // transcript) can see that exactly the intended fields moved.
            var changes = JsonDiff.ChangedPaths(JObject.FromObject(existing), JObject.FromObject(newItem));
            var changeSummary = changes.Count == 0
                ? "No property values changed."
                : $"Changed: {string.Join(", ", changes.Take(12))}{(changes.Count > 12 ? $" (+{changes.Count - 12} more)" : "")}.";

            return new ProjectOperationResult(true, $"Config item \"{newItem.Name}\" ({guid}) updated. {changeSummary} The change is applied in memory and visible in the UI, but NOT saved to the project file - tell the user; call save_project only when the user asks to save.");
        }

        [McpServerTool, Description("Adds a NEW config item to the loaded project - like the Add button in the UI, but with a complete definition. Build itemJson from get_config_item_template (input_button, input_encoder, input_analog or output) or from a similar existing item from get_config_item_details, then change the fields: leave \"GUID\" out (one is generated) or use a new unused GUID; set Name, Controller and Device, and for inputs the matching \"button\"/\"encoder\"/\"analog\" section with at least one action. The Device Type must be Button, Encoder or AnalogInput and must match the populated section; the Controller serial/name and Device name should exist on the connected hardware (use list_connected_controllers to find them). The item appears in the UI immediately but is NOT saved to disk - tell the user; call save_project only when the user asks to save. Only create items when the user asks; to change an existing mapping use update_config_item.")]
        public static ProjectOperationResult CreateConfigItem(
            [Description("The full JSON of the new config item (same format as get_config_item_details). \"Type\", Controller, Device and the input action section are required; GUID is optional.")] string itemJson)
        {
            if (_findConfigItem is null || _addConfigItem is null)
                throw new InvalidOperationException("ExecutionTools not initialized.");

            if (string.IsNullOrWhiteSpace(itemJson))
                return new ProjectOperationResult(false, "Item JSON is required.");

            JObject item;
            try
            {
                item = JObject.Parse(itemJson);
            }
            catch (JsonReaderException ex)
            {
                return new ProjectOperationResult(false, $"Item JSON is not valid JSON: {ex.Message}");
            }

            var jsonType = (string)item["Type"];
            if (string.IsNullOrEmpty(jsonType))
                return new ProjectOperationResult(false, "Item JSON must contain the \"Type\" field (\"InputConfigItem\" or \"OutputConfigItem\"), as returned by get_config_item_template.");

            var guid = (string)item["GUID"];
            if (string.IsNullOrWhiteSpace(guid))
            {
                guid = Guid.NewGuid().ToString();
                item["GUID"] = guid;
            }
            else if (_findConfigItem(guid) is not null)
            {
                return new ProjectOperationResult(false, $"A config item with GUID {guid} already exists - use update_config_item to change it instead of creating a new item.");
            }

            if (!TryDeserializeConfigItem(item, out var newItem, out var schemaError))
                return new ProjectOperationResult(false, schemaError);

            if (string.IsNullOrWhiteSpace(newItem.Name))
                newItem.Name = "New Config";

            if (newItem is InputConfigItem inputItem)
            {
                var inputError = ValidateInputConfigItem(inputItem);
                if (inputError is not null)
                    return new ProjectOperationResult(false, inputError);
            }

            if (!_addConfigItem(newItem))
                return new ProjectOperationResult(false, $"Adding the config item failed - the project has no active config file, or a config item with GUID {guid} already exists.");

            return new ProjectOperationResult(true, $"Config item \"{newItem.Name}\" ({guid}) created. It is applied in memory and visible in the UI, but NOT saved to the project file - tell the user; call save_project only when the user asks to save.");
        }

        private static string ValidateInputConfigItem(InputConfigItem item)
        {
            if (item.Controller is null)
                return "Input config items need a \"Controller\" with the serial and name of the connected controller (see list_connected_controllers).";

            if (string.IsNullOrEmpty(item.Device?.Type))
                return "Input config items need a \"Device\" with \"Type\" and \"Name\" (the physical input).";

            switch (item.Device.Type)
            {
                case MobiFlightButton.TYPE:
                    if (item.button is null)
                        return "Device Type is \"Button\": the item needs a populated \"button\" section (onPress/onRelease/onHold/onLongRelease).";
                    if (item.button.onPress is null && item.button.onRelease is null && item.button.onHold is null && item.button.onLongRelease is null)
                        return "The \"button\" section has no actions - set at least one of onPress, onRelease, onHold or onLongRelease.";
                    break;

                case MobiFlightEncoder.TYPE:
                    if (item.encoder is null)
                        return "Device Type is \"Encoder\": the item needs a populated \"encoder\" section (onLeft/onRight, optionally onLeftFast/onRightFast).";
                    if (item.encoder.onLeft is null && item.encoder.onRight is null && item.encoder.onLeftFast is null && item.encoder.onRightFast is null)
                        return "The \"encoder\" section has no actions - set at least one of onLeft, onRight, onLeftFast or onRightFast.";
                    break;

                case MobiFlightAnalogInput.TYPE:
                    if (item.analog is null || item.analog.onChange is null)
                        return "Device Type is \"AnalogInput\": the item needs an \"analog\" section with an onChange action.";
                    break;

                default:
                    return $"Unsupported input device Type \"{item.Device.Type}\" - use Button, Encoder or AnalogInput.";
            }

            return null;
        }

        [McpServerTool, Description("Deletes one config item from the loaded project - like the delete action in the UI. Only call this when the user explicitly asks to delete a mapping, and name the item in your confirmation. The deletion is applied in memory and the item disappears from the UI immediately, but the project file is NOT touched until save_project is called - the user can revert it by not saving (reopening the project restores the file) or by asking you to recreate it. Tell the user it is not saved; call save_project only when the user asks to save.")]
        public static ProjectOperationResult DeleteConfigItem(
            [Description("The GUID of the config item to delete, from get_config_summary.")] string guid)
        {
            if (_findConfigItem is null || _deleteConfigItem is null)
                throw new InvalidOperationException("ExecutionTools not initialized.");

            if (string.IsNullOrWhiteSpace(guid))
                return new ProjectOperationResult(false, "Guid is required.");

            var existing = _findConfigItem(guid);
            if (existing is null)
                return new ProjectOperationResult(false, $"Unknown config item GUID: {guid}. Call get_config_summary first.");

            if (!_deleteConfigItem(guid))
                return new ProjectOperationResult(false, $"Config item {guid} no longer exists - was the project reloaded?");

            return new ProjectOperationResult(true, $"Config item \"{existing.Name}\" ({guid}) deleted. The deletion is applied in memory and the item is gone from the UI, but the project file is NOT touched - tell the user; call save_project only when the user asks to save (reopening the project without saving reverts the deletion).");
        }

        [McpServerTool, Description("Triggers a synthetic input event for a config item's hardware, as if the physical button/encoder/analog input fired: every active config bound to the same controller and device executes its configured action for that event against the simulator. The execution engine must be running (the Run state in the UI), otherwise nothing happens. Buttons support 'press' and 'release' (call both for a full click); encoders support 'left', 'right', 'leftFast', 'rightFast'; analog inputs support 'change' with a numeric value (analog events carry a position value; modifiers configured on the item are applied as usual). Note: onHold/onLongRelease-classified button actions cannot be triggered by this tool.")]
        public static ProjectOperationResult ExecuteInputAction(
            [Description("The GUID of an input config item, from get_config_summary.")] string guid,
            [Description("The input event to fire: buttons: 'press', 'release'; encoders: 'left', 'right', 'leftFast', 'rightFast'; analog inputs: 'change'.")] string eventType,
            [Description("Required for analog inputs ('change'): the position value of the analog input, e.g. 0-1023.")] double? value = null)
        {
            if (_findConfigItem is null || _isExecutionStarted is null || _triggerInputEvent is null)
                throw new InvalidOperationException("ExecutionTools not initialized.");

            if (string.IsNullOrWhiteSpace(guid))
                return new ProjectOperationResult(false, "Guid is required.");

            if (string.IsNullOrWhiteSpace(eventType))
                return new ProjectOperationResult(false, "Event type is required.");

            var item = _findConfigItem(guid) as InputConfigItem;
            if (item is null)
                return new ProjectOperationResult(false, $"Unknown input config item GUID: {guid}. Call get_config_summary first.");

            if (item.Controller is null)
                return new ProjectOperationResult(false, $"Config item \"{item.Name}\" has no controller assigned.");

            if (string.IsNullOrEmpty(item.Device?.Type))
                return new ProjectOperationResult(false, $"Config item \"{item.Name}\" has no input device assigned.");

            DeviceType inputType;
            double eventValue;
            switch (item.Device.Type)
            {
                case MobiFlightButton.TYPE:
                    inputType = DeviceType.Button;
                    switch (eventType.ToLowerInvariant())
                    {
                        case "press": eventValue = (int)MobiFlightButton.InputEvent.PRESS; break;
                        case "release": eventValue = (int)MobiFlightButton.InputEvent.RELEASE; break;
                        default:
                            return new ProjectOperationResult(false, $"Config item \"{item.Name}\" is a button: event type must be 'press' or 'release', got '{eventType}'.");
                    }
                    break;

                case MobiFlightEncoder.TYPE:
                    inputType = DeviceType.Encoder;
                    switch (eventType.ToLowerInvariant())
                    {
                        case "left": eventValue = (int)MobiFlightEncoder.InputEvent.LEFT; break;
                        case "leftfast": eventValue = (int)MobiFlightEncoder.InputEvent.LEFT_FAST; break;
                        case "right": eventValue = (int)MobiFlightEncoder.InputEvent.RIGHT; break;
                        case "rightfast": eventValue = (int)MobiFlightEncoder.InputEvent.RIGHT_FAST; break;
                        default:
                            return new ProjectOperationResult(false, $"Config item \"{item.Name}\" is an encoder: event type must be 'left', 'right', 'leftFast' or 'rightFast', got '{eventType}'.");
                    }
                    break;

                case MobiFlightAnalogInput.TYPE:
                    inputType = DeviceType.AnalogInput;
                    if (!string.Equals(eventType, "change", StringComparison.OrdinalIgnoreCase))
                        return new ProjectOperationResult(false, $"Config item \"{item.Name}\" is an analog input: event type must be 'change', got '{eventType}'.");
                    if (value is null)
                        return new ProjectOperationResult(false, $"Config item \"{item.Name}\" is an analog input: a numeric value is required for 'change'.");
                    eventValue = value.Value;
                    break;

                default:
                    return new ProjectOperationResult(false, $"Config item \"{item.Name}\" is bound to unsupported input device type '{item.Device.Type}'.");
            }

            if (!_isExecutionStarted())
                return new ProjectOperationResult(false, "The execution engine is not running - actions are skipped while MobiFlight is stopped. Call get_runtime_status; if the user agrees, start it with start_project_execution (or the Run button) and retry.");

            // Mirror what real device events look like (see e.g. MobiFlightModule.OnButtonChange):
            // controller serial/name and device name/type/label from the config item, so the
            // serial + device-name matching in the execution engine finds the right configs.
            var e = new InputEventArgs
            {
                Controller = item.Controller.Clone() as Controller,
                Device = new DeviceReference { Type = inputType, Name = item.Device.Name, Label = item.Device.Name },
                InputType = inputType,
                Value = eventValue
            };

            _triggerInputEvent(e);

            return new ProjectOperationResult(true, $"{e.GetMsgEventLabel()} executed. Check get_log for what ran.");
        }

        [McpServerTool, Description("Saves the currently loaded project to its file on disk - the equivalent of File > Save in the UI. Fails if the loaded project has never been saved (no file path). Call this ONLY when the user explicitly asks to save/persist the project; do NOT call it automatically after update_config_item. Updates are already applied in memory and visible in the UI while unsaved, so the user can validate them before deciding to save.")]
        public static ProjectOperationResult SaveProject()
        {
            if (_saveProject is null)
                throw new InvalidOperationException("ExecutionTools not initialized.");

            var (error, filePath) = _saveProject();
            if (error is not null)
                return new ProjectOperationResult(false, error);

            return new ProjectOperationResult(true, $"Project saved to {filePath}.");
        }

        /// <summary>
        /// Deserializes agent-supplied item JSON. MissingMemberHandling.Error is load-bearing:
        /// without it a typo'd field name deserializes successfully and the intended change
        /// silently disappears. Frontend commands stay lenient; only the MCP tools are strict.
        /// </summary>
        private static bool TryDeserializeConfigItem(JObject item, out ConfigItem configItem, out string error)
        {
            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error
            };

            try
            {
                configItem = JsonConvert.DeserializeObject<ConfigItem>(item.ToString(), settings);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                configItem = null;
                error = $"Item JSON does not match the config item schema: {ex.Message}";
                return false;
            }
        }
    }
}
