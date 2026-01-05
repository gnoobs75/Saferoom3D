using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SafeRoom3D.Core;

/// <summary>
/// Encodes and decodes tile data for WYSIWYG tile-mode maps.
/// Uses RLE (Run-Length Encoding) + GZip compression + Base64 for efficient storage.
/// </summary>
public static class TileDataEncoder
{
    /// <summary>
    /// Encodes a 2D tile array to a compressed Base64 string.
    /// Uses RLE for run-length encoding, then GZip compression.
    /// </summary>
    /// <param name="tiles">2D array where tiles[x,z] = 0 (void) or 1 (floor)</param>
    /// <returns>Base64 encoded compressed string</returns>
    public static string Encode(int[,] tiles)
    {
        int width = tiles.GetLength(0);
        int depth = tiles.GetLength(1);

        // Debug: Log encoding info
        Godot.GD.Print($"[TileDataEncoder] ENCODE: Array dimensions GetLength(0)={width}, GetLength(1)={depth}");

        // Find first floor tile for debug
        for (int z = 0; z < Math.Min(depth, 30); z++)
        {
            for (int x = 0; x < Math.Min(width, 30); x++)
            {
                if (tiles[x, z] == 1)
                {
                    Godot.GD.Print($"[TileDataEncoder] ENCODE: First floor at tiles[{x},{z}]");
                    goto DoneEncode;
                }
            }
        }
        DoneEncode:

        // Step 1: Run-Length Encode the tile data
        var rleData = RunLengthEncode(tiles, width, depth);

        // Step 2: GZip compress the RLE data
        var compressedData = GZipCompress(rleData);

        // Step 3: Base64 encode
        return Convert.ToBase64String(compressedData);
    }

    /// <summary>
    /// Decodes a Base64 compressed string back to a 2D tile array.
    /// </summary>
    /// <param name="encodedData">Base64 encoded compressed string</param>
    /// <param name="width">Expected map width</param>
    /// <param name="depth">Expected map depth</param>
    /// <returns>2D array where tiles[x,z] = 0 (void) or 1 (floor)</returns>
    public static int[,] Decode(string encodedData, int width, int depth)
    {
        Godot.GD.Print($"[TileDataEncoder] DECODE: Expected dimensions width={width}, depth={depth}");

        if (string.IsNullOrEmpty(encodedData))
        {
            return new int[width, depth]; // Return empty (all void) map
        }

        try
        {
            // Step 1: Base64 decode
            var compressedData = Convert.FromBase64String(encodedData);

            // Step 2: GZip decompress
            var rleData = GZipDecompress(compressedData);

            // Step 3: Run-Length Decode
            var tiles = RunLengthDecode(rleData, width, depth);

            // Debug: Find first floor tile
            for (int z = 0; z < Math.Min(depth, 30); z++)
            {
                for (int x = 0; x < Math.Min(width, 30); x++)
                {
                    if (tiles[x, z] == 1)
                    {
                        Godot.GD.Print($"[TileDataEncoder] DECODE: First floor at tiles[{x},{z}]");
                        goto DoneDecode;
                    }
                }
            }
            DoneDecode:

            return tiles;
        }
        catch (Exception ex)
        {
            Godot.GD.PrintErr($"[TileDataEncoder] Failed to decode tile data: {ex.Message}");
            return new int[width, depth]; // Return empty map on error
        }
    }

