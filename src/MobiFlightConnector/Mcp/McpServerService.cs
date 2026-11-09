using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MobiFlight.UI;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
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
            ProjectTools.Initialize(
                getRecentProjects: () =>
                {
                    var form = _serviceProvider.GetService<MainForm>();
                    if (form is null || !form.IsHandleCreated) return [];

                    return form.Invoke(() => form.ProjectListManager.GetProjects());
                },
                openProject: filePath =>
                {
                    var form = _serviceProvider.GetRequiredService<MainForm>();
                    if (form.InvokeRequired)
                        form.BeginInvoke(() => form.LoadConfig(filePath));
                    else
                        form.LoadConfig(filePath);
                });
        }
    }
}
