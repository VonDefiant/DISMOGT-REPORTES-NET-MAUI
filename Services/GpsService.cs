using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Shiny.Locations;
using Microsoft.Maui.Networking;
using Newtonsoft.Json;
using DISMOGT_REPORTES.Models;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using DISMOGT_REPORTES.Services;
using DISMOGT_REPORTES; // Para acceder a ResMxFamReport
using System.IO; // Para manejo de archivos
using SQLite; // Para conexión a SQLite
using Location = Microsoft.Maui.Devices.Sensors.Location; // Usar un alias explícito
using AndroidLocation = Android.Locations.Location;

namespace DISMO_REPORTES.Services
{
    public class GpsService : IGpsDelegate
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly IGpsManager _gpsManager;
        private LocationSecurityService _securityService;
        private LocationFusionService _fusionService;
        public GpsService(IGpsManager gpsManager)
        {
            _gpsManager = gpsManager;

            // Inicializar el servicio de seguridad cuando sea posible
            try
            {
                var context = Android.App.Application.Context;
                _securityService = new LocationSecurityService(context);

                // Inicializar el nuevo servicio de fusión
                _fusionService = new LocationFusionService(context);

                Console.WriteLine("✅ Servicios de seguridad y fusión de ubicación inicializados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al inicializar servicios: {ex.Message}");
            }
        }

