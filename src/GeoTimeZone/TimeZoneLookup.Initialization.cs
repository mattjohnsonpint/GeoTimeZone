using System.IO;
using GeoTimeZone.Internal;

namespace GeoTimeZone;

public static partial class TimeZoneLookup
{
    private static readonly object InitializationLock = new();
    private static bool _initializationStarted;
    private static SpatialData? _configuredData;

    /// <summary>Loads the time zone index and geometry directory before their first use.</summary>
    /// <remarks>
    /// Initialization is synchronous, thread-safe, and performed only once. Polygon components
    /// remain loaded on demand. Repeated calls are safe; concurrent callers wait for initialization.
    /// </remarks>
    public static void Initialize()
    {
        _ = Data.Value.Geometry.Value;
    }

    /// <summary>Initializes using a complete version 1 GeoTimeZone data ZIP bundle.</summary>
    /// <param name="data">A readable stream containing the data bundle.</param>
    /// <remarks>
    /// Call before the first lookup or initialization. Reads from the current position, does not
    /// require seeking, and leaves the stream open. The bundle and its extracted geometry archive
    /// may not exceed 128 MiB each. All components are validated before publication; failures allow
    /// retry. An owned copy of the internally compressed geometry archive is retained so
    /// the caller can close or modify the stream afterward. Polygon components remain cached on demand.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Time zone initialization has already started.</exception>
    /// <exception cref="InvalidDataException">The bundle is invalid or incompatible.</exception>
    public static void Initialize(Stream data)
    {
        EnsureNotStarted();
        using var buffer = DataBundle.Buffer(data);
        InitializeBundle(buffer.ToArray(), CancellationToken.None);
    }

    /// <summary>Asynchronously reads a complete version 1 GeoTimeZone ZIP bundle, then initializes.</summary>
    /// <param name="data">A readable stream containing the data bundle.</param>
    /// <param name="cancellationToken">A token to cancel retrieval or validation before publication.</param>
    /// <remarks>
    /// Reads from the current position and leaves the stream open. Await this method before any
    /// lookup. Retrieval uses asynchronous reads; parsing and polygon validation are synchronous.
    /// Failed or canceled loads do not configure data. Concurrent first use may select embedded
    /// data instead, in which case this method throws. The size, format and retention rules match
    /// <see cref="Initialize(Stream)"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Time zone initialization has already started.</exception>
    /// <exception cref="InvalidDataException">The bundle is invalid or incompatible.</exception>
    public static async Task InitializeAsync(Stream data, CancellationToken cancellationToken = default)
    {
        EnsureNotStarted();
        using var buffer = await DataBundle.BufferAsync(data, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        InitializeBundle(buffer.ToArray(), cancellationToken);
    }

    private static void EnsureNotStarted()
    {
        lock (InitializationLock)
            if (_initializationStarted) throw new InvalidOperationException("Time zone initialization has already started.");
    }

    private static void InitializeBundle(byte[] snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SpatialData data;
        try
        {
            data = DataBundle.Read(() => new MemoryStream(snapshot, false), copyGeometry: true);
            data.Validate(cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or EndOfStreamException or OverflowException)
        {
            throw new InvalidDataException("The time zone data bundle is invalid or incompatible.", exception);
        }
        lock (InitializationLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_initializationStarted) throw new InvalidOperationException("Time zone initialization has already started.");
            _configuredData = data;
            _initializationStarted = true;
        }
        Initialize();
    }

    private static SpatialData LoadData()
    {
        // Publish one coherent index/archive pair. Neither a failed override
        // nor a racing initializer can splice new geometry onto an old grid.
        lock (InitializationLock)
        {
            _initializationStarted = true;
            if (_configuredData != null) return _configuredData;
        }
        return DataBundle.ReadEmbedded();
    }
}
