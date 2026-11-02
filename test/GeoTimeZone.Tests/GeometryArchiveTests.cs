using GeoTimeZone.Internal;

namespace GeoTimeZone.Tests;

public class GeometryArchiveTests
{
    [Fact]
    public void Reads_Only_The_Directory_And_Requested_Components_And_Reuses_Them()
    {
        var file = Create();
        long read = 0;
        var opened = 0;
        var closed = 0;
        var archive = new GeometryArchive(() =>
        {
            opened++;
            return new TrackingStream(file.Image, count => read += count, () => closed++);
        }, file.DirectoryHash, 3, 3);
        Assert.Equal(SpatialFormat.ArchiveHeaderBytes + 3 * SpatialFormat.DirectoryRecordBytes, read);
        Assert.Equal(0, archive.CachedCount);
        Assert.False(archive.Covers(0, 2, 2));
        Assert.Equal(1, opened); // bounds rejection never opens a component
        var first = archive.GetPolygon(0);
        Assert.True(archive.Covers(0, 0.5, 0.5));
        Assert.Same(first, archive.GetPolygon(0));
        Assert.Equal(2, opened);
        Assert.Equal(opened, closed); // no handles retained in the cache
        Assert.Equal(1, archive.CachedCount);
        // The first component must not read/decompress concatenated members.
        var firstCompressed = BitConverter.ToInt32(file.Image,
            SpatialFormat.ArchiveHeaderBytes + sizeof(int) + 4 * sizeof(double) + sizeof(long));
        Assert.Equal(SpatialFormat.ArchiveHeaderBytes + 3 * SpatialFormat.DirectoryRecordBytes + firstCompressed, read);
    }

