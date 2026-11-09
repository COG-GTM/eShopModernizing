using System.Buffers.Binary;
using System.Text;

namespace EShop.Storefront.Mvc.Api;

/// <summary>
/// Hand-written MS-NRBF (BinaryFormatter wire format, [MS-NRBF]) writer for exactly the payload legacy
/// <c>GET /api/files</c> produced: <c>List&lt;eShopLegacyMVC.Controllers.WebApi.FilesController+BrandDTO&gt;</c>.
/// Nothing is ever deserialized and BinaryFormatter stays disabled.
/// </summary>
internal static class BrandListNrbfWriter
{
    internal readonly record struct BrandDto(int Id, string? Brand);

    private const string LibraryName = "eShopLegacyMVC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
    private const string BrandDtoType = "eShopLegacyMVC.Controllers.WebApi.FilesController+BrandDTO";
    private const string ListType = "System.Collections.Generic.List`1[[" + BrandDtoType + ", " + LibraryName + "]]";

    private const int LibraryId = 2;
    private const int ListObjectId = 1;
    private const int ArrayObjectId = 3;
    private const int FirstItemObjectId = 4;

    private enum RecordType : byte
    {
        SerializedStreamHeader = 0,
        ClassWithId = 1,
        SystemClassWithMembersAndTypes = 4,
        ClassWithMembersAndTypes = 5,
        BinaryObjectString = 6,
        BinaryArray = 7,
        MemberReference = 9,
        ObjectNull = 10,
        MessageEnd = 11,
        BinaryLibrary = 12,
        ObjectNullMultiple256 = 13,
        ObjectNullMultiple = 14,
    }

    private enum BinaryType : byte
    {
        Primitive = 0,
        String = 1,
        Class = 4,
    }

    private const byte PrimitiveInt32 = 8;

    /// <summary>Backing-array length of a <c>List&lt;T&gt;</c> filled by repeated <c>Add</c> (0, 4, 8, 16, ...).</summary>
    public static int GrownCapacity(int count)
    {
        var capacity = 0;
        while (capacity < count)
        {
            capacity = capacity == 0 ? 4 : capacity * 2;
        }

        return capacity;
    }

    /// <param name="items">List contents (<c>_size</c> = <c>_version</c> = count, as each was added once).</param>
    /// <param name="capacity">Length of the serialized <c>_items</c> array (trailing slots are null).</param>
    public static byte[] Write(IReadOnlyList<BrandDto> items, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, items.Count);

        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream, Encoding.UTF8);

        // SerializationHeaderRecord: RootId 1, HeaderId -1, version 1.0
        w.Write((byte)RecordType.SerializedStreamHeader);
        w.Write(ListObjectId);
        w.Write(-1);
        w.Write(1);
        w.Write(0);

        w.Write((byte)RecordType.BinaryLibrary);
        w.Write(LibraryId);
        w.Write(LibraryName); // BinaryWriter strings are 7-bit length-prefixed UTF-8, as LengthPrefixedString

        // List<BrandDTO>: _items (BrandDTO[]), _size (Int32), _version (Int32)
        w.Write((byte)RecordType.SystemClassWithMembersAndTypes);
        w.Write(ListObjectId);
        w.Write(ListType);
        w.Write(3);
        w.Write("_items");
        w.Write("_size");
        w.Write("_version");
        w.Write((byte)BinaryType.Class);
        w.Write((byte)BinaryType.Primitive);
        w.Write((byte)BinaryType.Primitive);
        w.Write(BrandDtoType + "[]");
        w.Write(LibraryId);
        w.Write(PrimitiveInt32);
        w.Write(PrimitiveInt32);
        w.Write((byte)RecordType.MemberReference);
        w.Write(ArrayObjectId);
        w.Write(items.Count);
        w.Write(items.Count);

        // BrandDTO[]: single-dimension array of class references
        w.Write((byte)RecordType.BinaryArray);
        w.Write(ArrayObjectId);
        w.Write((byte)0); // BinaryArrayType.Single
        w.Write(1); // rank
        w.Write(capacity);
        w.Write((byte)BinaryType.Class);
        w.Write(BrandDtoType);
        w.Write(LibraryId);
        for (var i = 0; i < items.Count; i++)
        {
            w.Write((byte)RecordType.MemberReference);
            w.Write(FirstItemObjectId + i);
        }

        WriteNulls(w, capacity - items.Count);

        // Objects in reference order; strings get ids after every BrandDTO, in write order.
        var nextObjectId = FirstItemObjectId + items.Count;
        for (var i = 0; i < items.Count; i++)
        {
            if (i == 0)
            {
                w.Write((byte)RecordType.ClassWithMembersAndTypes);
                w.Write(FirstItemObjectId);
                w.Write(BrandDtoType);
                w.Write(2);
                w.Write("<Id>k__BackingField");
                w.Write("<Brand>k__BackingField");
                w.Write((byte)BinaryType.Primitive);
                w.Write((byte)BinaryType.String);
                w.Write(PrimitiveInt32);
                w.Write(LibraryId);
            }
            else
            {
                w.Write((byte)RecordType.ClassWithId);
                w.Write(FirstItemObjectId + i);
                w.Write(FirstItemObjectId); // metadata id of the first BrandDTO record
            }

            w.Write(items[i].Id);
            if (items[i].Brand is null)
            {
                w.Write((byte)RecordType.ObjectNull);
            }
            else
            {
                w.Write((byte)RecordType.BinaryObjectString);
                w.Write(nextObjectId++);
                w.Write(items[i].Brand!);
            }
        }

        w.Write((byte)RecordType.MessageEnd);
        w.Flush();
        return stream.ToArray();
    }

    private static void WriteNulls(BinaryWriter w, int count)
    {
        if (count == 1)
        {
            w.Write((byte)RecordType.ObjectNull);
        }
        else if (count is > 1 and < 256)
        {
            w.Write((byte)RecordType.ObjectNullMultiple256);
            w.Write((byte)count);
        }
        else if (count >= 256)
        {
            w.Write((byte)RecordType.ObjectNullMultiple);
            w.Write(count);
        }
    }
}
