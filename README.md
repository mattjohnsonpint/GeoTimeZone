GeoTimeZone  [![NuGet Version](https://img.shields.io/nuget/v/GeoTimeZone.svg?style=flat)](https://www.nuget.org/packages/GeoTimeZone/) 
===========

Provides an IANA time zone identifier from latitude and longitude coordinates.

## Nuget Installation

```powershell
PM> Install-Package GeoTimeZone
```

## Supported Environments

As of version 5.0.0, *GeoTimeZone* works with all of the following:

- .NET 5 or greater
- .NET Core 2.0 or greater
- .NET Framework 4.6.2 and greater

Note that .NET Framework versions less than 4.6.2 are no longer supported.

## Example Usage

```csharp
string tz = TimeZoneLookup.GetTimeZone(50.4372, -3.5559).Result;  // "Europe/London"
```

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
