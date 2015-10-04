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


Note: this uses data from http://efele.net/maps/tz/world/ - which currently does not take into account [tzdb 2014f](http://mm.icann.org/pipermail/tz-announce/2014-August/000023.html) (which split `Asia/Chita` from `Asia/Yakutsk`, and split `Asia/Srednekolymsk` from `Asia/Magadan`) or [tzdb 2015g](http://mm.icann.org/pipermail/tz-announce/2015-October/000034.html) (which split `America/Fort_Nelson` from `America/Vancouver`).  Use at your own risk.
