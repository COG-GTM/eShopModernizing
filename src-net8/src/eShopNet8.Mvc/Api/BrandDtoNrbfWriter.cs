using System.Text;

namespace eShopNet8.Mvc.Api
{
    /// <summary>
    /// Writes a <c>List&lt;FilesController+BrandDTO&gt;</c> in the MS-NRBF format exactly as the legacy
    /// <c>BinaryFormatter</c> did for <c>GET /api/files</c>, without using BinaryFormatter (disabled in .NET 8).
    /// Only this one shape is supported; type and assembly names are the legacy ones so existing
    /// .NET Framework clients can still deserialize the payload.
    /// </summary>
    /// <remarks>
    /// Record layout (MS-NRBF 2.x), object ids allocated in the same order as BinaryFormatter:
    /// SerializationHeader(root 1) | BinaryLibrary(2, eShopLegacyMVC) |
    /// SystemClassWithMembersAndTypes(1, List`1: _items, _size, _version) + MemberReference(3), size, version |
    /// BinaryArray(3, Single, BrandDTO[]) + one MemberReference per element (4..3+n) |
    /// ClassWithMembersAndTypes(4, BrandDTO: Id, Brand) then ClassWithId(i, 4) per element, each followed by
    /// Int32 Id and BinaryObjectString / MemberReference (same string instance) / ObjectNull for Brand |
    /// MessageEnd.
    /// </remarks>
    public static class BrandDtoNrbfWriter
    {
        public const string LegacyAssemblyName = "eShopLegacyMVC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        public const string LegacyBrandDtoTypeName = "eShopLegacyMVC.Controllers.WebApi.FilesController+BrandDTO";
        public const string LegacyListTypeName =
            "System.Collections.Generic.List`1[[" + LegacyBrandDtoTypeName + ", " + LegacyAssemblyName + "]]";

        private const byte SerializedStreamHeader = 0;
        private const byte ClassWithId = 1;
        private const byte SystemClassWithMembersAndTypes = 4;
        private const byte ClassWithMembersAndTypes = 5;
        private const byte BinaryObjectString = 6;
        private const byte BinaryArray = 7;
        private const byte MemberReference = 9;
        private const byte ObjectNull = 10;
        private const byte MessageEnd = 11;
        private const byte BinaryLibrary = 12;

        private const byte BinaryTypePrimitive = 0;
        private const byte BinaryTypeString = 1;
        private const byte BinaryTypeClass = 4;
        private const byte PrimitiveInt32 = 8;
        private const byte BinaryArrayTypeSingle = 0;

        private const int ListObjectId = 1;
        private const int LibraryId = 2;
        private const int ArrayObjectId = 3;
        private const int FirstElementObjectId = 4;

        public static byte[] Serialize(IReadOnlyList<BrandDto> brands)
        {
            ArgumentNullException.ThrowIfNull(brands);
            using var stream = new MemoryStream();
            Write(stream, brands);
            return stream.ToArray();
        }

        public static void Write(Stream output, IReadOnlyList<BrandDto> brands)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(brands);

            // The legacy list came from Enumerable.ToList() over a sized source, so capacity == count and
            // _version == count (one Add per element), as recorded in the baseline payload.
            int count = brands.Count;
            using var w = new BinaryWriter(output, new UTF8Encoding(false), leaveOpen: true);

            w.Write(SerializedStreamHeader);
            w.Write(ListObjectId);
            w.Write(-1);
            w.Write(1);
            w.Write(0);

            w.Write(BinaryLibrary);
            w.Write(LibraryId);
            w.Write(LegacyAssemblyName);

            w.Write(SystemClassWithMembersAndTypes);
            w.Write(ListObjectId);
            w.Write(LegacyListTypeName);
            w.Write(3);
            w.Write("_items");
            w.Write("_size");
            w.Write("_version");
            w.Write(BinaryTypeClass);
            w.Write(BinaryTypePrimitive);
            w.Write(BinaryTypePrimitive);
            w.Write(LegacyBrandDtoTypeName + "[]");
            w.Write(LibraryId);
            w.Write(PrimitiveInt32);
            w.Write(PrimitiveInt32);
            w.Write(MemberReference);
            w.Write(ArrayObjectId);
            w.Write(count);
            w.Write(count);

            w.Write(BinaryArray);
            w.Write(ArrayObjectId);
            w.Write(BinaryArrayTypeSingle);
            w.Write(1);
            w.Write(count);
            w.Write(BinaryTypeClass);
            w.Write(LegacyBrandDtoTypeName);
            w.Write(LibraryId);
            for (int i = 0; i < count; i++)
            {
                if (brands[i] == null)
                {
                    w.Write(ObjectNull);
                    continue;
                }
                w.Write(MemberReference);
                w.Write(FirstElementObjectId + i);
            }

            int nextObjectId = FirstElementObjectId + count;
            int? classMetadataId = null;
            var stringIds = new Dictionary<string, int>(ReferenceEqualityComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                var brand = brands[i];
                if (brand == null)
                {
                    continue;
                }

                int objectId = FirstElementObjectId + i;
                if (classMetadataId is int metadataId)
                {
                    w.Write(ClassWithId);
                    w.Write(objectId);
                    w.Write(metadataId);
                }
                else
                {
                    w.Write(ClassWithMembersAndTypes);
                    w.Write(objectId);
                    w.Write(LegacyBrandDtoTypeName);
                    w.Write(2);
                    w.Write("<Id>k__BackingField");
                    w.Write("<Brand>k__BackingField");
                    w.Write(BinaryTypePrimitive);
                    w.Write(BinaryTypeString);
                    w.Write(PrimitiveInt32);
                    w.Write(LibraryId);
                    classMetadataId = objectId;
                }

                w.Write(brand.Id);
                if (brand.Brand == null)
                {
                    w.Write(ObjectNull);
                }
                else if (stringIds.TryGetValue(brand.Brand, out int stringId))
                {
                    w.Write(MemberReference);
                    w.Write(stringId);
                }
                else
                {
                    stringId = nextObjectId++;
                    stringIds.Add(brand.Brand, stringId);
                    w.Write(BinaryObjectString);
                    w.Write(stringId);
                    w.Write(brand.Brand);
                }
            }

            w.Write(MessageEnd);
        }
    }
}
