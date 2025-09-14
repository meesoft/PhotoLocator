namespace PhotoLocator.Helpers;

[TestClass]
public class CelestialCalculatorTest
{
    [TestMethod]
    public void CelestialCalculator_ShouldGet()
    {
        var location = new MapControl.Location(55.6761, 12.5683); // Copenhagen
        var date = DateTime.Today;

        var sun = CelestialCalculator.GetSunRiseSet(location, date);
        if (sun.Sunrise.HasValue)
            Console.WriteLine($"Sunrise: {sun.Sunrise.Value.ToLocalTime()} Azimuth: {sun.RiseAzimuth:F1}°");
        if (sun.Sunset.HasValue)
            Console.WriteLine($"Sunset: {sun.Sunset.Value.ToLocalTime()} Azimuth: {sun.SetAzimuth:F1}°");

        var moon = CelestialCalculator.GetMoonRiseSet(location, date);
        if (moon.Moonrise.HasValue)
            Console.WriteLine($"Moonrise: {moon.Moonrise.Value.ToLocalTime()} Azimuth: {moon.RiseAzimuth:F1}° Illumination: {moon.RiseIllumination * 100:F0}");
        if (moon.Moonset.HasValue)
            Console.WriteLine($"Moonset: {moon.Moonset.Value.ToLocalTime()} Azimuth: {moon.SetAzimuth:F1}° Illumination: {moon.SetIllumination * 100:F0}");
    }
}