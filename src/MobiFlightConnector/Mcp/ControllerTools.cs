using MobiFlight.Base;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MobiFlight.Mcp
{
    /// <summary>A device on a controller as exposed to MCP clients.</summary>
    public record ControllerDevice(string Name, string Label, string DeviceType);

    /// <summary>A connected controller as exposed to MCP clients.</summary>
    public record ControllerSummary(string Name, string ControllerType, string Serial, string Port, List<ControllerDevice> Devices);

    /// <summary>
    /// Read-only tools about connected controllers; delegates are injected
    /// at startup so the tools stay unit-testable.
    /// </summary>
    [McpServerToolType]
    public static class ControllerTools
    {
        private static Func<List<Controller>> _getConnectedControllers;

        public static void Initialize(Func<List<Controller>> getConnectedControllers)
        {
            _getConnectedControllers = getConnectedControllers;
        }

        internal static void Reset()
        {
            _getConnectedControllers = null;
        }

        [McpServerTool, Description("Lists all controllers currently connected to Connector: MobiFlight boards (with COM port, serial number and their configured input and output devices like buttons, encoders, LED modules), joysticks and game controllers (buttons and axes), and MIDI boards. Every controller in the result is currently connected. Use this to answer questions like 'which controller has three encoders and what port is it connected to'.")]
        public static List<ControllerSummary> ListConnectedControllers()
        {
            if (_getConnectedControllers is null)
                throw new InvalidOperationException("ControllerTools not initialized.");

            return [.. _getConnectedControllers().Select(c => new ControllerSummary(
                Name: c.Name,
                ControllerType: c.Type,
                Serial: c.Serial,
                Port: c.Port,
                Devices: [.. c.Devices.Select(d => new ControllerDevice(d.Name, d.Label, d.Type.ToString()))]))];
        }
    }
}
