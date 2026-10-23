using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using eShop.Catalog.Core.Models;

namespace eShopNet8.Mvc.Api
{
    /// <summary>
    /// XML shape of Web API 2's XmlMediaTypeFormatter (DataContractSerializer) for legacy
    /// <c>eShopLegacyMVC.Models.CatalogBrand</c>: same contract namespace and root names, members alphabetical.
    /// </summary>
    public static class LegacyBrandXml
    {
        public const string LegacyNamespace = "http://schemas.datacontract.org/2004/07/eShopLegacyMVC.Models";

        [DataContract(Name = "CatalogBrand", Namespace = LegacyNamespace)]
        public sealed class CatalogBrandContract
        {
            [DataMember]
            public int Id { get; set; }

            [DataMember]
            public string Brand { get; set; }
        }

        [CollectionDataContract(Name = "ArrayOfCatalogBrand", Namespace = LegacyNamespace, ItemName = "CatalogBrand")]
        public sealed class CatalogBrandListContract : List<CatalogBrandContract>
        {
        }

        public static byte[] Serialize(CatalogBrand brand)
        {
            return Write(ToContract(brand));
        }

        public static byte[] Serialize(IEnumerable<CatalogBrand> brands)
        {
            var list = new CatalogBrandListContract();
            list.AddRange(brands.Select(ToContract));
            return Write(list);
        }

        private static CatalogBrandContract ToContract(CatalogBrand brand)
        {
            return brand == null ? null : new CatalogBrandContract { Id = brand.Id, Brand = brand.Brand };
        }

        private static byte[] Write<T>(T value)
        {
            using var stream = new MemoryStream();
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                CloseOutput = false,
            };
            using (var writer = XmlWriter.Create(stream, settings))
            {
                new DataContractSerializer(typeof(T)).WriteObject(writer, value);
            }
            return stream.ToArray();
        }
    }
}
