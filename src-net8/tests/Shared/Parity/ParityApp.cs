using Xunit;
using Xunit.Abstractions;

namespace eShop.Parity
{
    /// <summary>Shared assertions for the transcript-driven integration tests of both apps.</summary>
    public static class ParityApp
    {
        /// <summary>
        /// Set ESHOP_PARITY_MVC_URL / ESHOP_PARITY_WEBFORMS_URL to run the same tests against a live server
        /// (e.g. the legacy app on Mono to validate this C# normaliser, or a container) instead of in-process.
        /// </summary>
        public static Uri ExternalUrl(string app)
        {
            var value = Environment.GetEnvironmentVariable(app == "mvc" ? "ESHOP_PARITY_MVC_URL" : "ESHOP_PARITY_WEBFORMS_URL");
            return string.IsNullOrEmpty(value) ? null : new Uri(value.TrimEnd('/') + "/");
        }

        public static HttpClient ExternalClient(Uri baseAddress)
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true };
            return new HttpClient(handler) { BaseAddress = baseAddress, Timeout = TimeSpan.FromMinutes(3) };
        }

        public static IEnumerable<object[]> FlowIds(string app, string group)
        {
            return Baseline.Flows(app, group).Select(f => new object[] { (string)f["id"] });
        }

        public static async Task AssertReadFlowAsync(HttpClient client, string flowId, ITestOutputHelper output)
        {
            var result = await new FlowRunner(client).ExecuteAsync(Baseline.Flow(flowId));
            var diffs = FlowRunner.Compare(result);
            foreach (var d in diffs)
            {
                output?.WriteLine(d);
            }
            Assert.True(diffs.Count == 0, $"{flowId} diverges from the legacy transcript:\n  " + string.Join("\n  ", diffs));
        }

        /// <summary>Initial catalog check + every 'mutate' flow in order on one session, like replay.py.</summary>
        public static async Task AssertMutationSequenceAsync(HttpClient client, string app, ITestOutputHelper output)
        {
            var runner = new FlowRunner(client);
            var diffs = JsonDiff.Diff(Baseline.DataState(app + "-initial"), await runner.DataStateAsync(app), "initial_state");
            var failed = new List<string>();
            foreach (var flow in Baseline.Flows(app, "mutate"))
            {
                List<string> flowDiffs;
                try
                {
                    flowDiffs = FlowRunner.Compare(await runner.ExecuteAsync(flow, (bool?)flow["snapshot"] ?? false));
                }
                catch (Exception ex)
                {
                    flowDiffs = new List<string> { $"{flow["id"]}: replay error {ex.GetType().Name}: {ex.Message}" };
                }
                output?.WriteLine($"{(flowDiffs.Count == 0 ? "PASS" : "FAIL")}  {flow["id"]}");
                if (flowDiffs.Count > 0)
                {
                    failed.Add((string)flow["id"]);
                }
                diffs.AddRange(flowDiffs.Take(15));
            }
            Assert.True(diffs.Count == 0, $"{app} mutation sequence diverges ({failed.Count} flows: {string.Join(", ", failed)}):\n  " + string.Join("\n  ", diffs));
        }
    }
}
