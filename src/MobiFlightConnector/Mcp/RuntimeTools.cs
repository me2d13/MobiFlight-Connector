using MobiFlight.Base;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MobiFlight.Mcp
{
    /// <summary>
    /// Live state of the MobiFlight session, read straight from ExecutionManager and MainForm.
    /// Kept as a flat snapshot so MCP clients get the values without a parallel domain model.
    /// </summary>
    public record RuntimeStatus(
        bool ProjectRunning,
        bool SimAvailable,
        bool SimConnected,
        string SimType,
        string SimConnectionMethod,
        string Aircraft,
        string AircraftPath,
        string ProjectFilePath,
        string ProjectName,
        bool ProjectHasUnsavedChanges,
        List<AircraftInfo> ProjectAircraft);

    /// <summary>
    /// Tools about the running session (execution engine and simulator connection). Delegates
    /// are injected at startup so the UI/engine access stays in McpServerService and the tools
    /// remain unit-testable.
    /// </summary>
    [McpServerToolType]
    public static class RuntimeTools
    {
        private static Func<RuntimeStatus> _getRuntimeStatus;
        private static Func<ProjectOperationResult> _startProjectExecution;
        private static Func<ProjectOperationResult> _stopProjectExecution;

        public static void Initialize(
            Func<RuntimeStatus> getRuntimeStatus,
            Func<ProjectOperationResult> startProjectExecution,
            Func<ProjectOperationResult> stopProjectExecution)
        {
            _getRuntimeStatus = getRuntimeStatus;
            _startProjectExecution = startProjectExecution;
            _stopProjectExecution = stopProjectExecution;
        }

        internal static void Reset()
        {
            _getRuntimeStatus = null;
            _startProjectExecution = null;
            _stopProjectExecution = null;
        }

        [McpServerTool, Description("Returns the live state of the MobiFlight session: whether the project execution engine is running (the Run button state), whether the simulator process is detected and whether MobiFlight is connected to it, which simulator and connection method, the aircraft title/path last reported by the simulator, and the loaded project (file, name, unsaved-changes flag and the aircraft it is associated with). Use this to decide what to tell the user: when projectRunning is true, config edits are live - the next execution cycle sends them to the simulator, so tell the user to check the hardware/output now. When projectRunning is false, edits are visible in the UI but do not reach the simulator - offer to start the project with start_project_execution and only do so if the user agrees. If aircraft is empty the simulator has not reported one yet; if aircraft or aircraftPath does not match projectAircraft, warn the user that a different aircraft may be loaded before changing mappings.")]
        public static RuntimeStatus GetRuntimeStatus()
        {
            if (_getRuntimeStatus is null)
                throw new InvalidOperationException("RuntimeTools not initialized.");

            return _getRuntimeStatus()
                ?? throw new InvalidOperationException("MobiFlight Connector is not ready yet - the main window is still starting.");
        }

        [McpServerTool, Description("Starts project execution - exactly like pressing Run in the UI: output values are computed and sent to the simulator, and input mappings/actions become active. Check get_runtime_status first and call this only when the user asks or agrees (for example after an update that should take effect immediately); do not start execution on your own. Fails if no project is loaded. If no simulator is connected, execution still starts but nothing reaches the simulator - tell the user.")]
        public static ProjectOperationResult StartProjectExecution()
        {
            if (_startProjectExecution is null)
                throw new InvalidOperationException("RuntimeTools not initialized.");

            return _startProjectExecution();
        }

        [McpServerTool, Description("Stops project execution - exactly like pressing Stop in the UI: output values stop updating and input actions stop executing. Config edits remain in memory and stay visible in the UI. Call this only when the user asks. Returns the resulting state.")]
        public static ProjectOperationResult StopProjectExecution()
        {
            if (_stopProjectExecution is null)
                throw new InvalidOperationException("RuntimeTools not initialized.");

            return _stopProjectExecution();
        }
    }
}
