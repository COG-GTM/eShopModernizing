extern alias MvcApp;
extern alias PagesApp;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace eShopNet8.IntegrationTests.Parity;

public sealed record FlowResult(
    string Id, string Sequence, string App, string? Quirk, string Verdict, IReadOnlyList<string> Reasons,
    int? Status, int? ExpectedStatus, string Outcome, string? Note);

public sealed record AppParityReport(string App, string Target, IReadOnlyList<FlowResult> Results, string ReportPath)
{
    public FlowResult this[string id] => Results.Single(r => r.Id == id);
}

// Replays every flows.json sequence of one app once per test run (C# port of replay.py) and writes a report shaped like
// replay-report.json. Restart-sequences get a fresh WebApplicationFactory (fresh mock data, CONTRACT.md section 7).
//   ESHOP_PARITY_MVC_URL / ESHOP_PARITY_WEBFORMS_URL      replay against a running app instead of in-process
//   ESHOP_PARITY_RESTART_MVC / ESHOP_PARITY_RESTART_WEBFORMS  restart command for that external app
//   ESHOP_PARITY_REPORT_DIR                                report directory (default <test output>/parity-report)
//   ESHOP_PARITY_ENVIRONMENT                               in-process host environment (default Production)
public static class ParityRun
{
    public static readonly string BaselineDir = Path.Combine(AppContext.BaseDirectory, "baseline");
    private static readonly ConcurrentDictionary<string, Lazy<Task<AppParityReport>>> Runs = new();

    public static JsonObject Flows { get; } = JsonNode.Parse(File.ReadAllText(Path.Combine(BaselineDir, "flows.json")))!.AsObject();

    public static IReadOnlyDictionary<string, KnownDivergence> KnownDivergences { get; } = LoadKnownDivergences();

    public sealed record KnownDivergence(string? Quirk, string Reason);

    public static IEnumerable<FlowSpec> FlowSpecs(string app) =>
        Flows["sequences"]!.AsArray().Select(s => s!.AsObject()).Where(s => (string)s["app"]! == app)
            .SelectMany(s => s["flows"]!.AsArray().Select(f => new FlowSpec(f!.AsObject(), (string)s["id"]!, app)));

    public static Task<AppParityReport> ForApp(string app) =>
        Runs.GetOrAdd(app, a => new Lazy<Task<AppParityReport>>(() => RunAppAsync(a))).Value;

    private static IReadOnlyDictionary<string, KnownDivergence> LoadKnownDivergences()
    {
        var doc = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Parity", "known-divergences.json")))!;
        return doc["flows"]!.AsObject().ToDictionary(kv => kv.Key, kv => new KnownDivergence((string?)kv.Value!["quirk"], (string)kv.Value!["reason"]!));
    }

    private static IParityTarget CreateTarget(string app)
    {
        var url = Environment.GetEnvironmentVariable(app == "mvc" ? "ESHOP_PARITY_MVC_URL" : "ESHOP_PARITY_WEBFORMS_URL");
        if (!string.IsNullOrWhiteSpace(url))
            return new ExternalTarget(url, Environment.GetEnvironmentVariable(app == "mvc" ? "ESHOP_PARITY_RESTART_MVC" : "ESHOP_PARITY_RESTART_WEBFORMS"));
        return app == "mvc" ? new InProcessTarget<MvcApp::Program>(app) : new InProcessTarget<PagesApp::Program>(app);
    }

    private static async Task<AppParityReport> RunAppAsync(string app)
    {
        var results = new List<FlowResult>();
        var appCfg = Flows["apps"]![app]!.AsObject();
        await using var target = CreateTarget(app);
        var started = false;
        foreach (var seq in Flows["sequences"]!.AsArray().Select(s => s!.AsObject()).Where(s => (string)s["app"]! == app))
        {
            var seqId = (string)seq["id"]!;
            if (seq["restart"] is not JsonValue r || r.GetValue<bool>() || !started)
            {
                await target.RestartAsync();
                started = true;
            }
            using var session = target.NewSession();
            foreach (var f in seq["flows"]!.AsArray())
            {
                var flow = new FlowSpec(f!.AsObject(), seqId, app);
                results.Add(await RunOneAsync(target, session, flow, appCfg));
            }
        }
        var path = WriteReport(app, target.BaseUrl, results);
        return new AppParityReport(app, target.BaseUrl, results, path);
    }

