using eShop.Parity;
using Xunit;
using Xunit.Abstractions;

namespace eShopNet8.Mvc.IntegrationTests
{
    /// <summary>
    /// Every read-only flow of migration/baseline/flows.json for the mvc app, asserted in-process against its
    /// legacy transcript with the replay.py projection (status, MIME, Location, normalised HTML/JSON, sha256 of static files).
    /// </summary>
    public class TranscriptReplayTests : IClassFixture<MvcAppFactory>
    {
        private readonly MvcAppFactory factory;
        private readonly ITestOutputHelper output;

        public TranscriptReplayTests(MvcAppFactory factory, ITestOutputHelper output)
        {
            this.factory = factory;
            this.output = output;
        }

        public static IEnumerable<object[]> ReadFlows => ParityApp.FlowIds("mvc", "read");

        [Theory]
        [MemberData(nameof(ReadFlows))]
        public async Task Read_flow_matches_legacy_transcript(string flowId)
        {
            using var client = factory.CreateParityClient();
            await ParityApp.AssertReadFlowAsync(client, flowId, output);
        }

        [Fact]
        public async Task Mutation_sequence_matches_legacy_transcripts_and_data_state()
        {
            // Fresh app: the mock catalog is a process-wide singleton, so the ordered sequence needs its own host.
            using var fresh = new MvcAppFactory();
            using var client = fresh.CreateParityClient();
            await ParityApp.AssertMutationSequenceAsync(client, "mvc", output);
        }
    }
}
