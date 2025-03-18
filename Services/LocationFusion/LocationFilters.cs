using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using DISMOGT_REPORTES.Services.LocationFusion;

namespace DISMOGT_REPORTES.Services.LocationFusion
{
    /// <summary>
    /// Clase encargada de los filtros aplicados a las ubicaciones
    /// </summary>
    public class LocationFilters
    {
        private double _kalmanProcessNoise = 0.01;
        private double _kalmanMeasurementNoise = 1.0;
        private double _kalmanGain = 0;
        private double _estimatedError = 1;
        private double _latitude = 0;
        private double _longitude = 0;

        /// <summary>
        /// Actualiza los parámetros de filtrado Kalman según el contexto
        /// </summary>
        public void UpdateKalmanParameters(MovementContext context, int consecutiveImprovements, int consecutiveWorsenings)
        {
            // Ajustar parámetros según el contexto actual
            switch (context)
            {
                case MovementContext.Stationary:
                    // Cuando estamos quietos, queremos filtrar mucho el ruido
                    _kalmanProcessNoise = 0.001;    // Bajo ruido de proceso
                    _kalmanMeasurementNoise = 2.0;  // Alta desconfianza en mediciones
                    Console.WriteLine("⚙️ Calibración para modo estático");
                    break;

                case MovementContext.Walking:
                    // Balance para movimiento peatonal
                    _kalmanProcessNoise = 0.01;
                    _kalmanMeasurementNoise = 1.0;
                    Console.WriteLine("⚙️ Calibración para modo caminata");
                    break;

                case MovementContext.Vehicle:
                    // Para vehículos necesitamos adaptarnos rápido a cambios
                    _kalmanProcessNoise = 0.1;
                    _kalmanMeasurementNoise = 0.5; // Mayor confianza en mediciones
                    Console.WriteLine("⚙️ Calibración para modo vehículo");
                    break;

                case MovementContext.Indoor:
                    // Interiores tienen GPS poco confiable, usamos más historia
                    _kalmanProcessNoise = 0.005;
                    _kalmanMeasurementNoise = 3.0; // Muy baja confianza en mediciones
                    Console.WriteLine("⚙️ Calibración para modo interior");
                    break;

                default:
                    // Valores por defecto balanceados
                    _kalmanProcessNoise = 0.01;
                    _kalmanMeasurementNoise = 1.0;
                    break;
            }

            // Ajustar también según rendimiento reciente
            if (consecutiveImprovements > 3)
            {
                // Si las mejoras son consistentes, mantener configuración actual
                Console.WriteLine("⚙️ Manteniendo configuración por mejoras consistentes");
            }
            else if (consecutiveWorsenings > 2)
            {
                // Si consistentemente empeoramos la precisión, hacer ajuste
                _kalmanProcessNoise *= 0.8;
                _kalmanMeasurementNoise *= 1.2;
                Console.WriteLine("⚙️ Ajustando parámetros por deterioro de precisión");
            }
        }

        /// <summary>
        /// Aplica filtros para mejorar la precisión
        /// </summary>
        public Location ApplyFilters(List<Location> locations, MovementContext context)
        {
            try
            {
                // Si solo hay una ubicación, aplicamos el filtro de Kalman
                if (locations.Count == 1)
                {
                    return ApplyKalmanFilter(locations[0], context);
                }

                // Si hay múltiples ubicaciones, primero las ordenamos por precisión
                var orderedLocations = locations.OrderBy(l => l.Accuracy).ToList();

                // Tomar la ubicación más precisa como base
                Location bestLocation = orderedLocations.First();

                // Si hay ubicaciones de diferentes proveedores, podemos hacer fusión ponderada
                if (locations.Count > 1)
                {
                    // Implementar fusión ponderada basada en precisión
                    bestLocation = ApplyWeightedFusion(orderedLocations, context);
                }

                // Aplicar filtro de Kalman 
                bestLocation = ApplyKalmanFilter(bestLocation, context);

                return bestLocation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al aplicar filtros: {ex.Message}");
                return locations.First(); // En caso de error, devolver la primera ubicación
            }
        }

        /// <summary>
        /// Aplica fusión ponderada a múltiples ubicaciones basada en precisión
        /// </summary>
        public Location ApplyWeightedFusion(List<Location> locations, MovementContext context)
        {
            try
            {
                // Si no hay ubicaciones, devolver null
                if (locations == null || locations.Count == 0)
                    return null;

                // Si solo hay una ubicación, devolverla directamente
                if (locations.Count == 1)
                    return locations[0];

                // Calcular pesos inversos a la precisión (menor precisión = menor peso)
                double[] weights = new double[locations.Count];
                double totalWeight = 0;

                for (int i = 0; i < locations.Count; i++)
                {
                    // Si la precisión no está disponible, asignar un valor alto
                    double accuracy = locations[i].Accuracy ?? 100.0;

                    // Peso inversamente proporcional a la precisión (mayor precision = menor valor numérico = mayor peso)
                    weights[i] = 1.0 / Math.Max(accuracy, 1.0);

                    // Aplicar ajustes según el contexto
                    if (context == MovementContext.Indoor)
                    {
                        // En interiores, dar más peso a ubicaciones de red que a GPS
                        if (i > 0) weights[i] *= 1.5; // Asumimos que ubicaciones adicionales son de red
                    }

                    totalWeight += weights[i];
                }

                // Normalizar pesos
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] /= totalWeight;
                }

