using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public static class McpRouteUpdater
{
    [MenuItem("Tools/Update MCP Unity Route")]
    public static void UpdateMcpRoute()
    {
        var packageCache = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");
        packageCache = Path.GetFullPath(packageCache);

        var mcpDir = Directory.GetDirectories(packageCache)
            .FirstOrDefault(d => Path.GetFileName(d).StartsWith("com.gamelovers.mcp-unity@"));

        if (mcpDir == null)
        {
            EditorUtility.DisplayDialog("MCP Route Updater",
                "Could not find com.gamelovers.mcp-unity in Library/PackageCache.", "OK");
            return;
        }

        var indexJs = Path.Combine(mcpDir, "Server~", "build", "index.js");
        if (!File.Exists(indexJs))
        {
            EditorUtility.DisplayDialog("MCP Route Updater",
                $"Found package at:\n{mcpDir}\n\nBut Server~/build/index.js does not exist.", "OK");
            return;
        }

        var jsPath = indexJs.Replace("\\", "/");

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var mcpJsonPath = Path.Combine(projectRoot, ".mcp.json");

        var mcpJson = $@"{{
  ""mcpServers"": {{
    ""mcp-unity"": {{
      ""command"": ""node"",
      ""args"": [
        ""{jsPath}""
      ]
    }}
  }}
}}";

        File.WriteAllText(mcpJsonPath, mcpJson);
        Debug.Log($"[MCP Route Updater] Updated .mcp.json with path: {jsPath}");
        EditorUtility.DisplayDialog("MCP Route Updater",
            $"Updated .mcp.json successfully!\n\nPath: {jsPath}", "OK");
    }
}
