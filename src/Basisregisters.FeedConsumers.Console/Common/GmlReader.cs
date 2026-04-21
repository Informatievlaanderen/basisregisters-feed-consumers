namespace Basisregisters.FeedConsumers.Console.Common;

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.IO.GML2;

public static class GmlReaderFactory
{
    public static GMLReader CreateLambert2008GmlReader() =>
        new GMLReader(
            new GeometryFactory(
                new PrecisionModel(PrecisionModels.Floating),
                3812,
                new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XY)));
}
