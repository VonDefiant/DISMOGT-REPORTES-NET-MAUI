using Android.Content;
using Android.Net;
using Android.OS;
using Android.App;
using Android.Hardware;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using Java.Lang;
using AndroidLocation = Android.Locations.Location;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;
using Exception = System.Exception;
using Math = System.Math;
using Android.Runtime;

namespace DISMOGT_REPORTES.Services
{
    public class LocationSecurityService
    {
        private readonly Context _context;
        private AndroidLocation _lastLocation;
        private List<AndroidLocation> _previousLocations = new List<AndroidLocation>();
        private SensorManager _sensorManager;
        private Sensor _accelerometer;
        private AccelerometerListener _accelerometerListener;
        private bool _isMoving = false;
        private System.Collections.Generic.IList<float> _lastAcceleration;

        // Lista extendida de aplicaciones de ubicación simulada conocidas
        private readonly string[] _mockLocationApps = {
            // Lista original
            "com.lexa.fakegps",
            "com.incorporateapps.fakegps",
            "com.fake.gps.location",
            "com.fake.location",
            "com.gps.falsifier",
            "com.rosteam.gpsemulator",
            "com.chelpus.fakegps",
            "com.byterevapps.fakegps",
            "com.blogspot.newapphorizons.fakegps",
            
            // Nuevas aplicaciones detectadas en capturas de pantalla
            "com.blogspot.newapphorizons.fakegps",
            
            // Aplicaciones adicionales conocidas
            "com.theskywall.spoof",
            "com.excelliance.gpsemulator",
            "com.gsmartstudio.fakegps",
            "com.lkr.fakelocation",
            "org.hola.gpslocation",
            "com.fakegps.mock",
            "com.usefullapps.fakegpslocationpro",
            "com.evezzon.fakegps",
            "com.fgps.fake_gps_location",
            "com.pe.fakegps",
            "com.rosteam.gpsemulator",
            "com.theappninjas.gpsjosystemplayer",
            "com.lexa.fakegps.pro"
        };

        // Servicios conocidos de ubicación simulada
        private readonly string[] _mockLocationServices = {
            "com.blogspot.newapphorizons.fakegps.FakeGPSService",
            "com.blogspot.newapphorizons.fakegps.widget.MockWidgetProvider",
            "com.blogspot.newapphorizons.fakegps.widget.WidgetService",
            "com.lexa.fakegps.service.FakeGpsService",
            "com.fakegps.provider.FakeGpsProvider"
        };

