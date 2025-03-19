using Android.Content;
using Android.Hardware;
using Android.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using DISMOGT_REPORTES.Models;
using DISMOGT_REPORTES.Services.LocationFusion;

// Definir alias para evitar ambigüedad entre tipos Location
using AndroidLocation = Android.Locations.Location;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace DISMOGT_REPORTES.Services
{
    /// <summary>
    /// Servicio mejorado para la fusión y filtrado de datos de ubicación
    /// para seguir mejor los patrones naturales de movimiento y las redes de carreteras
    /// </summary>
    public class LocationFusionService : IDisposable
    {
        private readonly Context _context;
        private readonly LocationManager _locationManager;
        private readonly SensorManager _sensorManager;

        // Escuchadores de sensores
        private Sensor _accelerometer;
        private Sensor _magnetometer;
        private Sensor _gyroscope;
        private FusionAccelerometerListener _accelerometerListener;
        private FusionMagnetometerListener _magnetometerListener;
        private FusionGyroscopeListener _gyroscopeListener;

        // Almacenamiento de datos
        private readonly List<Vector3> _accelerationReadings = new List<Vector3>();
        private readonly List<Vector3> _rotationReadings = new List<Vector3>();
        private readonly List<MauiLocation> _locationHistory = new List<MauiLocation>();
        private bool _isMoving = false;

        // Servicios componentes
        private readonly ContextDetector _contextDetector;
        private readonly LocationFilters _locationFilters;
        private readonly LocationPredictor _locationPredictor;
        private readonly TelemetryService _telemetryService;

        // Configuración
        private MovementContext _currentContext = MovementContext.Unknown;
        private bool _isInitialized = false;
        private int _maxHistorySize = 15;
        private bool _useKalmanFilter = true;
        private bool _useContextCorrections = true;
        private DateTime _lastLocationTime = DateTime.MinValue;

        /// <summary>
        /// Constructor para el servicio mejorado de fusión de ubicación
        /// </summary>
        public LocationFusionService(Context context)
        {
            _context = context;

            try
            {
                // Inicializar servicios del sistema
                _locationManager = (LocationManager)_context.GetSystemService(Context.LocationService);
                _sensorManager = (SensorManager)_context.GetSystemService(Context.SensorService);

                // Inicializar servicios componentes
                _contextDetector = new ContextDetector(context);
                _locationFilters = new LocationFilters();
                _locationPredictor = new LocationPredictor();
                _telemetryService = new TelemetryService(_context.CacheDir.AbsolutePath);

                // Inicializar sensores
                InitializeSensors();

                _isInitialized = true;
                Console.WriteLine("✅ LocationFusionService inicializado correctamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al inicializar LocationFusionService: {ex.Message}");
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Inicializar los sensores necesarios para la detección de movimiento
        /// </summary>
        private void InitializeSensors()
        {
            try
            {
                // Inicializar acelerómetro
                _accelerometer = _sensorManager.GetDefaultSensor(SensorType.Accelerometer);
                if (_accelerometer != null)
                {
                    _accelerometerListener = new FusionAccelerometerListener();
                    _accelerometerListener.SensorChanged += (values) =>
                    {
                        var vector = new Vector3(values[0], values[1], values[2]);
                        _accelerationReadings.Add(vector);

                        // Limitar el tamaño del historial
                        if (_accelerationReadings.Count > 20)
                            _accelerationReadings.RemoveAt(0);

                        // Detección básica de movimiento
                        Vector3 gravity = new Vector3(0, 0, 9.8f);
                        float magnitude = (vector - gravity).Length();
                        _isMoving = magnitude > 1.2f;
                    };

                    _sensorManager.RegisterListener(
                        _accelerometerListener,
                        _accelerometer,
                        SensorDelay.Normal
                    );

                    Console.WriteLine("✅ Acelerómetro inicializado");
                }

                // Inicializar magnetómetro
                _magnetometer = _sensorManager.GetDefaultSensor(SensorType.MagneticField);
                if (_magnetometer != null)
                {
                    _magnetometerListener = new FusionMagnetometerListener();
                    _magnetometerListener.SensorChanged += (values) =>
                    {
                        // Procesar datos del magnetómetro si es necesario
                    };

                    _sensorManager.RegisterListener(
                        _magnetometerListener,
                        _magnetometer,
                        SensorDelay.Normal
                    );

                    Console.WriteLine("✅ Magnetómetro inicializado");
                }

                // Inicializar giroscopio
                _gyroscope = _sensorManager.GetDefaultSensor(SensorType.Gyroscope);
                if (_gyroscope != null)
                {
                    _gyroscopeListener = new FusionGyroscopeListener();
                    _gyroscopeListener.SensorChanged += (values) =>
                    {
                        var vector = new Vector3(values[0], values[1], values[2]);
                        _rotationReadings.Add(vector);

                        // Limitar el tamaño del historial
                        if (_rotationReadings.Count > 20)
                            _rotationReadings.RemoveAt(0);
                    };

                    _sensorManager.RegisterListener(
                        _gyroscopeListener,
                        _gyroscope,
                        SensorDelay.Normal
                    );

                    Console.WriteLine("✅ Giroscopio inicializado");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al inicializar sensores: {ex.Message}");
            }
        }

        /// <summary>
        /// Método principal para obtener una ubicación fusionada basada en datos GPS sin procesar
        /// </summary>
        public async Task<LocationResult> GetFusedLocationAsync(MauiLocation rawLocation)
        {
            if (!_isInitialized || rawLocation == null)
            {
                // Si no está inicializado, devolver la ubicación sin procesar
                return new LocationResult
                {
                    Location = rawLocation,
                    IsSuspicious = false,
                    SuspiciousReason = ""
                };
            }

            try
            {
                var startTime = DateTime.Now;
                double originalAccuracy = rawLocation.Accuracy ?? 25.0;
                bool isSuspicious = false;
                string suspiciousReason = "";

                // Almacenar ubicación sin procesar en el historial
                _locationHistory.Add(rawLocation);
                if (_locationHistory.Count > _maxHistorySize)
                    _locationHistory.RemoveAt(0);

                // Actualizar contexto de movimiento
                _currentContext = _contextDetector.UpdateMovementContext(
                    _accelerationReadings,
                    _rotationReadings,
                    _locationHistory,
                    _isMoving
                );

                // CAMBIO CLAVE: Evitar la fusión si la precisión ya es buena (< 10m) y estamos en un vehículo
                // Esto ayuda a evitar el "enderezamiento" del movimiento a lo largo de las carreteras
                if (rawLocation.Accuracy.HasValue &&
                    rawLocation.Accuracy.Value < 10.0 &&
                    _currentContext == MovementContext.Vehicle)
                {
                    Console.WriteLine("✅ Ubicación precisa en vehículo, omitiendo fusión para preservar patrón natural");

                    var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                    _telemetryService.RecordPerformanceMetric(
                        originalAccuracy,
                        rawLocation.Accuracy.Value,
                        processingTime,
                        false,
                        "",
                        _currentContext
                    );

                    return new LocationResult
                    {
                        Location = rawLocation,
                        IsSuspicious = false,
                        SuspiciousReason = ""
                    };
                }

                // Comprobar comportamiento sospechoso que indique ubicación falsa
                if (_locationHistory.Count >= 2)
                {
                    bool isConsistent = _locationPredictor.IsConsistentWithMotion(
                        rawLocation,
                        _locationHistory[_locationHistory.Count - 2],
                        _isMoving,
                        _currentContext
                    );

                    if (!isConsistent)
                    {
                        Console.WriteLine("⚠️ Posible ubicación simulada: inconsistencia de movimiento");
                        isSuspicious = true;
                        suspiciousReason = "Inconsistencia entre movimiento físico y GPS";
                    }
                }

                // Obtener contadores de telemetría para ajustar parámetros de filtrado
                var (improvements, worsenings) = _telemetryService.GetConsecutiveCounters();

                // Preparar ubicación filtrada
                MauiLocation filteredLocation = rawLocation;

                // Ajustar parámetros de filtro basado en contexto y rendimiento pasado
                if (_useKalmanFilter)
                {
                    _locationFilters.UpdateKalmanParameters(_currentContext, improvements, worsenings);
                }

                // CAMBIO CLAVE: Solo aplicar filtros si se cumplen ciertas condiciones
                bool shouldApplyFilters = true;

                // Evitar filtrado cuando las ubicaciones sucesivas están muy separadas en el tiempo (probablemente el dispositivo estaba en reposo)
                if (_lastLocationTime != DateTime.MinValue)
                {
                    TimeSpan timeSinceLastLocation = rawLocation.Timestamp - _lastLocationTime;
                    if (timeSinceLastLocation.TotalMinutes > 5)
                    {
                        shouldApplyFilters = false;
                        Console.WriteLine("ℹ️ Brecha temporal larga, omitiendo filtrado");
                    }
                }

                // Evitar filtrar patrones de movimiento en línea recta que pueden ser legítimos
                if (_locationHistory.Count >= 3 && _currentContext == MovementContext.Vehicle)
                {
                    bool isNaturalStraightPath = IsNaturalStraightMovement();
                    if (isNaturalStraightPath)
                    {
                        shouldApplyFilters = false;
                        Console.WriteLine("ℹ️ Movimiento recto natural detectado, reduciendo filtrado");
                    }
                }

                // Aplicar filtros si es apropiado
                if (shouldApplyFilters)
                {
                    // Si tenemos múltiples ubicaciones, aplicar filtros
                    List<MauiLocation> locationsToFilter = new List<MauiLocation>();
                    if (_locationHistory.Count > 1)
                    {
                        // Usar las últimas ubicaciones para filtrado
                        int samplesToUse = Math.Min(3, _locationHistory.Count);
                        locationsToFilter = _locationHistory.Skip(_locationHistory.Count - samplesToUse).ToList();
                    }
                    else
                    {
                        locationsToFilter.Add(rawLocation);
                    }

                    // Aplicar filtro Kalman y fusión ponderada
                    filteredLocation = _locationFilters.ApplyFilters(locationsToFilter, _currentContext);

                    // Aplicar correcciones específicas de contexto si están habilitadas
                    if (_useContextCorrections && _locationHistory.Count >= 3)
                    {
                        filteredLocation = _locationFilters.ApplyContextSpecificCorrections(
                            filteredLocation,
                            _locationHistory,
                            _currentContext
                        );
                    }
                }

                // Registrar tiempo de procesamiento y métricas de rendimiento
                var endTime = DateTime.Now;
                double processingTimeMs = (endTime - startTime).TotalMilliseconds;

                // Calcular precisión final (puede ser estimada después del filtrado)
                double finalAccuracy = filteredLocation.Accuracy ?? originalAccuracy;

                // Registrar métricas si la información de precisión está disponible
                if (rawLocation.Accuracy.HasValue)
                {
                    _telemetryService.RecordPerformanceMetric(
                        originalAccuracy,
                        finalAccuracy,
                        processingTimeMs,
                        isSuspicious,
                        suspiciousReason,
                        _currentContext
                    );
                }

                // Actualizar tiempo de última ubicación
                _lastLocationTime = rawLocation.Timestamp.DateTime;

                return new LocationResult
                {
                    Location = filteredLocation,
                    IsSuspicious = isSuspicious,
                    SuspiciousReason = suspiciousReason
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en fusión de ubicación: {ex.Message}");

                // En caso de error, devolver la ubicación original sin modificar
                return new LocationResult
                {
                    Location = rawLocation,
                    IsSuspicious = false,
                    SuspiciousReason = ""
                };
            }
        }

        /// <summary>
        /// Determinar si el movimiento actual parece seguir una trayectoria recta natural
        /// como una autopista o carretera recta, para evitar el filtrado excesivo
        /// </summary>
        private bool IsNaturalStraightMovement()
        {
            try
            {
                if (_locationHistory.Count < 3)
                    return false;

                // Obtener las últimas 3 ubicaciones
                var last3 = _locationHistory.Skip(_locationHistory.Count - 3).ToList();

                // Comprobar si están en una línea relativamente recta (patrón natural de carretera)
                bool isLinear = ArePointsLinear(last3);

                // También comprobar si tenemos velocidad constante (movimiento natural de vehículo)
                bool hasConsistentSpeed = HasConsistentSpeed(last3);

                // Devolver true si se cumplen ambas condiciones
                return isLinear && hasConsistentSpeed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al analizar movimiento recto: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Comprobar si los puntos parecen ser aproximadamente lineales, lo que podría indicar una carretera
        /// </summary>
        private bool ArePointsLinear(List<MauiLocation> points)
        {
            if (points.Count < 3)
                return false;

            // Calcular los rumbos entre puntos consecutivos
            double bearing1 = LocationUtils.CalculateBearing(
                points[0].Latitude, points[0].Longitude,
                points[1].Latitude, points[1].Longitude
            );

            double bearing2 = LocationUtils.CalculateBearing(
                points[1].Latitude, points[1].Longitude,
                points[2].Latitude, points[2].Longitude
            );

            // Calcular diferencia en rumbos (normalizado a 0-180 grados)
            double bearingDiff = Math.Abs(bearing1 - bearing2);
            if (bearingDiff > 180)
                bearingDiff = 360 - bearingDiff;

            // Si la diferencia de rumbo es pequeña, los puntos son aproximadamente lineales
            // Permitir hasta 30 grados para curvas naturales de carreteras
            return bearingDiff < 30;
        }

        /// <summary>
        /// Comprobar si la velocidad es consistente entre puntos, indicando movimiento natural
        /// </summary>
        private bool HasConsistentSpeed(List<MauiLocation> points)
        {
            if (points.Count < 3)
                return false;

            // Calcular velocidades entre puntos consecutivos
            double speed1 = LocationUtils.CalculateSpeed(points[0], points[1]);
            double speed2 = LocationUtils.CalculateSpeed(points[1], points[2]);

            // Comprobar cambios grandes de velocidad, que podrían ser sospechosos
            if (speed1 > 0 && speed2 > 0)
            {
                double speedRatio = Math.Max(speed1, speed2) / Math.Min(speed1, speed2);

                // Menos del 50% de cambio en velocidad se considera consistente
                return speedRatio < 1.5;
            }

            return false;
        }

        /// <summary>
        /// Obtiene estadísticas de rendimiento del servicio de fusión de ubicación
        /// </summary>
        public Dictionary<string, double> GetPerformanceStats()
        {
            return _telemetryService?.GetPerformanceStats() ?? new Dictionary<string, double>();
        }

        /// <summary>
        /// Liberar recursos cuando se desecha el servicio
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

                Console.WriteLine("♻️ LocationFusionService recursos liberados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al liberar recursos: {ex.Message}");
            }
        }
    }
}