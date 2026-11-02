using System.IO.Compression;
using GeoTimeZone.Internal;

namespace GeoTimeZone.DataBuilder.Tests;

public class CompressedResourceTests
{
    [Fact]
    public void Reads_Seekable_And_Nonseekable_Resources_Without_Closing_The_Caller_Stream()
    {
        var bytes = new byte[200000];
        new Random(32).NextBytes(bytes);
        using var compressed = Compress(bytes);
        Assert.Equal(bytes, SpatialFormat.Decompress(compressed));
        Assert.True(compressed.CanRead);
        compressed.Position = 0;
        using var nonseekable = new NonseekableStream(compressed);
        Assert.Equal(bytes, SpatialFormat.Decompress(nonseekable));
        Assert.True(compressed.CanRead);
    }

    [Fact]
    public void Handles_Short_Reads_And_A_Member_Starting_At_A_Nonzero_Position()
    {
        var expected = Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray();
        using var compressed = Compress(expected);
        using var prefixed = new MemoryStream();
        prefixed.Write(new byte[13], 0, 13);
        compressed.CopyTo(prefixed);
        prefixed.Position = 13;
        Assert.Equal(expected, SpatialFormat.Decompress(prefixed));
        prefixed.Position = 13;
        using var fragmented = new NonseekableStream(prefixed);
        Assert.Equal(expected, SpatialFormat.Decompress(fragmented));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void Rejects_Incorrect_Uncompressed_Length(int claimedLength)
    {
        using var compressed = Compress([1, 2, 3, 4]);
        var bytes = compressed.ToArray();
        BitConverter.GetBytes(claimedLength).CopyTo(bytes, bytes.Length - 4);
        Assert.Throws<InvalidDataException>(() => SpatialFormat.Decompress(new MemoryStream(bytes)));
    }

    [Fact]
    public void Handles_Empty_Members()
    {
        using var compressed = Compress([]);
        Assert.Empty(SpatialFormat.Decompress(compressed));
    }

    [Fact]
    public void Rejects_Implausible_Lengths_Before_Allocating_And_Checks_Crc()
    {
        using var compressed = Compress([1, 2, 3, 4]);
        var excessive = compressed.ToArray();
        excessive[excessive.Length - 1] = 0x7f;
        Assert.Throws<InvalidDataException>(() => SpatialFormat.Decompress(new MemoryStream(excessive)));
        var corrupt = compressed.ToArray();
        corrupt[corrupt.Length - 8] ^= 1; // CRC32, before the four-byte ISIZE field
        Assert.Throws<InvalidDataException>(() => SpatialFormat.Decompress(new MemoryStream(corrupt)));
    }

    private static MemoryStream Compress(byte[] bytes)
    {
        var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionLevel.Optimal, true)) gzip.Write(bytes, 0, bytes.Length);
        stream.Position = 0;
        return stream;
    }

    private sealed class NonseekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(count, 7));
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
