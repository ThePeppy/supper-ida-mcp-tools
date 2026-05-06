using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

var endpoint = ParseEndpoint(args);
using var http = new HttpClient();
using var input = Console.OpenStandardInput();
using var output = Console.OpenStandardOutput();
using var reader = new StreamReader(input, Encoding.UTF8);
await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
{
    AutoFlush = true,
    NewLine = "\n"
};

while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
{
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    try
    {
        using var content = new StringContent(line, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(endpoint, content).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var id = TryGetId(line);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                error = new
                {
                    code = -32000,
                    message = $"Center HTTP endpoint returned {(int)response.StatusCode}: {responseText}"
                }
            })).ConfigureAwait(false);
            continue;
        }

        await writer.WriteLineAsync(responseText).ConfigureAwait(false);
    }
    catch (Exception exc)
    {
        var id = TryGetId(line);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            error = new
            {
                code = -32000,
                message = $"Unable to reach Supper IDA MCP Center at {endpoint}: {exc.Message}"
            }
        })).ConfigureAwait(false);
    }
}

static string ParseEndpoint(string[] args)
{
    var endpoint = "http://127.0.0.1:9401/mcp";
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--endpoint" && i + 1 < args.Length)
        {
            endpoint = args[++i];
        }
    }

    return endpoint;
}

static JsonElement? TryGetId(string json)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("id", out var id)
            ? id.Clone()
            : null;
    }
    catch
    {
        return null;
    }
}
