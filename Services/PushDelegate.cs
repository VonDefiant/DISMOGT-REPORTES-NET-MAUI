using System;
using System.Threading;
using System.Threading.Tasks;
using Shiny.Push;
using Shiny.Jobs;
using DISMO_REPORTES.Services;

namespace DISMOGT_REPORTES
{
    public class PushDelegate : IPushDelegate
    {
        public Task OnEntry(PushNotification push)
        {
            Console.WriteLine("📩 [Push Notificación] ¡Notificación ABIERTA por el usuario!");

            if (push?.Data != null)
            {
                foreach (var kvp in push.Data)
                {
                    Console.WriteLine($"🔹 {kvp.Key}: {kvp.Value}");
                }
            }

            return Task.CompletedTask;
        }

        public Task OnReceived(PushNotification push)
        {
            Console.WriteLine("📲 [Push Notificación] ¡Notificación RECIBIDA en segundo plano!");

            if (push?.Data != null)
            {
                foreach (var kvp in push.Data)
                {
                    Console.WriteLine($"🔹 {kvp.Key}: {kvp.Value}");
                }
            }

            // Obtener directamente la instancia de LocationJob
            var locationJob = Shiny.Hosting.Host.Current.Services.GetRequiredService<LocationJob>();

            // Ejecutar el job directamente sin usar el IJobManager
            Task.Run(async () =>
            {
                try
                {
                    // Crear un JobInfo con el constructor correcto 
                    // La firma parece ser: JobInfo(string identifier, Type type, bool repeat, Dictionary<string, string>? parameters, InternetAccess internet, bool charging, bool deviceIdle, bool batteryNotLow)
                    var jobInfo = new JobInfo(
                        "LocationJob",                // identifier
                        typeof(LocationJob),          // type
                        false,                        // repeat (no es necesario ya que lo ejecutamos directamente)
                        null,                         // parameters (no es necesario)
                        InternetAccess.None,          // internet (no requerimos específicamente acceso a Internet)
                        false,                        // charging (no requerimos que esté cargando)
                        false,                        // deviceIdle (no requerimos que el dispositivo esté inactivo)
                        false                         // batteryNotLow (no requerimos que la batería no esté baja)
                    );

                    // Ejecutar directamente el LocationJob
                    await locationJob.Run(jobInfo, CancellationToken.None);
                    Console.WriteLine("🚀 LocationJob ejecutado directamente desde PushDelegate.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error al ejecutar LocationJob: {ex.Message}");
                }
            });

            return Task.CompletedTask;
        }

        public Task OnNewToken(string token)
        {
            Console.WriteLine($"🔄 [Push Notificación] Nuevo token generado: {token}");

            // Ejecutar el envío del token en una tarea aparte
            Task.Run(async () =>
            {
                try
                {
                    // Obtener la instancia de GpsService para enviar el token
                    var gpsService = Shiny.Hosting.Host.Current.Services.GetRequiredService<DISMO_REPORTES.Services.GpsService>();

                    // Enviar el token al servidor
                    await gpsService.SendTokenToServerAsync(token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error al enviar el token al servidor: {ex.Message}");
                }
            });

            return Task.CompletedTask;
        }

        public Task OnUnRegistered(string reason)
        {
            Console.WriteLine($"🚫 [Push Notificación] Token eliminado o usuario desuscrito. Razón: {reason}");
            return Task.CompletedTask;
        }
    }
}