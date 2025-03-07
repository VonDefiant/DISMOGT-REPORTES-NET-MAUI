using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using DISMO_REPORTES.Services;
using DISMOGT_REPORTES;
using DISMOGT_REPORTES.Services;
using Shiny;
using Shiny.Hosting;
using Shiny.Jobs;
using Shiny.Locations;
using Shiny.Notifications;
using Shiny.Push;
using System.Threading;
using Shiny.Infrastructure;

namespace DISMOGT_REPORTES
{
    public static class MauiProgram
    {
        // Timer para ejecutar periódicamente el LocationJob
        private static Timer _periodicTimer;

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseShiny()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Configurar los servicios Shiny
            builder.Services.AddShinyCoreServices();

            // Notificaciones
            builder.Services.AddNotifications();

            // GPS y Ubicación
            builder.Services.AddSingleton<GpsService>();
            builder.Services.AddGps<GpsService>();

            // Firebase Push
            builder.Services.AddSingleton<PushDelegate>();
            builder.Services.AddPush<PushDelegate>();

            // Registrar LocationJob como servicio
            builder.Services.AddSingleton<LocationJob>();

            // Configurar eventos de ciclo de vida
            builder.ConfigureLifecycleEvents(events =>
            {
#if ANDROID
                events.AddAndroid(android => android
                    .OnCreate((activity, bundle) =>
                    {
                        // Inicializaciones específicas de Android
                        Console.WriteLine("🚀 Aplicación MAUI inicializada");

                        // Iniciar el timer para ejecutar LocationJob cada 15 minutos
                        InitializePeriodicLocationJob();
                    })
                    .OnResume((activity) =>
                    {
                        Console.WriteLine("📱 Aplicación reanudada");

                        // Asegurarse de que el timer esté activo al reanudar la app
                        InitializePeriodicLocationJob();
                    })
                );
#endif
            });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        private static void InitializePeriodicLocationJob()
        {
            if (_periodicTimer == null)
            {
                _periodicTimer = new Timer(async (state) =>
                {
                    try
                    {
                        Console.WriteLine("⏰ Timer periódico: ejecutando LocationJob automáticamente");
                        var locationJob = Host.Current.Services.GetRequiredService<LocationJob>();
                        var jobInfo = new JobInfo(
                            "LocationJob",
                            typeof(LocationJob),
                            false,
                            null,
                            InternetAccess.None,
                            false,
                            false,
                            false
                        );

                        await locationJob.Run(jobInfo, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error en timer periódico: {ex.Message}");
                    }
                },
                null,
                TimeSpan.FromMinutes(1),      // Primera ejecución después de 1 minut
                TimeSpan.FromMinutes(15));    // Después cada 15 minutos

                Console.WriteLine("⏰ Timer periódico inicializado correctamente");
            }
        }
    }
}