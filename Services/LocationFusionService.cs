using Android.Content;
using Android.Hardware;
using Android.Locations;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using DISMOGT_REPORTES.Models;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;
using AndroidLocation = Android.Locations.Location;
using DISMOGT_REPORTES.Services.LocationFusion;

namespace DISMOGT_REPORTES.Services
{
    /// <summary>
    /// Servicio principal de fusión de datos de ubicación que coordina todos los componentes
    /// </summary>
    public class LocationFusionService : IDisposable
    {
        private readonly Context _context;
        private readonly SensorManager _sensorManager;
        private readonly Sensor _accelerometer;
        private readonly Sensor _magnetometer;
        private readonly Sensor _gyroscope;
        private readonly LocationManager _locationManager;
        private FusionAccelerometerListener _accelerometerListener;
        private FusionMagnetometerListener _magnetometerListener;
        private FusionGyroscopeListener _gyroscopeListener;

        // Componentes modulares
        private readonly ContextDetector _contextDetector;
        private readonly LocationFilters _locationFilters;
        private readonly LocationPredictor _locationPredictor;
        private readonly TelemetryService _telemetryService;

        // Datos de sensores y estado
        private readonly List<MauiLocation> _locationHistory = new List<MauiLocation>();
        private readonly List<Vector3> _accelerationReadings = new List<Vector3>();
        private readonly List<Vector3> _magneticReadings = new List<Vector3>();
        private readonly List<Vector3> _rotationReadings = new List<Vector3>();
        private Vector3 _lastAcceleration;
        private Vector3 _lastMagneticField;
        private Vector3 _lastRotation;
        private bool _isMoving = false;
        private bool _isInitialized = false;
        private readonly int _historySize = 20;

        /// <summary>
        /// Constructor del servicio de fusión
        /// </summary>
        public LocationFusionService(Context context)
        {
            _context = context;

            try
            {
                // Inicializar sensores
                _sensorManager = (SensorManager)_context.GetSystemService(Context.SensorService);
                _accelerometer = _sensorManager?.GetDefaultSensor(SensorType.Accelerometer);
                _magnetometer = _sensorManager?.GetDefaultSensor(SensorType.MagneticField);
                _gyroscope = _sensorManager?.GetDefaultSensor(SensorType.Gyroscope);
                _locationManager = (LocationManager)_context.GetSystemService(Context.LocationService);

                // Inicializar componentes
                _contextDetector = new ContextDetector(context);
                _locationFilters = new LocationFilters();
                _locationPredictor = new LocationPredictor();
                _telemetryService = new TelemetryService(
                    Android.OS.Environment.ExternalStorageDirectory.AbsolutePath + "/DISMOGTREPORTES");

                // Inicializar listeners de sensores
                InitializeSensors();

                _isInitialized = true;
                Console.WriteLine("✅ Servicio avanzado de fusión de ubicación inicializado correctamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al inicializar servicio de fusión: {ex.Message}");
            }
        }

        /// <summary>
        /// Inicializa los listeners de los sensores
        /// </summary>
        private void InitializeSensors()
        {
            try
            {
                if (_accelerometer != null)
                {
                    _accelerometerListener = new FusionAccelerometerListener();
                    _accelerometerListener.SensorChanged += (values) =>
                    {
                        _lastAcceleration = new Vector3(values[0], values[1], values[2]);
                        _accelerationReadings.Add(_lastAcceleration);
                        if (_accelerationReadings.Count > _historySize)
                            _accelerationReadings.RemoveAt(0);

                        UpdateMotionState();
                    };
                }

                if (_magnetometer != null)
                {
                    _magnetometerListener = new FusionMagnetometerListener();
                    _magnetometerListener.SensorChanged += (values) =>
                    {
                        _lastMagneticField = new Vector3(values[0], values[1], values[2]);
                        _magneticReadings.Add(_lastMagneticField);
                        if (_magneticReadings.Count > _historySize)
                            _magneticReadings.RemoveAt(0);
                    };
                }

                if (_gyroscope != null)
                {
                    _gyroscopeListener = new FusionGyroscopeListener();
                    _gyroscopeListener.SensorChanged += (values) =>
                    {
                        _lastRotation = new Vector3(values[0], values[1], values[2]);
                        _rotationReadings.Add(_lastRotation);
                        if (_rotationReadings.Count > _historySize)
                            _rotationReadings.RemoveAt(0);
                    };
                }

                RegisterSensors();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al inicializar sensores: {ex.Message}");
            }
        }

