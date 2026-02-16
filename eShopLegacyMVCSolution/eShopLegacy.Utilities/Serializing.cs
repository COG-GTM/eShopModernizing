using System.IO;
using System.Text.Json;

namespace eShopLegacy.Utilities
{
    public class Serializing
    {
        public Stream SerializeJson(object input)
        {
            var stream = new MemoryStream();
            JsonSerializer.Serialize(stream, input);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public T DeserializeJson<T>(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            return JsonSerializer.Deserialize<T>(stream);
        }
    }
}
