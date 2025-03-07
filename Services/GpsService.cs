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

namespace DISMO_REPORTES.Services
{
    public class GpsService : IGpsDelegate
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly IGpsManager _gpsManager;

        public GpsService(IGpsManager gpsManager)
        {
            _gpsManager = gpsManager;
        }

        public async Task OnReading(GpsReading reading)
        {
            try
            {
                var location = new Location
                {
                    Latitude = reading.Position.Latitude,
                    Longitude = reading.Position.Longitude
                };

                Console.WriteLine($"📍 GPS Reading: Lat={location.Latitude}, Lng={location.Longitude}");
                await SendLocationToServerAsync(location, AppConfig.IdRuta);

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

        public async Task SendLocationToServerAsync(Location location, string idRuta)
        {
            if (location == null) return;

            var deviceId = DeviceIdentifier.GetOrCreateUniqueId();
            var timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local).ToString("yyyy-MM-dd HH:mm:ss");

            Console.WriteLine($"📡 Enviando datos al servidor...");
            Console.WriteLine($"🔑 GUID del dispositivo: {deviceId}");
            Console.WriteLine($"📍 Latitud: {location.Latitude}, Longitud: {location.Longitude}");
            Console.WriteLine($"⚡ Batería: {Battery.Default.ChargeLevel * 100}%");
            Console.WriteLine($"🕒 Timestamp: {timestamp}");

            var locationData = new
            {
                latitude = location.Latitude,
                longitude = location.Longitude,
                timestamp = DateTime.Now,
                isSuspicious = false,
                id_ruta = idRuta,
                battery = Battery.Default.ChargeLevel * 100
            };

            var jsonContent = JsonConvert.SerializeObject(locationData);
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
                        SaveLocationToDatabase(location, idRuta);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"🌐 Error de red: {ex}");
                    SaveLocationToDatabase(location, idRuta);
                }
            }
            else
            {
                Console.WriteLine("🚫 Sin conexión al servidor. Guardando localmente.");
                SaveLocationToDatabase(location, idRuta);
            }
        }

        private static readonly object _dbLock = new object();

        private void SaveLocationToDatabase(Location location, string idRuta)
        {
            Task.Run(() =>
            {
                lock (_dbLock)
                {
                    try
                    {
                        var pendingLocation = new PendingLocation
                        {
                            Latitude = location.Latitude,
                            Longitude = location.Longitude,
                            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffK"),
                            IsSuspicious = false,
                            IdRuta = idRuta,
                            BatteryLevel = Battery.Default.ChargeLevel * 100
                        };

                        DatabaseService.Database.Insert(pendingLocation);
                        Console.WriteLine("✅ Ubicación guardada localmente con Timestamp y zona horaria.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error guardando localmente: {ex}");
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

                if (pendingLocations.Count == 0) return;

                var batchSize = 10;
                for (int i = 0; i < pendingLocations.Count; i += batchSize)
                {
                    var batch = pendingLocations.Skip(i).Take(batchSize).ToList();

                    var batchData = batch.Select(location => new
                    {
                        latitude = location.Latitude,
                        longitude = location.Longitude,
                        timestamp = location.Timestamp,
                        isSuspicious = location.IsSuspicious,
                        id_ruta = idRuta,
                        battery = location.BatteryLevel
                    });

                    var jsonContent = JsonConvert.SerializeObject(batchData);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    content.Headers.Add("Device-ID", deviceId);

                    var url = "https://dismo-gps-8df3af4b987d.herokuapp.com/coordinates";

                    try
                    {
                        var response = await _httpClient.PostAsync(url, content);
                        if (response.IsSuccessStatusCode)
                        {
                            foreach (var location in batch)
                            {
                                DatabaseService.Database.Delete(location);
                            }
                            Console.WriteLine("✅ Lote enviado exitosamente.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error enviando lote: {ex}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error general en envío de pendientes: {ex}");
            }
        }

        public async Task<Location> GetLocationAsync()
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted) return null;

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                return await Geolocation.Default.GetLocationAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo ubicación: {ex}");
                return null;
            }
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