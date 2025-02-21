using System;
using System.IO;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Widget;

namespace DISMOGT_REPORTES;

[Activity(
    LaunchMode = LaunchMode.SingleTop,
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density
)]
[IntentFilter(
    new[]
    {
        Shiny.ShinyNotificationIntents.NotificationClickAction,
        Shiny.ShinyPushIntents.NotificationClickAction
    },
    Categories = new[] { "android.intent.category.DEFAULT" }
)]
public class MainActivity : MauiAppCompatActivity
{
    protected override async void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Inicialización de Essentials para MAUI
        Platform.Init(this, savedInstanceState);

        // Crear canal de notificaciones
        CreateNotificationChannel();

        // Solicitar permisos necesarios
        await RequestPermissionsAsync();

        // Crear carpeta específica de la aplicación
        CreateAppFolder();

        // Solicitar exclusión de optimización de batería
        RequestIgnoreBatteryOptimizations();
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }

    private async Task RequestPermissionsAsync()
    {
        // Solicitar permisos de ubicación
        var locationStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (locationStatus != PermissionStatus.Granted)
        {
            locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        if (locationStatus == PermissionStatus.Granted)
        {
            var backgroundLocationStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            if (backgroundLocationStatus != PermissionStatus.Granted)
            {
                backgroundLocationStatus = await Permissions.RequestAsync<Permissions.LocationAlways>();
            }

            if (backgroundLocationStatus != PermissionStatus.Granted)
            {
                Toast.MakeText(this, "Permiso de ubicación en segundo plano denegado", ToastLength.Short).Show();
            }
        }
        else
        {
            Toast.MakeText(this, "Permiso de ubicación denegado", ToastLength.Short).Show();
        }

        // Solicitar permisos de notificaciones (Android 13+)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) 
        {
            RequestNotificationPermission();
        }

        // Solicitar permisos de almacenamiento si son necesarios
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            if (!Android.OS.Environment.IsExternalStorageManager)
            {
                RequestManageStoragePermission();
            }
        }
    }

    private void RequestNotificationPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            RequestPermissions(new[] { Manifest.Permission.PostNotifications }, 1002);
        }
    }
    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O) 
        {
            var channelName = "DISMOGT_REPORTES_Channel";
            var channelDescription = "Canal para las notificaciones de ubicación";
            var importance = NotificationImportance.Default;

            var channel = new NotificationChannel("DISMO_CHANNEL_ID", channelName, importance)
            {
                Description = channelDescription
            };

            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.CreateNotificationChannel(channel);
        }
    }


    private void RequestManageStoragePermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            var intent = new Intent(Android.Provider.Settings.ActionManageAllFilesAccessPermission);
            StartActivity(intent);
        }
    }

    private void RequestIgnoreBatteryOptimizations()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            var intent = new Intent();
            string packageName = this.PackageName;
            PowerManager pm = (PowerManager)this.GetSystemService(Context.PowerService);

            if (!pm.IsIgnoringBatteryOptimizations(packageName))
            {
                intent.SetAction(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                intent.SetData(Android.Net.Uri.Parse("package:" + packageName));
                StartActivity(intent);
            }
        }
    }

    private void CreateAppFolder()
    {
        try
        {
            string appFolderPath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "DISMOGTREPORTES");

            if (!Directory.Exists(appFolderPath))
            {
                Directory.CreateDirectory(appFolderPath);
                Console.WriteLine($"✅ Carpeta creada en: {appFolderPath}");
            }
            else
            {
                Console.WriteLine($"📂 Carpeta ya existe en: {appFolderPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error al crear la carpeta: {ex.Message}");
        }
    }

}
