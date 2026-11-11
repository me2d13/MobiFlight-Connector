using Microsoft.VisualStudio.TestTools.UnitTesting;
using MobiFlight.Base;
using Newtonsoft.Json;
using System;

namespace MobiFlight.Base.Serialization.Json.Tests
{
    [TestClass()]
    public class DeviceConfigConverterTests
    {
        [TestMethod()]
        public void ReadJson_UnknownType_NamesValidTypes()
        {
            // Arrange
            var json = "{\"Type\":\"Nope\"}";

            // Act
            var ex = Assert.ThrowsExactly<NotSupportedException>(() => JsonConvert.DeserializeObject<IDeviceConfig>(json));

            // Assert
            Assert.Contains("Nope", ex.Message);
            Assert.Contains("LedModule", ex.Message);
            Assert.Contains("Button", ex.Message);
        }
    }
}
