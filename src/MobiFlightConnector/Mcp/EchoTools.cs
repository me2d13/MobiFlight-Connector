using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MobiFlight.Mcp
{
    /// <summary>Proof-of-connectivity tools; to be replaced by real MobiFlight commands.</summary>
    [McpServerToolType]
    public static class EchoTools
    {
        /// <summary>Echoes the given message back to the caller.</summary>
        [McpServerTool, Description("Echoes the message back to the caller.")]
        public static string Echo([Description("The message to echo.")] string message)
        {
            return message;
        }
    }
}
