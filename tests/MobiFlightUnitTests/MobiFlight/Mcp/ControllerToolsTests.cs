using MobiFlight;
using MobiFlight.Base;
using MobiFlight.Mcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace MobiFlight.Mcp.Tests
{
    [TestClass()]
    public class ControllerToolsTests
    {
        #region ListConnectedControllers

        [TestMethod()]
        public void ListConnectedControllers_MapsMobiFlightBoard_WithPortAndDevices()
        {
            // Arrange
            var controllers = new List<Controller>
            {
                new Controller
                {
                    Name = "MobiFlight Mega",
                    Serial = "SN-123-456",
                    Type = "MobiFlight",
                    Port = "COM5",
                    Devices =
                    [
                        new DeviceReference(DeviceType.Encoder, "Encoder1"),
                        new DeviceReference(DeviceType.Button, "Button1"),
                    ]
                }
            };
            ControllerTools.Initialize(() => controllers);

            // Act
            var result = ControllerTools.ListConnectedControllers();

            // Assert
            Assert.HasCount(1, result);
            Assert.AreEqual("MobiFlight Mega", result[0].Name);
            Assert.AreEqual("MobiFlight", result[0].ControllerType);
            Assert.AreEqual("SN-123-456", result[0].Serial);
            Assert.AreEqual("COM5", result[0].Port);
            Assert.HasCount(2, result[0].Devices);
            Assert.AreEqual("Encoder", result[0].Devices[0].DeviceType);
            Assert.AreEqual("Button", result[0].Devices[1].DeviceType);
        }

        [TestMethod()]
        public void ListConnectedControllers_MapsJoystick_WithoutPort()
        {
            // Arrange
            var controllers = new List<Controller>
            {
                new Controller
                {
                    Name = "Flight Stick",
                    Serial = "JS-xyz",
                    Type = "Joystick",
                    Devices = [ new DeviceReference(DeviceType.AnalogInput, "Axis1") ]
                }
            };
            ControllerTools.Initialize(() => controllers);

            // Act
            var result = ControllerTools.ListConnectedControllers();

            // Assert
            Assert.HasCount(1, result);
            Assert.AreEqual("Joystick", result[0].ControllerType);
            Assert.IsNull(result[0].Port);
            Assert.HasCount(1, result[0].Devices);
            Assert.AreEqual("AnalogInput", result[0].Devices[0].DeviceType);
        }

        [TestMethod()]
        public void ListConnectedControllers_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            ControllerTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<System.InvalidOperationException>(() => ControllerTools.ListConnectedControllers());
        }

        #endregion
    }
}
