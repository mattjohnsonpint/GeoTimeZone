#if NET8_0_OR_GREATER
using System.Reflection;
using System.Runtime.Loader;
using GeoTimeZone.Internal;

namespace GeoTimeZone.Tests;

public class InitializationTests
{
    [Fact]
    public void Initialize_Is_Idempotent_And_Safe_With_Concurrent_Default_First_Use()
    {
        using var library = new IsolatedLibrary();
        Parallel.For(0, 32, i =>
        {
            if (i % 2 == 0) library.Invoke("Initialize");
            else Assert.Equal("America/Phoenix", library.Lookup(33.45, -112.0667).Primary);
        });
        library.Invoke("Initialize");
        Assert.True(library.DataLoaded);
    }

    [Fact]
    public void Custom_Data_Is_A_Complete_Override_Independent_Of_The_Callers_Stream()
    {
        using var library = new IsolatedLibrary();
        using var stream = new InputStream(BundleFixtures.Create());
        library.Invoke("Initialize", stream);
        Assert.False(stream.WasDisposed);
        stream.Dispose();
        var result = library.Lookup(0.5, 0.5);
        Assert.Equal("Test/A", result.Primary);
        Assert.Equal(new[] { "Test/B" }, result.Alternatives);
        Assert.Equal("Etc/GMT+7", library.Lookup(40, -100).Primary); // no embedded polygons merged in
        library.Invoke("Initialize");
        Assert.Equal("Test/A", library.Lookup(0.5, 0.5).Primary);
    }

    [Fact]
    public void Custom_Bundle_May_Use_Outer_Zip_Compression()
    {
        using var library = new IsolatedLibrary();
        var entries = BundleFixtures.Entries(BundleFixtures.Create());
        using var stream = new MemoryStream(BundleFixtures.Zip(entries, compressGeometry: true));
        library.Invoke("Initialize", stream);
        Assert.Equal("Test/A", library.Lookup(0.5, 0.5).Primary);
    }

    [Fact]
    public void Caller_Mutations_After_Initialization_Cannot_Change_The_Configured_Bundle()
    {
        using var library = new IsolatedLibrary();
        var bytes = BundleFixtures.Create();
        using var stream = new MemoryStream(bytes);
        library.Invoke("Initialize", stream);
        Array.Clear(bytes);
        Assert.Equal("Test/A", library.Lookup(0.5, 0.5).Primary);
    }

    [Fact]
    public void Null_Data_And_Invalid_Coordinates_Do_Not_Start_Initialization()
    {
        using var library = new IsolatedLibrary();
        Assert.IsType<ArgumentNullException>(Assert.Throws<TargetInvocationException>(() => library.InitializeNull()).InnerException);
        Assert.IsType<ArgumentOutOfRangeException>(Assert.Throws<TargetInvocationException>(() => library.Lookup(double.NaN, 0)).InnerException);
        Assert.False(library.DataLoaded);
        using var stream = new MemoryStream(BundleFixtures.Create());
        library.Invoke("Initialize", stream);
        Assert.Equal("Test/A", library.Lookup(0.5, 0.5).Primary);
    }

    [Fact]
    public void Reads_From_The_Current_Position()
    {
        using var library = new IsolatedLibrary();
        using var stream = new MemoryStream();
        stream.Write(new byte[17]);
        stream.Write(BundleFixtures.Create());
        stream.Position = 17;
        library.Invoke("Initialize", stream);
        Assert.True(stream.CanRead);
        Assert.Equal("Test/A", library.Lookup(0.5, 0.5).Primary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overrides_After_Default_Initialization_Are_Rejected_Without_Reading(bool asynchronous)
    {
        using var library = new IsolatedLibrary();
        Assert.Equal("America/Phoenix", library.Lookup(33.45, -112.0667).Primary);
        using var stream = new InputStream(BundleFixtures.Create(), asynchronousOnly: asynchronous);
        if (asynchronous) await Assert.ThrowsAsync<InvalidOperationException>(() => library.InitializeAsync(stream, TestContext.Current.CancellationToken));
        else Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => library.Invoke("Initialize", stream)).InnerException);
        Assert.Equal(0, stream.Reads);
        Assert.False(stream.WasDisposed);
    }

