using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MobiFlight.Base;
using MobiFlight.BrowserMessages;
using MobiFlight.UI;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MobiFlight.Mcp
{
    /// <summary>
    /// Hosts the MCP server over Streamable HTTP. Loopback only, same posture
    /// as the WebSocket bridge the frontend uses (see MessageServer).
    /// </summary>
    public sealed class McpServerService
    {
        /// <summary>Fixed loopback port; must match the MCP client configuration.</summary>
        public const int Port = 51800;

        private WebApplication _app;
        private readonly IServiceProvider _serviceProvider;

        public McpServerService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public string Url { get { return $"http://127.0.0.1:{Port}/mcp"; } }

        /// <summary>Builds and runs the MCP server until shutdown. Launch on a background task.</summary>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var builder = WebApplication.CreateBuilder([]);
                builder.WebHost.UseUrls($"http://127.0.0.1:{Port}");
                // Guard against DNS rebinding: only accept loopback host names.
                builder.WebHost.UseSetting("AllowedHosts", "localhost;127.0.0.1");

                var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

                InitializeProjectTools();
                builder.Services
                    .AddMcpServer(options =>
                    {
                        options.ServerInfo = new Implementation { Name = "MobiFlight Connector", Version = version };

                        // Compact workflow guidance for clients that surface server instructions.
                        // Without it an agent has to discover the tool set and JSON conventions by
                        // trial and error before its first useful call.
                        options.ServerInstructions =
                            "MobiFlight Connector inspects and edits the currently loaded flight-sim project. " +
                            "To change a mapping: get_config_summary (find the item GUID) -> get_config_item_details " +
                            "(full item JSON) -> update_config_item (send the edited JSON back). The change appears in " +
                            "the UI immediately but is not written to disk; call save_project only when the user " +
                            "explicitly asks to save, never automatically after an update. " +
                            "get_runtime_status reports whether execution is running, whether the simulator is " +
                            "connected and which aircraft is loaded; start_project_execution and stop_project_execution " +
                            "are the Run/Stop buttons (use them only when the user asks). While execution is running, " +
                            "config updates reach the simulator on the next cycle; otherwise changes are UI-only and you " +
                            "should offer to start execution. " +
                            "get_config_item_template returns complete example JSON for input items, output items, " +
                            "modifiers and input actions. create_config_item adds a new mapping and delete_config_item " +
                            "removes one - same in-memory, unsaved semantics, and use them only when the user asks. " +
                            "update_config_item replaces the whole item and rejects unknown " +
                            "field names, so start from the details JSON and change only what you need. In " +
                            "get_config_item_details output, an omitted Modifiers/Preconditions/ConfigRefs section means " +
                            "empty; RawValue is the value read from the simulator and Value is the current output after " +
                            "modifiers (both omitted when empty, present while the execution engine is running).";
                    })
                    // Stateless: no server-to-client requests (sampling, elicitation) needed.
                    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
                    .WithToolsFromAssembly();

                _app = builder.Build();
                _app.MapMcp("/mcp");
                await _app.RunAsync().ConfigureAwait(false);
                Log.Instance.log($"MCP server stopped, was listening on {Url}", LogSeverity.Info);
            }
            catch (Exception ex)
            {
                Log.Instance.log($"MCP server failed: {ex.Message}", LogSeverity.Error);
            }
        }

        /// <summary>Lazily resolved: the MainForm may not exist yet when the MCP server starts.</summary>
        private void InitializeProjectTools()
        {
            // Observers to trace whether publishes actually happen (temporary diagnostics).
            MessageExchange.Instance.Subscribe<Project>(project =>
                Log.Instance.log($"[MCP] observed Project publish: {project.FilePath}", LogSeverity.Info));
            MessageExchange.Instance.Subscribe<BrowserMessages.Outgoing.Notification>(n =>
                Log.Instance.log($"[MCP] observed Notification: {n.Event} {n.Context}", LogSeverity.Info));

            ProjectTools.Initialize(
                getRecentProjects: () =>
                {
                    Log.Instance.log("[MCP] list_recent_projects requested", LogSeverity.Info);
                    var form = _serviceProvider.GetService<MainForm>();
                    if (form is null || !form.IsHandleCreated) return [];

                    return form.Invoke(() => form.ProjectListManager.GetProjects());
                },
                openProject: filePath =>
                {
                    // Canonical separators so the recent list doesn't get duplicate entries.
                    try { filePath = Path.GetFullPath(filePath); }
                    catch (Exception ex)
                    {
                        Log.Instance.log($"[MCP] Could not normalize project path {filePath}: {ex.Message}", LogSeverity.Warn);
                    }
                    Log.Instance.log($"[MCP] open_project dispatch: {filePath}", LogSeverity.Info);
                    var form = _serviceProvider.GetRequiredService<MainForm>();
                    if (form.InvokeRequired)
                        form.BeginInvoke(() =>
                        {
                            Log.Instance.log("[MCP] open_project executing on UI thread", LogSeverity.Info);
                            form.LoadConfig(filePath);
                            Log.Instance.log("[MCP] open_project LoadConfig returned", LogSeverity.Info);
                        });
                    else
                        form.LoadConfig(filePath);
                },
                showConfigPage: path =>
                {
                    Log.Instance.log($"[MCP] show_config_page: {path}", LogSeverity.Info);
                    MessageExchange.Instance.Publish(new BrowserMessages.Navigation { Path = path });
                });

            ControllerTools.Initialize(
                getConnectedControllers: () =>
                {
                    var form = _serviceProvider.GetService<MainForm>();
                    if (form is null || !form.IsHandleCreated) return [];

                    return form.execManager.GetConnectedControllers();
                });

            LogTools.Initialize(getLogTail: count =>
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "log.txt");
                if (!File.Exists(logPath)) return "(no log file found)";

                using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var lines = new List<string>();
                while (!reader.EndOfStream) { var line = reader.ReadLine(); if (line != null) lines.Add(line); }
                return string.Join(Environment.NewLine, [.. lines.TakeLast(count)]);
            });

            ConfigTools.Initialize(getConfigFiles: () =>
            {
                var form = _serviceProvider.GetService<MainForm>();
                if (form is null || !form.IsHandleCreated) return [];

                return form.Invoke(() => form.execManager.Project?.ConfigFiles?.ToList() ?? []);
            });

            ExecutionTools.Initialize(
                findConfigItem: guid =>
                {
                    var form = _serviceProvider.GetService<MainForm>();
                    if (form is null || !form.IsHandleCreated) return null;

                    return form.Invoke(() => form.execManager.Project?.ConfigFiles?
                        .SelectMany(f => f.ConfigItems)
                        .FirstOrDefault(i => string.Equals(i.GUID, guid, StringComparison.OrdinalIgnoreCase))
                        as ConfigItem);
                },
                updateConfigItem: item =>
                {
                    // Same handler the frontend's CommandUpdateConfigItem command runs, but
                    // invoked synchronously on the UI thread so errors and the return value
                    // reach the MCP caller instead of vanishing into an async dispatch.
                    Log.Instance.log($"[MCP] update_config_item: {item.Name} ({item.GUID})", LogSeverity.Info);
                    var form = _serviceProvider.GetRequiredService<MainForm>();
                    if (form is null || !form.IsHandleCreated) return false;

                    return form.Invoke(() => form.execManager.HandleCommandUpdateConfigItem(item));
                },
                addConfigItem: item =>
                {
                    Log.Instance.log($"[MCP] create_config_item: {item.Name} ({item.GUID})", LogSeverity.Info);
                    var form = _serviceProvider.GetRequiredService<MainForm>();
                    if (form is null || !form.IsHandleCreated) return false;

                    return form.Invoke(() => form.execManager.HandleCommandAddConfigItem(item));
                },
                deleteConfigItem: guid =>
                {
                    Log.Instance.log($"[MCP] delete_config_item: {guid}", LogSeverity.Info);
                    var form = _serviceProvider.GetRequiredService<MainForm>();
                    if (form is null || !form.IsHandleCreated) return false;

                    return form.Invoke(() => form.execManager.HandleCommandDeleteConfigItem(guid));
                },
                isExecutionStarted: () =>
                {
                    var form = _serviceProvider.GetService<MainForm>();
                    if (form is null || !form.IsHandleCreated) return false;

                    return form.Invoke(() => form.execManager.IsStarted());
                },
                triggerInputEvent: e =>
                {
                    Log.Instance.log($"[MCP] execute_input_action dispatch: {e.GetMsgEventLabel()}", LogSeverity.Info);
                    _serviceProvider.GetRequiredService<MainForm>().execManager.TriggerInputEvent(e);
                },
                saveProject: () =>
                {
                    var form = _serviceProvider.GetService<MainForm>();
                    if (form is null || !form.IsHandleCreated) return ("MobiFlight Connector is not ready.", null);

                    return form.Invoke<(string Error, string FilePath)>(() =>
                    {
                        var filePath = form.execManager.Project?.FilePath;
                        if (string.IsNullOrEmpty(filePath))
                            return ("The loaded project has never been saved to disk - there is no file path to save to. Open or save a project file first.", null);

                        Log.Instance.log($"[MCP] save_project: {filePath}", LogSeverity.Info);
                        form.SaveConfig(filePath);
                        return (null, filePath);
                    });
                });

            RuntimeTools.Initialize(
                getRuntimeStatus: () =>
                {
                    var form = _serviceProvider.GetService<MainForm>();
                    if (form is null || !form.IsHandleCreated) return null;

                    return form.Invoke(() =>
                    {
                        var execManager = form.execManager;
                        // SimAvailable() also refreshes FlightSim.FlightSimType/ConnectionMethod,
                        // so read them only after this call.
                        var simAvailable = execManager.SimAvailable();

                        return new RuntimeStatus(
                            ProjectRunning: execManager.IsStarted(),
                            SimAvailable: simAvailable,
                            SimConnected: execManager.SimConnected(),
                            SimType: FlightSim.FlightSimType.ToString(),
                            SimConnectionMethod: FlightSim.FlightSimConnectionMethod.ToString(),
                            Aircraft: execManager.CurrentAircraft,
                            AircraftPath: execManager.CurrentAircraftPath,
                            ProjectFilePath: execManager.Project?.FilePath,
                            ProjectName: execManager.Project?.Name,
                            ProjectHasUnsavedChanges: form.ProjectHasUnsavedChanges,
                            ProjectAircraft: execManager.Project?.Aircraft?.ToList() ?? []);
                    });
                },
                startProjectExecution: () =>
                {
                    var form = _serviceProvider.GetService<MainForm>();
                    if (form is null || !form.IsHandleCreated)
                        return new ProjectOperationResult(false, "MobiFlight Connector is not ready yet.");

                    return form.Invoke(() =>
                    {
                        var execManager = form.execManager;
                        if (execManager.Project is null)
                            return new ProjectOperationResult(false, "No project is loaded - open a project before starting execution.");

                        if (execManager.IsStarted())
                            return new ProjectOperationResult(true, "Project execution is already running.");

                        Log.Instance.log("[MCP] start_project_execution", LogSeverity.Info);
                        form.StartProjectExecution();

                        if (!execManager.IsStarted())
                            return new ProjectOperationResult(false, "Project execution did not start - check the MobiFlight log (get_log) for details.");

                        var simNote = execManager.SimConnected()
                            ? " Outputs now reach the simulator on the next execution cycle."
                            : " No simulator connection is active, so values will not reach the simulator yet.";
                        return new ProjectOperationResult(true, "Project execution started." + simNote);
                    });
                },
                stopProjectExecution: () =>
                {
                    var form = _serviceProvider.GetService<MainForm>();
                    if (form is null || !form.IsHandleCreated)
                        return new ProjectOperationResult(false, "MobiFlight Connector is not ready yet.");

                    return form.Invoke(() =>
                    {
                        if (!form.execManager.IsStarted())
                            return new ProjectOperationResult(true, "Project execution is already stopped.");

                        Log.Instance.log("[MCP] stop_project_execution", LogSeverity.Info);
                        form.StopExecution();
                        return new ProjectOperationResult(true, "Project execution stopped. Config changes remain in memory and stay visible in the UI, but nothing is sent to the simulator.");
                    });
                });
        }
    }
}
