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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error procesando la lectura de GPS: {ex}");
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
                timestamp = timestamp,
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
            lock (_dbLock)
            {
                try
                {
                    var pendingLocation = new PendingLocation
                    {
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        Timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local).ToString("yyyy-MM-dd HH:mm:ss"),
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
                var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
