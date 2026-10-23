using System.Text.Json.Nodes;

namespace eShop.Parity
{
    /// <summary>Read-only access to migration/baseline (flows.json, transcripts, data-state snapshots).</summary>
    public static class Baseline
    {
        public const string DirectoryVariable = "ESHOP_BASELINE_DIR";

        private static readonly Lazy<string> directory = new Lazy<string>(Locate);
        private static readonly Lazy<JsonObject> flows = new Lazy<JsonObject>(() => Load("flows.json").AsObject());

        public static string Directory => directory.Value;

        public static JsonObject FlowsDocument => flows.Value;

        public static IEnumerable<JsonObject> Flows(string app, string group = null)
        {
            return FlowsDocument["flows"].AsArray()
                .Select(f => f.AsObject())
                .Where(f => (string)f["app"] == app && (group == null || (string)f["group"] == group))
                .OrderBy(f => (int)f["order"]);
        }

        public static JsonObject Flow(string id)
        {
            return FlowsDocument["flows"].AsArray().Select(f => f.AsObject()).Single(f => (string)f["id"] == id);
        }

        public static JsonObject Transcript(string flowId) => Load(Path.Combine("transcripts", flowId + ".json")).AsObject();

        public static JsonObject DataState(string name)
        {
            var state = Load(Path.Combine("data-state", name + ".json")).AsObject();
            state.Remove("app");
            return state;
        }

        public static string DataStatePath(string app) => (string)FlowsDocument["data_state_paths"][app];

        public static JsonNode Load(string relativePath)
        {
            return JsonNode.Parse(File.ReadAllText(Path.Combine(Directory, relativePath)));
        }

        private static string Locate()
        {
            var configured = Environment.GetEnvironmentVariable(DirectoryVariable);
            if (!string.IsNullOrEmpty(configured))
            {
                return configured;
            }
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "migration", "baseline");
                if (File.Exists(Path.Combine(candidate, "flows.json")))
                {
                    return candidate;
                }
            }
            throw new DirectoryNotFoundException($"migration/baseline not found above {AppContext.BaseDirectory}; set {DirectoryVariable}.");
        }
    }
}
