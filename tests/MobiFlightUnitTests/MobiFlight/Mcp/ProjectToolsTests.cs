using MobiFlight.Base;
using MobiFlight.Mcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace MobiFlight.Mcp.Tests
{
    [TestClass()]
    public class ProjectToolsTests
    {
        private string _tempProjectFile;

        [TestInitialize]
        public void SetUp()
        {
            // Real temp file: OpenProject now validates file existence, like File > Open.
            _tempProjectFile = Path.Combine(Path.GetTempPath(), $"mcp-test-{Guid.NewGuid():N}.mfproj");
            File.WriteAllText(_tempProjectFile, "{}");
        }

        [TestCleanup]
        public void TearDown()
        {
            if (File.Exists(_tempProjectFile)) File.Delete(_tempProjectFile);
            ProjectTools.Reset();
        }

        #region ListRecentProjects

        [TestMethod()]
        public void ListRecentProjects_ReturnsMappedEntries_WhenInitialized()
        {
            // Arrange
            var projects = new List<ProjectInfo>
            {
                new ProjectInfo { Name = "ifly737_2024", Sim = "MSFS", FilePath = @"C:\projects\ifly737_2024.mfproj" }
            };
            ProjectTools.Initialize(() => projects, filePath => { }, path => { });

            // Act
            var result = ProjectTools.ListRecentProjects();

            // Assert
            Assert.HasCount(1, result);
            Assert.AreEqual("ifly737_2024", result[0].Name);
            Assert.AreEqual("MSFS", result[0].Sim);
            Assert.AreEqual(@"C:\projects\ifly737_2024.mfproj", result[0].FilePath);
        }

        [TestMethod()]
        public void ListRecentProjects_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            ProjectTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => ProjectTools.ListRecentProjects());
        }

        #endregion

        #region OpenProject

        [TestMethod()]
        public void OpenProject_Succeeds_WhenFileExists_EvenIfNotInRecentList()
        {
            // Arrange
            var opened = new List<string>();
            ProjectTools.Initialize(() => [], filePath => opened.Add(filePath), path => { });

            // Act
            var result = ProjectTools.OpenProject(_tempProjectFile);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.HasCount(1, opened);
            Assert.AreEqual(_tempProjectFile, opened[0]);
        }

        [TestMethod()]
        public void OpenProject_Fails_WhenFileDoesNotExist()
        {
            // Arrange
            var opened = new List<string>();
            ProjectTools.Initialize(() => [], filePath => opened.Add(filePath), path => { });
            var missingPath = Path.Combine(Path.GetTempPath(), $"mcp-test-{Guid.NewGuid():N}.mfproj");

            // Act
            var result = ProjectTools.OpenProject(missingPath);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsEmpty(opened);
        }

        [TestMethod()]
        public void OpenProject_Fails_WhenPathEmpty()
        {
            // Arrange
            var opened = new List<string>();
            ProjectTools.Initialize(() => [], filePath => opened.Add(filePath), path => { });

            // Act
            var result = ProjectTools.OpenProject("");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsEmpty(opened);
        }

        #endregion

        #region OpenProjectConfig

        [TestMethod()]
        public void OpenProjectConfig_NavigatesToConfigPage_WhenInitialized()
        {
            // Arrange
            var navigated = new List<string>();
            ProjectTools.Initialize(() => [], filePath => { }, path => navigated.Add(path));

            // Act
            var result = ProjectTools.OpenProjectConfig();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.Contains("/config", navigated);
        }

        [TestMethod()]
        public void OpenProjectConfig_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            // Arrange
            ProjectTools.Reset();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => ProjectTools.OpenProjectConfig());
        }

        #endregion
    }
}
