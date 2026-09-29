extern alias ported;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Legacy = eShopLegacyMVC.Models;
using Ported = ported::eShopLegacyMVC.Models;

namespace eShopLegacyMVC.Data.ParityTests
{
    [Collection(SqlServerCollection.Name)]
    public class ParityTests : IDisposable
    {
        private readonly ITestOutputHelper output;

        public ParityTests(ITestOutputHelper output)
        {
            this.output = output;
            if (!string.IsNullOrEmpty(SqlServer.BaseConnectionString))
            {
                DropDatabases();
            }
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(SqlServer.BaseConnectionString))
            {
                DropDatabases();
            }
        }

        private static void DropDatabases()
        {
            SqlServer.DropDatabase(SqlServer.LegacyDatabaseName);
            SqlServer.DropDatabase(SqlServer.PortedDatabaseName);
        }

        private void AssertSame(IReadOnlyList<string> legacy, IReadOnlyList<string> ported, string what)
        {
            if (!legacy.SequenceEqual(ported))
            {
                output.WriteLine($"--- legacy {what}");
                legacy.ToList().ForEach(output.WriteLine);
                output.WriteLine($"--- ported {what}");
                ported.ToList().ForEach(output.WriteLine);
            }
            Assert.Equal(legacy, ported);
        }

        private static readonly (string Label, Func<ICatalogScenarioTarget, string> Run)[] ControllerScenario =
        {
            ("brands", t => t.Brands()),
            ("types", t => t.Types()),
            ("first page", t => t.Page(10, 0)),
            ("last page", t => t.Page(5, 2)),
            ("past the end", t => t.Page(4, 5)),
            ("find existing", t => t.Find(1)),
            ("find missing", t => t.Find(999)),
            ("create", t => t.Create(new ItemData { Name = "Parity mug", Description = "d", Price = 12.5M, CatalogBrandId = 2, CatalogTypeId = 1, AvailableStock = 3 })),
            ("create invalid annotations", t => t.Create(new ItemData { Name = null, Price = 1.234M, AvailableStock = -1, PictureFileName = null })),
            ("create name too long", t => t.Create(new ItemData { Name = new string('n', 51), Price = 1 })),
            ("create price out of range", t => t.Create(new ItemData { Name = "Pricey", Price = 1000000.01M })),
            ("create unknown brand", t => t.Create(new ItemData { Name = "Orphan", Price = 1, CatalogBrandId = 999 })),
            ("create after failures", t => t.Create(new ItemData { Name = "Second", Price = 0, CatalogTypeId = 3, MaxStockThreshold = 10, OnReorder = true })),
            ("update detached", t => t.Update(new ItemData { Id = 2, Name = "Renamed", Description = null, Price = 99.99M, CatalogBrandId = 3, CatalogTypeId = 2, RestockThreshold = 4 })),
            ("update invalid", t => t.Update(new ItemData { Id = 2, Name = null, Price = 1 })),
            ("update missing row", t => t.Update(new ItemData { Id = 5000, Name = "Ghost", Price = 1 })),
            ("update loaded graph", t => t.UpdateLoadedGraph(3, "Graph renamed", 4)),
            ("find updated", t => t.Find(2)),
            ("find graph updated", t => t.Find(3)),
            ("remove loaded", t => t.RemoveLoaded(4)),
            ("remove detached", t => t.RemoveDetached(5)),
            ("find removed", t => t.Find(4)),
            ("find detached remove target", t => t.Find(5)),
            ("all items", t => t.Page(50, 0)),
        };

        private static List<string> Replay(ICatalogScenarioTarget target) =>
            ControllerScenario.Select(step => $"[{step.Label}] {step.Run(target)}").ToList();

        [SkippableFact]
        public void Created_schema_matches_legacy_EF6_schema()
        {
            SqlServer.SkipIfUnavailable();

            LegacyHarness.CreateAndSeed(new Legacy.CatalogItemHiLoGenerator());
            Assert.True(PortedHarness.CreateAndSeed(new Ported.CatalogItemHiLoGenerator()));

            AssertSame(DatabaseSnapshot.Columns(SqlServer.LegacyDatabaseName), DatabaseSnapshot.Columns(SqlServer.PortedDatabaseName), "columns");
            AssertSame(DatabaseSnapshot.Indexes(SqlServer.LegacyDatabaseName), DatabaseSnapshot.Indexes(SqlServer.PortedDatabaseName), "indexes");
            AssertSame(DatabaseSnapshot.ForeignKeys(SqlServer.LegacyDatabaseName), DatabaseSnapshot.ForeignKeys(SqlServer.PortedDatabaseName), "foreign keys");
            AssertSame(DatabaseSnapshot.Sequences(SqlServer.LegacyDatabaseName), DatabaseSnapshot.Sequences(SqlServer.PortedDatabaseName), "sequences");

            // Known, intentional difference: EF6 also creates its __MigrationHistory model-hash table.
            AssertSame(
                DatabaseSnapshot.Tables(SqlServer.LegacyDatabaseName).Where(t => t != "name=__MigrationHistory").ToList(),
                DatabaseSnapshot.Tables(SqlServer.PortedDatabaseName),
                "tables");
            Assert.Contains("name=__MigrationHistory", DatabaseSnapshot.Tables(SqlServer.LegacyDatabaseName));
        }

