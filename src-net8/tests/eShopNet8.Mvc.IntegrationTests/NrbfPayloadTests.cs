using System.Formats.Nrbf;
using System.Security.Cryptography;
using eShop.Parity;
using Xunit;

namespace eShopNet8.Mvc.IntegrationTests
{
    /// <summary>
    /// /api/files returned a BinaryFormatter (MS-NRBF) payload in legacy. .NET 8 has no BinaryFormatter, so T4 writes the
    /// bytes explicitly; these tests pin the served bytes to the recorded payload and decode them with System.Formats.Nrbf.
    /// </summary>
    public class NrbfPayloadTests : IClassFixture<MvcAppFactory>
    {
        private const string BrandDto = "eShopLegacyMVC.Controllers.WebApi.FilesController+BrandDTO";
        private readonly MvcAppFactory factory;

        public NrbfPayloadTests(MvcAppFactory factory)
        {
            this.factory = factory;
        }

        private static byte[] RecordedPayload()
        {
            var body = Baseline.Transcript("mvc-api-files")["response"]["body"];
            var bytes = Convert.FromBase64String((string)body["base64"]);
            Assert.Equal((int)body["length"], bytes.Length);
            Assert.Equal((string)body["sha256"], Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            return bytes;
        }

        private static List<(int Id, string Brand)> Decode(byte[] payload)
        {
            using var stream = new MemoryStream(payload);
            Assert.True(NrbfDecoder.StartsWithPayloadHeader(payload));
            var list = Assert.IsAssignableFrom<ClassRecord>(NrbfDecoder.Decode(stream));
            Assert.Equal($"System.Collections.Generic.List`1[[{BrandDto}, eShopLegacyMVC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]",
                list.TypeName.FullName);
            var size = list.GetInt32("_size");
            var items = Assert.IsAssignableFrom<SZArrayRecord<SerializationRecord>>(list.GetArrayRecord("_items")).GetArray();
            return items.Take(size).Cast<ClassRecord>().Select(r =>
            {
                Assert.Equal(BrandDto, r.TypeName.FullName);
                Assert.Equal("eShopLegacyMVC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", r.TypeName.AssemblyName.FullName);
                return (r.GetInt32("<Id>k__BackingField"), r.GetString("<Brand>k__BackingField"));
            }).ToList();
        }

        private static readonly (int, string)[] Brands =
        {
            (1, "Azure"), (2, ".NET"), (3, "Visual Studio"), (4, "SQL Server"), (5, "Other"),
        };

        [Fact]
        public void Recorded_legacy_payload_decodes_to_the_five_brand_dtos()
        {
            Assert.Equal(Brands, Decode(RecordedPayload()));
        }

        [Fact]
        public async Task Api_files_serves_the_recorded_bytes_exactly()
        {
            using var client = factory.CreateParityClient();
            using var response = await client.GetAsync("api/files");

            Assert.Equal(200, (int)response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            var served = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(Convert.ToBase64String(RecordedPayload()), Convert.ToBase64String(served));
            Assert.Equal(Brands, Decode(served));
        }
    }
}
