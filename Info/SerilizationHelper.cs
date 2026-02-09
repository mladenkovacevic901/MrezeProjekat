using System;
using System.Text;
using System.Text.Json;

namespace Common
{
    /// <summary>
    /// Helper klasa za serijalizaciju/deserijalizaciju objekata
    /// Zamena za zastareli BinaryFormatter
    /// </summary>
    public static class SerializationHelper
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        /// <summary>
        /// Serijalizuje objekat u byte array (JSON format)
        /// </summary>
        public static byte[] Serialize<T>(T obj)
        {
            try
            {
                string json = JsonSerializer.Serialize(obj, _options);
                return Encoding.UTF8.GetBytes(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Greška pri serijalizaciji objekta tipa {typeof(T).Name}", ex);
            }
        }

        /// <summary>
        /// Deserijalizuje byte array u objekat (JSON format)
        /// </summary>
        public static T? Deserialize<T>(byte[] data, int offset = 0, int? length = null)
        {
            try
            {
                int actualLength = length ?? (data.Length - offset);
                string json = Encoding.UTF8.GetString(data, offset, actualLength);
                return JsonSerializer.Deserialize<T>(json, _options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Greška pri deserijalizaciji objekta tipa {typeof(T).Name}", ex);
            }
        }

        /// <summary>
        /// Deserijalizuje byte array u objekat (JSON format) - verzija sa ReadOnlySpan
        /// </summary>
        public static T? Deserialize<T>(ReadOnlySpan<byte> data)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(data, _options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Greška pri deserijalizaciji objekta tipa {typeof(T).Name}", ex);
            }
        }
    }
}