namespace SupperIdaMcp.Center.Mcp;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, string> _schemas = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Schemas => _schemas;

    public void RegisterSchema(string name, string jsonSchema) => _schemas[name] = jsonSchema;
}
