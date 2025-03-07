using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Shiny;
using Shiny.Jobs;
using Shiny.Locations;
using Shiny.Push;

namespace DISMOGT_REPORTES
{
    public class PushDelegate : IPushDelegate
    {
        private readonly IJobManager _jobManager;
        private readonly IGpsManager _gpsManager;

        public PushDelegate(IJobManager jobManager, IGpsManager gpsManager)
        {
            _jobManager = jobManager;
            _gpsManager = gpsManager;
        }

        public Task OnEntry(PushNotification push)
        {
            Console.WriteLine("📩 [PushDelegate] Notificación abierta por el usuario.");

            if (push?.Data != null)
            {
                foreach (var kvp in push.Data)
                {
                    Console.WriteLine($"🔹 {kvp.Key}: {kvp.Value}");
                }
            }

            return Task.CompletedTask;
        }

        public async Task OnReceived(PushNotification push)
        {
            Console.WriteLine("📲 [PushDelegate] Notificación recibida en segundo plano o con la app cerrada.");

            if (push?.Data != null)
            {
                foreach (var kvp in push.Data)
                {
                    Console.WriteLine($"🔹 {kvp.Key}: {kvp.Value}");
                }

                // ✅ Asegurar que siempre se maneje la notificación, incluso si no tiene datos específicos
                Console.WriteLine("📍 [PushDelegate] Procesando notificación en segundo plano...");

                // 🚀 Ejecutar LocationJob en segundo plano si la notificación lo requiere
                if (push.Data.ContainsKey("tipo") && push.Data["tipo"] == "location_update")
                {
                    Console.WriteLine("📍 [PushDelegate] Activando LocationJob...");
                    var result = await _jobManager.Run("LocationJob");
                    Console.WriteLine($"✅ [PushDelegate] LocationJob ejecutado con estado: {result}");

                    // 📡 Obtener ubicación si se requiere
                    Console.WriteLine("📡 [PushDelegate] Intentando obtener ubicación...");
                    var gpsReading = await _gpsManager.GetCurrentPosition();
                    if (gpsReading != null)
                    {
                        Console.WriteLine($"📍 Ubicación obtenida: Latitud={gpsReading.Position.Latitude}, Longitud={gpsReading.Position.Longitude}");
                    }
                    else
                    {
                        Console.WriteLine("❌ No se pudo obtener la ubicación.");
                    }
                }
            }
            else
            {
                Console.WriteLine("⚠️ [PushDelegate] Notificación recibida sin datos.");
            }
        }

        public Task OnNewToken(string token)
        {
            Console.WriteLine($"🔄 [PushDelegate] Nuevo token recibido: {token}");
            return Task.CompletedTask;
        }

        public Task OnUnRegistered(string reason)
        {
            Console.WriteLine($"🚫 [PushDelegate] Token eliminado. Razón: {reason}");
            return Task.CompletedTask;
        }
    }
}
