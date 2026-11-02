using GeoTimeZone.Internal;

namespace GeoTimeZone.Tests;

public class DataBundleTests
{
    [Fact]
    public void Bundle_Reads_Real_Index_And_Lazy_Seekable_Geometry_Without_Full_Resource_Buffering()
    {
        var bytes = BundleFixtures.Create();
        long read = 0;
        var opens = 0;
        var closed = 0;
        var data = DataBundle.Read(() => { opens++; return new TrackingStream(new MemoryStream(bytes, false), count => read += count, () => closed++); }, copyGeometry: true);
        Assert.Equal(new[] { "Test/A", "Test/B" }, data.Index.Zones);
        Assert.False(data.Geometry.IsValueCreated);
        Assert.Equal(opens, closed);
        var result = TimeZoneLookup.Resolve(data.Index, data.Geometry, 0.5, 0.5);
        Assert.Equal("Test/A", result.Result);
        Assert.Equal(new[] { "Test/B" }, result.AlternativeResults);
        Assert.Equal(opens, closed);
        var loaded = data.Geometry.Value.GetPolygon(0);
        Assert.Same(loaded, data.Geometry.Value.GetPolygon(0));
        // ZIP directory discovery reads at most the final 64 KiB; no whole
        // geometry copy is needed for the real embedded 26 MB bundle.
        long embeddedReads = 0;
        var embedded = DataBundle.Read(() => new TrackingStream(typeof(TimeZoneLookup).Assembly.GetManifestResourceStream(DataBundle.ResourceName)!,
            count => embeddedReads += count, () => { }));
        Assert.False(embedded.Geometry.IsValueCreated);
        Assert.InRange(embeddedReads, 1, 256 * 1024);
        Assert.True(read > 0);
    }

    [Theory]
    [InlineData("missing-manifest")]
    [InlineData("wrong-version")]
    [InlineData("missing-index")]
    [InlineData("missing-geometry")]
    [InlineData("bad-index")]
    [InlineData("mixed-geometry")]
    [InlineData("corrupt-component")]
    public void Invalid_Bundles_Are_Rejected(string failure)
    {
        var bytes = BundleFixtures.Create(change: entries =>
        {
            switch (failure)
            {
                case "missing-manifest": entries.Remove("manifest.txt"); break;
                case "wrong-version": entries["manifest.txt"] = System.Text.Encoding.UTF8.GetBytes("GeoTimeZone/999"); break;
                case "missing-index": entries.Remove(DataBundle.IndexName); break;
                case "missing-geometry": entries.Remove(DataBundle.GeometryName); break;
                case "bad-index": entries[DataBundle.IndexName] = new byte[32]; break;
                case "mixed-geometry": entries[DataBundle.GeometryName][16] ^= 1; break;
                case "corrupt-component": entries[DataBundle.GeometryName][entries[DataBundle.GeometryName].Length - 8] ^= 1; break;
            }
        });
        Assert.Throws<InvalidDataException>(() => DataBundle.Read(() => new MemoryStream(bytes, false), copyGeometry: true).Validate(CancellationToken.None));
    }

    [Fact]
    public void Duplicates_And_Outer_Compressed_Geometry_Are_Rejected()
    {
        var entries = BundleFixtures.Entries(BundleFixtures.Create());
        foreach (var zip in new[] { BundleFixtures.Zip(entries, duplicate: true), BundleFixtures.Zip(entries, compressGeometry: true) })
            Assert.Throws<InvalidDataException>(() => DataBundle.Read(() => new MemoryStream(zip, false)));
    }

    [Fact]
    public void Truncated_Zip_Headers_And_Directories_Are_Rejected()
    {
        var bytes = BundleFixtures.Create();
        foreach (var length in new[] { 0, 4, 21, 22, bytes.Length / 2, bytes.Length - 22, bytes.Length - 1 })
        {
            var truncated = bytes.Take(length).ToArray();
            Assert.Throws<InvalidDataException>(() => DataBundle.Read(() => new MemoryStream(truncated, false)));
        }
    }

