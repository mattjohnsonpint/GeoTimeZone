GeoTimeZone
===========

Provides an IANA time zone identifier from latitude and longitude coordinates.

## Installation

```powershell
PM> Install-Package GeoTimeZone
```

## Supported Environments

As of version 2.0, The Nuget package targets `netstandard1.0` and `portable40-net40+sl5+win8+wp8`.
This provides support for the most environments, including:
 - .NET Core (Windows, Mac, Linux, etc.)
 - .NET Framework 4.0+
 - Windows 8, 8.1, 10, WinRT, UWP, etc.
 - Xamarian for iOS / Android
 - Windows Phone 8+
 - Silverlight 5

## Example Usage

```csharp
string tz = TimeZoneLookup.GetTimeZone(50.4372, -3.5559).Result;  // "Europe/London"
```

## Usage Notes

This library uses the time zone border definitions from the [Timezone Boundary Builder][1] project,
which in-turn derive from [Open Street Map][2].  As some international borders are the subject of dispute,
the results may or may not align with your worldview.  Use at your own risk.

## License

This library is provided free of charge, under the terms of the [MIT license][3].


[1]: https://github.com/evansiroky/timezone-boundary-builder
[2]: https://www.openstreetmap.org/
[3]: https://raw.githubusercontent.com/mj1856/GeoTimeZone/master/LICENSE
