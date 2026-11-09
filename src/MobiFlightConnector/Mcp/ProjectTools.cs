using MobiFlight.Base;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace MobiFlight.Mcp
{
    /// <summary>A recent project as exposed to MCP clients.</summary>
    public record RecentProject(string Name, string Sim, string FilePath);

    /// <summary>The result of an MCP project operation.</summary>
    public record ProjectOperationResult(bool Success, string Message);

    /// <summary>
    /// Project browsing and opening tools; mirrors the "My Projects" UI flow.
    /// Delegates are injected at startup so the tools stay unit-testable.
    /// </summary>
    [McpServerToolType]
    public static class ProjectTools
    {
        private static Func<List<ProjectInfo>> _getRecentProjects;
        private static Action<string> _openProject;
        private static Action<string> _showConfigPage;

        public static void Initialize(Func<List<ProjectInfo>> getRecentProjects, Action<string> openProject, Action<string> showConfigPage)
        {
            _getRecentProjects = getRecentProjects;
            _openProject = openProject;
            _showConfigPage = showConfigPage;
        }

        internal static void Reset()
        {
            _getRecentProjects = null;
            _openProject = null;
            _showConfigPage = null;
        }

        [McpServerTool, Description("Lists the recently opened MobiFlight projects, like the 'My Projects' home screen. Note: Connector always has one project loaded (shown in the title bar); this list is about recently used project files.")]
        public static List<RecentProject> ListRecentProjects()
        {
            if (_getRecentProjects is null)
                throw new InvalidOperationException("ProjectTools not initialized.");

            return [.. _getRecentProjects().Select(p => new RecentProject(p.Name, p.Sim, p.FilePath))];
        }

        [McpServerTool, Description("Loads a MobiFlight project file (.mfproj) into Connector, replacing the currently loaded project - like File > Open. Note: Connector always has one project loaded, and this does not change which page the UI shows. What users call 'opening a project' (seeing its config page) additionally requires navigation in the UI, e.g. the arrow on the home screen project card.")]
        public static ProjectOperationResult OpenProject(string filePath)
        {
            if (_openProject is null)
                throw new InvalidOperationException("ProjectTools not initialized.");

            if (string.IsNullOrWhiteSpace(filePath))
                return new ProjectOperationResult(false, "File path is required.");

            if (!File.Exists(filePath))
                return new ProjectOperationResult(false, $"File not found: {filePath}");

            _openProject(filePath);
            return new ProjectOperationResult(true, $"Loading project: {filePath}. The UI keeps showing its current page.");
        }

        [McpServerTool, Description("Shows the project config page in the UI - the same as clicking the arrow on the home screen project card. Call after open_project when the user wants to see the project's configuration.")]
        public static ProjectOperationResult OpenProjectConfig()
        {
            if (_showConfigPage is null)
                throw new InvalidOperationException("ProjectTools not initialized.");

            _showConfigPage("/config");
            return new ProjectOperationResult(true, "Showing the project config page.");
        }
    }
}
