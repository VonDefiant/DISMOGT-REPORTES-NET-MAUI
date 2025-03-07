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
using DISMOGT_REPORTES.Services;
using DISMOGT_REPORTES.Platforms.Android;
using Firebase.Messaging;
using Firebase;
using Microsoft.Extensions.DependencyInjection;
using Shiny.Push;
using Shiny.Hosting;
using Shiny;
using Shiny.Jobs;

namespace DISMOGT_REPORTES
{
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
        private const int INSTALL_PERMISSION_REQUEST_CODE = 1004;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Console.WriteLine("🚀 Aplicación iniciada...");

            Platform.Init(this, savedInstanceState);
            Firebase.FirebaseApp.InitializeApp(this); // 🔥 Inicializar Firebase

            // 🔹 Registrar PushDelegate en Shiny para que reciba notificaciones en segundo plano
            RegisterPushDelegate();

            CreateNotificationChannel();
            await RequestPermissionsAsync();
            CreateAppFolder();
            RequestIgnoreBatteryOptimizations();
            CheckPhoneStatePermission();
            RequestInstallPackagesPermission();

            // 🔍 Verificar actualizaciones en GitHub
            Task.Run(async () =>
            {
                Console.WriteLine("🔍 Buscando actualizaciones...");
                var updateService = new UpdateService();
                var updateInfo = await updateService.CheckForUpdate();

                if (updateInfo.HasUpdate)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ShowUpdateDialog(updateInfo.ApkUrl);
                    });
                }
            });

            // Iniciar el servicio en primer plano para mantener el seguimiento de ubicación
            try
            {
                var serviceIntent = new Intent(this, typeof(LocationForegroundService));
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    StartForegroundService(serviceIntent);
                    Console.WriteLine("✅ Servicio de ubicación en primer plano iniciado");
                }
                else
                {
                    StartService(serviceIntent);
                    Console.WriteLine("✅ Servicio de ubicación iniciado");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al iniciar el servicio de ubicación: {ex.Message}");
            }
        }

        private void RegisterPushDelegate()
        {
            var pushManager = Host.Current.Services.GetService<IPushManager>();
            if (pushManager == null)
            {
                Console.WriteLine("❌ No se pudo obtener IPushManager en MainActivity.");
                return;
            }

            pushManager.RequestAccess().ContinueWith(async task =>
            {
                if (task.Result.Status == AccessState.Available)
                {
                    var token = task.Result.RegistrationToken;
                    Console.WriteLine($"📲 Token de Firebase en MainActivity: {token}");

                    // Enviar el token actual al servidor cuando la app inicia
                    try
                    {
                        var gpsService = Host.Current.Services.GetService<DISMO_REPORTES.Services.GpsService>();
                        if (gpsService != null && !string.IsNullOrEmpty(token))
                        {
                            await gpsService.SendTokenToServerAsync(token);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error al enviar token actual al servidor: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("⚠️ No se pudieron activar las notificaciones push en MainActivity.");
                }
            });
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channelId = "default_channel";
                var channelName = "Canal de Notificaciones";
                var channelDescription = "Canal por defecto para las notificaciones";
                var importance = NotificationImportance.High;

                var channel = new NotificationChannel(channelId, channelName, importance)
                {
                    Description = channelDescription
                };

                var notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);
            }
        }


        private void ShowUpdateDialog(string apkUrl)
        {
            RunOnUiThread(() =>
            {
                var builder = new AlertDialog.Builder(this);
                builder.SetTitle("Nueva versión disponible");
                builder.SetMessage("Hay una nueva actualización disponible. Es necesario actualizar para continuar usando la aplicación.");
                builder.SetPositiveButton("Actualizar", async (sender, args) =>
                {
                    var updateService = new UpdateService();
                    await updateService.DownloadAndInstallUpdate(apkUrl, this);
                });

                builder.SetCancelable(false);
                builder.Show();
            });
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

        private void RequestInstallPackagesPermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                if (!PackageManager.CanRequestPackageInstalls())
                {
                    Console.WriteLine("⚠️ No tiene permiso para instalar paquetes. Solicitando...");

                    var intent = new Intent(Android.Provider.Settings.ActionManageUnknownAppSources);
                    intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));

                    StartActivityForResult(intent, INSTALL_PERMISSION_REQUEST_CODE);
                }
                else
                {
                    Console.WriteLine("✅ Permiso para instalar paquetes concedido.");
                }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == PHONE_STATE_PERMISSION_CODE)
            {
                if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                {
                    Console.WriteLine("✅ Permiso READ_PHONE_STATE concedido");
                }
                else
                {
                    Toast.MakeText(this, "", ToastLength.Long).Show();
                }
            }
        }

        private async Task RequestPermissionsAsync()
        {
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
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.PostNotifications) != Permission.Granted)
                {
                    RequestPermissions(new[] { Manifest.Permission.PostNotifications }, 1002);
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
                        Console.WriteLine($"❌ Error al solicitar ignorar optimización de batería: {ex.Message}");
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
}