    private static async Task<FlowResult> RunOneAsync(IParityTarget target, HttpClient session, FlowSpec flow, JsonObject appCfg)
    {
        var pending = target.PendingReason(flow);
        if (pending is not null)
            return new FlowResult(flow.Id, flow.SequenceId, flow.App, flow.Quirk, "pending", [pending], null, null, "pending", pending);

        string verdict;
        var reasons = new List<string>();
        int? status = null, expectedStatus = null;
        var expPath = Path.Combine(BaselineDir, "transcripts", flow.Id + ".json");
        JsonObject? tr = null;
        string? error = null;
        try
        {
            tr = await FlowRunner.RunFlowAsync(session, target.BaseUrl, flow);
        }
        catch (Exception e)
        {
            error = $"{e.GetType().Name}: {e.Message}";
        }
        JsonObject? snap = null;
        if (flow.Snapshot && error is null)
        {
            using var snapSession = target.NewSession(followRedirects: true);
            try
            {
                snap = await FlowRunner.SnapshotAsync(snapSession, target.BaseUrl, appCfg);
            }
            catch (Exception e)
            {
                error = $"snapshot {e.GetType().Name}: {e.Message}";
            }
        }

        if (!File.Exists(expPath))
        {
            verdict = "different";
            reasons.Add("no baseline transcript");
        }
        else if (error is not null)
        {
            verdict = "different";
            reasons.Add("replay error: " + error);
        }
        else
        {
            var exp = JsonNode.Parse(File.ReadAllText(expPath))!.AsObject();
            if (exp["error"] is not null)
            {
                verdict = "different";
                reasons.Add("baseline flow errored: " + exp["error"]);
            }
            else
            {
                var response = tr!["response"]!;
                (verdict, var rs) = Normalizer.Compare(exp["response"]!, response, flow.Compare);
                reasons.AddRange(rs);
                if (flow.Snapshot)
                {
                    var expSnap = exp["data_state"] is JsonValue ds
                        ? JsonNode.Parse(File.ReadAllText(Path.Combine(BaselineDir, (string)ds!)))
                        : null;
                    if (!JsonEq.Equal(expSnap, snap))
                    {
                        verdict = "different";
                        reasons.Add(!JsonEq.Equal(expSnap?["items"], snap?["items"])
                            ? "data-state differs: " + JsonEq.FirstDiff(expSnap?["items"] ?? new JsonArray(), snap?["items"] ?? new JsonArray())
                            : "data-state lookups/status differ");
                    }
                }
                status = response["status"]!.GetValue<int>();
                expectedStatus = exp["response"]!["status"]!.GetValue<int>();
            }
        }

        string outcome = "pass";
        string? note = null;
        if (verdict == "different")
        {
            if (KnownDivergences.TryGetValue(flow.Id, out var kd))
            {
                outcome = "known-divergence";
                note = kd.Reason;
            }
            else
            {
                outcome = "fail";
            }
        }
        else if (KnownDivergences.ContainsKey(flow.Id))
        {
            note = "listed in known-divergences.json but now matches the baseline: remove the entry";
        }
        return new FlowResult(flow.Id, flow.SequenceId, flow.App, flow.Quirk, verdict, reasons, status, expectedStatus, outcome, note);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string WriteReport(string app, string target, List<FlowResult> results)
    {
        var dir = Environment.GetEnvironmentVariable("ESHOP_PARITY_REPORT_DIR");
        if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(AppContext.BaseDirectory, "parity-report");
        Directory.CreateDirectory(dir);
        var verdicts = results.GroupBy(r => r.Verdict).ToDictionary(g => g.Key, g => g.Count());
        var outcomes = results.GroupBy(r => r.Outcome).ToDictionary(g => g.Key, g => g.Count());
        var summary = new { app, target, flows = results.Count, baseline_flows = FlowSpecs(app).Count(), verdicts, outcomes };
        var json = Path.Combine(dir, $"parity-report-{app}.json");
        File.WriteAllText(json, JsonSerializer.Serialize(new { summary, results }, JsonOpts) + "\n");
        var sb = new StringBuilder();
        sb.AppendLine($"eShop transcript parity (in-process xUnit port of replay.py) - {app}");
        sb.AppendLine($"target: {target}");
        sb.AppendLine($"verdicts: {JsonSerializer.Serialize(verdicts)}");
        sb.AppendLine($"outcomes: {JsonSerializer.Serialize(outcomes)}  flows: {results.Count}/{summary.baseline_flows}");
        sb.AppendLine();
        foreach (var r in results)
        {
            var tag = r.Quirk is null ? "" : $" [quirk {r.Quirk}]";
            sb.AppendLine($"{r.Sequence,-34} {r.Id,-52} {r.Verdict,-32} {r.Outcome}{tag}");
            foreach (var reason in r.Reasons.Where(x => x != r.Note)) sb.AppendLine("      - " + (reason.Length > 300 ? reason[..300] : reason));
            if (r.Note is not null) sb.AppendLine("      * " + r.Note);
        }
        File.WriteAllText(Path.Combine(dir, $"parity-report-{app}.txt"), sb.ToString());
        return json;
    }
}
