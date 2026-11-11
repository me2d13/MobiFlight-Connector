using MobiFlight.Base;
using MobiFlight.InputConfig;
using MobiFlight.Mcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace MobiFlight.Mcp.Tests
{
    [TestClass()]
    public class ExecutionToolsTests
    {
        private List<ConfigItem> _updatedItems;
        private List<ConfigItem> _addedItems;
        private List<string> _deletedGuids;
        private List<InputEventArgs> _triggeredEvents;
        private bool _isStarted;
        private string _savedFilePath;
        private string _saveError;

        [TestInitialize]
        public void SetUp()
        {
            _updatedItems = [];
            _addedItems = [];
            _deletedGuids = [];
            _triggeredEvents = [];
            _isStarted = true;
            _savedFilePath = null;
            _saveError = null;
        }

        [TestCleanup]
        public void TearDown()
        {
            ExecutionTools.Reset();
        }

        private InputConfigItem CreateButtonItem(string guid = "g1", string serial = "SN-MOD-1")
        {
            return new InputConfigItem
            {
                GUID = guid,
                Name = "Test item",
                Controller = new Controller { Name = "Test Module", Serial = serial },
                Device = new MobiFlight.InputConfig.Button { Name = "BTN1" },
                button = new ButtonInputConfig()
            };
        }

        private void InitializeTools(
            Func<string, ConfigItem> findConfigItem = null,
            Func<ConfigItem, bool> updateConfigItem = null,
            Func<ConfigItem, bool> addConfigItem = null,
            Func<string, bool> deleteConfigItem = null)
        {
            ExecutionTools.Initialize(
                findConfigItem: findConfigItem ?? (guid => CreateButtonItem(guid)),
                updateConfigItem: updateConfigItem ?? (item => { _updatedItems.Add(item); return true; }),
                addConfigItem: addConfigItem ?? (item => { _addedItems.Add(item); return true; }),
                deleteConfigItem: deleteConfigItem ?? (guid => { _deletedGuids.Add(guid); return true; }),
                isExecutionStarted: () => _isStarted,
                triggerInputEvent: e => _triggeredEvents.Add(e),
                saveProject: () => (_saveError, _savedFilePath));
        }

        #region UpdateConfigItem

        [TestMethod()]
        public void UpdateConfigItem_PassesDeserializedItemToHandler_WhenJsonIsValid()
        {
            // Arrange
            InitializeTools();
            var itemJson = "{\"Type\":\"InputConfigItem\",\"GUID\":\"g1\",\"Name\":\"Renamed\"}";

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", itemJson);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.HasCount(1, _updatedItems);
            var item = Assert.IsInstanceOfType<InputConfigItem>(_updatedItems[0]);
            Assert.AreEqual("g1", item.GUID);
            Assert.AreEqual("Renamed", item.Name);
        }

        [TestMethod()]
        public void UpdateConfigItem_FillsMissingGuid_FromParameter()
        {
            // Arrange
            InitializeTools();
            var itemJson = "{\"Type\":\"InputConfigItem\",\"Name\":\"Renamed\"}";

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", itemJson);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.HasCount(1, _updatedItems);
            Assert.AreEqual("g1", _updatedItems[0].GUID);
        }

        [TestMethod()]
        public void UpdateConfigItem_Fails_WhenGuidUnknown()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => null);

            // Act
            var result = ExecutionTools.UpdateConfigItem("unknown", "{\"Type\":\"InputConfigItem\"}");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("Unknown config item GUID", result.Message);
            Assert.IsEmpty(_updatedItems);
        }

        [TestMethod()]
        public void UpdateConfigItem_Fails_WhenJsonInvalid()
        {
            // Arrange
            InitializeTools();

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", "{not json");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("not valid JSON", result.Message);
            Assert.IsEmpty(_updatedItems);
        }

        [TestMethod()]
        public void UpdateConfigItem_Fails_WhenPayloadDoesNotMatchSchema()
        {
            // Arrange
            InitializeTools();
            // Modifiers must be an object ({"Items": [...]}), not an array - the exact mistake
            // that used to fail silently on the UI thread before the update became synchronous.
            var itemJson = "{\"Type\":\"InputConfigItem\",\"GUID\":\"g1\",\"Modifiers\":[]}";

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", itemJson);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("does not match the config item schema", result.Message);
            Assert.IsEmpty(_updatedItems);
        }

        [TestMethod()]
        public void UpdateConfigItem_Fails_WhenHandlerReportsItemGone()
        {
            // Arrange
            InitializeTools(updateConfigItem: item => false);

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", "{\"Type\":\"InputConfigItem\",\"GUID\":\"g1\"}");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("no longer exists", result.Message);
        }

        [TestMethod()]
        public void UpdateConfigItem_Fails_WhenHandlerThrows()
        {
            // Arrange
            InitializeTools(updateConfigItem: item => throw new InvalidOperationException("boom"));

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", "{\"Type\":\"InputConfigItem\",\"GUID\":\"g1\"}");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("Applying the update failed", result.Message);
            Assert.Contains("boom", result.Message);
        }

        [TestMethod()]
        public void UpdateConfigItem_Fails_WhenTypeMissing()
        {
            // Arrange
            InitializeTools();

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", "{\"GUID\":\"g1\",\"Name\":\"Renamed\"}");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("\"Type\"", result.Message);
            Assert.IsEmpty(_updatedItems);
        }

        [TestMethod()]
        public void UpdateConfigItem_Fails_WhenTypeChanged()
        {
            // Arrange
            InitializeTools();

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", "{\"Type\":\"OutputConfigItem\",\"GUID\":\"g1\"}");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("cannot be changed", result.Message);
            Assert.IsEmpty(_updatedItems);
        }

        [TestMethod()]
        public void UpdateConfigItem_Fails_WhenGuidMismatched()
        {
            // Arrange
            InitializeTools();

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", "{\"Type\":\"InputConfigItem\",\"GUID\":\"other\"}");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("does not match", result.Message);
            Assert.IsEmpty(_updatedItems);
        }

        [TestMethod()]
        public void UpdateConfigItem_Fails_WhenGuidOrJsonEmpty()
        {
            // Arrange
            InitializeTools();

            // Act
            var noGuid = ExecutionTools.UpdateConfigItem("", "{\"Type\":\"InputConfigItem\"}");
            var noJson = ExecutionTools.UpdateConfigItem("g1", "  ");

            // Assert
            Assert.IsFalse(noGuid.Success);
            Assert.IsFalse(noJson.Success);
            Assert.IsEmpty(_updatedItems);
        }

        [TestMethod()]
        public void UpdateConfigItem_Fails_WhenNestedFieldNameIsUnknown()
        {
            // Arrange
            InitializeTools();
            // "Expressionn" is a typo for "Expression". Newtonsoft silently ignores unknown
            // fields by default, which would make this update succeed without changing anything.
            var itemJson = "{\"Type\":\"InputConfigItem\",\"GUID\":\"g1\",\"Modifiers\":{\"Items\":[{\"Type\":\"Transformation\",\"Active\":true,\"Expressionn\":\"($ / 10)\"}]}}";

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", itemJson);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("Expressionn", result.Message);
            Assert.IsEmpty(_updatedItems);
        }

        [TestMethod()]
        public void UpdateConfigItem_ReportsChangedFields_OnSuccess()
        {
            // Arrange
            InitializeTools();
            var item = CreateButtonItem();
            var itemJson = JObject.Parse(JsonConvert.SerializeObject(item));
            itemJson["Name"] = "Renamed";

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", itemJson.ToString());

            // Assert
            Assert.IsTrue(result.Success);
            Assert.Contains("Changed: Name", result.Message);
        }

        [TestMethod()]
        public void UpdateConfigItem_ReportsNoChanges_WhenJsonMatchesItem()
        {
            // Arrange
            InitializeTools();
            var item = CreateButtonItem();
            var itemJson = JsonConvert.SerializeObject(item);

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", itemJson);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.Contains("No property values changed", result.Message);
        }

        [TestMethod()]
        public void UpdateConfigItem_TellsCallerChangeIsNotSaved()
        {
            // Arrange
            InitializeTools();
            var itemJson = JsonConvert.SerializeObject(CreateButtonItem());

            // Act
            var result = ExecutionTools.UpdateConfigItem("g1", itemJson);

            // Assert
            // Saving is the user's decision - the tool must not nudge the agent to save automatically.
            Assert.IsTrue(result.Success);
            Assert.Contains("NOT saved", result.Message);
            Assert.Contains("only when the user asks to save", result.Message);
        }

        [TestMethod()]
        public void UpdateConfigItem_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            ExecutionTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => ExecutionTools.UpdateConfigItem("g1", "{}"));
        }

        #endregion

        #region CreateConfigItem

        private InputConfigItem CreateCompleteButtonItem(string guid = "new-1")
        {
            var item = CreateButtonItem(guid);
            item.button.onPress = new MSFS2020CustomInputAction { Command = "(>K:TEST_COMMAND)" };
            return item;
        }

        [TestMethod()]
        public void CreateConfigItem_AddsDeserializedItem_WhenJsonIsValid()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => null);
            var item = CreateCompleteButtonItem();

            // Act
            var result = ExecutionTools.CreateConfigItem(JsonConvert.SerializeObject(item));

            // Assert
            Assert.IsTrue(result.Success);
            Assert.HasCount(1, _addedItems);
            var added = Assert.IsInstanceOfType<InputConfigItem>(_addedItems[0]);
            Assert.AreEqual("new-1", added.GUID);
            Assert.IsNotNull(added.button.onPress);
        }

        [TestMethod()]
        public void CreateConfigItem_GeneratesGuid_WhenMissing()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => null);
            var json = JObject.Parse(JsonConvert.SerializeObject(CreateCompleteButtonItem()));
            json.Remove("GUID");

            // Act
            var result = ExecutionTools.CreateConfigItem(json.ToString());

            // Assert
            Assert.IsTrue(result.Success);
            Assert.HasCount(1, _addedItems);
            Assert.IsFalse(string.IsNullOrWhiteSpace(_addedItems[0].GUID));
            Assert.Contains(_addedItems[0].GUID, result.Message);
        }

        [TestMethod()]
        public void CreateConfigItem_Fails_WhenGuidAlreadyExists()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => CreateButtonItem(guid));

            // Act
            var result = ExecutionTools.CreateConfigItem(JsonConvert.SerializeObject(CreateCompleteButtonItem("existing")));

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("already exists", result.Message);
            Assert.Contains("update_config_item", result.Message);
            Assert.IsEmpty(_addedItems);
        }

        [TestMethod()]
        public void CreateConfigItem_Fails_WhenInputHasNoController()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => null);
            var item = CreateCompleteButtonItem();
            item.Controller = null;

            // Act
            var result = ExecutionTools.CreateConfigItem(JsonConvert.SerializeObject(item));

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("Controller", result.Message);
            Assert.IsEmpty(_addedItems);
        }

        [TestMethod()]
        public void CreateConfigItem_Fails_WhenInputHasNoActions()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => null);

            // Act
            var result = ExecutionTools.CreateConfigItem(JsonConvert.SerializeObject(CreateButtonItem()));

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("no actions", result.Message);
            Assert.IsEmpty(_addedItems);
        }

        [TestMethod()]
        public void CreateConfigItem_TellsCallerChangeIsNotSaved()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => null);

            // Act
            var result = ExecutionTools.CreateConfigItem(JsonConvert.SerializeObject(CreateCompleteButtonItem()));

            // Assert
            Assert.IsTrue(result.Success);
            Assert.Contains("NOT saved", result.Message);
            Assert.Contains("only when the user asks to save", result.Message);
        }

        [TestMethod()]
        public void CreateConfigItem_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            ExecutionTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => ExecutionTools.CreateConfigItem("{}"));
        }

        #endregion

        #region DeleteConfigItem

        [TestMethod()]
        public void DeleteConfigItem_RemovesItemAndReportsUnsaved()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => CreateButtonItem(guid));

            // Act
            var result = ExecutionTools.DeleteConfigItem("g1");

            // Assert
            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(new[] { "g1" }, _deletedGuids);
            Assert.Contains("deleted", result.Message);
            Assert.Contains("NOT touched", result.Message);
            Assert.Contains("only when the user asks to save", result.Message);
        }

        [TestMethod()]
        public void DeleteConfigItem_Fails_WhenGuidUnknown()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => null);

            // Act
            var result = ExecutionTools.DeleteConfigItem("unknown");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("Unknown config item GUID", result.Message);
            Assert.IsEmpty(_deletedGuids);
        }

        [TestMethod()]
        public void DeleteConfigItem_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            ExecutionTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => ExecutionTools.DeleteConfigItem("g1"));
        }

        #endregion

        #region ExecuteInputAction

        [TestMethod()]
        public void ExecuteInputAction_TriggersButtonPress_WithRealDeviceEventShape()
        {
            // Arrange
            InitializeTools();

            // Act
            var result = ExecutionTools.ExecuteInputAction("g1", "press");

            // Assert
            Assert.IsTrue(result.Success);
            Assert.HasCount(1, _triggeredEvents);
            var e = _triggeredEvents[0];
            Assert.AreEqual(DeviceType.Button, e.InputType);
            Assert.AreEqual((int)MobiFlightButton.InputEvent.PRESS, (int)e.Value);
            Assert.AreEqual("SN-MOD-1", e.Controller.Serial);
            Assert.AreEqual("BTN1", e.Device.Name);
            Assert.AreEqual("BTN1", e.Device.Label);
        }

        [TestMethod()]
        public void ExecuteInputAction_TriggersEncoderRightFast()
        {
            // Arrange
            var encoderItem = new InputConfigItem
            {
                GUID = "g1",
                Name = "Test encoder",
                Controller = new Controller { Name = "Test Module", Serial = "SN-MOD-1" },
                Device = new MobiFlight.InputConfig.Encoder { Name = "ENC1" },
                encoder = new EncoderInputConfig()
            };
            InitializeTools(findConfigItem: guid => encoderItem);

            // Act
            var result = ExecutionTools.ExecuteInputAction("g1", "rightFast");

            // Assert
            Assert.IsTrue(result.Success);
            Assert.HasCount(1, _triggeredEvents);
            Assert.AreEqual(DeviceType.Encoder, _triggeredEvents[0].InputType);
            Assert.AreEqual((int)MobiFlightEncoder.InputEvent.RIGHT_FAST, (int)_triggeredEvents[0].Value);
        }

        [TestMethod()]
        public void ExecuteInputAction_TriggersAnalogChange_WithValue()
        {
            // Arrange
            var analogItem = new InputConfigItem
            {
                GUID = "g1",
                Name = "Test analog",
                Controller = new Controller { Name = "Test Module", Serial = "SN-MOD-1" },
                Device = new MobiFlight.InputConfig.AnalogInput { Name = "AI1" },
                analog = new AnalogInputConfig()
            };
            InitializeTools(findConfigItem: guid => analogItem);

            // Act
            var result = ExecutionTools.ExecuteInputAction("g1", "change", value: 512);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.HasCount(1, _triggeredEvents);
            Assert.AreEqual(DeviceType.AnalogInput, _triggeredEvents[0].InputType);
            Assert.AreEqual(512, _triggeredEvents[0].Value);
        }

        [TestMethod()]
        public void ExecuteInputAction_Fails_WhenGuidUnknown()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => null);

            // Act
            var result = ExecutionTools.ExecuteInputAction("unknown", "press");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("Unknown input config item", result.Message);
            Assert.IsEmpty(_triggeredEvents);
        }

        [TestMethod()]
        public void ExecuteInputAction_Fails_WhenItemIsOutputConfig()
        {
            // Arrange
            InitializeTools(findConfigItem: guid => new OutputConfigItem { GUID = "g1", Name = "Out" });

            // Act
            var result = ExecutionTools.ExecuteInputAction("g1", "press");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("Unknown input config item", result.Message);
            Assert.IsEmpty(_triggeredEvents);
        }

        [TestMethod()]
        public void ExecuteInputAction_Fails_WhenItemHasNoController()
        {
            // Arrange
            var item = CreateButtonItem();
            item.Controller = null;
            InitializeTools(findConfigItem: guid => item);

            // Act
            var result = ExecutionTools.ExecuteInputAction("g1", "press");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("no controller assigned", result.Message);
            Assert.IsEmpty(_triggeredEvents);
        }

        [TestMethod()]
        public void ExecuteInputAction_Fails_WhenEventTypeInvalidForDevice()
        {
            // Arrange
            InitializeTools();

            // Act
            var buttonResult = ExecutionTools.ExecuteInputAction("g1", "left");
            var changeResult = ExecutionTools.ExecuteInputAction("g1", "change");

            // Assert
            Assert.IsFalse(buttonResult.Success);
            Assert.Contains("must be 'press' or 'release'", buttonResult.Message);
            Assert.IsFalse(changeResult.Success);
            Assert.Contains("must be 'press' or 'release'", changeResult.Message);
            Assert.IsEmpty(_triggeredEvents);
        }

        [TestMethod()]
        public void ExecuteInputAction_Fails_WhenAnalogValueMissing()
        {
            // Arrange
            var analogItem = new InputConfigItem
            {
                GUID = "g1",
                Name = "Test analog",
                Controller = new Controller { Name = "Test Module", Serial = "SN-MOD-1" },
                Device = new MobiFlight.InputConfig.AnalogInput { Name = "AI1" },
                analog = new AnalogInputConfig()
            };
            InitializeTools(findConfigItem: guid => analogItem);

            // Act
            var result = ExecutionTools.ExecuteInputAction("g1", "change");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("value is required", result.Message);
            Assert.IsEmpty(_triggeredEvents);
        }

        [TestMethod()]
        public void ExecuteInputAction_Fails_WhenExecutionNotStarted()
        {
            // Arrange
            InitializeTools();
            _isStarted = false;

            // Act
            var result = ExecutionTools.ExecuteInputAction("g1", "press");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("not running", result.Message);
            Assert.IsEmpty(_triggeredEvents);
        }

        [TestMethod()]
        public void ExecuteInputAction_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            ExecutionTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => ExecutionTools.ExecuteInputAction("g1", "press"));
        }

        #endregion

        #region SaveProject

        [TestMethod()]
        public void SaveProject_Succeeds_WhenProjectHasFilePath()
        {
            // Arrange
            _savedFilePath = @"C:\projects\test.mfproj";
            InitializeTools();

            // Act
            var result = ExecutionTools.SaveProject();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.Contains(@"C:\projects\test.mfproj", result.Message);
        }

        [TestMethod()]
        public void SaveProject_Fails_WhenProjectNeverSaved()
        {
            // Arrange
            _saveError = "The loaded project has never been saved to disk";
            InitializeTools();

            // Act
            var result = ExecutionTools.SaveProject();

            // Assert
            Assert.IsFalse(result.Success);
            Assert.Contains("never been saved", result.Message);
        }

        [TestMethod()]
        public void SaveProject_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            ExecutionTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => ExecutionTools.SaveProject());
        }

        #endregion
    }
}