                // Calcular ubicación fusionada ponderada
                double weightedLat = 0;
                double weightedLon = 0;
                double weightedAltitude = 0;
                double weightedSpeed = 0;
                double weightedCourse = 0;
                double weightedAccuracy = 0;
                int altitudeCount = 0;
                int speedCount = 0;
                int courseCount = 0;

                for (int i = 0; i < locations.Count; i++)
                {
                    weightedLat += locations[i].Latitude * weights[i];
                    weightedLon += locations[i].Longitude * weights[i];

                    if (locations[i].Altitude.HasValue)
                    {
                        weightedAltitude += locations[i].Altitude.Value * weights[i];
                        altitudeCount++;
                    }

                    if (locations[i].Speed.HasValue)
                    {
                        weightedSpeed += locations[i].Speed.Value * weights[i];
                        speedCount++;
                    }

                    if (locations[i].Course.HasValue)
                    {
                        weightedCourse += locations[i].Course.Value * weights[i];
                        courseCount++;
                    }

                    if (locations[i].Accuracy.HasValue)
                    {
                        weightedAccuracy += locations[i].Accuracy.Value * weights[i];
                    }
                }

                // Crear ubicación fusionada
                Location fusedLocation = new Location
                {
                    Latitude = weightedLat,
                    Longitude = weightedLon,
                    Accuracy = weightedAccuracy > 0 ? weightedAccuracy : null,
                    Altitude = altitudeCount > 0 ? weightedAltitude : null,
                    Speed = speedCount > 0 ? weightedSpeed : null,
                    Course = courseCount > 0 ? weightedCourse : null,
                    Timestamp = DateTime.Now
                };

