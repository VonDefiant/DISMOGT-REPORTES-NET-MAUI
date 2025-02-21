using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shiny;

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

        // Registro de servicios y características de Shiny
        builder.Services.AddJob(typeof(DISMO_REPORTES.Services.LocationJob)); // Registro del trabajo recurrente
        builder.Services.AddGps<DISMO_REPORTES.Services.GpsService>(); // Registro del servicio GPS
        builder.Services.AddNotifications(); // Registro del servicio de notificaciones
        //builder.Services.AddPush<DISMO_REPORTES.Services.PushDelegate>(); // Si necesitas notificaciones push

        // Configuración adicional si deseas usar geocercas
        // builder.Services.AddGeofencing<DISMO_REPORTES.Services.GeofenceDelegate>();

        // Configuración de logs
        builder.Logging.AddDebug(); // Agrega logs detallados para facilitar la depuración

        return builder.Build();
    }
}
