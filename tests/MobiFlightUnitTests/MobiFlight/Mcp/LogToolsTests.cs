using MobiFlight.Mcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace MobiFlight.Mcp.Tests
{
    [TestClass()]
    public class LogToolsTests
    {
        #region GetLog

        [TestMethod()]
        public void GetLog_PassesLineCountThrough_WithinBounds()
        {
            // Arrange
            var received = new List<int>();
            LogTools.Initialize(count => { received.Add(count); return "tail"; });

            // Act
            var result = LogTools.GetLog(50);

            // Assert
            Assert.AreEqual("tail", result);
            Assert.Contains(50, received);
        }

        [TestMethod()]
        public void GetLog_ClampsLineCount_ToMinimum()
        {
            // Arrange
            var received = new List<int>();
            LogTools.Initialize(count => { received.Add(count); return "tail"; });

            // Act
            LogTools.GetLog(0);

            // Assert
            Assert.Contains(1, received);
        }

        [TestMethod()]
        public void GetLog_ClampsLineCount_ToMaximum()
        {
            // Arrange
            var received = new List<int>();
            LogTools.Initialize(count => { received.Add(count); return "tail"; });

            // Act
            LogTools.GetLog(9999);

            // Assert
            Assert.Contains(2000, received);
        }

        [TestMethod()]
        public void GetLog_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            LogTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<System.InvalidOperationException>(() => LogTools.GetLog(10));
        }

        #endregion
    }
}
