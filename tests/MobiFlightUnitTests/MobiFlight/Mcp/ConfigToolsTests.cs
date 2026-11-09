using MobiFlight.Base;
using MobiFlight.InputConfig;
using MobiFlight.Mcp;
using MobiFlight.OutputConfig;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace MobiFlight.Mcp.Tests
{
    [TestClass()]
    public class ConfigToolsTests
    {
        private ConfigFile _configFile;

        [TestInitialize]
        public void SetUp()
        {
            var inputItem = new InputConfigItem
            {
                Name = "Gear DOWN",
                Controller = new Controller { Name = "MobiFlight Mega", Serial = "SN-123", Type = "MobiFlight", Port = "COM5" },
                Device = new InputConfig.Button { Name = "In30" },
                button = new ButtonInputConfig(),
                encoder = new EncoderInputConfig()
            };

            var outputItem = new OutputConfigItem
            {
                Name = "Gear lights",
                Controller = new Controller { Name = "Joystick One", Serial = "JS-abc", Type = "Joystick" },
                // Output.Device.Name resolves to Pin at runtime
                Device = new Output { Pin = "Led45" }
            };

            _configFile = new ConfigFile("Test.mfproj") { Label = "Test config" };
            _configFile.ConfigItems.Add(inputItem);
            _configFile.ConfigItems.Add(outputItem);

            ConfigTools.Initialize(() => [_configFile]);
        }

        [TestCleanup]
        public void TearDown()
        {
            ConfigTools.Reset();
        }

        #region GetConfigSummary

        [TestMethod()]
        public void GetConfigSummary_MapsDeviceAndControllerFields()
        {
            // Act
            var result = ConfigTools.GetConfigSummary();

            // Assert
            Assert.HasCount(2, result);

            var input = result.Find(r => r.Type == "InputConfigItem");
            Assert.IsNotNull(input);
            Assert.AreEqual("Gear DOWN", input.Name);
            Assert.AreEqual("In30", input.DeviceName);
            Assert.AreEqual("Button", input.DeviceType);
            Assert.AreEqual("MobiFlight Mega", input.ControllerName);
            Assert.AreEqual("SN-123", input.ControllerSerial);
            Assert.AreEqual("Test config", input.ConfigFile);
            Assert.Contains("button", input.InputEvents);
            Assert.Contains("encoder", input.InputEvents);

            var output = result.Find(r => r.Type == "OutputConfigItem");
            Assert.IsNotNull(output);
            Assert.AreEqual("Led45", output.DeviceName);
            Assert.AreEqual("Output", output.DeviceType);
            Assert.AreEqual(string.Empty, output.InputEvents);
        }

        [TestMethod()]
        public void GetConfigSummary_FiltersByConfigType()
        {
            // Act
            var result = ConfigTools.GetConfigSummary(configType: "OutputConfigItem");

            // Assert
            Assert.HasCount(1, result);
            Assert.AreEqual("OutputConfigItem", result[0].Type);
        }

        [TestMethod()]
        public void GetConfigSummary_FiltersByControllerSerial()
        {
            // Act
            var result = ConfigTools.GetConfigSummary(controllerSerial: "JS-abc");

            // Assert
            Assert.HasCount(1, result);
            Assert.AreEqual("Gear lights", result[0].Name);
        }

        [TestMethod()]
        public void GetConfigSummary_ReturnsEmpty_WhenFilterMatchesNothing()
        {
            // Act
            var result = ConfigTools.GetConfigSummary(controllerSerial: "SN-unknown");

            // Assert
            Assert.IsEmpty(result);
        }

        #endregion

        #region GetConfigItemDetails

        [TestMethod()]
        public void GetConfigItemDetails_ReturnsJsonWithName_WhenGuidMatches()
        {
            // Arrange
            var guid = _configFile.ConfigItems[0].GUID;

            // Act
            var json = ConfigTools.GetConfigItemDetails(guid);

            // Assert
            Assert.Contains("Gear DOWN", json);
        }

        [TestMethod()]
        public void GetConfigItemDetails_ThrowsArgumentException_WhenGuidUnknown()
        {
            // Act & Assert
            Assert.ThrowsExactly<System.ArgumentException>(() => ConfigTools.GetConfigItemDetails("no-such-guid"));
        }

        #endregion
    }
}
