using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using DISMOGT_REPORTES.Services.LocationFusion;

namespace DISMOGT_REPORTES.Services.LocationFusion
{
    /// <summary>
    /// Clase encargada de aplicar filtrado ultramínimo para preservar patrones naturales de movimiento
    /// </summary>
    public class LocationFilters
    {
        private double _kalmanProcessNoise = 0.15;  // Valor alto para permitir cambios naturales
        private double _kalmanMeasurementNoise = 0.4; // Valor bajo para confiar en las mediciones nuevas
        private double _kalmanGain = 0;
        private double _estimatedError = 1;
        private double _latitude = 0;
        private double _longitude = 0;
        private double _filteringLevel; // 0.0 a 1.0, controla la intensidad del filtrado

        /// <summary>
        /// Constructor con nivel de filtrado configurable
        /// </summary>
        /// <param name="filteringLevel">0.0 a 1.0, donde 0.0 significa prácticamente sin filtrado</param>
        public LocationFilters(double filteringLevel = 0.05)
        {
            _filteringLevel = Math.Max(0.0, Math.Min(1.0, filteringLevel));
            Console.WriteLine($"🔧 Filtros de ubicación inicializados con nivel: {_filteringLevel:P0}");
        }

        /// <summary>
        /// Establece el nivel de filtrado
        /// </summary>
        /// <param name="level">0.0 a 1.0, donde 0.0 significa prácticamente sin filtrado</param>
        public void SetFilteringLevel(double level)
        {
            _filteringLevel = Math.Max(0.0, Math.Min(1.0, level));
            Console.WriteLine($"🔧 Nivel de filtrado actualizado a: {_filteringLevel:P0}");
        }

        /// <summary>
        /// Actualiza los parámetros de Kalman según el contexto, con ajustes para filtrado ultramínimo
        /// </summary>
        public void UpdateKalmanParameters(MovementContext context, int consecutiveImprovements, int consecutiveWorsenings)
        {
            // Valores base según el contexto
            switch (context)
            {
                case MovementContext.Stationary:
                    _kalmanProcessNoise = 0.01;
                    _kalmanMeasurementNoise = 0.8;
                    break;

                case MovementContext.Walking:
                    _kalmanProcessNoise = 0.08;
                    _kalmanMeasurementNoise = 0.6;
                    break;

                case MovementContext.Vehicle:
                    _kalmanProcessNoise = 0.15;     // Valor muy alto para vehículos
                    _kalmanMeasurementNoise = 0.4;  // Mayor confianza en mediciones nuevas
                    break;

                case MovementContext.Indoor:
                    _kalmanProcessNoise = 0.05;
                    _kalmanMeasurementNoise = 1.0;
                    break;

                default:
                    _kalmanProcessNoise = 0.08;
                    _kalmanMeasurementNoise = 0.6;
                    break;
            }

            // Ajustar según el nivel de filtrado configurado - valores más extremos
            // Para filtrado ultramínimo (filteringLevel cercano a 0)
            _kalmanProcessNoise /= _filteringLevel * 0.5 + 0.1; // Aumentar ruido de proceso cuando el filtrado es bajo
            _kalmanMeasurementNoise *= _filteringLevel * 0.5 + 0.1; // Reducir ruido de medición cuando el filtrado es bajo

            Console.WriteLine($"⚙️ Parámetros Kalman: Contexto={context}, " +
                $"Ruido proceso={_kalmanProcessNoise:F4}, " +
                $"Ruido medición={_kalmanMeasurementNoise:F2}, " +
                $"Nivel filtrado={_filteringLevel:P0}");
        }

