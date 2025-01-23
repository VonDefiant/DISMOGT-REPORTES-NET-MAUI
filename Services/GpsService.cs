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

        // Método que responde a lecturas de GPS de Shiny
        public async Task OnReading(GpsReading reading)
        {
            try
            {
                var location = new Location
                {
                    Latitude = reading.Position.Latitude,
                    Longitude = reading.Position.Longitude
                };

                Console.WriteLine($"GPS Reading: Lat={location.Latitude}, Lng={location.Longitude}");

                // Envía la ubicación al servidor
                await SendLocationToServerAsync(location, AppConfig.IdRuta);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando la lectura de GPS: {ex}");
            }
        }

        // Método para enviar la ubicación al servidor
        public async Task SendLocationToServerAsync(Location location, string idRuta)
        {
            bool isConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

            if (location != null)
            {
                var locationData = new
                {
                    latitude = location.Latitude,
                    longitude = location.Longitude,
                    timestamp = DateTime.UtcNow,
                    isSuspicious = false,
                    id_ruta = idRuta,
                    battery = Battery.Default.ChargeLevel * 100
                };

                var jsonContent = JsonConvert.SerializeObject(locationData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var url = "https://dismo-gps-8df3af4b987d.herokuapp.com/coordinates";

                if (isConnected)
                {
                    await SendPendingLocations(idRuta);

                    try
                    {
                        var response = await _httpClient.PostAsync(url, content);
                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine("Ubicación enviada correctamente.");
                        }
                        else
                        {
                            Console.WriteLine($"Error al enviar la ubicación. StatusCode: {response.StatusCode}");
                            SaveLocationToDatabase(location, idRuta);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error de red al enviar la ubicación: {ex}");
                        SaveLocationToDatabase(location, idRuta);
                    }
                }
                else
                {
                    Console.WriteLine("No hay conexión a internet. Guardando ubicación en base de datos local.");
                    SaveLocationToDatabase(location, idRuta);
                }
            }
            else
            {
                Console.WriteLine("La ubicación es nula. No se puede enviar al servidor.");
            }
        }

        // Método para guardar ubicaciones pendientes en la base de datos
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
                        Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffK"),
                        IsSuspicious = false,
                        IdRuta = idRuta,
                        BatteryLevel = Battery.Default.ChargeLevel * 100
                    };

                    DatabaseService.Database.Insert(pendingLocation);
                    Console.WriteLine("Ubicación guardada en base de datos local.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al guardar ubicación en la base de datos: {ex}");
                }
            }
        }

        // Método para enviar ubicaciones pendientes al servidor en lotes
        private async Task SendPendingLocations(string idRuta)
        {
            try
            {
                var pendingLocations = DatabaseService.Database.Table<PendingLocation>().ToList();
                if (pendingLocations.Count == 0)
                {
                    Console.WriteLine("No hay ubicaciones pendientes para enviar.");
                    return;
                }

                var batchSize = 10; // Tamaño del lote
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
                            Console.WriteLine("Lote de ubicaciones enviado correctamente y eliminado de la base de datos.");
                        }
                        else
                        {
                            Console.WriteLine($"Error al enviar lote de ubicaciones. StatusCode: {response.StatusCode}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error de red al enviar lote de ubicaciones: {ex}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar ubicaciones pendientes: {ex}");
            }
        }

        // Método para obtener la ubicación actual
        public async Task<Location> GetLocationAsync()
        {
            try
            {
                // Solicitar permisos de ubicación
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    Console.WriteLine("Permiso de geolocalización no concedido.");
                    return null;
                }

                // Crear solicitud de ubicación
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location != null)
                {
                    Console.WriteLine($"Ubicación obtenida: Latitud={location.Latitude}, Longitud={location.Longitude}");
                    return location;
                }
                else
                {
                    Console.WriteLine("No se pudo obtener la ubicación.");
                    return null;
                }
            }
            catch (FeatureNotSupportedException ex)
            {
                Console.WriteLine($"La geolocalización no está soportada en este dispositivo: {ex}");
            }
            catch (PermissionException ex)
            {
                Console.WriteLine($"Permisos de geolocalización denegados: {ex}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener la ubicación: {ex}");
            }

            return null;
        }
    }
}