        [SkippableFact]
        public void Default_seed_produces_identical_rows_keys_and_sequence_positions()
        {
            SqlServer.SkipIfUnavailable();

            LegacyHarness.CreateAndSeed(new Legacy.CatalogItemHiLoGenerator());
            PortedHarness.CreateAndSeed(new Ported.CatalogItemHiLoGenerator());

            AssertSame(DatabaseSnapshot.Data(SqlServer.LegacyDatabaseName), DatabaseSnapshot.Data(SqlServer.PortedDatabaseName), "data");
            AssertSame(DatabaseSnapshot.SequenceValues(SqlServer.LegacyDatabaseName), DatabaseSnapshot.SequenceValues(SqlServer.PortedDatabaseName), "sequence values");
            Assert.Equal(12, DatabaseSnapshot.Data(SqlServer.PortedDatabaseName).Count(r => r.StartsWith("Id=") && r.Contains("| Name=")));
        }

        [SkippableFact]
        public void Customization_seed_reads_the_same_csv_files_and_extracts_the_same_pictures()
        {
            SqlServer.SkipIfUnavailable();
            var legacyRoot = CreateContentRoot();
            var portedRoot = CreateContentRoot();
            try
            {
                LegacyHarness.CreateAndSeed(new Legacy.CatalogItemHiLoGenerator(), useCustomizationData: true, contentRoot: legacyRoot);
                PortedHarness.CreateAndSeed(new Ported.CatalogItemHiLoGenerator(), useCustomizationData: true, contentRoot: portedRoot);

                AssertSame(DatabaseSnapshot.Data(SqlServer.LegacyDatabaseName), DatabaseSnapshot.Data(SqlServer.PortedDatabaseName), "data");
                AssertSame(DatabaseSnapshot.SequenceValues(SqlServer.LegacyDatabaseName), DatabaseSnapshot.SequenceValues(SqlServer.PortedDatabaseName), "sequence values");
                AssertSame(PictureListing(legacyRoot), PictureListing(portedRoot), "Pics folder");
                Assert.DoesNotContain(PictureListing(portedRoot), f => f.StartsWith("stale.png"));
                Assert.NotEmpty(PictureListing(portedRoot));
            }
            finally
            {
                Directory.Delete(legacyRoot, recursive: true);
                Directory.Delete(portedRoot, recursive: true);
            }
        }

        [SkippableFact]
        public void Controller_scenario_behaves_identically()
        {
            SqlServer.SkipIfUnavailable();

            // As in the legacy Autofac container, the HiLo generator is a singleton shared by the initializer and services.
            var legacyGenerator = new Legacy.CatalogItemHiLoGenerator();
            var portedGenerator = new Ported.CatalogItemHiLoGenerator();
            LegacyHarness.CreateAndSeed(legacyGenerator);
            PortedHarness.CreateAndSeed(portedGenerator);

            var legacyLog = Replay(new LegacyTarget(legacyGenerator));
            var portedLog = Replay(new PortedTarget(portedGenerator));
            legacyLog.ForEach(output.WriteLine);

            AssertSame(legacyLog, portedLog, "scenario");
            AssertSame(DatabaseSnapshot.Data(SqlServer.LegacyDatabaseName), DatabaseSnapshot.Data(SqlServer.PortedDatabaseName), "data");
            AssertSame(DatabaseSnapshot.SequenceValues(SqlServer.LegacyDatabaseName), DatabaseSnapshot.SequenceValues(SqlServer.PortedDatabaseName), "sequence values");
        }