    [Fact]
    public void Index_Is_Raw_Data_Compressed_Only_By_The_Zip_Entry()
    {
        var entries = BundleFixtures.Entries(BundleFixtures.Create());
        var bytes = DataBundle.Write(entries[DataBundle.IndexName], entries[DataBundle.GeometryName]);
        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes, false), System.IO.Compression.ZipArchiveMode.Read);
        Assert.Null(archive.GetEntry("TZIndex.dat.gz"));
        var index = archive.GetEntry("TZIndex.dat")!;
        Assert.True(index.CompressedLength < index.Length);
        using var input = index.Open();
        using var reader = new BinaryReader(input);
        Assert.Equal(SpatialFormat.IndexMagic, reader.ReadInt32());
    }

    [Fact]
    public void Bundle_Output_Is_Deterministic_And_Validation_Does_Not_Populate_The_Runtime_Cache()
    {
        var entries = BundleFixtures.Entries(BundleFixtures.Create());
        var first = DataBundle.Write(entries[DataBundle.IndexName], entries[DataBundle.GeometryName]);
        Assert.Equal(first, DataBundle.Write(entries[DataBundle.IndexName], entries[DataBundle.GeometryName]));
        var data = DataBundle.Read(() => new MemoryStream(first, false), copyGeometry: true);
        data.Validate(CancellationToken.None);
        Assert.False(data.Geometry.IsValueCreated);
        Assert.Equal(0, data.Geometry.Value.CachedCount);
    }

    [Fact]
    public void Stored_Entry_Parser_Rejects_Encrypted_Mismatched_And_Out_Of_Range_Metadata()
    {
        var original = BundleFixtures.Create();
        var end = original.Length - 22;
        var directory = BitConverter.ToInt32(original, end + 16);
        var geometryRecord = directory;
        while (System.Text.Encoding.UTF8.GetString(original, geometryRecord + 46,
            BitConverter.ToUInt16(original, geometryRecord + 28)) != DataBundle.GeometryName)
            geometryRecord += 46 + BitConverter.ToUInt16(original, geometryRecord + 28)
                + BitConverter.ToUInt16(original, geometryRecord + 30) + BitConverter.ToUInt16(original, geometryRecord + 32);
        var local = BitConverter.ToInt32(original, geometryRecord + 42);
        foreach (var edit in new Action<byte[]>[]
        {
            bytes => bytes[geometryRecord + 8] |= 1, // encryption flag
            bytes => bytes[local + 8] = 8, // local method differs from stored directory entry
            bytes => BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, geometryRecord + 42),
            bytes => BitConverter.GetBytes((ushort)17).CopyTo(bytes, end + 10), // excessive entry count
            bytes => BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, end + 16),
            bytes => bytes[local + 18] ^= 1 // local compressed size differs from directory
        })
        {
            var damaged = (byte[])original.Clone();
            edit(damaged);
            Assert.Throws<InvalidDataException>(() => DataBundle.Read(() => new MemoryStream(damaged, false)));
        }
    }

    [Fact]
    public void Validation_Can_Be_Canceled_Between_Component_Loads_Without_Populating_The_Cache()
    {
        using var cancellation = new CancellationTokenSource();
        var opened = 0;
        var data = DataBundle.Read(() =>
        {
            if (++opened == 3) cancellation.Cancel(); // first component after ZIP and archive directories
            return typeof(TimeZoneLookup).Assembly.GetManifestResourceStream(DataBundle.ResourceName)!;
        }, copyGeometry: false);
        Assert.ThrowsAny<OperationCanceledException>(() => data.Validate(cancellation.Token));
        Assert.False(data.Geometry.IsValueCreated);
    }

    [Fact]
    public async Task Async_Buffering_Uses_Async_Reads_And_Enforces_The_Same_Limit()
    {
        using var source = new AsyncOnlyStream(new byte[129]);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DataBundle.BufferAsync(source, TestContext.Current.CancellationToken, 128));
        Assert.True(source.CanRead);
        using var good = new AsyncOnlyStream([1, 2, 3]);
        using var buffered = await DataBundle.BufferAsync(good, TestContext.Current.CancellationToken, 128);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffered.ToArray());
        Assert.True(good.CanRead);
    }

    [Fact]
    public void Buffer_Enforces_Limits_Without_Closing_The_Caller_Stream()
    {
        using var source = new MemoryStream(new byte[129]);
        Assert.Throws<InvalidDataException>(() => DataBundle.Buffer(source, 128));
        Assert.True(source.CanRead);
        Assert.Throws<ArgumentNullException>(() => DataBundle.Buffer(null!));
        using var unreadable = new MemoryStream();
        unreadable.Dispose();
        Assert.Throws<ArgumentException>(() => DataBundle.Buffer(unreadable));
    }

    private sealed class AsyncOnlyStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _source = new(bytes, false);
        public override bool CanRead => _source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous read.");
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_source.Read(buffer, offset, Math.Min(count, 7)));
        }
        protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class TrackingStream(Stream source, Action<int> read, Action closed) : Stream
    {
        private readonly Stream _source = source;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _source.Length;
        public override long Position { get => _source.Position; set => _source.Position = value; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var actual = _source.Read(buffer, offset, Math.Min(count, 71));
            read(actual);
            return actual;
        }
        public override long Seek(long offset, SeekOrigin origin) => _source.Seek(offset, origin);
        protected override void Dispose(bool disposing) { if (disposing) { _source.Dispose(); closed(); } base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
