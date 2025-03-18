using DISMOGT_REPORTES.Models;
using DISMOGT_REPORTES.Services.LocationFusion;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DISMOGT_REPORTES.Services
{
    /// <summary>
    /// Este es un ejemplo de cómo integrar el nuevo LocationFusionService modular en tu servicio GPS existente
    /// </summary>
    public class GpsServiceEnhanced
    {
        private readonly LocationFusionService _fusionService;

        public GpsServiceEnhanced()
        {
            // Inicializa el servicio de fusión (puedes hacerlo en el constructor de tu servicio GPS)
            _fusionService = new LocationFusionService(Android.App.Application.Context);
            Console.WriteLine("✅ Servicio GPS mejorado con fusión de datos inicializado");
        }

        /// <summary>
        /// Método para obtener una ubicación mejorada
        /// </summary>
        public async Task<LocationResult> GetLocationAsync()
        {
            try
            {
                // 1. Obtener la ubicación cruda del GPS
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

                // 2. Aplicar la fusión de datos para mejorar la ubicación
                var fusedResult = await _fusionService.GetFusedLocationAsync(rawLocation);

                return fusedResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en GetLocationAsync: {ex.Message}");
                return new LocationResult { Location = null, IsSuspicious = false, SuspiciousReason = $"Error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Método para enviar la ubicación al servidor, con comprobaciones de seguridad adicionales
        /// </summary>
        public async Task SendLocationToServerAsync(Location location, string idRuta)
        {
            if (location == null) return;

            try
            {
                // Obtener una ubicación mejorada con el servicio de fusión
                var locationResult = await GetLocationAsync();
                if (locationResult.Location == null) return;

                // Obtener información de sospecha de los módulos de fusión
                bool isSuspicious = locationResult.IsSuspicious;
                string suspiciousReason = locationResult.SuspiciousReason;

                // Incluir la información de sospecha al enviar al servidor
                // [Tu código existente para enviar al servidor]

                // Obtener estadísticas de rendimiento (opcional, para diagnóstico)
                var stats = _fusionService.GetPerformanceStats();
                foreach (var stat in stats)
                {
                    Console.WriteLine($"📊 Estadística de fusión - {stat.Key}: {stat.Value:F2}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al enviar ubicación: {ex.Message}");
            }
        }

        /// <summary>
        /// Asegurar liberar los recursos al finalizar
        /// </summary>
        public void Dispose()
        {
            _fusionService?.Dispose();
        }
    }
}