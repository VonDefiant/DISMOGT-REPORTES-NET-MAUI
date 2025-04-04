﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Shiny.Jobs;
using Shiny.Notifications;
using Microsoft.Extensions.Logging;
using DISMOGT_REPORTES.Models;

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

            // Notificación de inicio del servicio
            TrySendNotificationAsync("📢 DISMOGT REPORTES", "Cada no que recibes te acerca más a un sí.");

            if (IsCancelled(cancellationToken, "LocationJob cancelado antes de comenzar.")) return;

            try
            {
                // Obtener la ubicación actual
                var locationResult = await GetLocationSafeAsync();
                if (locationResult.Location == null) return;

                Console.WriteLine($"📍 Ubicación obtenida: Latitud={locationResult.Location.Latitude}, Longitud={locationResult.Location.Longitude}");

                if (IsCancelled(cancellationToken, "LocationJob cancelado antes de enviar la ubicación.")) return;

                // Intentar enviar la ubicación al servidor
                await TrySendLocationAsync(locationResult.Location, cancellationToken, locationResult.IsSuspicious, locationResult.SuspiciousReason);

                if (IsCancelled(cancellationToken, "LocationJob cancelado antes de enviar la notificación.")) return;

                // Notificación de confirmación al usuario
                TrySendNotificationAsync("💡 Da lo mejor de ti", "Recuerda no somos mejores que nadie, simplemente somos diferentes.");
            }
            catch (Exception ex)
            {
                LogError(ex, "Error en LocationJob");
            }

            Console.WriteLine(" LocationJob finalizado.");
        }

        private async Task<LocationResult> GetLocationSafeAsync()
        {
            try
            {
                return await _gpsService.GetLocationAsync();
            }
            catch (Exception ex)
            {
                LogError(ex, "Error obteniendo la ubicación");
                return new LocationResult { Location = null, IsSuspicious = false, SuspiciousReason = "" };
            }
        }

        private async Task TrySendLocationAsync(Location location, CancellationToken cancellationToken, bool isSuspicious = false, string suspiciousReason = "")
        {
            try
            {
                if (IsCancelled(cancellationToken, "⏹ Envío de ubicación cancelado antes de comenzar.")) return;

                await _gpsService.SendLocationToServerAsync(location, AppConfig.IdRuta, isSuspicious, suspiciousReason);
                Console.WriteLine("📡 Ubicación enviada correctamente.");
            }
            catch (Exception ex)
            {
                LogError(ex, "Error al enviar la ubicación al servidor");
            }
        }

        private void TrySendNotificationAsync(string title, string message)
        {
            Task.Run(async () =>
            {
                try
                {
                    await _notificationManager.Send(new Notification
                    {
                        Title = title,
                        Message = message
                    });
                    Console.WriteLine("🔔 Notificación enviada correctamente.");
                }
                catch (Exception ex)
                {
                    LogError(ex, "Error al enviar la notificación");
                }
            });
        }

        private bool IsCancelled(CancellationToken token, string message)
        {
            if (token.IsCancellationRequested)
            {
                Console.WriteLine($"⏹ {message}");
                return true;
            }
            return false;
        }

        private void LogError(Exception ex, string message)
        {
            _logger.LogError(ex, $"❌ {message}: {ex.Message}");
#if DEBUG
            Console.WriteLine($"❌ {message}: {ex.Message}\n{ex.StackTrace}");
#endif
        }
    }
}