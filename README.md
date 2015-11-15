GeoTimeZone
===========

Provides an IANA time zone identifier from latitude and longitude coordinates.

```powershell
PM> Install-Package GeoTimeZone
```

Example:

```csharp
string tz = TimeZoneLookup.GetTimeZone(50.4372, -3.5559).Result;  // "Europe/London"
```


Note: this uses the public domain time zone border map data from http://efele.net/maps/tz/world/ which may or may not align with your worldview.  Use at your own risk.
