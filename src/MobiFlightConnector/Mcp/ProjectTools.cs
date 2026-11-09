using MobiFlight.Base;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

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

        public static void Initialize(Func<List<ProjectInfo>> getRecentProjects, Action<string> openProject)
        {
            _getRecentProjects = getRecentProjects;
            _openProject = openProject;
        }

        internal static void Reset()
        {
            _getRecentProjects = null;
            _openProject = null;
        }

        [McpServerTool, Description("Lists the recent MobiFlight projects, like the 'My Projects' home screen.")]
        public static List<RecentProject> ListRecentProjects()
        {
            if (_getRecentProjects is null)
                throw new InvalidOperationException("ProjectTools not initialized.");

            return [.. _getRecentProjects().Select(p => new RecentProject(p.Name, p.Sim, p.FilePath))];
        }

        [McpServerTool, Description("Opens a MobiFlight project by file path. The path must match a recent project; call list_recent_projects first.")]
        public static ProjectOperationResult OpenProject(string filePath)
        {
            if (_openProject is null)
                throw new InvalidOperationException("ProjectTools not initialized.");

            if (string.IsNullOrWhiteSpace(filePath))
                return new ProjectOperationResult(false, "File path is required.");

            var known = ListRecentProjects().Any(p =>
                string.Equals(p.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

            if (!known)
                return new ProjectOperationResult(false, $"Unknown project: {filePath}. Call list_recent_projects to get valid paths.");

            _openProject(filePath);
            return new ProjectOperationResult(true, $"Opening project: {filePath}");
        }
    }
}
