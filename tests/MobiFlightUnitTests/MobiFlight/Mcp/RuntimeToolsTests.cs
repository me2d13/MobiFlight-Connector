using MobiFlight.Base;
using MobiFlight.Mcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace MobiFlight.Mcp.Tests
{
    [TestClass()]
    public class RuntimeToolsTests
    {
        private RuntimeStatus _status;
        private ProjectOperationResult _startResult;
        private ProjectOperationResult _stopResult;

        [TestInitialize]
        public void SetUp()
        {
            _status = new RuntimeStatus(
                ProjectRunning: true,
                SimAvailable: true,
                SimConnected: true,
                SimType: "MSFS2020",
                SimConnectionMethod: "SIMCONNECT",
                Aircraft: "PMDG 737-800",
                AircraftPath: @"C:\...\pmdg-aircraft-738\aircraft.cfg",
                ProjectFilePath: @"C:\projects\pmdg738.mfproj",
                ProjectName: "PMDG 738",
                ProjectHasUnsavedChanges: false,
                ProjectAircraft: [new AircraftInfo { Vendor = "PMDG", Name = "737-800" }]);

            _startResult = new ProjectOperationResult(true, "started");
            _stopResult = new ProjectOperationResult(true, "stopped");
        }

        [TestCleanup]
        public void TearDown()
        {
            RuntimeTools.Reset();
        }

        private void Initialize(RuntimeStatus status = null, ProjectOperationResult startResult = null, ProjectOperationResult stopResult = null)
        {
            RuntimeTools.Initialize(
                getRuntimeStatus: () => status ?? _status,
                startProjectExecution: () => startResult ?? _startResult,
                stopProjectExecution: () => stopResult ?? _stopResult);
        }

        #region GetRuntimeStatus

        [TestMethod()]
        public void GetRuntimeStatus_ReturnsInjectedStatus()
        {
            // Arrange
            Initialize();

            // Act
            var result = RuntimeTools.GetRuntimeStatus();

            // Assert
            Assert.AreSame(_status, result);
            Assert.IsTrue(result.ProjectRunning);
            Assert.AreEqual("PMDG 737-800", result.Aircraft);
            Assert.HasCount(1, result.ProjectAircraft);
        }

        [TestMethod()]
        public void GetRuntimeStatus_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            RuntimeTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => RuntimeTools.GetRuntimeStatus());
        }

        [TestMethod()]
        public void GetRuntimeStatus_ThrowsInvalidOperationException_WhenNotReady()
        {
            // Arrange - the service delegate returns null while the main window is starting
            RuntimeTools.Initialize(() => null, () => _startResult, () => _stopResult);

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => RuntimeTools.GetRuntimeStatus());
        }

        #endregion

        #region StartProjectExecution

        [TestMethod()]
        public void StartProjectExecution_ReturnsDelegateResult()
        {
            // Arrange
            Initialize();

            // Act
            var result = RuntimeTools.StartProjectExecution();

            // Assert
            Assert.AreSame(_startResult, result);
        }

        [TestMethod()]
        public void StartProjectExecution_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            RuntimeTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => RuntimeTools.StartProjectExecution());
        }

        #endregion

        #region StopProjectExecution

        [TestMethod()]
        public void StopProjectExecution_ReturnsDelegateResult()
        {
            // Arrange
            Initialize();

            // Act
            var result = RuntimeTools.StopProjectExecution();

            // Assert
            Assert.AreSame(_stopResult, result);
        }

        [TestMethod()]
        public void StopProjectExecution_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            RuntimeTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => RuntimeTools.StopProjectExecution());
        }

        #endregion
    }
}
