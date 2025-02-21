using System;
using System.Threading;
using System.Threading.Tasks;
using Shiny.Jobs;
using Shiny.Notifications;
using Microsoft.Extensions.Logging;

namespace DISMO_REPORTES.Services
{
    public class LocationJob : IJob
    {
        private readonly ILogger<LocationJob> _logger;
        private readonly GpsService _gpsService;
        private readonly INotificationManager _notificationManager;

        public LocationJob(
            ILogger<LocationJob> logger,
            GpsService gpsService,
            INotificationManager notificationManager)
        {
            _logger = logger;
            _gpsService = gpsService;
            _notificationManager = notificationManager;
        }

        public async Task Run(JobInfo jobInfo, CancellationToken cancellationToken)
        {
            Console.WriteLine("🛰 LocationJob iniciado.");

            // Enviar notificación al inicio del servicio
            await NotifyServiceStartedAsync();

            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("⏹ LocationJob cancelado antes de comenzar.");
                return;
            }

            try
            {
                // Obtener la ubicación actual
                var location = await _gpsService.GetLocationAsync();
                if (location == null)
                {
                    Console.WriteLine("⚠ No se pudo obtener la ubicación.");
                    return;
                }

                Console.WriteLine($"📍 Ubicación obtenida: Latitud={location.Latitude}, Longitud={location.Longitude}");

                // Verificar si se ha cancelado antes de proceder
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("⏹ LocationJob cancelado antes de enviar la ubicación.");
                    return;
                }

                // Intentar enviar la ubicación al servidor
                await TrySendLocationAsync(location, cancellationToken);

                // Verificar cancelación antes de enviar notificación
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("⏹ LocationJob cancelado antes de enviar la notificación.");
                    return;
                }

                // Mostrar notificación al usuario
                await TrySendNotificationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en LocationJob: {Message}", ex.Message);
                Console.WriteLine($"❌ Error en LocationJob: {ex.Message}\n{ex.StackTrace}");
            }

            Console.WriteLine("✅ LocationJob finalizado.");
        }

        private async Task NotifyServiceStartedAsync()
        {
            try
            {
                await _notificationManager.Send(new Notification
                {
                    Title = "📢 DISMOGT REPORTES",
                    Message = "Cada no que recibes te acerca más a un sí."
                });
                Console.WriteLine("🔔 Notificación de inicio de servicio enviada correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al enviar la notificación de inicio.");
                Console.WriteLine($"❌ Error al enviar la notificación de inicio: {ex.Message}");
            }
        }

        private async Task TrySendLocationAsync(Location location, CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("⏹ Envío de ubicación cancelado antes de comenzar.");
                    return;
                }

                await _gpsService.SendLocationToServerAsync(location, AppConfig.IdRuta);
                Console.WriteLine("📡 Ubicación enviada correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al enviar la ubicación al servidor.");
                Console.WriteLine($"❌ Error al enviar la ubicación al servidor: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task TrySendNotificationAsync()
        {
            try
            {
                await _notificationManager.Send(new Notification
                {
                    Title = "💡 Da lo mejor de ti",
                    Message = "Recuerda no somos mejores que nadie, simplemente somos diferentes."
                });
                Console.WriteLine("🔔 Notificación enviada correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al enviar la notificación.");
                Console.WriteLine($"❌ Error al enviar la notificación: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
