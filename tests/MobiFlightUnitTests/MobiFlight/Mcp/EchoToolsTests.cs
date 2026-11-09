using MobiFlight.Mcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MobiFlight.Mcp.Tests
{
    [TestClass()]
    public class EchoToolsTests
    {
        #region Echo

        [TestMethod()]
        public void Echo_ReturnsMessage_WhenCalled()
        {
            // Arrange
            var message = "hello MCP";

            // Act
            var result = EchoTools.Echo(message);

            // Assert
            Assert.AreEqual(message, result);
        }

        [TestMethod()]
        public void Echo_ReturnsEmptyString_WhenMessageEmpty()
        {
            // Act
            var result = EchoTools.Echo("");

            // Assert
            Assert.AreEqual("", result);
        }

        #endregion
    }
}