    /// <summary>
    /// Run-length encodes tile data. Format: [value, count] pairs as bytes.
    /// For longer runs, uses multi-byte counts.
    /// </summary>
    private static byte[] RunLengthEncode(int[,] tiles, int width, int depth)
    {
        var output = new List<byte>();

        // Add header with width and depth (4 bytes each, little-endian)
        output.AddRange(BitConverter.GetBytes(width));
        output.AddRange(BitConverter.GetBytes(depth));

        int currentValue = -1;
        int runLength = 0;

        // Iterate row-by-row (Z then X for memory locality)
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int value = tiles[x, z] > 0 ? 1 : 0; // Normalize to 0 or 1

                if (value == currentValue && runLength < 65535)
                {
                    runLength++;
                }
                else
                {
                    // Write previous run if exists
                    if (runLength > 0)
                    {
                        WriteRun(output, currentValue, runLength);
                    }
                    currentValue = value;
                    runLength = 1;
                }
            }
        }

        // Write final run
        if (runLength > 0)
        {
            WriteRun(output, currentValue, runLength);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Writes a run to the output list.
    /// Format:
    /// - If count <= 63: [value | (count << 1)]  (single byte, bits 1-6 = count, bit 0 = value, bit 7 = 0)
    /// - If count > 63: [0x80 | value, count_low, count_high] (3 bytes, bit 7 = 1 marks multi-byte)
    /// </summary>
    private static void WriteRun(List<byte> output, int value, int count)
    {
        // BUG FIX: count << 1 must not set bit 7, so max single-byte count is 63 (63 << 1 = 126)
        if (count <= 63)
        {
            // Single byte: bit 0 = value, bits 1-6 = count, bit 7 = 0
            output.Add((byte)((count << 1) | value));
        }
        else
        {
            // Multi-byte: marker byte (bit 7 = 1) + 2-byte count
            output.Add((byte)(0x80 | value)); // Marker with value in bit 0
            output.Add((byte)(count & 0xFF));
            output.Add((byte)((count >> 8) & 0xFF));
        }
    }

    /// <summary>
    /// Run-length decodes byte data back to tile array.
    /// </summary>
    private static int[,] RunLengthDecode(byte[] data, int expectedWidth, int expectedDepth)
    {
        var tiles = new int[expectedWidth, expectedDepth];

        if (data.Length < 8)
        {
            return tiles; // Invalid data, return empty
        }

        // Read header
        int storedWidth = BitConverter.ToInt32(data, 0);
        int storedDepth = BitConverter.ToInt32(data, 4);

        Godot.GD.Print($"[TileDataEncoder] RLE_DECODE: Header storedWidth={storedWidth}, storedDepth={storedDepth}");

        // Use the smaller of stored and expected dimensions
        int width = Math.Min(storedWidth, expectedWidth);
        int depth = Math.Min(storedDepth, expectedDepth);

        int dataIndex = 8;
        int x = 0;
        int z = 0;

        while (dataIndex < data.Length && z < depth)
        {
            byte firstByte = data[dataIndex++];
            int value;
            int count;

            if ((firstByte & 0x80) != 0)
            {
                // Multi-byte run
                value = firstByte & 0x01;
                if (dataIndex + 1 >= data.Length) break;
                count = data[dataIndex] | (data[dataIndex + 1] << 8);
                dataIndex += 2;
            }
            else
            {
                // Single byte run
                value = firstByte & 0x01;
                count = firstByte >> 1;
            }

            // Apply the run
            for (int i = 0; i < count && z < depth; i++)
            {
                if (x < width)
                {
                    tiles[x, z] = value;
                }
                x++;
                if (x >= storedWidth)
                {
                    x = 0;
                    z++;
                }
            }
        }

        return tiles;
    }

    /// <summary>
    /// Compresses data using GZip.
    /// </summary>
    private static byte[] GZipCompress(byte[] data)
    {
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
        {
            gzipStream.Write(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }

    /// <summary>
    /// Decompresses GZip data.
    /// </summary>
    private static byte[] GZipDecompress(byte[] compressedData)
    {
        using var inputStream = new MemoryStream(compressedData);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        gzipStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    /// <summary>
    /// Creates a simple tile array for testing.
    /// </summary>
    public static int[,] CreateEmptyTileArray(int width, int depth)
    {
        return new int[width, depth]; // All zeros (void)
    }

    /// <summary>
    /// Fills a rectangular area in a tile array with floor tiles.
    /// </summary>
    public static void FillRect(int[,] tiles, int startX, int startZ, int endX, int endZ, int value = 1)
    {
        int width = tiles.GetLength(0);
        int depth = tiles.GetLength(1);

        int minX = Math.Clamp(Math.Min(startX, endX), 0, width - 1);
        int maxX = Math.Clamp(Math.Max(startX, endX), 0, width - 1);
        int minZ = Math.Clamp(Math.Min(startZ, endZ), 0, depth - 1);
        int maxZ = Math.Clamp(Math.Max(startZ, endZ), 0, depth - 1);

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                tiles[x, z] = value;
            }
        }
    }

    /// <summary>
    /// Counts the number of floor tiles in an array.
    /// </summary>
    public static int CountFloorTiles(int[,] tiles)
    {
        int count = 0;
        int width = tiles.GetLength(0);
        int depth = tiles.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (tiles[x, z] > 0) count++;
            }
        }

        return count;
    }
}
