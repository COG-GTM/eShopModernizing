using eShopNet8.IntegrationTests.Parity;
using Xunit.Abstractions;

namespace eShopNet8.IntegrationTests;

// Every baseline flow (modernization/baseline/flows.json) replayed in-process against the .NET 8 apps and compared with
// the recorded legacy transcript using the CONTRACT.md normalisation. A flow passes when its verdict is identical or
// equivalent-after-normalisation, or when it is a documented divergence in Parity/known-divergences.json. Flows whose
// implementing ticket is not in the build yet (scaffold stub still mapped) are reported as skipped, never as passed.
public class TranscriptParityTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> MvcFlows => ParityRun.FlowSpecs("mvc").Select(f => new object[] { f.Id });
    public static IEnumerable<object[]> WebFormsFlows => ParityRun.FlowSpecs("webforms").Select(f => new object[] { f.Id });

    [SkippableTheory]
    [MemberData(nameof(MvcFlows))]
    public Task Mvc_flow_matches_legacy_transcript(string flow) => Check("mvc", flow);

    [SkippableTheory]
    [MemberData(nameof(WebFormsFlows))]
    public Task WebForms_flow_matches_legacy_transcript(string flow) => Check("webforms", flow);

    [Fact]
    public void Baseline_has_155_flows_each_with_a_transcript()
    {
        var flows = ParityRun.FlowSpecs("mvc").Concat(ParityRun.FlowSpecs("webforms")).ToList();
        Assert.Equal(155, flows.Count);
        Assert.Equal(flows.Count, flows.Select(f => f.Id).Distinct().Count());
        Assert.All(flows, f => Assert.True(File.Exists(Path.Combine(ParityRun.BaselineDir, "transcripts", f.Id + ".json")), f.Id));
    }

    [Fact]
    public void Known_divergences_reference_baseline_flows()
    {
        var ids = ParityRun.FlowSpecs("mvc").Concat(ParityRun.FlowSpecs("webforms")).Select(f => f.Id).ToHashSet();
        Assert.All(ParityRun.KnownDivergences, kd => Assert.Contains(kd.Key, ids));
        Assert.All(ParityRun.KnownDivergences.Values, kd => Assert.NotEmpty(kd.Accept));
        Assert.All(ParityRun.KnownDivergences.Values, kd => Assert.DoesNotContain(kd.Accept, a => a.StartsWith("status", StringComparison.Ordinal)));
    }

    private async Task Check(string app, string flow)
    {
        var report = await ParityRun.ForApp(app);
        var r = report[flow];
        output.WriteLine($"{r.Id}: {r.Verdict} -> {r.Outcome} (status {r.Status?.ToString() ?? "-"}, expected {r.ExpectedStatus?.ToString() ?? "-"})");
        foreach (var reason in r.Reasons) output.WriteLine("  - " + reason);
        if (r.Note is not null) output.WriteLine("  * " + r.Note);
        output.WriteLine($"report: {report.ReportPath}");
        Skip.If(r.Outcome == "pending", r.Note);
        if (r.Outcome == "fail")
            Assert.Fail($"{r.Id} differs from the legacy transcript: {string.Join("; ", r.Reasons)}");
    }
}
