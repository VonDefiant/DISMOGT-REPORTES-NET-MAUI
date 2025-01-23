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
            Console.WriteLine("LocationJob iniciado.");

            // Enviar notificación al inicio del servicio
            await NotifyServiceStartedAsync();

            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("LocationJob cancelado antes de comenzar.");
                return;
            }

            try
            {
                // Obtener la ubicación actual
                var location = await _gpsService.GetLocationAsync();
                if (location == null)
                {
                    Console.WriteLine("No se pudo obtener la ubicación.");
                    return;
                }

                Console.WriteLine($"Ubicación obtenida: Latitud={location.Latitude}, Longitud={location.Longitude}");

                // Cancelar si es necesario
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("LocationJob cancelado antes de enviar la ubicación.");
                    return;
                }

                // Intentar enviar la ubicación al servidor
                await TrySendLocationAsync(location, cancellationToken);

                // Cancelar si es necesario
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("LocationJob cancelado antes de enviar la notificación.");
                    return;
                }

                // Mostrar notificación al usuario
                await TrySendNotificationAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general en LocationJob: {ex}");
            }

            Console.WriteLine("LocationJob finalizado.");
        }

        private async Task NotifyServiceStartedAsync()
        {
            try
            {
                await _notificationManager.Send(new Notification
                {
                    Title = "DISMOGT REPORTES",
                    Message = "Cada no que recibes te acerca más a un sí.",
                    ScheduleDate = DateTimeOffset.Now.AddSeconds(1) // Programada 1 segundo en el futuro
                });
                Console.WriteLine("Notificación de inicio de servicio enviada correctamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar la notificación de inicio de servicio: {ex}");
            }
        }


        private async Task TrySendLocationAsync(Location location, CancellationToken cancellationToken)
        {
            try
            {
                await _gpsService.SendLocationToServerAsync(location, AppConfig.IdRuta);
                Console.WriteLine("Ubicación enviada correctamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar la ubicación al servidor: {ex}");
            }
        }

        private async Task TrySendNotificationAsync()
        {
            try
            {
                await _notificationManager.Send(new Notification
                {
                    Title = "Da lo mejor de ti",
                    Message = "Recuerda no somos mejores que nadie, simplemente somos diferentes.",
                    ScheduleDate = DateTimeOffset.Now.AddSeconds(1) // Programada 1 segundo en el futuro
                });
                Console.WriteLine("Notificación enviada correctamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar la notificación: {ex}");
            }
        }


    }
}