        [SkippableFact]
        public void Ported_layer_works_against_a_database_created_by_the_legacy_app()
        {
            SqlServer.SkipIfUnavailable();

            LegacyHarness.CreateAndSeed(new Legacy.CatalogItemHiLoGenerator());
            var legacyLog = Replay(new LegacyTarget(new Legacy.CatalogItemHiLoGenerator()));
            var legacyData = DatabaseSnapshot.Everything(SqlServer.LegacyDatabaseName);

            SqlServer.DropDatabase(SqlServer.LegacyDatabaseName);
            LegacyHarness.CreateAndSeed(new Legacy.CatalogItemHiLoGenerator());
            var portedGenerator = new Ported.CatalogItemHiLoGenerator();
            Assert.False(PortedHarness.CreateAndSeed(portedGenerator, database: SqlServer.LegacyDatabaseName));
            var portedLog = Replay(new PortedTarget(portedGenerator, SqlServer.LegacyDatabaseName));

            AssertSame(legacyLog, portedLog, "scenario");
            AssertSame(legacyData, DatabaseSnapshot.Everything(SqlServer.LegacyDatabaseName), "database");
        }

        [SkippableFact]
        public void Legacy_app_works_against_a_database_created_by_the_ported_layer()
        {
            SqlServer.SkipIfUnavailable();

            PortedHarness.CreateAndSeed(new Ported.CatalogItemHiLoGenerator());
            var portedLog = Replay(new PortedTarget(new Ported.CatalogItemHiLoGenerator()));

            PortedHarness.CreateAndSeed(new Ported.CatalogItemHiLoGenerator(), database: SqlServer.LegacyDatabaseName);
            // EF6's CreateDatabaseIfNotExists must accept the database (no __MigrationHistory) and leave it alone.
            LegacyHarness.CreateAndSeed(new Legacy.CatalogItemHiLoGenerator());
            var legacyLog = Replay(new LegacyTarget(new Legacy.CatalogItemHiLoGenerator()));

            AssertSame(legacyLog, portedLog, "scenario");
            AssertSame(DatabaseSnapshot.Everything(SqlServer.LegacyDatabaseName), DatabaseSnapshot.Everything(SqlServer.PortedDatabaseName), "database");
        }

        [SkippableFact]
        public void Initializing_an_existing_database_does_not_seed_again()
        {
            SqlServer.SkipIfUnavailable();

            LegacyHarness.CreateAndSeed(new Legacy.CatalogItemHiLoGenerator());
            LegacyHarness.CreateAndSeed(new Legacy.CatalogItemHiLoGenerator());
            Assert.True(PortedHarness.CreateAndSeed(new Ported.CatalogItemHiLoGenerator()));
            Assert.False(PortedHarness.CreateAndSeed(new Ported.CatalogItemHiLoGenerator()));

            AssertSame(DatabaseSnapshot.Everything(SqlServer.LegacyDatabaseName), DatabaseSnapshot.Everything(SqlServer.PortedDatabaseName), "database");
        }

        [SkippableFact]
        public void HiLo_generator_hands_out_unique_ids_across_threads_and_contexts()
        {
            SqlServer.SkipIfUnavailable();
            PortedHarness.CreateAndSeed(new Ported.CatalogItemHiLoGenerator());
            var generator = new Ported.CatalogItemHiLoGenerator();
            var ids = new ConcurrentBag<int>();

            Parallel.For(0, 8, _ =>
            {
                using var context = PortedHarness.CreateContext();
                for (var i = 0; i < 25; i++)
                {
                    ids.Add(generator.GetNextSequenceValue(context));
                }
            });

            // Seeding consumed catalog_hilo blocks 1 and 11; 200 more ids take exactly 20 further blocks.
            Assert.Equal(Enumerable.Range(21, 200), ids.OrderBy(id => id));
            Assert.Contains("name=catalog_hilo | current_value=211", DatabaseSnapshot.SequenceValues(SqlServer.PortedDatabaseName));
        }

        private static string CreateContentRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "eshop-parity-" + Guid.NewGuid().ToString("N"));
            var setup = Path.Combine(root, "Setup");
            Directory.CreateDirectory(setup);
            foreach (var file in Directory.GetFiles(Path.Combine(SqlServer.LegacySourceDirectory(), "Setup")))
            {
                File.Copy(file, Path.Combine(setup, Path.GetFileName(file)));
            }
            Directory.CreateDirectory(Path.Combine(root, "Pics"));
            File.WriteAllText(Path.Combine(root, "Pics", "stale.png"), "stale");
            return root;
        }

        private static List<string> PictureListing(string root) =>
            new DirectoryInfo(Path.Combine(root, "Pics")).GetFiles().OrderBy(f => f.Name).Select(f => $"{f.Name}:{f.Length}").ToList();
    }
}
