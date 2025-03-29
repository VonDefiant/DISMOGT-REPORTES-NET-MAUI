using Android.Content;
using Android.Hardware;
using Android.Locations;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using DISMOGT_REPORTES.Models;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;
using AndroidLocation = Android.Locations.Location;

namespace DISMOGT_REPORTES.Services.LocationFusion
{
    /// <summary>
    /// Enum para definir niveles de alerta para la detección de anomalías
    /// </summary>
    public enum AnomalyAlertLevel
    {
        Low,    // Alertas mínimas - solo falsificaciones obvias
        Medium, // Equilibrio entre alertas y falsos positivos (valor predeterminado)
        High    // Alertas máximas - detecta patrones sutiles pero puede generar falsos positivos
    }

    /// <summary>
    /// Clase para almacenar datos históricos de ubicación con contexto
    /// </summary>
    public class FusionLocationData
    {
        public MauiLocation Location { get; set; }
        public MovementContext Context { get; set; }
        public DateTime Timestamp { get; set; }
    }

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
        private bool _isMoving = false;
        private bool _isInitialized = false;
        private readonly int _historySize = 25; // Increased history size for better analysis

        // Configuración
        private AnomalyAlertLevel _alertLevel = AnomalyAlertLevel.Medium;
        private double _accelerationThreshold = 0.7;
        private double _filteringLevel = 0.05; // Ultra-minimal filtering (5% of normal)
        private double _samplingFrequencyMs = 500; // 250ms (4 samples per second)
        private DateTime _lastSamplingTime = DateTime.MinValue;

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
                _locationFilters = new LocationFilters(_filteringLevel);
                _locationPredictor = new LocationPredictor();
                _telemetryService = new TelemetryService(
                    Android.OS.Environment.ExternalStorageDirectory.AbsolutePath + "/DISMOGTREPORTES");

                // Inicializar listeners de sensores
                InitializeSensors();

                // Ajustar umbrales de anomalías según nivel predeterminado
                UpdateAnomalyThresholds();