        public async Task OnReading(GpsReading reading)
        {
            try
            {
                var location = new Location
                {
                    Latitude = reading.Position.Latitude,
                    Longitude = reading.Position.Longitude,
                    Accuracy = null,
                    Altitude = reading.Altitude,
                    Course = reading.Heading,
                    Speed = reading.Speed,
                    Timestamp = reading.Timestamp.DateTime
                };

                Console.WriteLine($"📍 GPS Reading: Lat={location.Latitude}, Lng={location.Longitude}");

                // Verificar si existe una simulación o VPN
                bool isSuspicious = false;
                string suspiciousReason = "";

                // Convertir Location de Maui a Android.Locations.Location
                AndroidLocation androidLocation = null;
                try
                {
                    androidLocation = new AndroidLocation("gps")
                    {
                        Latitude = location.Latitude,
                        Longitude = location.Longitude
                    };

                    if (location.Accuracy.HasValue)
                        androidLocation.Accuracy = (float)location.Accuracy.Value;

                    if (location.Altitude.HasValue)
                        androidLocation.Altitude = location.Altitude.Value;

                    if (location.Speed.HasValue)
                        androidLocation.Speed = (float)location.Speed.Value;

                    if (location.Course.HasValue)
                        androidLocation.Bearing = (float)location.Course.Value;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error al convertir ubicación: {ex.Message}");
                }

                // Aplicar fusión de datos GPS si está disponible
                if (_fusionService != null)
                {
                    try
                    {
                        var fusedResult = await _fusionService.GetFusedLocationAsync(location);
                        if (fusedResult != null && fusedResult.Location != null)
                        {
                            Console.WriteLine($"🔄 Fusión de datos aplicada");
                            location = fusedResult.Location;
                            // Obtener y usar información de movimiento
                            bool isMoving = fusedResult.IsMoving;
                            string contextName = fusedResult.MovementContextName;

                            Console.WriteLine($"🧠 Estado actual: {(isMoving ? "En movimiento" : "Estacionario")}");
                            Console.WriteLine($"🧠 Contexto: {contextName}");

                            // Si la fusión detectó sospecha, actualizamos las banderas
                            if (fusedResult.IsSuspicious)
                            {
                                isSuspicious = true;
                                suspiciousReason = fusedResult.SuspiciousReason ?? "";
                                Console.WriteLine($"⚠️ La fusión de datos detectó comportamiento sospechoso: {suspiciousReason}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error en la fusión de datos GPS: {ex.Message}");
                    }
                }

                if (_securityService != null && androidLocation != null)
                {
                    try
                    {
                        // Verificar si la ubicación está siendo simulada
                        bool isLocationMocked = _securityService.IsLocationMocked(androidLocation);
                        if (isLocationMocked)
                        {
                            isSuspicious = true;
                            suspiciousReason += "Ubicación simulada detectada; ";
                            Console.WriteLine("⚠️ ALERTA: Ubicación simulada detectada");
                        }

                        // Verificar VPN
                        bool isVpnActive = _securityService.IsVpnActive();
                        if (isVpnActive)
                        {
                            isSuspicious = true;
                            suspiciousReason += "VPN activo; ";
                            Console.WriteLine("⚠️ ALERTA: Se detectó uso de VPN");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error al verificar seguridad: {ex.Message}");
                    }
                }

                await SendLocationToServerAsync(location, AppConfig.IdRuta, isSuspicious, suspiciousReason);

                // Intenta enviar token pendiente (si hay alguno)
                await TrySendPendingTokenAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error procesando la lectura de GPS: {ex}");
            }
        }

        public async Task SendTokenToServerAsync(string token, int maxRetries = 3)
        {
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("❌ Token vacío, no se puede enviar al servidor");
                return;
            }

            var deviceId = DeviceIdentifier.GetOrCreateUniqueId();

            // Almacenar el token para envío posterior si es necesario
            await StoreTokenForLaterSending(token, deviceId);

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    Console.WriteLine($"📡 Enviando token FCM al servidor... (Intento {retry + 1}/{maxRetries})");
                    Console.WriteLine($"🔑 GUID del dispositivo: {deviceId}");
                    Console.WriteLine($"🔑 FCM Token: {token.Substring(0, Math.Min(15, token.Length))}...");

                    var tokenData = new
                    {
                        device_id = deviceId,
                        fcm_token = token,
                        app_version = AppInfo.VersionString,
                        device_model = DeviceInfo.Model,
                        device_manufacturer = DeviceInfo.Manufacturer,
                        device_name = DeviceInfo.Name,
                        platform = DeviceInfo.Platform.ToString(),
                        id_ruta = AppConfig.IdRuta
                    };

                    var jsonContent = JsonConvert.SerializeObject(tokenData);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    content.Headers.Add("Device-ID", deviceId);

                    var url = "https://dismo-gps-8df3af4b987d.herokuapp.com/device/token";

                    // Enviar directamente sin verificar disponibilidad
                    var response = await _httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✅ Token FCM enviado correctamente. Respuesta: {responseBody}");

                        // Marcar el token como enviado
                        await SecureStorage.SetAsync("TokenSent", "true");
                        return; // Éxito, salir del método
                    }
                    else
                    {
                        Console.WriteLine($"⚠ Error al enviar token FCM. Status: {response.StatusCode}");
                        if (retry < maxRetries - 1)
                        {
                            Console.WriteLine($"🔄 Reintentando en 2 segundos...");
                            await Task.Delay(2000); // Esperar antes de reintentar
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error enviando token FCM: {ex.Message}");
                    if (retry < maxRetries - 1)
                    {
                        Console.WriteLine($"🔄 Reintentando en 3 segundos...");
                        await Task.Delay(3000); // Esperar antes de reintentar
                    }
                }
            }

            Console.WriteLine($"❌ No se pudo enviar el token FCM después de {maxRetries} intentos.");
        }

        private async Task StoreTokenForLaterSending(string token, string deviceId)
        {
            try
            {
                // Guardar el token en preferencias
                await SecureStorage.SetAsync("FCMToken", token);
                await SecureStorage.SetAsync("TokenSent", "false");
                Console.WriteLine("💾 Token guardado localmente para envío posterior");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error guardando token: {ex.Message}");
            }
        }

        public async Task TrySendPendingTokenAsync()
        {
            try
            {
                string sentStatus = await SecureStorage.GetAsync("TokenSent");
                if (sentStatus == "false")
                {
                    string token = await SecureStorage.GetAsync("FCMToken");
                    if (!string.IsNullOrEmpty(token))
                    {
                        Console.WriteLine("🔄 Intentando enviar token pendiente...");
                        await SendTokenToServerAsync(token);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en TrySendPendingTokenAsync: {ex.Message}");
            }
        }

        public async Task SendLocationToServerAsync(Location location, string idRuta, bool isSuspicious = false, string suspiciousReason = "")
        {
            if (location == null) return;

            // Valores por defecto para estado de movimiento
            bool isMoving = false;
            string movementContextName = "Unknown";

            // Convertir Location de Maui a Android.Locations.Location
            AndroidLocation androidLocation = null;
            try
            {
                androidLocation = new AndroidLocation("gps")
                {
                    Latitude = location.Latitude,
                    Longitude = location.Longitude
                };

                if (location.Accuracy.HasValue)
                    androidLocation.Accuracy = (float)location.Accuracy.Value;

                if (location.Altitude.HasValue)
                    androidLocation.Altitude = location.Altitude.Value;

                if (location.Speed.HasValue)
                    androidLocation.Speed = (float)location.Speed.Value;

                if (location.Course.HasValue)
                    androidLocation.Bearing = (float)location.Course.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al convertir ubicación: {ex.Message}");
            }

            // Si tenemos servicio de fusión disponible, obtenemos información de movimiento
            if (_fusionService != null)
            {
                try
                {
                    // Intentar obtener información de movimiento del servicio de fusión
                    var fusionResult = await _fusionService.GetFusedLocationAsync(location);
                    if (fusionResult != null)
                    {
                        isMoving = fusionResult.IsMoving;
                        movementContextName = fusionResult.MovementContextName;

                        // Si hay detección de sospecha desde la fusión, incorporarla
                        if (fusionResult.IsSuspicious)
                        {
                            isSuspicious = true;
                            if (!string.IsNullOrEmpty(fusionResult.SuspiciousReason))
                            {
                                suspiciousReason += fusionResult.SuspiciousReason + "; ";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error al obtener información de movimiento: {ex.Message}");
                }
            }

            // Verificar si la ubicación es sospechosa usando el nuevo método de seguridad
            if (_securityService != null && androidLocation != null)
            {
                // Verificar si la ubicación está siendo simulada
                bool isLocationMocked = _securityService.IsLocationMocked(androidLocation);
                if (isLocationMocked)
                {
                    isSuspicious = true;
                    suspiciousReason += "Ubicación simulada detectada; ";
                    Console.WriteLine("⚠️ ALERTA: Ubicación simulada detectada");
                }

                // Verificar VPN
                bool isVpnActive = _securityService.IsVpnActive();
                if (isVpnActive)
                {
                    isSuspicious = true;
                    suspiciousReason += "VPN activo; ";
                    Console.WriteLine("⚠️ ALERTA: Se detectó uso de VPN");
                }
            }

            var deviceId = DeviceIdentifier.GetOrCreateUniqueId();
            var timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local).ToString("yyyy-MM-dd HH:mm:ss");

            Console.WriteLine($"📡 Enviando datos al servidor...");
            Console.WriteLine($"🔑 GUID del dispositivo: {deviceId}");
            Console.WriteLine($"📍 Latitud: {location.Latitude}, Longitud: {location.Longitude}");
            Console.WriteLine($"⚡ Batería: {Battery.Default.ChargeLevel * 100}%");
            Console.WriteLine($"🕒 Timestamp: {timestamp}");
            Console.WriteLine($"🔍 Ruta actual: {idRuta}");
            Console.WriteLine($"🔍 Ubicación falsa: {isSuspicious}");
            Console.WriteLine($"🧠 Estado de movimiento: {(isMoving ? "En movimiento" : "Estacionario")}");
            Console.WriteLine($"🧠 Contexto: {movementContextName}");

            if (isSuspicious)
            {
                Console.WriteLine($"⚠️ ALERTA: Ubicación sospechosa. Motivo: {suspiciousReason}");
            }

            // Siempre intentamos obtener los datos del reporte, ya que ahora sabemos que funciona con la base de datos correcta
            List<ReporteData> reportData = null;

            try
            {
                reportData = ObtenerDatosReporte();
                if (reportData != null && reportData.Count > 0)
                {
                    Console.WriteLine($"📊 Obtenidos {reportData.Count} registros del reporte de ventas");
                }
                else
                {
                    Console.WriteLine("📊 No se obtuvieron datos del reporte");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al obtener datos del reporte: {ex.Message}");
            }

            // Objeto para enviar al servidor
            object locationData;

            // Si tenemos datos del reporte, incluirlos en el JSON independientemente de la ruta
            if (reportData != null && reportData.Count > 0)
            {
                locationData = new
                {
                    latitude = location.Latitude,
                    longitude = location.Longitude,
                    timestamp = DateTime.Now,
                    isSuspicious = isSuspicious,
                    suspiciousReason = suspiciousReason,
                    id_ruta = idRuta,
                    battery = Battery.Default.ChargeLevel * 100,
                    isMoving = isMoving,
                    movementContext = movementContextName,
                    reportData = reportData // Incluir los datos del reporte
                };
                Console.WriteLine("📊 Incluyendo datos de ventas en el envío");
            }
            else
            {
                locationData = new
                {
                    latitude = location.Latitude,
                    longitude = location.Longitude,
                    timestamp = DateTime.Now,
                    isSuspicious = isSuspicious,
                    suspiciousReason = suspiciousReason,
                    id_ruta = idRuta,
                    battery = Battery.Default.ChargeLevel * 100,
                    isMoving = isMoving,
                    movementContext = movementContextName
                };

                Console.WriteLine("⚠️ No se incluyen datos de ventas porque no hay datos disponibles");
            }

            var jsonContent = JsonConvert.SerializeObject(locationData);

            // Imprimir el JSON que se enviará (limitado para evitar sobrecarga en la consola)
            Console.WriteLine($"📦 JSON a enviar: {(jsonContent.Length > 500 ? jsonContent.Substring(0, 500) + "..." : jsonContent)}");

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            content.Headers.Add("Device-ID", deviceId);

            var url = "https://dismo-gps-8df3af4b987d.herokuapp.com/coordinates";

            if (await IsServerAvailable(url))
            {
                await SendPendingLocations(idRuta);
                try
                {
                    var response = await _httpClient.PostAsync(url, content);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("✅ Ubicación enviada correctamente.");
                    }
                    else
                    {
                        Console.WriteLine($"⚠ Error al enviar. Status: {response.StatusCode}");
                        // Guardar la ubicación y los datos del reporte
                        SaveLocationToDatabase(location, idRuta, isSuspicious, suspiciousReason, isMoving, movementContextName, reportData);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"🌐 Error de red: {ex}");
                    // Guardar la ubicación y los datos del reporte
                    SaveLocationToDatabase(location, idRuta, isSuspicious, suspiciousReason, isMoving, movementContextName, reportData);
                }
            }
            else
            {
                Console.WriteLine("🚫 Sin conexión al servidor. Guardando localmente.");
                // Guardar la ubicación y los datos del reporte
                SaveLocationToDatabase(location, idRuta, isSuspicious, suspiciousReason, isMoving, movementContextName, reportData);
            }
        }   

        // Método para obtener los datos del reporte
        private List<ReporteData> ObtenerDatosReporte()
        {
            try
            {
                // Obtener la fecha actual en formato M/dd/yyyy (con barras en lugar de guiones)
                // Ejemplo: 3/14/2025
                string fechaActual = DateTime.Now.ToString("M/dd/yyyy");

                Console.WriteLine($"📅 Consultando reporte con fecha: {fechaActual}");

                // Usar la ruta a la base de datos correcta
                string dbPath = "/storage/emulated/0/FRM600.db";

                // Verificar si existe la base de datos
                if (!File.Exists(dbPath))
                {
                    Console.WriteLine("❌ No se encontró la base de datos para el reporte en: " + dbPath);
                    return null;
                }

                Console.WriteLine("✅ Base de datos encontrada en: " + dbPath);

                // Crear instancia de ResMxFamReport y obtener los datos
                ResMxFamReport reporte = new ResMxFamReport(dbPath);

                // Usar directamente el método RealizarConsulta (que ahora es público)
                using (var conn = new SQLiteConnection(dbPath))
                {
                    // La compañía es DISMOGT y usamos la fecha actual con formato correcto
                    Console.WriteLine($"🔍 Ejecutando consulta con compañía: DISMOGT");
                    var reportData = reporte.RealizarConsulta(conn, fechaActual, "DISMOGT");

                    if (reportData != null)
                    {
                        Console.WriteLine($"📊 Reporte generado con {reportData.Count} registros");

                        // Imprimir algunos registros del reporte para depuración
                        int countToShow = Math.Min(3, reportData.Count);
                        Console.WriteLine($"📋 Mostrando {countToShow} registros de ejemplo:");

                        for (int i = 0; i < countToShow; i++)
                        {
                            var registro = reportData[i];
                            Console.WriteLine($"{registro.COD_FAM} | {registro.DESCRIPCION} | {registro.UNIDADES} | {registro.VENTA}");
                        }

                        if (reportData.Count > 0 && reportData[0].TotalClientes > 0)
                        {
                            Console.WriteLine($"TOTAL DE CLIENTES: {reportData[0].TotalClientes}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("⚠️ La consulta devolvió null");
                    }

                    return reportData;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al obtener datos del reporte: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private static readonly object _dbLock = new object();

        private void SaveLocationToDatabase(Location location, string idRuta, bool isSuspicious, string suspiciousReason, bool isMoving = false, string movementContext = "Unknown", List<ReporteData> reportData = null)
        {
            Task.Run(() =>
            {
                lock (_dbLock)
                {
                    try
                    {
                        // Convertir los datos del reporte a JSON si existen
                        string reportDataJson = null;
                        if (reportData != null && reportData.Count > 0)
                        {
                            try
                            {
                                reportDataJson = JsonConvert.SerializeObject(reportData);
                                Console.WriteLine($"💾 Datos del reporte serializados para guardado local: {reportData.Count} registros");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Error al serializar datos del reporte: {ex.Message}");
                            }
                        }

                        var pendingLocation = new PendingLocation
                        {
                            Latitude = location.Latitude,
                            Longitude = location.Longitude,
                            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffK"),
                            IsSuspicious = isSuspicious,
                            IdRuta = idRuta,
                            BatteryLevel = Battery.Default.ChargeLevel * 100,
                            ReportDataJson = reportDataJson,
                            SuspiciousReason = suspiciousReason,
                            IsMoving = isMoving,
                            MovementContext = movementContext
                        };

                        DatabaseService.Database.Insert(pendingLocation);
                        Console.WriteLine("✅ Ubicación guardada localmente con Timestamp, zona horaria" +
                            (isSuspicious ? $", motivo de sospecha: {suspiciousReason}" : "") +
                            $", estado: {(isMoving ? "En movimiento" : "Estacionario")}, contexto: {movementContext}" +
                            (reportDataJson != null ? " y datos del reporte" : ""));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error guardando localmente: {ex.Message}");
                    }
                }
            });
        }

        private async Task SendPendingLocations(string idRuta)
        {
            try
            {
                var deviceId = DeviceIdentifier.GetOrCreateUniqueId();
                var pendingLocations = DatabaseService.Database.Table<PendingLocation>().ToList();

                if (pendingLocations.Count == 0)
                {
                    Console.WriteLine("✅ No hay ubicaciones pendientes para enviar");
                    return;
                }

                Console.WriteLine($"🔄 Enviando {pendingLocations.Count} ubicaciones pendientes...");

                // Enviar las ubicaciones una por una para evitar problemas de formato
                foreach (var location in pendingLocations)
                {
                    try
                    {
                        // Objeto base con información de ubicación
                        object locationData;

                        // Si hay datos del reporte almacenados, deserializamos y los incluimos
                        if (!string.IsNullOrEmpty(location.ReportDataJson))
                        {
                            try
                            {
                                var reportData = JsonConvert.DeserializeObject<List<ReporteData>>(location.ReportDataJson);
                                // En la parte donde creas el objeto locationData en SendPendingLocations
                                locationData = new
                                {
                                    latitude = location.Latitude,
                                    longitude = location.Longitude,
                                    timestamp = location.Timestamp,
                                    isSuspicious = location.IsSuspicious,
                                    suspiciousReason = location.SuspiciousReason ?? "",
                                    id_ruta = idRuta,
                                    battery = location.BatteryLevel,
                                    isMoving = location.IsMoving,
                                    movementContext = location.MovementContext,
                                    reportData = reportData // Si existen datos del reporte
                                };
                                Console.WriteLine($"📊 Enviando ubicación pendiente con datos de reporte");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Error al deserializar datos de reporte: {ex.Message}");
                                // Si hay error al deserializar, enviamos sin los datos del reporte
                                locationData = new
                                {
                                    latitude = location.Latitude,
                                    longitude = location.Longitude,
                                    timestamp = location.Timestamp,
                                    isSuspicious = location.IsSuspicious,
                                    suspiciousReason = location.SuspiciousReason ?? "",
                                    id_ruta = idRuta,
                                    battery = location.BatteryLevel
                                };
                            }
                        }
                        else
                        {
                            // Sin datos de reporte
                            locationData = new
                            {
                                latitude = location.Latitude,
                                longitude = location.Longitude,
                                timestamp = location.Timestamp,
                                isSuspicious = location.IsSuspicious,
                                suspiciousReason = location.SuspiciousReason ?? "",
                                id_ruta = idRuta,
                                battery = location.BatteryLevel,
                                isMoving = location.IsMoving,
                                movementContext = location.MovementContext,
                            };
                        }

                        var jsonContent = JsonConvert.SerializeObject(locationData);
                        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                        content.Headers.Add("Device-ID", deviceId);

                        var url = "https://dismo-gps-8df3af4b987d.herokuapp.com/coordinates";

                        var response = await _httpClient.PostAsync(url, content);

                        if (response.IsSuccessStatusCode)
                        {
                            DatabaseService.Database.Delete(location);
                            Console.WriteLine($"✅ Ubicación pendiente ID {location.Id} enviada correctamente");
                        }
                        else
                        {
                            string responseBody = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"⚠ Error al enviar ubicación pendiente. Status: {response.StatusCode}");
                            Console.WriteLine($"⚠ Respuesta: {responseBody}");

                            // Si es un error de servidor, esperamos antes de continuar
                            if ((int)response.StatusCode >= 500)
                            {
                                Console.WriteLine("⏱ Esperando 5 segundos antes de continuar debido a error del servidor");
                                await Task.Delay(5000);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error al enviar ubicación pendiente: {ex.Message}");
                        // Pausa para no sobrecargar el servidor si hay error
                        await Task.Delay(2000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error general en envío de pendientes: {ex.Message}");
            }
        }
        public async Task<LocationResult> GetLocationAsync()
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    return new LocationResult { Location = null, IsSuspicious = false, SuspiciousReason = "" };

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var rawLocation = await Geolocation.Default.GetLocationAsync(request);

                if (rawLocation == null)
                {
                    Console.WriteLine("⚠️ No se pudo obtener ubicación del GPS");
                    return new LocationResult { Location = null, IsSuspicious = false, SuspiciousReason = "" };
                }

                // Si hay servicio de fusión disponible, usarlo para mejorar la ubicación
                LocationResult result;
                if (_fusionService != null)
                {
                    Console.WriteLine("🔄 Aplicando fusión de datos GPS para mejorar precisión...");
                    result = await _fusionService.GetFusedLocationAsync(rawLocation);

                    // Combinar resultados de fusión con verificaciones de seguridad
                    if (_securityService != null)
                    {
                        try
                        {
                            // Convertir la ubicación de Maui a Android para las verificaciones
                            var androidLocation = _securityService.ConvertMauiLocationToAndroid(result.Location);

                            // Verificar si la ubicación está siendo simulada
                            bool isMocked = _securityService.IsLocationMocked(androidLocation);

                            // Verificar VPN activo
                            bool isVpnActive = _securityService.IsVpnActive();

                            // Verificar aplicaciones de GPS falso instaladas
                            bool hasMockApps = _securityService.IsFakeGpsActive();

                            // Combinar todas las verificaciones
                            bool isSuspicious = isMocked || isVpnActive || hasMockApps || result.IsSuspicious;
                            string suspiciousReason = result.SuspiciousReason ?? "";

                            if (isMocked) suspiciousReason += "Ubicación simulada detectada; ";
                            if (isVpnActive) suspiciousReason += "VPN activo; ";
                            if (hasMockApps) suspiciousReason += "Apps de GPS falso instaladas; ";

                            if (isSuspicious)
                            {
                                Console.WriteLine($"⚠️ ALERTA: {suspiciousReason}");
                            }

                            // Actualizar el resultado con la información combinada
                            result.IsSuspicious = isSuspicious;
                            result.SuspiciousReason = suspiciousReason;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Error en la verificación de seguridad: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"📍 Ubicación fusionada: Lat={result.Location.Latitude}, Lng={result.Location.Longitude}, " +
                                     $"Precisión={result.Location.Accuracy}m");
                    return result;
                }
                else
                {
                    // Si no hay servicio de fusión, usar solo las verificaciones de seguridad (código original)
                    bool isSuspicious = false;
                    string suspiciousReason = "";

                    if (_securityService != null)
                    {
                        try
                        {
                            // Convertir la ubicación de Maui a Android para las verificaciones
                            var androidLocation = _securityService.ConvertMauiLocationToAndroid(rawLocation);

                            // Verificar si la ubicación está siendo simulada
                            bool isMocked = _securityService.IsLocationMocked(androidLocation);

                            // Verificar VPN activo
                            bool isVpnActive = _securityService.IsVpnActive();

                            // Verificar aplicaciones de GPS falso instaladas
                            bool hasMockApps = _securityService.IsFakeGpsActive();

                            // Combinar todas las verificaciones
                            isSuspicious = isMocked || isVpnActive || hasMockApps;

                            if (isMocked) suspiciousReason += "Ubicación simulada detectada; ";
                            if (isVpnActive) suspiciousReason += "VPN activo; ";
                            if (hasMockApps) suspiciousReason += "Apps de GPS falso instaladas; ";

                            if (isSuspicious)
                            {
                                Console.WriteLine($"⚠️ ALERTA: {suspiciousReason}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Error en la verificación de seguridad: {ex.Message}");
                        }
                    }

                    return new LocationResult
                    {
                        Location = rawLocation,
                        IsSuspicious = isSuspicious,
                        SuspiciousReason = suspiciousReason
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo ubicación: {ex.Message}");
                return new LocationResult { Location = null, IsSuspicious = false, SuspiciousReason = "" };
            }
        }

        // Asegúrate de liberar los recursos en el Dispose() si implementas IDisposable
        public void Dispose()
        {
            _fusionService?.Dispose();
            // Liberar otros recursos si es necesario
        }

        private async Task<bool> IsServerAvailable(string url)
        {
            try
            {
                // Uso de GET en lugar de HEAD para mejor compatibilidad
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("check-only", "true"); // Header personalizado para indicar que solo es una verificación

                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"⚙️ Estado del servidor: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error verificando disponibilidad del servidor: {ex.Message}");
                return false;
            }
        }
    }
}