        /// <summary>
        /// Aplica filtrado ultramínimo para preservar patrones naturales de movimiento
        /// </summary>
        public Location ApplyUltraMinimalFiltering(List<Location> locations, MovementContext context)
        {
            try
            {
                // Si estamos en un vehículo, devolver la ubicación sin procesar para preservar el patrón de las carreteras
                if (context == MovementContext.Vehicle && _filteringLevel < 0.2)
                {
                    return locations.First(); // Devolver ubicación sin procesar para vehículos con filtrado bajo
                }

                // Si solo hay una ubicación, aplicar filtrado mínimo de Kalman
                if (locations.Count == 1)
                {
                    return ApplyMinimalKalmanFilter(locations[0], context);
                }

                // Si hay múltiples ubicaciones, usar un promedio ponderado con alto peso en la más reciente
                Location fusedLocation = GetWeightedNewest(locations, context);

                // Aplicar filtro Kalman mínimo
                return ApplyMinimalKalmanFilter(fusedLocation, context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al aplicar filtrado ultramínimo: {ex.Message}");
                return locations.First(); // En caso de error, devolver la primera ubicación
            }
        }

        /// <summary>
        /// Aplica ponderación con fuerte preferencia por la ubicación más reciente
        /// </summary>
        private Location GetWeightedNewest(List<Location> locations, MovementContext context)
        {
            try
            {
                if (locations.Count == 0)
                    return null;

                if (locations.Count == 1)
                    return locations[0];

                // Dar un peso muy alto a la ubicación más reciente (90-95%)
                double newestWeight = 0.95;

                // En contexto vehículo, dar aún más peso a la última ubicación
                if (context == MovementContext.Vehicle)
                {
                    newestWeight = 0.98; // 98% para vehículos
                }

                Location newest = locations[0];
                double otherWeight = (1.0 - newestWeight) / (locations.Count - 1);

                // Acumular valores ponderados de ubicaciones anteriores
                double avgLat = 0, avgLon = 0;
                double? avgAlt = null, avgAcc = null, avgSpeed = null, avgCourse = null;
                int altCount = 0, accCount = 0, speedCount = 0, courseCount = 0;

                for (int i = 1; i < locations.Count; i++)
                {
                    avgLat += locations[i].Latitude * otherWeight;
                    avgLon += locations[i].Longitude * otherWeight;

                    if (locations[i].Altitude.HasValue)
                    {
                        avgAlt = (avgAlt ?? 0) + locations[i].Altitude.Value * otherWeight;
                        altCount++;
                    }

                    if (locations[i].Accuracy.HasValue)
                    {
                        avgAcc = (avgAcc ?? 0) + locations[i].Accuracy.Value * otherWeight;
                        accCount++;
                    }

                    if (locations[i].Speed.HasValue)
                    {
                        avgSpeed = (avgSpeed ?? 0) + locations[i].Speed.Value * otherWeight;
                        speedCount++;
                    }

                    if (locations[i].Course.HasValue)
                    {
                        avgCourse = (avgCourse ?? 0) + locations[i].Course.Value * otherWeight;
                        courseCount++;
                    }
                }

                // Crear ubicación resultante, combinando la más reciente (con alto peso) con el promedio ponderado
                var result = new Location
                {
                    Latitude = (newest.Latitude * newestWeight) + avgLat,
                    Longitude = (newest.Longitude * newestWeight) + avgLon,
                    Timestamp = newest.Timestamp,

                    Altitude = newest.Altitude.HasValue ?
                        (newest.Altitude.Value * newestWeight) + (avgAlt ?? 0) : avgAlt,

                    Accuracy = newest.Accuracy.HasValue ?
                        (newest.Accuracy.Value * newestWeight) + (avgAcc ?? 0) : avgAcc,

                    Speed = newest.Speed.HasValue ?
                        (newest.Speed.Value * newestWeight) + (avgSpeed ?? 0) : avgSpeed,

                    Course = newest.Course.HasValue ?
                        (newest.Course.Value * newestWeight) + (avgCourse ?? 0) : avgCourse
                };

                Console.WriteLine($"🔄 Ponderación aplicada: original({newest.Latitude:F6}, {newest.Longitude:F6}) " +
                     $"-> resultado({result.Latitude:F6}, {result.Longitude:F6}), " +
                     $"peso nuevo:{newestWeight:P0}, peso histórico:{(1 - newestWeight):P0}");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en la ponderación: {ex.Message}");
                return locations.First();
            }
        }

        /// <summary>
        /// Aplica un filtro Kalman ultraligero para mantener patrones naturales de movimiento
        /// mientras reduce ligeramente el ruido GPS
        /// </summary>
        public Location ApplyMinimalKalmanFilter(Location location, MovementContext context)
        {
            try
            {
                // Si es la primera vez, inicializar el filtro con la ubicación actual
                if (_latitude == 0 && _longitude == 0)
                {
                    _latitude = location.Latitude;
                    _longitude = location.Longitude;
                    return location;
                }

                // Para vehículos con filtrado ultrabajo, devolver la ubicación original
                if (context == MovementContext.Vehicle && _filteringLevel < 0.1)
                {
                    return location;
                }

                // Aplicar un filtrado Kalman mínimo

                // Ajustar ruido de medición basado en la precisión reportada, con mínima influencia
                double measurementNoise = location.Accuracy.HasValue
                    ? Math.Max(location.Accuracy.Value * _filteringLevel, 0.2) // Valor mínimo muy bajo
                    : _kalmanMeasurementNoise * _filteringLevel;

                // Garantizar valor mínimo para evitar división por cero
                measurementNoise = Math.Max(measurementNoise, 0.1);

                // Aumentar el ruido de proceso para permitir cambios naturales
                double processNoise = _kalmanProcessNoise / (_filteringLevel * 0.5 + 0.1);

                // Actualizar error estimado
                _estimatedError = _estimatedError + processNoise;

                // Calcular ganancia de Kalman
                _kalmanGain = _estimatedError / (_estimatedError + measurementNoise);

                // Limitar ganancia para asegurar mínima influencia de la historia
                _kalmanGain = Math.Max(_kalmanGain, 0.8); // Al menos 80% de la nueva ubicación

                // Actualizar estado (ubicación)
                _latitude = _latitude + _kalmanGain * (location.Latitude - _latitude);
                _longitude = _longitude + _kalmanGain * (location.Longitude - _longitude);

                // Actualizar error estimado
                _estimatedError = (1 - _kalmanGain) * _estimatedError;

                // No reducir la precisión reportada (dejarla casi igual)
                double? filteredAccuracy = location.Accuracy.HasValue
                    ? location.Accuracy.Value * 0.98 // Reducción mínima
                    : null;

                // Crear nueva ubicación con valores filtrados mínimamente
                Location filteredLocation = new Location
                {
                    Latitude = _latitude,
                    Longitude = _longitude,
                    Accuracy = filteredAccuracy,
                    Altitude = location.Altitude,
                    Course = location.Course,
                    Speed = location.Speed,
                    Timestamp = location.Timestamp
                };

                Console.WriteLine($"🔄 Kalman ultraligero: Original({location.Latitude:F6}, {location.Longitude:F6}) " +
                                 $"-> Filtrado({filteredLocation.Latitude:F6}, {filteredLocation.Longitude:F6}), " +
                                 $"Ganancia:{_kalmanGain:F3}");

                return filteredLocation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en filtro Kalman ultraligero: {ex.Message}");
                return location; // En caso de error, devolver la ubicación original
            }
        }
    }
}