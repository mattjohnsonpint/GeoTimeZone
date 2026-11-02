using GeoTimeZone.Internal;

namespace GeoTimeZone.Tests;

public class PackedEncodingValidationTests
{
    [Fact]
    public void Rejects_Nonfinite_Coordinates_Even_When_The_Image_Hash_Was_Recomputed()
    {
        var image = GeometryWriter.Serialize([new GeometryPolygon(0,
            [new GeometryRing([Math.PI, 4, 4, Math.PI, Math.PI], [0, 0, 1, 1, 0])])]);
        // One polygon, ring and block: the first coordinate is a float64
        // escape. Preserve every record length while corrupting its payload.
        var payload = 36 + SpatialFormat.PolygonRecordBytes + SpatialFormat.RingRecordBytes + SpatialFormat.BlockRecordBytes;
        Assert.Equal(0, image[payload]);
        BitConverter.GetBytes(double.NaN).CopyTo(image, payload + 1);
        var geometry = new PackedGeometry(image, SpatialFormat.Hash(image), 1, 1);
        Assert.Throws<InvalidDataException>(() => geometry.ValidateEncoding(CancellationToken.None));
    }

    [Fact]
    public void Rejects_An_Unclosed_Ring_And_Bounds_That_Exclude_Encoded_Vertices()
    {
        var source = GeometryWriter.Serialize([new GeometryPolygon(0,
            [new GeometryRing([0, 1, 1, 0, 0], [0, 0, 1, 1, 0])])]);
        var unclosed = (byte[])source.Clone();
        // The final y delta is -1 degree, not zero: replace its three-byte
        // encoding with a delta ending one microdegree above the start.
        using (var replacement = new MemoryStream())
        {
            using (var writer = new BinaryWriter(replacement, System.Text.Encoding.UTF8, true))
            {
                var previous = 1_000_000;
                CoordinateCodec.Write(writer, 0.000001, ref previous);
            }
            Assert.Equal(3, replacement.Length);
            replacement.ToArray().CopyTo(unclosed, unclosed.Length - 3);
        }
        var geometry = new PackedGeometry(unclosed, SpatialFormat.Hash(unclosed), 1, 1);
        Assert.Throws<InvalidDataException>(() => geometry.ValidateEncoding(CancellationToken.None));
        var narrow = (byte[])source.Clone();
        var firstBlock = 36 + SpatialFormat.PolygonRecordBytes + SpatialFormat.RingRecordBytes;
        BitConverter.GetBytes(0.5).CopyTo(narrow, firstBlock); // min latitude excludes bottom vertices
        geometry = new PackedGeometry(narrow, SpatialFormat.Hash(narrow), 1, 1);
        Assert.Throws<InvalidDataException>(() => geometry.ValidateEncoding(CancellationToken.None));
    }
}
