using System.Diagnostics;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Diagnostics;

namespace VirtoCommerce.UCP.Web.Mcp;

public static class UcpMcpActivityStatusFilter
{
    public const string ErrorStatusDescription = "UCP tool returned an error.";

    public static McpMessageFilter Create()
    {
        return next => async (context, cancellationToken) =>
        {
            var shouldSanitize = IsUcpToolCall(context.JsonRpcMessage);

            await next(context, cancellationToken);

            // The MCP SDK has already added response tags when the inner handler returns.
            // Keep the Error status, but do not let its payload bypass Platform log levels.
            if (shouldSanitize &&
                Activity.Current is
                {
                    Source.Name: UcpDiagnostics.McpActivitySourceName,
                    Status: ActivityStatusCode.Error,
                } activity)
            {
                activity.SetStatus(ActivityStatusCode.Error, ErrorStatusDescription);
            }
        };
    }

    private static bool IsUcpToolCall(JsonRpcMessage message)
    {
        return message is JsonRpcRequest
        {
            Method: RequestMethods.ToolsCall,
            Params: JsonObject parameters,
        } &&
            parameters.TryGetPropertyValue("name", out var nameNode) &&
            nameNode is JsonValue nameValue &&
            nameValue.TryGetValue<string>(out var name) &&
            ModuleConstants.McpTools.IsUcpTool(name);
    }
}
