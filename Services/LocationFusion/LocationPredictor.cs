using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using DISMOGT_REPORTES.Services.LocationFusion;

namespace DISMOGT_REPORTES.Services.LocationFusion
{
    /// <summary>
    /// Clase encargada de predicciones de movimiento basadas en sensores y ubicaciones anteriores
    /// </summary>
    public class LocationPredictor
    {
        private DateTime _lastPredictionTime = DateTime.MinValue;
        private double _lastSpeed = 0;
        private double _lastBearing = 0;
        private double _consistencyThreshold = 1.5; // Valor predeterminado

        /// <summary>
        /// Establece el umbral de consistencia para la detección de movimientos
        /// </summary>
        /// <param name="threshold">Valor del umbral</param>
        public void SetConsistencyThreshold(double threshold)
        {
            _consistencyThreshold = threshold;
            Console.WriteLine($"🔧 Umbral de consistencia actualizado a: {threshold}");
        }

        /// <summary>
        /// Predice una ubicación basada en el movimiento actual
        /// </summary>
        public Location PredictLocationFromMovement(Location currentLocation, MovementContext context)
        {
            try
            {
                // Si no tenemos datos o la velocidad/rumbo no está disponible, no podemos predecir
                if (!currentLocation.Speed.HasValue ||
                    !currentLocation.Course.HasValue ||
                    currentLocation.Speed.Value < 1.0)
                {
                    return null;
                }

                // Calcular el tiempo transcurrido desde la última predicción
                var timeSinceLastPrediction = (DateTime.Now - _lastPredictionTime).TotalSeconds;
                if (timeSinceLastPrediction < 1.0)
                {
                    return null; // No predecir demasiado frecuentemente
                }

                // Calcular velocidad y dirección
                double speed = currentLocation.Speed.Value;
                double bearing = currentLocation.Course.Value;

                // En algunas circunstancias, usar valores del historial para estabilizar
                if (context == MovementContext.Vehicle && _lastSpeed > 0 && Math.Abs(speed - _lastSpeed) > 5)
                {
                    // Suavizar cambios bruscos de velocidad
                    speed = (_lastSpeed * 0.7) + (speed * 0.3);
                }

                if (_lastBearing > 0 && Math.Abs(bearing - _lastBearing) < 15)
                {
                    // Suavizar pequeñas variaciones en el rumbo
                    bearing = (_lastBearing * 0.8) + (bearing * 0.2);
                }

                // Convertir rumbo a radianes
                double bearingRad = bearing * Math.PI / 180.0;

                // Calcular tiempo de predicción (entre 1 y 3 segundos dependiendo del contexto)
                double predictionTime = context == MovementContext.Vehicle ? 2.0 : 1.0;

                // Calcular distancia que se moverá
                double distance = speed * predictionTime;

                // Convertir a coordenadas
                double R = 6371000; // Radio de la Tierra en metros
                double d = distance / R;
                double lat1 = currentLocation.Latitude * Math.PI / 180;
                double lon1 = currentLocation.Longitude * Math.PI / 180;

                double lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(d) +
                                     Math.Cos(lat1) * Math.Sin(d) * Math.Cos(bearingRad));
                double lon2 = lon1 + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(d) * Math.Cos(lat1),
                                            Math.Cos(d) - Math.Sin(lat1) * Math.Sin(lat2));

                // Convertir de radianes a grados
                lat2 = lat2 * 180 / Math.PI;
                lon2 = lon2 * 180 / Math.PI;

                // Crear ubicación predicha
                var predictedLocation = new Location
                {
                    Latitude = lat2,
                    Longitude = lon2,
                    Accuracy = currentLocation.Accuracy.HasValue ?
                               currentLocation.Accuracy.Value * 1.5 : null, // Precisión reducida
                    Timestamp = DateTime.Now,
                    Speed = speed,
                    Course = bearing
                };

                // Guardar para futuras predicciones
                _lastPredictionTime = DateTime.Now;
                _lastSpeed = speed;
                _lastBearing = bearing;

                Console.WriteLine($"🔮 Ubicación predicha: ({predictedLocation.Latitude:F6}, {predictedLocation.Longitude:F6})");
                return predictedLocation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en predicción de movimiento: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifica si la ubicación es consistente con el estado de movimiento actual
        /// </summary>
        public bool IsConsistentWithMotion(Location location, Location lastLocation, bool isDeviceMoving, MovementContext context)
        {
            if (lastLocation == null)
                return true; // No hay suficiente historial para determinar

            try
            {
                // Calcular el tiempo transcurrido en segundos
                TimeSpan timeDiff = location.Timestamp - lastLocation.Timestamp;
                if (timeDiff.TotalSeconds <= 0)
                    return true; // No podemos calcular la velocidad

                // Distancia aproximada en metros
                double distance = LocationUtils.CalculateDistance(
                    lastLocation.Latitude, lastLocation.Longitude,
                    location.Latitude, location.Longitude
                );

                // Velocidad en metros por segundo
                double speed = distance / timeDiff.TotalSeconds;

                // Verificar consistencia:
                // 1. Si el dispositivo se está moviendo, debería haber cambio en la ubicación
                // 2. Si el dispositivo está quieto, no debería haber grandes cambios en la ubicación

                // Ajustar umbrales según el contexto y la precisión
                double threshold = _consistencyThreshold; // Usar el umbral configurado

                if (context == MovementContext.Indoor)
                {
                    threshold = threshold * 2.0; // Más tolerancia en interiores debido a mala señal
                }
                else if (context == MovementContext.Vehicle)
                {
                    threshold = threshold * 1.0; // Mantener umbral en vehículos
                }

                bool locationShowsMovement = speed > threshold;

                // Considerar la precisión de la ubicación al evaluar inconsistencias
                bool hasGoodAccuracy = location.Accuracy.HasValue && location.Accuracy.Value < 20;

                // Si los sensores indican movimiento pero la ubicación no cambia (o viceversa),
                // hay una inconsistencia, pero solo si la precisión es buena
                bool inconsistency = ((isDeviceMoving && !locationShowsMovement) ||
                                    (!isDeviceMoving && locationShowsMovement)) && hasGoodAccuracy;

                if (inconsistency)
                {
                    Console.WriteLine($"⚠️ Inconsistencia detectada: Sensores:{(isDeviceMoving ? "en movimiento" : "quieto")}, " +
                                     $"GPS:{(locationShowsMovement ? "en movimiento" : "quieto")}, " +
                                     $"Velocidad:{speed:F2} m/s, Precisión:{location.Accuracy:F2} m");
                }

                return !inconsistency;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al verificar consistencia con movimiento: {ex.Message}");
                return true; // En caso de error, asumimos que es consistente
            }
        }
    }
}