using System;
using Microsoft.Maui.Devices.Sensors;

namespace DISMOGT_REPORTES.Services.LocationFusion
{
    /// <summary>
    /// Clase con utilidades para el procesamiento de ubicaciones
    /// </summary>
    public static class LocationUtils
    {
        /// <summary>
        /// Calcula la distancia aproximada entre dos puntos geográficos usando la fórmula de Haversine
        /// </summary>
        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371e3; // Radio de la Tierra en metros
            double latRad1 = lat1 * Math.PI / 180;
            double latRad2 = lat2 * Math.PI / 180;
            double deltaLat = (lat2 - lat1) * Math.PI / 180;
            double deltaLon = (lon2 - lon1) * Math.PI / 180;

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                       Math.Cos(latRad1) * Math.Cos(latRad2) *
                       Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c; // Distancia en metros
        }

        /// <summary>
        /// Calcula la velocidad entre dos ubicaciones
        /// </summary>
        public static double CalculateSpeed(Location location1, Location location2)
        {
            if (location1 == null || location2 == null)
                return 0;

            double distance = CalculateDistance(
                location1.Latitude, location1.Longitude,
                location2.Latitude, location2.Longitude);

            TimeSpan timeDiff = location2.Timestamp - location1.Timestamp;

            if (timeDiff.TotalSeconds <= 0)
                return 0;

            return distance / timeDiff.TotalSeconds; // metros por segundo
        }

        /// <summary>
        /// Calcula el rumbo entre dos ubicaciones (en grados, 0-360)
        /// </summary>
        public static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            // Convertir a radianes
            lat1 = lat1 * Math.PI / 180;
            lon1 = lon1 * Math.PI / 180;
            lat2 = lat2 * Math.PI / 180;
            lon2 = lon2 * Math.PI / 180;

            double y = Math.Sin(lon2 - lon1) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) -
                      Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(lon2 - lon1);
            double bearing = Math.Atan2(y, x);

            // Convertir a grados
            bearing = bearing * 180 / Math.PI;

            // Normalizar a 0-360
            return (bearing + 360) % 360;
        }

        /// <summary>
        /// Calcula la desviación estándar de un conjunto de valores
        /// </summary>
        public static double StandardDeviation(double[] values)
        {
            if (values == null || values.Length == 0)
                return 0;

            double avg = values.Average();
            double sumOfSquaresOfDifferences = values.Select(val => Math.Pow(val - avg, 2)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / values.Length);
        }
    }
}