        public LocationSecurityService(Context context)
        {
            _context = context;

            // Inicializar sensor de acelerómetro para detección de movimiento
            try
            {
                _sensorManager = (SensorManager)_context.GetSystemService(Context.SensorService);
                _accelerometer = _sensorManager.GetDefaultSensor(SensorType.Accelerometer);

                if (_accelerometer != null)
                {
                    _accelerometerListener = new AccelerometerListener();
                    _accelerometerListener.SensorChanged += (values) => {
                        _lastAcceleration = values;

                        // Detección básica de movimiento basada en magnitud de aceleración
                        float magnitude = (float)Math.Sqrt(
                            _lastAcceleration[0] * _lastAcceleration[0] +
                            _lastAcceleration[1] * _lastAcceleration[1] +
                            _lastAcceleration[2] * _lastAcceleration[2]
                        );

                        // Restar gravedad (9.8 m/s²) y verificar si hay movimiento significativo
                        _isMoving = Math.Abs(magnitude - 9.8f) > 1.5f;
                    };

                    _sensorManager.RegisterListener(
                        _accelerometerListener,
                        _accelerometer,
                        SensorDelay.Normal
                    );

                    Console.WriteLine("✅ Sensor de acelerómetro inicializado para detección de movimiento");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ No se pudo inicializar sensor de acelerómetro: {ex.Message}");
            }
        }

        /// <summary>
        /// Método principal para verificar si una ubicación está siendo simulada.
        /// Implementa una lógica más robusta para evitar falsos positivos.
        /// </summary>
        public bool IsLocationMocked(AndroidLocation location)
        {
            try
            {
                if (location == null) return false;

                Console.WriteLine($"🔍 Verificando si la ubicación está siendo simulada: {location.Latitude}, {location.Longitude}");

                // Recopilar todas las razones de detección para un registro detallado
                List<string> detectionReasons = new List<string>();

                // Contador de "puntos de sospecha" - requiere cierto umbral para considerar una ubicación como simulada
                int suspicionPoints = 0;

                // CRITERIOS DE ALTA CONFIANZA (si alguno de estos es positivo, casi seguro es una ubicación simulada)
                bool highConfidenceCriteriaMet = false;

                // 1. Verificar la marca isFromMockProvider (API estándar de Android)
                if (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr2 && location.IsFromMockProvider)
                {
                    Console.WriteLine("🎭 Ubicación reportada como simulada por isFromMockProvider");
                    detectionReasons.Add("isFromMockProvider = true");
                    suspicionPoints += 4; // Alta confianza en este indicador
                    highConfidenceCriteriaMet = true;
                }

                // 2. Verificar si el proveedor de ubicación simulada está activo en el sistema
                if (IsMockLocationProviderEnabled())
                {
                    Console.WriteLine("⚠️ Proveedor de ubicación simulada activo en el sistema");
                    detectionReasons.Add("Proveedor de ubicación simulada activo");
                    suspicionPoints += 3;
                    highConfidenceCriteriaMet = true;
                }

                // CRITERIOS SECUNDARIOS (requieren combinación para ser considerados confiables)

                // 3. Verificar aplicaciones de ubicación simulada instaladas
                if (IsFakeGpsActive())
                {
                    // Solo porque la app esté instalada no significa que esté activa
                    Console.WriteLine("ℹ️ Aplicación Fake GPS detectada instalada");

                    // Verificar si la app está realmente en ejecución
                    if (IsFakeGpsRunning())
                    {
                        Console.WriteLine("🎭 Aplicación Fake GPS en ejecución");
                        detectionReasons.Add("Aplicación de GPS falso en ejecución");
                        suspicionPoints += 2;
                    }
                    else
                    {
                        suspicionPoints += 1; // Solo instalada pero no en ejecución
                    }
                }

                // 4. Verificar comportamiento sospechoso de ubicación (solo si hay suficiente historial)
                // Solo considerar este criterio si tenemos suficiente historial y no hay criterios de alta confianza
                if (_previousLocations.Count >= 3)
                {
                    bool behaviorSuspicious = IsLocationBehaviorSuspicious(location);
                    if (behaviorSuspicious)
                    {
                        Console.WriteLine("⚠️ Comportamiento de ubicación sospechoso detectado");
                        detectionReasons.Add("Comportamiento de ubicación sospechoso");

                        // Dar menos peso al comportamiento sospechoso cuando el dispositivo está quieto
                        // ya que en dispositivos fijos, la ubicación puede parecer sospechosamente estable
                        bool deviceIsStationary = !IsDeviceMoving();
                        if (deviceIsStationary)
                        {
                            Console.WriteLine("ℹ️ Comportamiento sospechoso, pero el dispositivo está quieto (normal)");
                            suspicionPoints += 1;
                        }
                        else
                        {
                            suspicionPoints += 2;
                        }
                    }
                }

                // 5. Verificar inconsistencia con sensores (si hay ubicación previa)
                if (_lastLocation != null)
                {
                    bool sensorInconsistent = !IsLocationConsistentWithSensors(location);
                    if (sensorInconsistent)
                    {
                        Console.WriteLine("⚠️ Inconsistencia entre movimiento de ubicación y sensores");
                        detectionReasons.Add("Inconsistencia con sensores");

                        // Reducir el peso de este criterio en lugares con mala recepción GPS
                        if (!location.HasAccuracy || location.Accuracy > 20)
                        {
                            Console.WriteLine("ℹ️ Inconsistencia con sensores, pero la precisión GPS es baja (puede ser normal)");
                            suspicionPoints += 1;
                        }
                        else
                        {
                            suspicionPoints += 2;
                        }
                    }
                }

                // 6. Verificar precisión sospechosa
                if (location.HasAccuracy && IsAccuracySuspicious(location))
                {
                    Console.WriteLine($"⚠️ Precisión sospechosamente perfecta: {location.Accuracy} metros");
                    detectionReasons.Add("Precisión sospechosa");
                    suspicionPoints += 2;
                }

                // Almacenar esta ubicación para comparaciones futuras
                _lastLocation = location;
                StoreLocationHistory(location);

                // DECISIÓN FINAL

                // Determinar si la ubicación es simulada basado en el puntaje de sospecha
                // Si se ha cumplido algún criterio de alta confianza, requerimos menos puntos totales
                bool isMocked = highConfidenceCriteriaMet ? suspicionPoints >= 3 : suspicionPoints >= 5;

                if (isMocked)
                {
                    Console.WriteLine($"🚨 Ubicación simulada detectada (Puntuación: {suspicionPoints}/10) - Razones: {string.Join(", ", detectionReasons)}");
                }
                else
                {
                    if (suspicionPoints > 0)
                    {
                        Console.WriteLine($"ℹ️ Algunos indicadores de sospecha, pero insuficientes (Puntuación: {suspicionPoints}/10)");
                    }
                    else
                    {
                        Console.WriteLine("✅ No se detectaron señales de ubicación simulada");
                    }
                }

                return isMocked;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al verificar simulación de ubicación: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifica si el dispositivo está en movimiento según los sensores
        /// </summary>
        private bool IsDeviceMoving()
        {
            return _isMoving; // Esta variable ya se actualiza en el listener del acelerómetro
        }


        /// <summary>
        /// Verificar si hay aplicaciones de ubicación simulada instaladas
        /// </summary>
        public bool IsFakeGpsActive()
        {
            try
            {
                // Opcional: descomentar para depuración
                // PrintInstalledApps();

                var packageManager = _context.PackageManager;
                foreach (var packageName in _mockLocationApps)
                {
                    try
                    {
                        packageManager.GetPackageInfo(packageName, 0);
                        Console.WriteLine($"🎭 Aplicación Fake GPS detectada: {packageName}");
                        return true;
                    }
                    catch
                    {
                        // La aplicación no está instalada, continuar verificando las demás
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error verificando Fake GPS: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Imprimir todas las aplicaciones instaladas (útil para depuración)
        /// </summary>
        private void PrintInstalledApps()
        {
            try
            {
                var packageManager = _context.PackageManager;
                var installedApps = packageManager.GetInstalledApplications(Android.Content.PM.PackageInfoFlags.MetaData);

                Console.WriteLine("📌 Lista de aplicaciones instaladas:");
                foreach (var app in installedApps)
                {
                    Console.WriteLine($"📦 Paquete: {app.PackageName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al listar aplicaciones: {ex.Message}");
            }
        }
        /// <summary>
        /// Verifica si hay algún proveedor de ubicación simulada realmente activo
        /// </summary>
        private bool IsAnyMockProviderActive()
        {
            try
            {
                var locationManager = (Android.Locations.LocationManager)_context.GetSystemService(Context.LocationService);
                var providers = locationManager.GetProviders(true);

                // El proveedor "mock" se usa para ubicaciones simuladas
                bool mockProviderActive = providers.Contains("mock");

                // También verificar la última ubicación conocida del proveedor GPS y de red
                var lastGps = locationManager.GetLastKnownLocation(Android.Locations.LocationManager.GpsProvider);
                var lastNetwork = locationManager.GetLastKnownLocation(Android.Locations.LocationManager.NetworkProvider);

                bool lastGpsMocked = (lastGps != null) && (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr2) && lastGps.IsFromMockProvider;
                bool lastNetworkMocked = (lastNetwork != null) && (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr2) && lastNetwork.IsFromMockProvider;

                bool anyProviderMocked = mockProviderActive || lastGpsMocked || lastNetworkMocked;

                if (anyProviderMocked)
                {
                    Console.WriteLine("🎭 Proveedor de ubicación simulada ACTIVO detectado");
                }

                return anyProviderMocked;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error verificando proveedores activos: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verificar si el proveedor de ubicación simulada está habilitado en las configuraciones
        /// </summary>
        private bool IsMockLocationProviderEnabled()
        {
            try
            {
                // Para Android < 6.0
                if (Build.VERSION.SdkInt < BuildVersionCodes.M)
                {
                    // Verificar configuración AllowMockLocation
                    string mockLocationEnabled = Android.Provider.Settings.Secure.GetString(
                        _context.ContentResolver,
                        Android.Provider.Settings.Secure.AllowMockLocation);

                    bool enabled = mockLocationEnabled == "1";
                    if (enabled)
                    {
                        Console.WriteLine("⚠️ Mock location habilitado en configuraciones de desarrollador");

                        // Verificación adicional: comprobar si alguna aplicación está activamente proporcionando ubicaciones simuladas
                        var locationManager = (Android.Locations.LocationManager)_context.GetSystemService(Context.LocationService);
                        var providers = locationManager.GetProviders(true);
                        bool mockProviderActive = providers.Contains("mock");

                        if (!mockProviderActive)
                        {
                            Console.WriteLine("✅ Opciones de desarrollador habilitadas, pero ningún proveedor mock activo");
                            return false; // No hay proveedor de ubicación simulada ACTIVO, solo habilitado
                        }
                    }
                    return enabled;
                }
                else
                {
                    // Para Android 6.0+, verificar si hay alguna app seleccionada como mock location
                    string mockLocationApp = Android.Provider.Settings.Secure.GetString(
                        _context.ContentResolver,
                        "mock_location");

                    bool hasApp = !string.IsNullOrEmpty(mockLocationApp);
                    if (hasApp)
                    {
                        Console.WriteLine($"⚠️ Aplicación configurada como proveedor de ubicación simulada: {mockLocationApp}");

                        // Verificación adicional: Si hay una app seleccionada, verificar si está activamente simulando ubicación
                        if (!IsAnyMockProviderActive())
                        {
                            Console.WriteLine("✅ App de ubicación simulada configurada pero no activa");
                            return false;
                        }
                    }
                    return hasApp && IsAnyMockProviderActive();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error verificando proveedor de ubicación simulada: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verificar si hay servicios de ubicación simulada en ejecución
        /// </summary>
        private bool IsFakeGpsRunning()
        {
            try
            {
                var activityManager = (ActivityManager)_context.GetSystemService(Context.ActivityService);

                // Verificar procesos en ejecución
                var runningApps = activityManager.RunningAppProcesses;
                foreach (var process in runningApps)
                {
                    if (_mockLocationApps.Contains(process.ProcessName))
                    {
                        Console.WriteLine($"⚠️ Aplicación Fake GPS detectada en ejecución: {process.ProcessName}");
                        return true;
                    }
                }

                // Verificar servicios en ejecución
                var runningServices = activityManager.GetRunningServices(100);
                foreach (var service in runningServices)
                {
                    string serviceName = service.Service.ClassName;

                    // Verificar contra nombres de servicios conocidos
                    if (_mockLocationServices.Contains(serviceName))
                    {
                        Console.WriteLine($"⚠️ Servicio de ubicación simulada en ejecución: {serviceName}");
                        return true;
                    }

                    // Verificar si el servicio pertenece a una app de mock location conocida
                    foreach (var mockApp in _mockLocationApps)
                    {
                        if (serviceName.StartsWith(mockApp))
                        {
                            Console.WriteLine($"⚠️ Posible servicio de ubicación simulada: {serviceName}");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al verificar apps en ejecución: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Guardar historial de ubicaciones para análisis
        /// </summary>
        private void StoreLocationHistory(AndroidLocation location)
        {
            try
            {
                // Mantener un historial limitado (últimas 5 ubicaciones)
                _previousLocations.Add(location);
                if (_previousLocations.Count > 5)
                {
                    _previousLocations.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al almacenar historial de ubicaciones: {ex.Message}");
            }
        }

        /// <summary>
        /// Verificar comportamiento sospechoso en la ubicación
        /// </summary>
        private bool IsLocationBehaviorSuspicious(AndroidLocation location)
        {
            try
            {
                // Si no hay ubicación previa para comparar, no podemos determinar comportamiento
                if (_lastLocation == null)
                {
                    _lastLocation = location;
                    return false;
                }

                // 1. Verificar velocidad imposible
                bool speedSuspicious = IsSpeedSuspicious(location);

                // 2. Verificar valores de precisión sospechosos
                bool accuracySuspicious = IsAccuracySuspicious(location);

                // 3. Verificar valores de coordenadas sospechosamente perfectos
                bool coordinatesSuspicious = AreCoordinatesSuspicious(location);

                // 4. Verificar patrones de movimiento no naturales
                bool movementPatternSuspicious = false;
                if (_previousLocations.Count >= 3)
                {
                    movementPatternSuspicious = IsMovementPatternSuspicious();
                }

                return speedSuspicious || accuracySuspicious || coordinatesSuspicious || movementPatternSuspicious;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al verificar comportamiento de ubicación: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verificar si la velocidad entre ubicaciones es sospechosa
        /// </summary>
        private bool IsSpeedSuspicious(AndroidLocation location)
        {
            if (_lastLocation == null) return false;

            float distance = _lastLocation.DistanceTo(location);
            long timeDiff = (location.Time - _lastLocation.Time) / 1000; // en segundos

            if (timeDiff <= 0)
            {
                // El tiempo no debería retroceder
                Console.WriteLine("⚠️ Inconsistencia temporal detectada en ubicación");
                return true;
            }

            if (timeDiff > 0)
            {
                float speed = distance / timeDiff; // metros por segundo

                // Verificar velocidades imposibles (más de 1000 km/h)
                if (speed > 278) // ~1000 km/h en m/s
                {
                    Console.WriteLine($"⚠️ Velocidad imposible detectada: {speed} m/s ({speed * 3.6} km/h)");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Verificar si la precisión de la ubicación es sospechosa
        /// </summary>
        private bool IsAccuracySuspicious(AndroidLocation location)
        {
            // Muchas aplicaciones de GPS falso tienen precisión irreal o perfecta
            if (location.HasAccuracy)
            {
                float accuracy = location.Accuracy;

                // Precisión demasiado perfecta (menos de 1 metro)
                if (accuracy < 1.0f)
                {
                    Console.WriteLine($"⚠️ Precisión sospechosamente perfecta: {accuracy} metros");
                    return true;
                }

                // Precisión exactamente igual a un valor redondo
                if (Math.Abs(accuracy - Math.Round(accuracy)) < 0.001f)
                {
                    Console.WriteLine($"⚠️ Precisión sospechosamente redondeada: {accuracy} metros");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Verificar si las coordenadas son sospechosamente perfectas
        /// </summary>
        private bool AreCoordinatesSuspicious(AndroidLocation location)
        {
            double lat = location.Latitude;
            double lng = location.Longitude;

            // Verificar coordenadas con demasiados decimales exactamente iguales a cero
            string latStr = lat.ToString();
            string lngStr = lng.ToString();

            // Verificar si tiene muchos ceros al final (común en ubicaciones falsas)
            if ((latStr.EndsWith("0000") || lngStr.EndsWith("0000")) &&
                !latStr.EndsWith("00000") && !lngStr.EndsWith("00000"))
            {
                Console.WriteLine("⚠️ Coordenadas con múltiples ceros al final");
                return true;
            }

            // Verificar si las coordenadas son exactamente valores redondeados
            if ((Math.Abs(lat - Math.Round(lat, 4)) < 0.00001) &&
                (Math.Abs(lng - Math.Round(lng, 4)) < 0.00001))
            {
                Console.WriteLine("⚠️ Coordenadas sospechosamente redondeadas");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Verificar si el patrón de movimiento es sospechoso (demasiado lineal o regular)
        /// </summary>
        private bool IsMovementPatternSuspicious()
        {
            try
            {
                if (_previousLocations.Count < 3) return false;

                // Verificar si el movimiento es perfectamente lineal (común en ubicaciones falsas)
                var last3Points = _previousLocations.TakeLast(3).ToList();

                // Calcular si 3 puntos están en línea perfecta
                if (ArePointsPerfectlyLinear(last3Points))
                {
                    Console.WriteLine("⚠️ Patrón de movimiento sospechosamente lineal");
                    return true;
                }

                // Verificar si las distancias entre puntos consecutivos son exactamente iguales
                if (AreDistancesTooRegular(last3Points))
                {
                    Console.WriteLine("⚠️ Distancias de movimiento sospechosamente regulares");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al verificar patrón de movimiento: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verificar si tres puntos están perfectamente alineados
        /// </summary>
        private bool ArePointsPerfectlyLinear(List<AndroidLocation> points)
        {
            if (points.Count < 3) return false;

            // Calcular pendiente entre primer y segundo punto
            double slope1 = 0;
            if (points[1].Longitude - points[0].Longitude != 0)
            {
                slope1 = (points[1].Latitude - points[0].Latitude) /
                         (points[1].Longitude - points[0].Longitude);
            }

            // Calcular pendiente entre segundo y tercer punto
            double slope2 = 0;
            if (points[2].Longitude - points[1].Longitude != 0)
            {
                slope2 = (points[2].Latitude - points[1].Latitude) /
                         (points[2].Longitude - points[1].Longitude);
            }

            // Comparar pendientes con una pequeña tolerancia
            return Math.Abs(slope1 - slope2) < 0.0001;
        }

        /// <summary>
        /// Verificar si las distancias entre puntos consecutivos son demasiado regulares
        /// </summary>
        private bool AreDistancesTooRegular(List<AndroidLocation> points)
        {
            if (points.Count < 3) return false;

            float distance1 = points[0].DistanceTo(points[1]);
            float distance2 = points[1].DistanceTo(points[2]);

            // Comparar distancias con un margen de 1% de diferencia
            float ratio = Math.Max(distance1, distance2) / Math.Min(distance1, distance2);

            return ratio < 1.01f && distance1 > 10; // Solo es sospechoso si están muy cerca y la distancia es significativa
        }

        /// <summary>
        /// Verificar si la ubicación es consistente con las lecturas del sensor,
        /// con lógica mejorada para evitar falsos positivos
        /// </summary>
        private bool IsLocationConsistentWithSensors(AndroidLocation location)
        {
            try
            {
                // Si no tenemos lecturas de acelerómetro o ubicación anterior, no podemos verificar
                if (_lastLocation == null || _accelerometer == null || _lastAcceleration == null) return true;

                // Calcular distancia entre ubicaciones
                float distance = _lastLocation.DistanceTo(location);

                // Calcular tiempo entre ubicaciones
                long timeDiffMs = location.Time - _lastLocation.Time;

                // Si el intervalo de tiempo es muy corto, no podemos hacer una verificación confiable
                if (timeDiffMs < 1000) return true; // Menos de 1 segundo entre ubicaciones

                // Aumentamos a 15 metros para reducir falsos positivos en ubicaciones con precisión media
                bool locationShowsMovement = distance > 15;

                // Si los sensores indican que no nos estamos moviendo pero la ubicación cambió significativamente
                if (!_isMoving && locationShowsMovement)
                {
                    // Si la precisión es baja, las fluctuaciones son normales incluso sin movimiento
                    if (location.HasAccuracy && location.Accuracy > 20)
                    {
                        Console.WriteLine($"ℹ️ Movimiento detectado sin aceleración, pero precisión baja ({location.Accuracy}m)");
                        return true; 
                    }

                    Console.WriteLine("⚠️ Inconsistencia: Los sensores indican quietud pero la ubicación muestra movimiento");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al verificar consistencia con sensores: {ex.Message}");
                return true; // En caso de error, no queremos un falso positivo
            }
        }

        /// <summary>
        /// Verificar si VPN está activo
        /// </summary>
        public bool IsVpnActive()
        {
            try
            {
                // Método 1: Verificar usando NetworkInterfaces (funciona en la mayoría de dispositivos)
                bool vpnDetectedByInterfaces = IsVpnActiveByNetworkInterfaces();

                // Método 2: Verificar usando ConnectivityManager (API de Android)
                bool vpnDetectedByConnectivityManager = IsVpnActiveByConnectivityManager();

                bool isVpnActive = vpnDetectedByInterfaces || vpnDetectedByConnectivityManager;

                if (isVpnActive)
                {
                    Console.WriteLine("⚠️ VPN activo detectado");
                }

                return isVpnActive;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al verificar VPN: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verificar VPN usando NetworkInterfaces
        /// </summary>
        private bool IsVpnActiveByNetworkInterfaces()
        {
            try
            {
                string[] vpnInterfaces = { "tun0", "ppp0", "ipsec", "pptp", "l2tp", "wireguard", "nordlynx" };
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (var networkInterface in networkInterfaces)
                {
                    foreach (var vpnInterface in vpnInterfaces)
                    {
                        if (networkInterface.Name.IndexOf(vpnInterface, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Console.WriteLine($"⚠️ Interfaz VPN detectada: {networkInterface.Name}");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en verificación de interfaces: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verificar VPN usando ConnectivityManager de Android
        /// </summary>
        private bool IsVpnActiveByConnectivityManager()
        {
            try
            {
                ConnectivityManager connectivityManager = (ConnectivityManager)_context.GetSystemService(Context.ConnectivityService);

                if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    Network[] networks = connectivityManager.GetAllNetworks();
                    foreach (Network network in networks)
                    {
                        NetworkCapabilities capabilities = connectivityManager.GetNetworkCapabilities(network);
                        if (capabilities != null && capabilities.HasTransport(TransportType.Vpn))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ConnectivityManager: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Convertir una Location de MAUI a una Location de Android para los métodos de detección
        /// </summary>
        public AndroidLocation ConvertMauiLocationToAndroid(MauiLocation mauiLocation)
        {
            if (mauiLocation == null) return null;

            var androidLocation = new AndroidLocation("gps");
            androidLocation.Latitude = mauiLocation.Latitude;
            androidLocation.Longitude = mauiLocation.Longitude;

            // Establecer la hora actual si no tiene
            androidLocation.Time = Java.Lang.JavaSystem.CurrentTimeMillis();

            // Copiar precisión si está disponible
            if (mauiLocation.Accuracy.HasValue)
            {
                androidLocation.Accuracy = (float)mauiLocation.Accuracy.Value;
            }

            // Copiar altitud si está disponible
            if (mauiLocation.Altitude.HasValue)
            {
                androidLocation.Altitude = mauiLocation.Altitude.Value;
            }

            return androidLocation;
        }
    }

    /// <summary>
    /// Clase auxiliar para gestionar eventos del acelerómetro
    /// </summary>
    public class AccelerometerListener : Java.Lang.Object, ISensorEventListener
    {
        // Evento para notificar cambios en el sensor
        public event Action<System.Collections.Generic.IList<float>> SensorChanged;

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {

        }

        public void OnSensorChanged(SensorEvent e)
        {
            if (e.Sensor.Type == SensorType.Accelerometer)
            {
                SensorChanged?.Invoke(e.Values);
            }
        }
    }
}