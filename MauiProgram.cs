using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shiny;
using Shiny.Push;


namespace DISMOGT_REPORTES;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    { 
        var builder = MauiApp
            .CreateBuilder()
            .UseMauiApp<App>()
            .UseShiny()     
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddJob(typeof(DISMO_REPORTES.Services.LocationJob)); // ✅ Registro del trabajo recurrente
        builder.Services.AddGps<DISMO_REPORTES.Services.GpsService>(); //  Registro del servicio GPS
        Console.WriteLine("Registrando PushDelegate...");
        builder.Services.AddPushFirebaseMessaging<PushDelegate>();
        Console.WriteLine("PushDelegate registrado correctamente.");
        builder.Services.AddNotifications(); // Registro del servicio de notificaciones                                           

        // Configuración adicional si deseas usar geocercas
        // builder.Services.AddGeofencing<DISMO_REPORTES.Services.GeofenceDelegate>();

        // Configuración de logs
        builder.Logging.AddDebug(); // Agrega logs detallados para facilitar la depuración

        return builder.Build();
    }
}
