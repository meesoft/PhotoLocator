using MapControl;
using SunCalcNet;
using SunCalcNet.Model;
using System;

namespace PhotoLocator.Helpers
{
    static class CelestialCalculator
    {
        /// <summary>
        /// Calculates sun rise/set times and azimuths for a given date and location.
        /// </summary>
        public static (DateTime? Sunrise, double? RiseAzimuth, DateTime? Sunset, double? SetAzimuth) GetSunRiseSet(
            Location location, DateTime date)
        {
            date = (date - date.TimeOfDay + TimeSpan.FromHours(12)).ToUniversalTime();

            DateTime? sunrise = null, sunset = null;
            double? sunriseAzimuth = null, sunsetAzimuth = null;
            foreach (var phase in SunCalc.GetSunPhases(date, location.Latitude, location.Longitude))
                if (phase.Name == SunPhaseName.Sunrise)
                {
                    sunrise = phase.PhaseTime;
                    var pos = SunCalc.GetSunPosition(sunrise.Value, location.Latitude, location.Longitude);
                    sunriseAzimuth = SunCalcNetAzimuthRadiansToDegrees(pos.Azimuth);
                }
                else if (phase.Name == SunPhaseName.Sunset)
                {
                    sunset = phase.PhaseTime;
                    var pos = SunCalc.GetSunPosition(sunset.Value, location.Latitude, location.Longitude);
                    sunsetAzimuth = SunCalcNetAzimuthRadiansToDegrees(pos.Azimuth);
                }
            return (sunrise, sunriseAzimuth, sunset, sunsetAzimuth);
        }

        public static double? GetSunPosition(Location location, DateTime time)
        {
            var pos = SunCalc.GetSunPosition(time, location.Latitude, location.Longitude);
            return pos.Altitude < 0 ? null : SunCalcNetAzimuthRadiansToDegrees(pos.Azimuth);
        }

        /// <summary>
        /// Calculates moon rise/set times and azimuths for a given date and location.
        /// </summary>
        public static (DateTime? Moonrise, double? RiseAzimuth, double? RiseIllumination, DateTime? Moonset, double? SetAzimuth, double? SetIllumination) GetMoonRiseSet(
            Location location, DateTime date)
        {
            date = (date - date.TimeOfDay + TimeSpan.FromHours(12)).ToUniversalTime();

            var times = MoonCalc.GetMoonPhase(date, location.Latitude, location.Longitude);
            var moonrise = times.Rise;
            var moonset = times.Set;

            double? moonriseAzimuth = null, moonsetAzimuth = null;
            MoonIllumination? moonriseIllumination = null, moonsetIllumination = null;

            if (moonrise.HasValue)
            {
                var pos = MoonCalc.GetMoonPosition(moonrise.Value, location.Latitude, location.Longitude);
                moonriseIllumination = MoonCalc.GetMoonIllumination(moonrise.Value);
                moonriseAzimuth = SunCalcNetAzimuthRadiansToDegrees(pos.Azimuth);
            }
            if (moonset.HasValue)
            {
                var pos = MoonCalc.GetMoonPosition(moonset.Value, location.Latitude, location.Longitude);
                moonsetIllumination = MoonCalc.GetMoonIllumination(moonset.Value);
                moonsetAzimuth = SunCalcNetAzimuthRadiansToDegrees(pos.Azimuth);
            }

            return (moonrise, moonriseAzimuth, moonriseIllumination?.Fraction, moonset, moonsetAzimuth, moonsetIllumination?.Fraction);
        }

        public static (double? Azimuth, double? Illumination) GetMoonPosition(Location location, DateTime time)
        {
            var pos = MoonCalc.GetMoonPosition(time, location.Latitude, location.Longitude);
            if (pos.Altitude < 0)
                return (null, null);
            var illumination = MoonCalc.GetMoonIllumination(time);
            return (SunCalcNetAzimuthRadiansToDegrees(pos.Azimuth), illumination.Fraction);
        }

        private static double SunCalcNetAzimuthRadiansToDegrees(double radians)
        {
            // SunCalcNet azimuth: 0 = south, positive westward, negative eastward
            // To convert to compass: (azimuth * 180 / Math.PI + 180) % 360
            var deg = (radians * 180.0 / Math.PI + 180.0) % 360.0;
            return deg <= 180 ? deg : deg - 360;
        }

        /// <summary>
        /// Calculates a new GPS position distanceKm away from the given location in the specified azimuth direction (degrees).
        /// </summary>
        public static Location GetLocationAtDistance(Location start, double azimuthDegrees, double distanceKm)
        {
            const double EarthRadiusKm = 6371.0;
            double lat1 = DegreesToRadians(start.Latitude);
            double lon1 = DegreesToRadians(start.Longitude);
            double bearing = DegreesToRadians(azimuthDegrees);

            double angularDistance = distanceKm / EarthRadiusKm;

            double lat2 = Math.Asin(
                Math.Sin(lat1) * Math.Cos(angularDistance) +
                Math.Cos(lat1) * Math.Sin(angularDistance) * Math.Cos(bearing)
            );

            double lon2 = lon1 + Math.Atan2(
                Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(lat1),
                Math.Cos(angularDistance) - Math.Sin(lat1) * Math.Sin(lat2)
            );

            return new Location(RadiansToDegrees(lat2), RadiansToDegrees(lon2));
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
        private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
    }
}