    [Theory]
    [InlineData("corrupt-zip")]
    [InlineData("wrong-version")]
    [InlineData("corrupt-component")]
    public void Invalid_Overrides_Do_Not_Poison_Initialization(string failure)
    {
        using var library = new IsolatedLibrary();
        var bytes = failure == "corrupt-zip" ? new byte[32] : BundleFixtures.Create(change: entries =>
        {
            if (failure == "wrong-version") entries["manifest.txt"] = System.Text.Encoding.UTF8.GetBytes("GeoTimeZone/999");
            else entries[DataBundle.GeometryName][entries[DataBundle.GeometryName].Length - 8] ^= 1;
        });
        using var bad = new MemoryStream(bytes);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => library.Invoke("Initialize", bad)).InnerException);
        Assert.False(library.DataLoaded);
        Assert.True(bad.CanRead);
        using var good = new MemoryStream(BundleFixtures.Create());
        library.Invoke("Initialize", good);
        Assert.Equal("Test/A", library.Lookup(0.5, 0.5).Primary);
    }

    [Fact]
    public async Task Async_Initialization_Uses_Only_Async_Reads_And_Leaves_Stream_Open()
    {
        using var library = new IsolatedLibrary();
        using var stream = new InputStream(BundleFixtures.Create(), asynchronousOnly: true);
        await library.InitializeAsync(stream, TestContext.Current.CancellationToken);
        Assert.True(stream.Reads > 0);
        Assert.False(stream.WasDisposed);
        Assert.Equal("Test/A", library.Lookup(0.5, 0.5).Primary);
    }

    [Fact]
    public async Task PreCanceled_Retrieval_Does_Not_Read_Or_Configure_Data()
    {
        using var library = new IsolatedLibrary();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var stream = new InputStream(BundleFixtures.Create(), asynchronousOnly: true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => library.InitializeAsync(stream, cancellation.Token));
        Assert.Equal(0, stream.Reads);
        Assert.False(library.DataLoaded);
        using var retry = new MemoryStream(BundleFixtures.Create());
        library.Invoke("Initialize", retry);
    }

    [Fact]
    public async Task Cancellation_During_Retrieval_Allows_Retry()
    {
        using var library = new IsolatedLibrary();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stream = new InputStream(BundleFixtures.Create(), true, gate.Task);
        using var cancellation = new CancellationTokenSource();
        var initialization = library.InitializeAsync(stream, cancellation.Token);
        await stream.Started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);
        Assert.False(library.DataLoaded);
        Assert.False(stream.WasDisposed);
        using var retry = new MemoryStream(BundleFixtures.Create());
        library.Invoke("Initialize", retry);
        Assert.Equal("Test/A", library.Lookup(0.5, 0.5).Primary);
    }

    [Fact]
    public async Task Source_Read_Failure_Does_Not_Configure_Data()
    {
        using var library = new IsolatedLibrary();
        using var stream = new InputStream(BundleFixtures.Create(), true, Task.FromException(new IOException("Disconnected.")));
        await Assert.ThrowsAsync<IOException>(() => library.InitializeAsync(stream, TestContext.Current.CancellationToken));
        Assert.False(library.DataLoaded);
        Assert.False(stream.WasDisposed);
        using var retry = new MemoryStream(BundleFixtures.Create());
        library.Invoke("Initialize", retry);
    }

    [Fact]
    public async Task First_Use_During_Custom_Retrieval_Selects_Embedded_Data_Without_Mixing_Sources()
    {
        using var library = new IsolatedLibrary();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stream = new InputStream(BundleFixtures.Create(), true, gate.Task);
        var initialization = library.InitializeAsync(stream, TestContext.Current.CancellationToken);
        await stream.Started.Task;
        Assert.Equal("America/Phoenix", library.Lookup(33.45, -112.0667).Primary);
        gate.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => initialization);
        Assert.Equal("Europe/Zurich", library.Lookup(47.58735, 7.588967).Primary);
    }

    [Fact]
    public async Task Concurrent_Custom_Initializers_Publish_Exactly_One_Complete_Bundle()
    {
        using var library = new IsolatedLibrary();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var first = new InputStream(BundleFixtures.Create("First"), true, gate.Task);
        using var second = new InputStream(BundleFixtures.Create("Second"), true, gate.Task);
        var firstTask = library.InitializeAsync(first, TestContext.Current.CancellationToken);
        var secondTask = library.InitializeAsync(second, TestContext.Current.CancellationToken);
        await Task.WhenAll(first.Started.Task, second.Started.Task);
        gate.SetResult();
        var results = await Task.WhenAll(Observe(firstTask), Observe(secondTask));
        Assert.Single(results, result => result == null);
        Assert.Single(results, result => result is InvalidOperationException);
        var prefix = results[0] == null ? "First" : "Second";
        var lookup = library.Lookup(0.5, 0.5);
        Assert.Equal(prefix + "/A", lookup.Primary);
        Assert.Equal(new[] { prefix + "/B" }, lookup.Alternatives);

        static async Task<Exception?> Observe(Task task)
        {
            try { await task; return null; }
            catch (Exception exception) { return exception; }
        }
    }

    private sealed class InputStream(byte[] bytes, bool asynchronousOnly = false, Task? gate = null) : Stream
    {
        private readonly MemoryStream _source = new(bytes, false);
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool WasDisposed { get; private set; }
        internal int Reads { get; private set; }
        public override bool CanRead => !WasDisposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (asynchronousOnly) throw new InvalidOperationException("Unexpected synchronous source read.");
            Reads++;
            return _source.Read(buffer, offset, Math.Min(count, 73));
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reads++;
            Started.TrySetResult();
            if (gate != null) await gate.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return _source.Read(buffer.Span[..Math.Min(buffer.Length, 73)]);
        }
        protected override void Dispose(bool disposing) { WasDisposed = true; if (disposing) _source.Dispose(); base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // Isolate startup state in a fresh actual assembly, rather than resetting
    // production statics or letting test ordering select the winning bundle.
    private sealed class IsolatedLibrary : IDisposable
    {
        private readonly AssemblyLoadContext _context = new(null, isCollectible: true);
        private readonly Type _type;
        internal IsolatedLibrary() => _type = _context.LoadFromAssemblyPath(typeof(TimeZoneLookup).Assembly.Location)
            .GetType(typeof(TimeZoneLookup).FullName!)!;
        internal object? Invoke(string name, params object[] arguments) => _type.GetMethods().Single(method => method.Name == name
            && method.GetParameters().Length == arguments.Length
            && method.GetParameters().Select((parameter, i) => parameter.ParameterType.IsInstanceOfType(arguments[i])).All(match => match))
            .Invoke(null, arguments);
        internal void InitializeNull() => _type.GetMethod("Initialize", [typeof(Stream)])!.Invoke(null, [null]);
        internal Task InitializeAsync(Stream stream, CancellationToken cancellationToken) =>
            (Task)Invoke("InitializeAsync", stream, cancellationToken)!;
        internal (string Primary, string[] Alternatives) Lookup(double latitude, double longitude)
        {
            var result = Invoke("GetTimeZone", latitude, longitude)!;
            return ((string)result.GetType().GetProperty("Result")!.GetValue(result)!,
                ((IEnumerable<string>)result.GetType().GetProperty("AlternativeResults")!.GetValue(result)!).ToArray());
        }
        internal bool DataLoaded
        {
            get
            {
                var lazy = _type.GetField("Data", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                return (bool)lazy.GetType().GetProperty("IsValueCreated")!.GetValue(lazy)!;
            }
        }
        public void Dispose() => _context.Unload();
    }
}
#endif