                Console.WriteLine($"🔄 Fusión ponderada aplicada: ({fusedLocation.Latitude:F6}, {fusedLocation.Longitude:F6})");
                return fusedLocation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en fusión ponderada: {ex.Message}");
                return locations.First(); // En caso de error, devolver la primera ubicación
            }
        }

        /// <summary>
        /// Aplica un filtro de Kalman para suavizar las lecturas de ubicación
        /// </summary>
        public Location ApplyKalmanFilter(Location location, MovementContext context)
        {
            try
            {
                // Si es la primera vez, inicializamos el filtro con la ubicación actual
                if (_latitude == 0 && _longitude == 0)
                {
                    _latitude = location.Latitude;
                    _longitude = location.Longitude;
                    return location;
                }

                // Ajustar ruido de medición basado en la precisión reportada
                double measurementNoise = location.Accuracy.HasValue
                    ? Math.Max(location.Accuracy.Value, 1.0)
                    : _kalmanMeasurementNoise;

                // Modificar ruido de medición basado en el contexto
                if (context == MovementContext.Vehicle && location.Speed.HasValue && location.Speed.Value > 10)
                {
                    // En vehículos a alta velocidad, confiar más en las nuevas mediciones
                    measurementNoise *= 0.7;
                }
                else if (context == MovementContext.Indoor)
                {
                    // En interiores, desconfiar más de las mediciones
                    measurementNoise *= 1.5;
                }

                // Actualización del error estimado
                _estimatedError = _estimatedError + _kalmanProcessNoise;

                // Cálculo de la ganancia de Kalman
                _kalmanGain = _estimatedError / (_estimatedError + measurementNoise);

                // Actualización del estado (ubicación)
                _latitude = _latitude + _kalmanGain * (location.Latitude - _latitude);
                _longitude = _longitude + _kalmanGain * (location.Longitude - _longitude);

                // Actualización del error estimado
                _estimatedError = (1 - _kalmanGain) * _estimatedError;

                // Mejorar estimación de precisión
                double? filteredAccuracy = location.Accuracy.HasValue
                    ? Math.Max(location.Accuracy.Value * (1 - (_kalmanGain / 2)), 1.0)
                    : null;

                // Crear una nueva ubicación con los valores filtrados
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

                Console.WriteLine($"🔄 Filtro Kalman aplicado: Original({location.Latitude:F6}, {location.Longitude:F6}) " +
                                 $"-> Filtrado({filteredLocation.Latitude:F6}, {filteredLocation.Longitude:F6}), " +
                                 $"Ganancia:{_kalmanGain:F3}");

                return filteredLocation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al aplicar filtro Kalman: {ex.Message}");
                return location; // En caso de error, devolver la ubicación original
            }
        }

        /// <summary>
        /// Aplica correcciones específicas según el contexto de movimiento
        /// </summary>
        public Location ApplyContextSpecificCorrections(Location location, List<Location> historyLocations, MovementContext context)
        {
            try
            {
                // Si no hay suficiente historial, devolver ubicación sin cambios
                if (historyLocations.Count < 3)
                    return location;

                var lastLocations = historyLocations.TakeLast(3).ToList();

                // Aplicar correcciones específicas según el contexto
                switch (context)
                {
                    case MovementContext.Stationary:
                        // Si estamos quietos pero hay pequeñas variaciones, suavizarlas
                        if (location.Accuracy.HasValue && location.Accuracy.Value < 30)
                        {
                            // Calcular posición promedio de las últimas ubicaciones
                            double avgLat = lastLocations.Average(l => l.Latitude);
                            double avgLon = lastLocations.Average(l => l.Longitude);

                            // Mezclar la ubicación actual con el promedio (más peso al promedio)
                            location.Latitude = (avgLat * 0.7) + (location.Latitude * 0.3);
                            location.Longitude = (avgLon * 0.7) + (location.Longitude * 0.3);

                            Console.WriteLine("🧠 Aplicada corrección para modo estático");
                        }
                        break;

                    case MovementContext.Indoor:
                        // En interiores, confiar más en el historial y velocidad/dirección constante
                        if (lastLocations.Count >= 3)
                        {
                            // Verificar si hay un patrón de movimiento consistente
                            var directionVectors = new List<(double, double)>();
                            for (int i = 1; i < lastLocations.Count; i++)
                            {
                                directionVectors.Add((
                                    lastLocations[i].Latitude - lastLocations[i - 1].Latitude,
                                    lastLocations[i].Longitude - lastLocations[i - 1].Longitude
                                ));
                            }

                            // Si hay una dirección de movimiento consistente
                            if (directionVectors.Count >= 2)
                            {
                                double avgDeltaLat = directionVectors.Average(v => v.Item1);
                                double avgDeltaLon = directionVectors.Average(v => v.Item2);

                                // Si la nueva ubicación es inconsistente con la dirección anterior,
                                // ajustarla ligeramente hacia la dirección esperada
                                double newDeltaLat = location.Latitude - lastLocations.Last().Latitude;
                                double newDeltaLon = location.Longitude - lastLocations.Last().Longitude;

                                if (Math.Sign(newDeltaLat) != Math.Sign(avgDeltaLat) ||
                                    Math.Sign(newDeltaLon) != Math.Sign(avgDeltaLon))
                                {
                                    // Ajustar la ubicación para que siga más la tendencia anterior
                                    location.Latitude = lastLocations.Last().Latitude +
                                                      (avgDeltaLat * 0.6 + newDeltaLat * 0.4);
                                    location.Longitude = lastLocations.Last().Longitude +
                                                       (avgDeltaLon * 0.6 + newDeltaLon * 0.4);

                                    Console.WriteLine("🧠 Aplicada corrección para modo interior (consistencia direccional)");
                                }
                            }
                        }
                        break;

                    case MovementContext.Vehicle:
                        // En vehículos, asegurarse de que el movimiento siga rutas naturales
                        if (location.Speed.HasValue && location.Speed.Value > 5 &&
                            location.Course.HasValue && lastLocations.Last().Course.HasValue)
                        {
                            // Calcular cambio de dirección
                            double courseChange = Math.Abs(location.Course.Value - lastLocations.Last().Course.Value);
                            if (courseChange > 180) courseChange = 360 - courseChange;

                            // Si hay un cambio de dirección muy abrupto a alta velocidad, suavizarlo
                            if (courseChange > 45 && location.Speed.Value > 15)
                            {
                                double lastCourse = lastLocations.Last().Course.Value;
                                double adjustedCourse = lastCourse + (((location.Course.Value - lastCourse + 540) % 360) - 180) * 0.5;
                                location.Course = adjustedCourse;

                                // También ajustar ligeramente la posición para que coincida con el curso ajustado
                                double distFromLast = LocationUtils.CalculateDistance(
                                    lastLocations.Last().Latitude, lastLocations.Last().Longitude,
                                    location.Latitude, location.Longitude);

                                // Calcular nueva posición basada en el curso ajustado
                                double bearingRad = adjustedCourse * Math.PI / 180.0;
                                double lat1 = lastLocations.Last().Latitude * Math.PI / 180;
                                double lon1 = lastLocations.Last().Longitude * Math.PI / 180;
                                double R = 6371000; // Radio tierra en metros
                                double d = distFromLast / R;

                                double lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(d) +
                                             Math.Cos(lat1) * Math.Sin(d) * Math.Cos(bearingRad));
                                double lon2 = lon1 + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(d) * Math.Cos(lat1),
                                                            Math.Cos(d) - Math.Sin(lat1) * Math.Sin(lat2));

                                // Convertir de radianes a grados
                                lat2 = lat2 * 180 / Math.PI;
                                lon2 = lon2 * 180 / Math.PI;

                                // Mezclar la posición original con la ajustada
                                location.Latitude = location.Latitude * 0.5 + lat2 * 0.5;
                                location.Longitude = location.Longitude * 0.5 + lon2 * 0.5;

                                Console.WriteLine("🧠 Aplicada corrección para modo vehículo (suavizado de curvas)");
                            }
                        }
                        break;
                }

                return location;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al aplicar correcciones específicas: {ex.Message}");
                return location; // En caso de error, devolver la ubicación sin cambios
            }
        }
    }
}