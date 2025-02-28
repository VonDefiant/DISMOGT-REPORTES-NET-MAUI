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
using AndroidX.Core.Content;
using AndroidX.Core.App;

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
    private const int PHONE_STATE_PERMISSION_CODE = 1003;

    protected override async void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Platform.Init(this, savedInstanceState);
        CreateNotificationChannel();
        await RequestPermissionsAsync();
        CreateAppFolder();
        RequestIgnoreBatteryOptimizations();
        CheckPhoneStatePermission();
    }

    private void CheckPhoneStatePermission()
    {
#if ANDROID
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.ReadPhoneState)
                != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(
                    this,
                    new[] { Manifest.Permission.ReadPhoneState },
                    PHONE_STATE_PERMISSION_CODE
                );
            }
        }
#endif
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == PHONE_STATE_PERMISSION_CODE)
        {
            if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
            {
                Console.WriteLine("Permiso READ_PHONE_STATE concedido");
            }
            else
            {
                Toast.MakeText(this, "El permiso para leer el IMEI fue denegado", ToastLength.Long).Show();
            }
        }
    }

    private async Task RequestPermissionsAsync()
    {
        // Permisos de ubicación
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
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.PostNotifications) != Permission.Granted)
            {
                RequestPermissions(new[] { Manifest.Permission.PostNotifications }, 1002);
            }
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
            try
            {
                var intent = new Intent(Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                StartActivity(intent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al solicitar permiso de almacenamiento: {ex.Message}");
            }
        }
    }

    private void RequestIgnoreBatteryOptimizations()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            var packageName = this.PackageName;
            PowerManager pm = (PowerManager)this.GetSystemService(Context.PowerService);

            if (!pm.IsIgnoringBatteryOptimizations(packageName))
            {
                try
                {
                    var intent = new Intent(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                    intent.SetData(Android.Net.Uri.Parse("package:" + packageName));
                    StartActivity(intent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al solicitar ignorar optimización de batería: {ex.Message}");
                }
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
