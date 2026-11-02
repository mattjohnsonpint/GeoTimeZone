GeoTimeZone  [![NuGet Version](https://img.shields.io/nuget/v/GeoTimeZone.svg?style=flat)](https://www.nuget.org/packages/GeoTimeZone/) 
===========

Provides an IANA time zone identifier from latitude and longitude coordinates.

## Nuget Installation

```powershell
PM> Install-Package GeoTimeZone
```

## Supported Environments

As of version 6.0.0, *GeoTimeZone* works with all of the following:

- .NET 6 or greater
- .NET Core 2.0 or greater
- .NET Framework 4.6.2 and greater

Note that .NET Framework versions less than 4.6.2 are no longer supported.

## Example Usage

```csharp
string tz = TimeZoneLookup.GetTimeZone(50.4372, -3.5559).Result;  // "Europe/London"
```

## Initialization and Custom Data

By default, the embedded data is selected automatically on the first lookup. No explicit initialization is required. To load the index and geometry directory during application startup instead, call:

```csharp
TimeZoneLookup.Initialize();
```

To override the embedded data, supply a compatible `GeoTimeZoneData.zip` produced by this project's data builder **before any lookup or initialization**:

```csharp
using (var data = File.OpenRead("GeoTimeZoneData.zip"))
{
    await TimeZoneLookup.InitializeAsync(data);
}

string tz = TimeZoneLookup.GetTimeZone(50.4372, -3.5559).Result;
```

The synchronous equivalent is:

```csharp
using (var data = File.OpenRead("GeoTimeZoneData.zip"))
{
    TimeZoneLookup.Initialize(data);
}
```

The stream overloads read from the current position, support nonseekable streams, and leave the stream open. The library retains its own copy of the internally compressed geometry archive, so the caller can close the stream afterward. Polygon components are still cached on demand.

Custom data is fully validated before it is selected. Failed or canceled loads allow a retry. `InitializeAsync` accepts an optional `CancellationToken`; await it before making lookups. Once initialization has started, supplying custom data throws `InvalidOperationException`. Repeated calls to the parameterless `Initialize()` are safe.

## Usage Notes

This library returns [IANA time zone IDs](https://en.wikipedia.org/wiki/List_of_tz_database_time_zones).  If you need a Windows time zone ID, pass the return value into the [TimeZoneConverter](https://github.com/mattjohnsonpint/TimeZoneConverter) library's `TZConvert.IanaToWindows` method, or to `TZConvert.GetTimeZoneInfo` to get a `TimeZoneInfo` object in a platform-neutral manner.

This library uses the time zone border definitions from the [Timezone Boundary Builder][1] project,
which in-turn derive from [Open Street Map][2].  As some international borders are the subject of dispute,
the results may or may not align with your worldview.  Use at your own risk.

## Acknowledgements

Huge thank you to the following people:

- Evan Siroky, who tirelessly maintains the Time Zone Boundary Builder project, which we use for our source data.
- Eric Muller, who authored the original tz_world data set (now deprecated in favor of TBB).
- Simon Bartlett, who contributed all the polygon indexing and lookup bits to this library.
- Sharon Lourduraj, who wrote GeoHash-net that we used for our original implementation.
- David Troy, who wrote Geohash-js that Sharon later ported to .NET
- Nick Johnson, who's [excellent blog post](http://blog.notdot.net/2009/11/Damn-Cool-Algorithms-Spatial-indexing-with-Quadtrees-and-Hilbert-Curves) has been an inspiration to this project and so many others!
- Jonas Nyrup, who has helped with performance optimizations.

## License

This library is provided free of charge, under the terms of the [MIT license][3].


[1]: https://github.com/evansiroky/timezone-boundary-builder
[2]: https://www.openstreetmap.org/
[3]: https://github.com/mattjohnsonpint/GeoTimeZone/blob/master/LICENSE
