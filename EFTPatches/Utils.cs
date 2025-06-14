using System.IO.Compression;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EFTPatches
{
    public static class Utils
    {
        public static bool IsZLibCompressed(byte[] data)
        {
            return data?.Length >= 2 && data[0] == 0x78 && (
                data[1] == 0x01 ||  // low compression
                data[1] == 0x5E ||  // medium compression
                data[1] == 0x9C ||  // high compression (default)
                data[1] == 0xDA);   // highest compression
        }

        public static byte[] DecompressZLib(byte[] zlibData)
        {
            // Skip the first 2 bytes (ZLib header) and last 4 bytes (checksum)
            var compressedLength = zlibData.Length - 6; // subtract header(2) + adler32(4)
            var compressedBytes = new byte[compressedLength];
            Buffer.BlockCopy(zlibData, 2, compressedBytes, 0, compressedLength);

            using (var input = new MemoryStream(compressedBytes))
            using (var decompressor = new DeflateStream(input, CompressionMode.Decompress))
            using (var result = new MemoryStream())
            {
                decompressor.CopyTo(result);
                return result.ToArray();
            }
        }
        public static string HeadersToString(Dictionary<string, string> headers, string separator)
        {
            if (headers == null) return string.Empty;

            var result = headers.Aggregate(string.Empty,
                (current, item) => string.Concat(current, "{", item.Key, ":", item.Value, "}", separator));

            return result.TrimEnd(separator.ToCharArray());
        }
    }
}