using Android.Content;
using Android.Locations;
using Android.Net.Wifi;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Location = Microsoft.Maui.Devices.Sensors.Location;

namespace DISMOGT_REPORTES.Services.LocationFusion
{
    /// <summary>
    /// Clase encargada de detectar el contexto de movimiento del usuario
    /// </summary>
    public class ContextDetector
    {
        private readonly Context _context;
        private readonly LocationManager _locationManager;
        private readonly WifiManager _wifiManager;
        private MovementContext _currentContext = MovementContext.Unknown;
        private int _indoorConfidence = 50; // 0-100
        private DateTime _lastContextUpdate = DateTime.MinValue;

        public ContextDetector(Context context)
        {
            _context = context;
            _locationManager = (LocationManager)_context.GetSystemService(Context.LocationService);
            _wifiManager = (WifiManager)_context.GetSystemService(Context.WifiService);
        }

        /// <summary>
        /// Obtiene el contexto de movimiento actual
        /// </summary>
        public MovementContext CurrentContext => _currentContext;

        /// <summary>
        /// Actualiza el contexto de movimiento basado en los sensores y datos de ubicación
        /// </summary>
        public MovementContext UpdateMovementContext(
            List<Vector3> accelerationReadings,
            List<Vector3> rotationReadings,
            List<Location> locationHistory,
            bool isMoving)
        {
            try
            {
                // Limitar la frecuencia de actualización (cada 3 segundos)
                if ((DateTime.Now - _lastContextUpdate).TotalSeconds < 3)
                {
                    return _currentContext;
                }

                MovementContext newContext = _currentContext;

                // 1. Análisis de acelerómetro para determinar tipo de movimiento
                if (accelerationReadings != null && accelerationReadings.Count >= 5)
                {
                    // Calcular las últimas lecturas sin gravedad
                    Vector3 gravity = new Vector3(0, 0, 9.8f);
                    var recentAccel = accelerationReadings.TakeLast(5)
                        .Select(a => (a - gravity).Length())
                        .ToList();

                    // Calcular estadísticas
                    double avgAccel = recentAccel.Average();
                    double maxAccel = recentAccel.Max();
                    double stdDev = Math.Sqrt(recentAccel.Select(a => Math.Pow(a - avgAccel, 2)).Average());

                    // Toma de decisiones basada en características de aceleración
                    if (avgAccel < 0.3)
                    {
                        newContext = MovementContext.Stationary;
                    }
                    else if (avgAccel < 1.5 && stdDev < 0.7)
                    {
                        newContext = MovementContext.Walking;
                    }
                    else if (avgAccel > 1.0 || stdDev > 1.0)
                    {
                        newContext = MovementContext.Vehicle;
                    }

                    // Usar también giroscopio si está disponible
                    if (rotationReadings != null && rotationReadings.Count >= 5)
                    {
                        var recentRot = rotationReadings.TakeLast(5)
                            .Select(r => r.Length())
                            .ToList();

                        double avgRot = recentRot.Average();

                        // Rotación alta indica vehículo
                        if (avgRot > 1.0 && newContext != MovementContext.Stationary)
                        {
                            newContext = MovementContext.Vehicle;
                        }
                        // Rotación baja refuerza estado estacionario
                        else if (avgRot < 0.2 && newContext == MovementContext.Stationary)
                        {
                            newContext = MovementContext.Stationary;
                        }
                    }
                }

                // 2. Complementar con datos de ubicación si están disponibles
                if (locationHistory != null && locationHistory.Count >= 3)
                {
                    var recentLocations = locationHistory.TakeLast(3).ToList();
                    double avgSpeed = 0;

                    for (int i = 1; i < recentLocations.Count; i++)
                    {
                        double speed = LocationUtils.CalculateSpeed(recentLocations[i - 1], recentLocations[i]);
                        avgSpeed += speed;
                    }

                    avgSpeed /= (recentLocations.Count - 1);

                    // Usar velocidad GPS para refinar la clasificación
                    if (avgSpeed < 0.5)
                    {
                        newContext = MovementContext.Stationary;
                    }
                    else if (avgSpeed < 2.0 && newContext != MovementContext.Vehicle)
                    {
                        newContext = MovementContext.Walking;
                    }
                    else if (avgSpeed > 5.0)
                    {
                        newContext = MovementContext.Vehicle;
                    }
                }

                // 3. Detección de interiores
                DetectIndoorEnvironment();
                if (_indoorConfidence > 70)
                {
                    // Solo cambiar a indoor si estamos relativamente seguros
                    newContext = MovementContext.Indoor;
                }

                // Si el contexto cambió, registrarlo
                if (newContext != _currentContext)
                {
                    Console.WriteLine($"🧠 Contexto actualizado: {_currentContext} → {newContext}");
                    _currentContext = newContext;
                }

                _lastContextUpdate = DateTime.Now;
                return _currentContext;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al actualizar contexto de movimiento: {ex.Message}");
                return _currentContext;
            }
        }

        /// <summary>
        /// Detecta si el usuario está en un ambiente interior
        /// </summary>
        private void DetectIndoorEnvironment()
        {
            try
            {
                // Inicializar con confianza neutra
                int confidence = 50;

                // 1. Verificar disponibilidad y potencia de señal GPS
                bool hasGps = _locationManager.IsProviderEnabled(LocationManager.GpsProvider);

                // Intentar obtener la última ubicación y su precisión
                Android.Locations.Location lastLocation = _locationManager.GetLastKnownLocation(LocationManager.GpsProvider);
                if (lastLocation != null && lastLocation.HasAccuracy)
                {
                    // Precisión baja (valores altos) indica probable interior
                    float accuracy = lastLocation.Accuracy;
                    if (accuracy > 30)
                    {
                        confidence += 15; // Aumentar confianza de interior
                    }
                    else if (accuracy < 10)
                    {
                        confidence -= 20; // Disminuir confianza de interior (probablemente exterior)
                    }
                }
                else if (!hasGps)
                {
                    confidence += 10; // Sin GPS activo, aumenta probabilidad de interior
                }

                // 2. Verificar señales WiFi (cantidad de redes suele ser mayor en interiores)
                if (_wifiManager != null)
                {
                    _wifiManager.StartScan();
                    var scanResults = _wifiManager.ScanResults;

                    if (scanResults != null)
                    {
                        int networkCount = scanResults.Count;

                        // Muchas redes suelen indicar entorno interior
                        if (networkCount > 8)
                        {
                            confidence += 20;
                        }
                        else if (networkCount > 4)
                        {
                            confidence += 10;
                        }
                        else if (networkCount < 2)
                        {
                            confidence -= 15; // Pocas redes sugieren exterior
                        }

                        // Analizar potencia de señal
                        float avgSignalStrength = 0;
                        if (networkCount > 0)
                        {
                            avgSignalStrength = (float)scanResults.Average(r => r.Level);
                            // Señal débil sugiere obstáculos, típico de interiores
                            if (avgSignalStrength < -75)
                            {
                                confidence += 10;
                            }
                        }
                    }
                }

                // Limitar confianza al rango 0-100
                _indoorConfidence = Math.Max(0, Math.Min(100, confidence));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al detectar ambiente interior: {ex.Message}");
            }
        }
    }
}