    [Fact]
    public void Evicts_Least_Recently_Used_Components_Within_The_Byte_Budget()
    {
        var file = Create();
        var probe = new GeometryArchive(() => new MemoryStream(file.Image, false), file.DirectoryHash, 3, 3);
        var cost = probe.GetPolygon(0).RetainedBytes + 128;
        var opened = 0;
        var archive = new GeometryArchive(() => { opened++; return new MemoryStream(file.Image, false); }, file.DirectoryHash, 3, 3, 2 * cost);
        var first = archive.GetPolygon(0);
        var second = archive.GetPolygon(1);
        Assert.Same(first, archive.GetPolygon(0)); // refresh recency
        archive.GetPolygon(2); // must evict 1, not 0
        Assert.Equal(2, archive.CachedCount);
        Assert.InRange(archive.CachedBytes, 1, 2 * cost);
        Assert.Same(first, archive.GetPolygon(0));
        Assert.NotSame(second, archive.GetPolygon(1));
        Assert.Equal(5, opened); // directory plus four actual loads
        Assert.True(second.Covers(0, 0.5, 0.5)); // eviction cannot invalidate active readers
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Oversized_Components_Are_Usable_But_Not_Retained(long budget)
    {
        var file = Create();
        var archive = new GeometryArchive(() => new MemoryStream(file.Image, false), file.DirectoryHash, 3, 3, budget);
        Assert.True(archive.Covers(0, 0.5, 0.5));
        Assert.NotSame(archive.GetPolygon(0), archive.GetPolygon(0));
        Assert.Equal(0, archive.CachedCount);
        Assert.Equal(0, archive.CachedBytes);
    }

    [Fact]
    public async Task Coalesces_Concurrent_Loads_Of_The_Same_Component()
    {
        var file = Create();
        var opened = 0;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var archive = new GeometryArchive(() =>
        {
            if (Interlocked.Increment(ref opened) > 1)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)) throw new TimeoutException();
            }
            return new MemoryStream(file.Image, false);
        }, file.DirectoryHash, 3, 3);
        var queries = Enumerable.Range(0, 16).Select(_ => Task.Run(() => archive.GetPolygon(0))).ToArray();
        try { Assert.True(entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)); }
        finally { release.Set(); }
        var results = await Task.WhenAll(queries);
        foreach (var result in results) Assert.Same(results[0], result);
        Assert.Equal(2, opened);
        Assert.Equal(1, archive.CachedCount);
    }

    [Fact]
    public async Task Unrelated_Components_Can_Decompress_Concurrently()
    {
        var file = Create();
        var opened = 0;
        using var bothLoading = new CountdownEvent(2);
        var archive = new GeometryArchive(() =>
        {
            if (Interlocked.Increment(ref opened) > 1)
            {
                bothLoading.Signal();
                if (!bothLoading.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Misses serialized behind the cache lock.");
            }
            return new MemoryStream(file.Image, false);
        }, file.DirectoryHash, 3, 3);
        var first = Task.Factory.StartNew(() => archive.GetPolygon(0), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var second = Task.Factory.StartNew(() => archive.GetPolygon(1), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(first, second);
        Assert.Equal(2, archive.CachedCount);
        Assert.Equal(3, opened);
    }

    [Fact]
    public void Concurrent_Evictions_Never_Invalidate_Readers_Or_Exceed_The_Cache_Budget()
    {
        var file = Create();
        var probe = new GeometryArchive(() => new MemoryStream(file.Image, false), file.DirectoryHash, 3, 3);
        var budget = probe.GetPolygon(0).RetainedBytes + 128;
        var archive = new GeometryArchive(() => new MemoryStream(file.Image, false), file.DirectoryHash, 3, 3, budget);
        Parallel.For(0, 2000, i =>
        {
            var polygon = i % 3;
            var loaded = archive.GetPolygon(polygon);
            Assert.Equal(polygon, loaded.Zone(0));
            Assert.True(loaded.Covers(0, 0.5, 0.5));
            Assert.InRange(archive.CachedBytes, 0, budget);
        });
        Assert.InRange(archive.CachedCount, 0, 1);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Rejects_A_Component_Whose_Decoded_Length_Disagrees_With_The_Directory(int difference)
    {
        var file = Create();
        var rawLengthOffset = SpatialFormat.ArchiveHeaderBytes + 48;
        var length = BitConverter.ToInt32(file.Image, rawLengthOffset);
        BitConverter.GetBytes(length + difference).CopyTo(file.Image, rawLengthOffset);
        var hash = SpatialFormat.Hash(file.Image.Skip(SpatialFormat.ArchiveHeaderBytes)
            .Take(3 * SpatialFormat.DirectoryRecordBytes).ToArray());
        var archive = new GeometryArchive(() => new MemoryStream(file.Image, false), hash, 3, 3);
        Assert.Throws<InvalidDataException>(() => archive.GetPolygon(0));
        Assert.Equal(0, archive.CachedCount);
    }

    [Fact]
    public void Rejects_A_Valid_Component_Assigned_To_The_Wrong_Zone()
    {
        var file = Create();
        BitConverter.GetBytes(1).CopyTo(file.Image, SpatialFormat.ArchiveHeaderBytes);
        var hash = SpatialFormat.Hash(file.Image.Skip(SpatialFormat.ArchiveHeaderBytes)
            .Take(3 * SpatialFormat.DirectoryRecordBytes).ToArray());
        var archive = new GeometryArchive(() => new MemoryStream(file.Image, false), hash, 3, 3);
        Assert.Throws<InvalidDataException>(() => archive.GetPolygon(0));
        Assert.Equal(0, archive.CachedCount);
    }

    [Fact]
    public void Failed_Load_Does_Not_Poison_The_Cache_And_Can_Be_Retried()
    {
        var file = Create();
        var opened = 0;
        var archive = new GeometryArchive(() =>
        {
            if (++opened == 2) throw new IOException("Simulated stream failure.");
            return new MemoryStream(file.Image, false);
        }, file.DirectoryHash, 3, 3);
        Assert.Throws<IOException>(() => archive.GetPolygon(0));
        Assert.Equal(0, archive.CachedCount);
        Assert.True(archive.Covers(0, 0.5, 0.5));
        Assert.Equal(3, opened);
    }

    [Fact]
    public void Corruption_In_An_Unused_Component_Is_Not_Read_But_Is_Rejected_When_Requested()
    {
        var file = Create();
        file.Image[file.Image.Length - 8] ^= 1; // CRC of the last component
        var archive = new GeometryArchive(() => new MemoryStream(file.Image, false), file.DirectoryHash, 3, 3);
        Assert.True(archive.Covers(0, 0.5, 0.5));
        Assert.Throws<InvalidDataException>(() => archive.GetPolygon(2));
        Assert.Equal(1, archive.CachedCount);
    }

    [Fact]
    public void Every_Truncated_Archive_Is_Rejected_Without_Reading_Components()
    {
        var file = Create();
        for (var length = 0; length < file.Image.Length; length++)
        {
            var truncated = file.Image.Take(length).ToArray();
            var error = Record.Exception(() => new GeometryArchive(() => new MemoryStream(truncated, false), file.DirectoryHash, 3, 3));
            Assert.True(error is InvalidDataException or EndOfStreamException, $"Length {length}: {error}");
        }
    }

    [Theory]
    [InlineData(0, 99)] // invalid zone
    [InlineData(36, 1)] // first payload offset must be zero
    [InlineData(44, int.MaxValue)] // compressed length beyond resource
    [InlineData(48, 0)] // raw length must be positive
    [InlineData(48, int.MaxValue)] // raw length must be capped
    public void Rejects_Invalid_Directory_Metadata_Even_With_Recomputed_Fingerprint(int recordOffset, int value)
    {
        var file = Create();
        BitConverter.GetBytes(value).CopyTo(file.Image, SpatialFormat.ArchiveHeaderBytes + recordOffset);
        var hash = SpatialFormat.Hash(file.Image.Skip(SpatialFormat.ArchiveHeaderBytes)
            .Take(3 * SpatialFormat.DirectoryRecordBytes).ToArray());
        Assert.Throws<InvalidDataException>(() => new GeometryArchive(() => new MemoryStream(file.Image, false), hash, 3, 3));
    }

    [Fact]
    public void Rejects_Mixed_Directories_And_Checks_Each_Components_Own_Fingerprint()
    {
        var file = Create();
        Assert.Throws<InvalidDataException>(() => new GeometryArchive(() => new MemoryStream(file.Image, false), new byte[32], 3, 3));
        // Structurally valid directory, wrong component hash: loading the
        // directory succeeds, but the first component cannot enter the cache.
        file.Image[SpatialFormat.ArchiveHeaderBytes + 52] ^= 1;
        var hash = SpatialFormat.Hash(file.Image.Skip(SpatialFormat.ArchiveHeaderBytes)
            .Take(3 * SpatialFormat.DirectoryRecordBytes).ToArray());
        var archive = new GeometryArchive(() => new MemoryStream(file.Image, false), hash, 3, 3);
        Assert.Throws<InvalidDataException>(() => archive.GetPolygon(0));
        Assert.Equal(0, archive.CachedCount);
    }

    private static GeometryArchiveImage Create() => GeometryArchiveWriter.Serialize(Enumerable.Range(0, 3)
        .Select(zone => new GeometryPolygon(zone, [new GeometryRing([0, 1, 1, 0, 0], [0, 0, 1, 1, 0])])).ToArray());

    private sealed class TrackingStream(byte[] bytes, Action<int> read, Action closed) : Stream
    {
        private readonly MemoryStream _inner = new(bytes, false);
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var actual = _inner.Read(buffer, offset, Math.Min(count, 7));
            read(actual);
            return actual;
        }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _inner.Dispose(); closed(); }
            base.Dispose(disposing);
        }
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
