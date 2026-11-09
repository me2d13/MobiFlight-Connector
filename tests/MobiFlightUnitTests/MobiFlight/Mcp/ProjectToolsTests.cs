using MobiFlight.Base;
using MobiFlight.Mcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace MobiFlight.Mcp.Tests
{
    [TestClass()]
    public class ProjectToolsTests
    {
        #region ListRecentProjects

        [TestMethod()]
        public void ListRecentProjects_ReturnsMappedEntries_WhenInitialized()
        {
            // Arrange
            var projects = new List<ProjectInfo>
            {
                new ProjectInfo { Name = "ifly737_2024", Sim = "MSFS", FilePath = @"C:\projects\ifly737_2024.mfproj" }
            };
            ProjectTools.Initialize(() => projects, filePath => { });

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
            ResetTools();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => ProjectTools.ListRecentProjects());
        }

        #endregion

        #region OpenProject

        [TestMethod()]
        public void OpenProject_Succeeds_WhenPathMatchesRecentProject()
        {
            // Arrange
            var opened = new List<string>();
            var projects = new List<ProjectInfo>
            {
                new ProjectInfo { Name = "Test", FilePath = @"C:\projects\test.mfproj" }
            };
            ProjectTools.Initialize(() => projects, filePath => opened.Add(filePath));

            // Act
            var result = ProjectTools.OpenProject(@"C:\projects\test.mfproj");

            // Assert
            Assert.IsTrue(result.Success);
            Assert.HasCount(1, opened);
        }

        [TestMethod()]
        public void OpenProject_Fails_WhenPathCaseInsensitiveMatches()
        {
            // Arrange
            var opened = new List<string>();
            var projects = new List<ProjectInfo>
            {
                new ProjectInfo { Name = "Test", FilePath = @"C:\projects\test.mfproj" }
            };
            ProjectTools.Initialize(() => projects, filePath => opened.Add(filePath));

            // Act
            var result = ProjectTools.OpenProject(@"C:\PROJECTS\TEST.mfproj");

            // Assert
            Assert.IsTrue(result.Success);
            Assert.HasCount(1, opened);
        }

        [TestMethod()]
        public void OpenProject_Fails_WhenPathUnknown()
        {
            // Arrange
            var opened = new List<string>();
            var projects = new List<ProjectInfo>();
            ProjectTools.Initialize(() => projects, filePath => opened.Add(filePath));

            // Act
            var result = ProjectTools.OpenProject(@"C:\projects\unknown.mfproj");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsEmpty(opened);
        }

        [TestMethod()]
        public void OpenProject_Fails_WhenPathEmpty()
        {
            // Arrange
            var opened = new List<string>();
            var projects = new List<ProjectInfo>();
            ProjectTools.Initialize(() => projects, filePath => opened.Add(filePath));

            // Act
            var result = ProjectTools.OpenProject("");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsEmpty(opened);
        }

        #endregion

        private static void ResetTools()
        {
            ProjectTools.Reset();
        }
    }
}