                _isInitialized = true;
                Console.WriteLine($"✅ Servicio avanzado de fusión inicializado con filtrado al {_filteringLevel:P0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al inicializar servicio de fusión: {ex.Message}");
            }
        }

        /// <summary>
        /// Configura el nivel de filtrado (0.0 a 1.0, donde 0.0 es sin filtrado)
        /// </summary>
        public void SetFilteringLevel(double level)
        {
            _filteringLevel = Math.Max(0.0, Math.Min(1.0, level));
            if (_locationFilters != null)
            {
                _locationFilters.SetFilteringLevel(_filteringLevel);
            }
            Console.WriteLine($"🔧 Nivel de filtrado establecido a: {_filteringLevel:P0}");
        }

        /// <summary>
        /// Configura la frecuencia de muestreo en milisegundos
        /// </summary>
        public void SetSamplingFrequency(double milliseconds)
        {
            _samplingFrequencyMs = Math.Max(100, milliseconds); // Mínimo 100ms
            Console.WriteLine($"⏱️ Frecuencia de muestreo establecida a: {_samplingFrequencyMs}ms");
        }

        /// <summary>
        /// Configura el nivel de alerta para la detección de anomalías
        /// </summary>
        public void ConfigureAnomalyAlerts(AnomalyAlertLevel level)
        {
            _alertLevel = level;
            Console.WriteLine($"🔔 Nivel de alerta de anomalías configurado como: {level}");

            // Ajustar umbrales según el nivel seleccionado
            UpdateAnomalyThresholds();
        }

        /// <summary>
        /// Actualiza los umbrales para detección de anomalías según el nivel configurado
        /// </summary>
        private void UpdateAnomalyThresholds()
        {
            switch (_alertLevel)
            {
                case AnomalyAlertLevel.Low:
                    // Configuración para alertas mínimas
                    _locationPredictor.SetConsistencyThreshold(3.0);
                    _accelerationThreshold = 1.2;
                    break;

                case AnomalyAlertLevel.Medium:
                    // Configuración equilibrada
                    _locationPredictor.SetConsistencyThreshold(1.5);
                    _accelerationThreshold = 0.7;
                    break;

                case AnomalyAlertLevel.High:
                    // Configuración sensible para máxima detección
                    _locationPredictor.SetConsistencyThreshold(0.8);
                    _accelerationThreshold = 0.5;
                    break;
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
                        var magneticReading = new Vector3(values[0], values[1], values[2]);
                        _magneticReadings.Add(magneticReading);
                        if (_magneticReadings.Count > _historySize)
                            _magneticReadings.RemoveAt(0);
                    };
                }

                if (_gyroscope != null)
                {
                    _gyroscopeListener = new FusionGyroscopeListener();
                    _gyroscopeListener.SensorChanged += (values) =>
                    {
                        var rotationReading = new Vector3(values[0], values[1], values[2]);
                        _rotationReadings.Add(rotationReading);
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
                // Usar tasa de muestreo más rápida para mejor detección de movimiento
                SensorDelay samplingRate = SensorDelay.Game;

                if (_accelerometer != null)
                {
                    _sensorManager.RegisterListener(
                        _accelerometerListener,
                        _accelerometer,
                        samplingRate
                    );
                    Console.WriteLine("🔄 Acelerómetro registrado con muestreo rápido");
                }

                if (_magnetometer != null)
                {
                    _sensorManager.RegisterListener(
                        _magnetometerListener,
                        _magnetometer,
                        samplingRate
                    );
                    Console.WriteLine("🧲 Magnetómetro registrado");
                }

                if (_gyroscope != null)
                {
                    _sensorManager.RegisterListener(
                        _gyroscopeListener,
                        _gyroscope,
                        samplingRate
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
                Vector3 gravity = new Vector3(0, 0, 9.8f);
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

                // Actualizar estado de movimiento usando umbral configurable
                bool previousState = _isMoving;
                _isMoving = avgMagnitude > _accelerationThreshold;

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

            // Control sampling frequency - use whatever value was last set
            var timeSinceLastSample = (DateTime.Now - _lastSamplingTime).TotalMilliseconds;
            if (timeSinceLastSample < _samplingFrequencyMs)
            {
                // Skip processing to maintain sampling rate, but still update context
                if (_locationHistory.Count > 0)
                {
                    _contextDetector.UpdateMovementContext(
                        _accelerationReadings,
                        _rotationReadings,
                        _locationHistory,
                        _isMoving);
                }

                return new LocationResult
                {
                    Location = rawLocation,
                    IsSuspicious = false,
                    IsMoving = _isMoving,
                    MovementContext = _contextDetector.CurrentContext,
                    MovementContextName = _contextDetector.CurrentContext.ToString()
                };
            }

            _lastSamplingTime = DateTime.Now;
            var startTime = DateTime.Now;
            double originalAccuracy = rawLocation.Accuracy ?? 100.0;

            try
            {
                Console.WriteLine($"🧩 Procesando ubicación, precisión original: {originalAccuracy}m");

                // 1. Verificar si la ubicación es consistente con el estado de movimiento
                MauiLocation lastLocation = _locationHistory.Count > 0 ? _locationHistory.Last() : null;
                MovementContext currentContext = _contextDetector.CurrentContext;

                bool isConsistentWithMotion = lastLocation == null ||
                    _locationPredictor.IsConsistentWithMotion(rawLocation, lastLocation, _isMoving, currentContext);

                // 2. Detectar anomalías según el nivel de alerta configurado
                var anomalyDetection = DetectAnomalies(rawLocation, currentContext);
                bool isSuspicious = !isConsistentWithMotion || anomalyDetection.Item1;
                string suspiciousReason = !isConsistentWithMotion ? "Inconsistencia entre movimiento y ubicación; " : "";

                if (anomalyDetection.Item1)
                {
                    suspiciousReason += anomalyDetection.Item2;
                }

                // 3. Aplicar procesamiento mínimo o nulo según el contexto
                MauiLocation processedLocation = rawLocation;

                // Para vehículos, usar la ubicación sin procesar para preservar patrones naturales
                if (currentContext == MovementContext.Vehicle)
                {
                    Console.WriteLine("🚗 Modo vehículo - usando ubicación sin procesar para preservar patrones naturales");
                }
                // Para otros contextos, aplicar filtrado mínimo solo para reducir el ruido
                else if (lastLocation != null)
                {
                    List<MauiLocation> locationsForFiltering = new List<MauiLocation> { rawLocation };
                    if (_locationHistory.Count > 0)
                    {
                        locationsForFiltering.Add(_locationHistory.Last());
                    }
                    processedLocation = _locationFilters.ApplyUltraMinimalFiltering(locationsForFiltering, currentContext);
                }

                // 4. Actualizar el historial de ubicaciones
                UpdateLocationHistory(processedLocation);

                // 5. Actualizar los parámetros de Kalman para próximos cálculos
                var (improvements, worsenings) = _telemetryService.GetConsecutiveCounters();
                _locationFilters.UpdateKalmanParameters(currentContext, improvements, worsenings);

                // 6. Registrar métricas de rendimiento
                double finalAccuracy = processedLocation.Accuracy ?? originalAccuracy;
                double processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                _telemetryService.RecordPerformanceMetric(originalAccuracy, finalAccuracy, processingTime,
                    isSuspicious, suspiciousReason, currentContext);

                Console.WriteLine($"✅ Procesamiento completado en {processingTime:F1}ms. Contexto: {currentContext}");

                // 7. Devolver el resultado con información adicional
                return new LocationResult
                {
                    Location = processedLocation,
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

                return new LocationResult
                {
                    Location = rawLocation,
                    IsSuspicious = false,
                    IsMoving = _isMoving,
                    MovementContext = _contextDetector.CurrentContext,
                    MovementContextName = _contextDetector.CurrentContext.ToString()
                };
            }
        }

        /// <summary>
        /// Detecta anomalías en la ubicación usando el nivel de alerta configurado
        /// </summary>
        private Tuple<bool, string> DetectAnomalies(MauiLocation location, MovementContext context)
        {
            List<string> anomalies = new List<string>();
            bool isSuspicious = false;

            try
            {
                // Basado en un análisis rápido de la ubicación (todos los niveles)
                if (location.Accuracy.HasValue)
                {
                    // Precisión demasiado buena para ser real
                    if (location.Accuracy.Value < 1.0 && context != MovementContext.Stationary)
                    {
                        anomalies.Add("Precisión sospechosamente perfecta");
                        isSuspicious = true;
                    }

                    // Precisión exactamente igual a números redondos (típico en apps falsas)
                    if (Math.Abs(location.Accuracy.Value - Math.Round(location.Accuracy.Value)) < 0.001)
                    {
                        anomalies.Add("Precisión exactamente redondeada");
                        isSuspicious = true;
                    }
                }

                // Coordenadas sospechosamente perfectas
                string latStr = location.Latitude.ToString();
                string lngStr = location.Longitude.ToString();

                if ((latStr.EndsWith("00000") || lngStr.EndsWith("00000")) ||
                    (latStr.EndsWith("11111") || lngStr.EndsWith("11111")) ||
                    (latStr.EndsWith("22222") || lngStr.EndsWith("22222")))
                {
                    anomalies.Add("Coordenadas con patrón sospechoso");
                    isSuspicious = true;
                }

                // Si hay historial, realizar verificaciones basadas en él
                if (_locationHistory.Count >= 3)
                {
                    var recentLocations = _locationHistory.TakeLast(3).ToList();

                    // Verificaciones específicas para nivel High
                    if (_alertLevel == AnomalyAlertLevel.High)
                    {
                        // Detectar cambios sutiles de dirección que no parecen naturales
                        if (HasUnnaturalDirectionChanges(recentLocations, location))
                        {
                            anomalies.Add("Cambios de dirección no naturales");
                            isSuspicious = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al detectar anomalías: {ex.Message}");
            }

            return new Tuple<bool, string>(isSuspicious, string.Join("; ", anomalies));
        }

        /// <summary>
        /// Detecta patrones no naturales en los cambios de dirección
        /// </summary>
        private bool HasUnnaturalDirectionChanges(List<MauiLocation> history, MauiLocation current)
        {
            // Necesitamos 3+ ubicaciones para analizar cambios de dirección
            if (history.Count < 2 || !current.Course.HasValue)
                return false;

            var lastLoc = history.Last();
            var prevLoc = history[history.Count - 2];

            // Si no tenemos información de curso en todas, no podemos verificar
            if (!lastLoc.Course.HasValue || !prevLoc.Course.HasValue)
                return false;

            // Calcular cambios de dirección
            double lastChange = Math.Abs(lastLoc.Course.Value - prevLoc.Course.Value);
            if (lastChange > 180) lastChange = 360 - lastChange;

            double currentChange = Math.Abs(current.Course.Value - lastLoc.Course.Value);
            if (currentChange > 180) currentChange = 360 - currentChange;

            // Si el cambio de dirección actual es exactamente igual al anterior (muy improbable naturalmente)
            // Y es un cambio significativo (> 5 grados)
            if (Math.Abs(lastChange - currentChange) < 0.1 && lastChange > 5)
            {
                return true;
            }

            return false;
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

                Console.WriteLine("🧹 Recursos del servicio liberados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al liberar recursos: {ex.Message}");
            }
        }
    }
}