        /// <summary>
        /// Registra los sensores con las tasas de muestreo adecuadas
        /// </summary>
        private void RegisterSensors()
        {
            try
            {
                if (_accelerometer != null)
                {
                    _sensorManager.RegisterListener(
                        _accelerometerListener,
                        _accelerometer,
                        SensorDelay.Normal
                    );
                    Console.WriteLine("🔄 Acelerómetro registrado");
                }

                if (_magnetometer != null)
                {
                    _sensorManager.RegisterListener(
                        _magnetometerListener,
                        _magnetometer,
                        SensorDelay.Normal
                    );
                    Console.WriteLine("🧲 Magnetómetro registrado");
                }

                if (_gyroscope != null)
                {
                    _sensorManager.RegisterListener(
                        _gyroscopeListener,
                        _gyroscope,
                        SensorDelay.Normal
                    );
                    Console.WriteLine("🔄 Giroscopio registrado");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al registrar sensores: {ex.Message}");
            }
        }

        /// <summary>
        /// Actualiza el estado de movimiento basado en lecturas del acelerómetro
        /// </summary>
        private void UpdateMotionState()
        {
            if (_accelerationReadings.Count < 3)
                return;

            try
            {
                // Calcular la magnitud de la aceleración actual sin gravedad
                Vector3 gravity = new Vector3(0, 0, 9.8f); // Aproximación simple de la gravedad
                Vector3 linearAcceleration = _lastAcceleration - gravity;
                float magnitude = linearAcceleration.Length();

                // Usar un promedio móvil para reducir ruido
                float avgMagnitude = 0;
                int count = 0;
                foreach (var accel in _accelerationReadings.TakeLast(3))
                {
                    avgMagnitude += (accel - gravity).Length();
                    count++;
                }
                avgMagnitude /= count;

                // Actualizar estado de movimiento
                bool previousState = _isMoving;
                _isMoving = avgMagnitude > 0.8; // Umbral de movimiento: 0.8 m/s²

                // Si el estado cambió, registrarlo
                if (previousState != _isMoving)
                {
                    Console.WriteLine($"🧠 Estado de movimiento: {(_isMoving ? "En movimiento" : "Estacionario")}");
                }

                // Actualizar contexto de movimiento
                _contextDetector.UpdateMovementContext(
                    _accelerationReadings,
                    _rotationReadings,
                    _locationHistory,
                    _isMoving);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al actualizar estado de movimiento: {ex.Message}");
            }
        }

        /// <summary>
        /// Método principal para obtener una ubicación mejorada con fusión de datos
        /// </summary>
        public async Task<LocationResult> GetFusedLocationAsync(MauiLocation rawLocation, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized || rawLocation == null)
                return new LocationResult { Location = rawLocation, IsSuspicious = false };

            var startTime = DateTime.Now;
            double originalAccuracy = rawLocation.Accuracy ?? 100.0;

            try
            {
                Console.WriteLine($"🧩 Comenzando fusión de ubicación, precisión original: {originalAccuracy}m");

                // 1. Obtenemos ubicaciones de diferentes proveedores (GPS, Red, Pasivos)
                List<MauiLocation> candidateLocations = new List<MauiLocation>();

                // Añadir la ubicación cruda recibida
                candidateLocations.Add(rawLocation);

                // Comprobar si podemos obtener ubicaciones adicionales de Android
                if (_locationManager != null)
                {
                    try
                    {
                        // Intentar obtener la última ubicación conocida del proveedor de red
                        AndroidLocation networkLocation = _locationManager.GetLastKnownLocation(LocationManager.NetworkProvider);
                        if (networkLocation != null)
                        {
                            candidateLocations.Add(new MauiLocation
                            {
                                Latitude = networkLocation.Latitude,
                                Longitude = networkLocation.Longitude,
                                Accuracy = networkLocation.HasAccuracy ? networkLocation.Accuracy : 100,
                                Altitude = networkLocation.HasAltitude ? networkLocation.Altitude : null,
                                Course = networkLocation.HasBearing ? networkLocation.Bearing : null,
                                Speed = networkLocation.HasSpeed ? networkLocation.Speed : null,
                                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(networkLocation.Time).DateTime
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error al obtener ubicación de red: {ex.Message}");
                    }
                }

                // 2. Validar y filtrar ubicaciones basadas en criterios de calidad
                var validLocations = candidateLocations
                    .Where(loc => loc != null && loc.Accuracy.HasValue && loc.Accuracy.Value < 100)
                    .ToList();

                if (validLocations.Count == 0)
                {
                    Console.WriteLine("⚠️ No hay ubicaciones válidas disponibles");
                    return new LocationResult { Location = rawLocation, IsSuspicious = false };
                }

                // 3. Verificar si la ubicación es consistente con el estado de movimiento
                MauiLocation lastLocation = _locationHistory.Count > 0 ? _locationHistory.Last() : null;
                MovementContext currentContext = _contextDetector.CurrentContext;

                bool isConsistentWithMotion = lastLocation == null ||
                    _locationPredictor.IsConsistentWithMotion(rawLocation, lastLocation, _isMoving, currentContext);

                bool isSuspicious = !isConsistentWithMotion;
                string suspiciousReason = isSuspicious ? "Inconsistencia entre movimiento y ubicación; " : "";

                // 4. Aplicar predicción de movimiento si estamos en vehículo o caminando
                if (currentContext == MovementContext.Vehicle || currentContext == MovementContext.Walking)
                {
                    var predictedLocation = _locationPredictor.PredictLocationFromMovement(rawLocation, currentContext);
                    if (predictedLocation != null)
                    {
                        candidateLocations.Add(predictedLocation);
                        Console.WriteLine("🔮 Añadida ubicación predicha basada en movimiento");
                    }
                }

                // 5. Aplicar filtros para mejorar la precisión
                MauiLocation fusedLocation = _locationFilters.ApplyFilters(validLocations, currentContext);

                // 6. Aplicar correcciones específicas para el contexto
                List<MauiLocation> historyList = _locationHistory.Count > 0 ? _locationHistory : null;
                if (historyList != null)
                {
                    fusedLocation = _locationFilters.ApplyContextSpecificCorrections(fusedLocation, historyList, currentContext);
                }

                // 7. Actualizar el historial de ubicaciones
                UpdateLocationHistory(fusedLocation);

                // 8. Actualizar los parámetros de Kalman basados en el rendimiento reciente
                var (improvements, worsenings) = _telemetryService.GetConsecutiveCounters();
                _locationFilters.UpdateKalmanParameters(currentContext, improvements, worsenings);

                // 9. Registrar métricas de rendimiento
                double finalAccuracy = fusedLocation.Accuracy ?? originalAccuracy;
                double processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                _telemetryService.RecordPerformanceMetric(originalAccuracy, finalAccuracy, processingTime,
                    isSuspicious, suspiciousReason, currentContext);

                Console.WriteLine($"✅ Fusión completada en {processingTime:F1}ms. " +
                    $"Precisión: {originalAccuracy:F1}m → {finalAccuracy:F1}m " +
                    $"(Mejora: {originalAccuracy - finalAccuracy:F1}m)");

                // 10. Devolver el resultado
                return new LocationResult
                {
                    Location = fusedLocation,
                    IsSuspicious = isSuspicious,
                    SuspiciousReason = suspiciousReason,
                    IsMoving = _isMoving,
                    MovementContext = currentContext,
                    MovementContextName = currentContext.ToString()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en fusión de ubicación: {ex.Message}");

                // Registrar error en métricas
                _telemetryService.RecordPerformanceMetric(originalAccuracy, originalAccuracy,
                    (DateTime.Now - startTime).TotalMilliseconds, false,
                    $"Error: {ex.Message}", MovementContext.Unknown);

                return new LocationResult { Location = rawLocation, IsSuspicious = false };
            }
        }

        /// <summary>
        /// Actualiza el historial de ubicaciones
        /// </summary>
        private void UpdateLocationHistory(MauiLocation location)
        {
            _locationHistory.Add(location);
            if (_locationHistory.Count > _historySize)
            {
                _locationHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Obtiene estadísticas de rendimiento del sistema de fusión
        /// </summary>
        public Dictionary<string, double> GetPerformanceStats()
        {
            return _telemetryService.GetPerformanceStats();
        }

        /// <summary>
        /// Libera los recursos y cancela las suscripciones a sensores
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_sensorManager != null)
                {
                    if (_accelerometerListener != null)
                        _sensorManager.UnregisterListener(_accelerometerListener);

                    if (_magnetometerListener != null)
                        _sensorManager.UnregisterListener(_magnetometerListener);

                    if (_gyroscopeListener != null)
                        _sensorManager.UnregisterListener(_gyroscopeListener);
                }

                Console.WriteLine("🧹 Recursos del servicio de fusión liberados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al liberar recursos: {ex.Message}");
            }
        }
    }
}