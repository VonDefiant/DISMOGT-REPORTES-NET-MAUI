using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Android.Content;
using Android.App;
using System.IO;
using Microsoft.Maui.Networking;
using Android.OS;
using Android.Widget;
using AndroidX.Core.Content;
using Android.Content.PM;
using Android.Provider;

namespace DISMOGT_REPORTES.Services
{
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string ApkUrl { get; set; }
        public string Version { get; set; }
    }

    public class UpdateService
    {
        private static readonly string repoOwner = "VonDefiant";
        private static readonly string repoName = "DISMOGT-REPORTES-NET-MAUI";
        private static readonly string apiUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
        private static readonly HttpClient httpClient = new HttpClient();
        private PowerManager.WakeLock _wakeLock;

        public async Task<UpdateInfo> CheckForUpdate()
        {
            try
            {
                if (!IsConnectedToInternet())
                {
                    Console.WriteLine("🚫 No hay conexión a Internet. No se pueden verificar actualizaciones.");
                    return new UpdateInfo { HasUpdate = false };
                }

                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; DISMOGT_APP)");

                Console.WriteLine("🌐 Enviando solicitud a GitHub...");
                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Error en la solicitud: {response.StatusCode} - {response.ReasonPhrase}");
                    return new UpdateInfo { HasUpdate = false };
                }

                string jsonString = await response.Content.ReadAsStringAsync();
                using JsonDocument json = JsonDocument.Parse(jsonString);
                string latestVersion = json.RootElement.GetProperty("tag_name").GetString().TrimStart('v');
                string currentVersion = VersionTracking.CurrentVersion;

                Console.WriteLine($"🔎 Versión en GitHub: {latestVersion}");
                Console.WriteLine($"📌 Versión actual instalada: {currentVersion}");

                if (!Version.TryParse(latestVersion, out Version latest) ||
                    !Version.TryParse(currentVersion, out Version current))
                {
                    Console.WriteLine("⚠️ No se pudo determinar la versión correctamente.");
                    return new UpdateInfo { HasUpdate = false };
                }

                if (current >= latest)
                {
                    Console.WriteLine("✅ Ya tienes la última versión.");
                    return new UpdateInfo { HasUpdate = false };
                }

                string apkUrl = null;
                var assets = json.RootElement.GetProperty("assets");

                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString();
                    string url = asset.GetProperty("browser_download_url").GetString();

                    Console.WriteLine($"📂 Archivo en la release: {name} → {url}");

                    if (name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    {
                        apkUrl = url;
                        break;
                    }
                }

                if (apkUrl == null)
                {
                    Console.WriteLine("❌ No se encontró un APK en la última release.");
                    return new UpdateInfo { HasUpdate = false };
                }

                Console.WriteLine($"🚀 Nueva versión disponible: {latestVersion}");
                return new UpdateInfo { HasUpdate = true, ApkUrl = apkUrl, Version = latestVersion };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en CheckForUpdate: {ex.Message}");
                return new UpdateInfo { HasUpdate = false };
            }
        }

        public async Task DownloadAndInstallUpdate(string apkUrl, Activity activity)
        {
            try
            {
                // Asegurarse de tener permisos para instalar paquetes en Android 8+
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    if (!activity.PackageManager.CanRequestPackageInstalls())
                    {
                        // Solicitar permiso para instalar
                        var intent = new Intent(Settings.ActionManageUnknownAppSources);
                        intent.SetData(Android.Net.Uri.Parse("package:" + activity.PackageName));
                        activity.StartActivityForResult(intent, 1004);

                        Toast.MakeText(
                            activity,
                            "Por favor, permita la instalación de aplicaciones desde esta fuente",
                            ToastLength.Long
                        ).Show();

                        // Esperar un momento para que el usuario conceda el permiso
                        await Task.Delay(5000);
                    }
                }

                // Adquirir WakeLock para mantener la CPU activa durante la descarga
                AcquireWakeLock(activity);

                // Descargar directamente en la actividad para mayor control
                string downloadPath = await DownloadApkDirectly(apkUrl, activity);

                if (string.IsNullOrEmpty(downloadPath))
                {
                    Console.WriteLine("❌ No se pudo descargar el APK");
                    return;
                }

                // Mostrar diálogo de instalación obligatoria
                ShowForceUpdateDialog(downloadPath, activity);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al iniciar descarga: {ex.Message}");
                ReleaseWakeLock();
            }
        }

        private async Task<string> DownloadApkDirectly(string apkUrl, Activity activity)
        {
            try
            {
                // Mostrar diálogo de progreso
                var progressDialog = new ProgressDialog(activity);
                progressDialog.SetTitle("Descargando actualización");
                progressDialog.SetMessage("Por favor, espere mientras se descarga la actualización. No cierre la aplicación.");
                progressDialog.SetProgressStyle(ProgressDialogStyle.Horizontal);
                progressDialog.SetCancelable(false);
                progressDialog.Progress = 0;
                progressDialog.Max = 100;
                progressDialog.Show();

                string downloadPath = Path.Combine(FileSystem.CacheDirectory, "update.apk");

                Console.WriteLine($"📥 Descargando APK desde: {apkUrl}");

                using (var response = await httpClient.GetAsync(apkUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"❌ Error al descargar el APK: {response.StatusCode}");
                        progressDialog.Dismiss();
                        return null;
                    }

                    using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var buffer = new byte[8192];
                        int bytesRead;
                        long totalRead = 0;

                        using (var responseStream = await response.Content.ReadAsStreamAsync())
                        {
                            while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalRead += bytesRead;

                                if (totalBytes > 0)
                                {
                                    activity.RunOnUiThread(() => {
                                        int progress = (int)((totalRead * 100) / totalBytes);
                                        progressDialog.Progress = progress;
                                    });
                                }
                            }
                        }
                    }
                }

                progressDialog.Dismiss();
                Console.WriteLine($"✅ APK descargado correctamente en: {downloadPath}");
                return downloadPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al descargar APK: {ex.Message}");
                return null;
            }
        }

        private void ShowForceUpdateDialog(string apkPath, Activity activity)
        {
            try
            {
                // Crear un diálogo de actualización obligatoria que no pueda ser descartado
                var builder = new AlertDialog.Builder(activity);
                builder.SetTitle("Actualización Requerida");
                builder.SetMessage("Una actualización importante está disponible y debe ser instalada para continuar usando la aplicación.");
                builder.SetCancelable(false);

                builder.SetPositiveButton("Instalar Ahora", async (sender, args) => {
                    try
                    {
                        // Intentar instalar directamente
                        InstallApk(apkPath, activity);

                        // Si el usuario no completa la instalación, intentar de nuevo después de un rato
                        await Task.Delay(10000);

                        // Comprobar si la versión actual sigue siendo la misma (es decir, no se instaló)
                        var updateCheck = await CheckForUpdate();
                        if (updateCheck.HasUpdate)
                        {
                            // Mostrar de nuevo el diálogo
                            ShowForceUpdateDialog(apkPath, activity);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error al iniciar instalación: {ex.Message}");
                        // Reintentar
                        ShowForceUpdateDialog(apkPath, activity);
                    }
                });

                // No dar opción a cancelar
                var dialog = builder.Create();
                dialog.Show();

                // Impedir que el usuario cierre la aplicación presionando atrás
                dialog.KeyPress += (sender, e) => {
                    if (e.KeyCode == Android.Views.Keycode.Back)
                    {
                        e.Handled = true; // Evitar que el evento sea manejado por el sistema
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al mostrar diálogo de actualización: {ex.Message}");
            }
        }

        private void AcquireWakeLock(Context context)
        {
            try
            {
                if (_wakeLock == null)
                {
                    PowerManager powerManager = (PowerManager)context.GetSystemService(Context.PowerService);
                    _wakeLock = powerManager.NewWakeLock(
                        WakeLockFlags.Partial | WakeLockFlags.AcquireCausesWakeup,
                        "DISMOGT_REPORTES:UpdateWakeLock");
                    _wakeLock.Acquire(30 * 60 * 1000L); // 30 minutos máximo
                    Console.WriteLine("🔋 WakeLock adquirido para evitar suspensión durante la descarga");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al adquirir WakeLock: {ex.Message}");
            }
        }

        private void ReleaseWakeLock()
        {
            try
            {
                if (_wakeLock != null && _wakeLock.IsHeld)
                {
                    _wakeLock.Release();
                    _wakeLock = null;
                    Console.WriteLine("🔋 WakeLock liberado");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al liberar WakeLock: {ex.Message}");
            }
        }

        public void InstallApk(string apkPath, Context context)
        {
            try
            {
                var file = new Java.IO.File(apkPath);

                if (!file.Exists())
                {
                    Console.WriteLine($"❌ No se encontró el archivo APK en: {apkPath}");
                    return;
                }

                // Utilizar FileProvider para obtener URI
                var fileUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                    context,
                    $"{context.PackageName}.fileprovider",
                    file
                );

                Console.WriteLine($"📦 Iniciando instalación del APK desde: {fileUri}");

                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(fileUri, "application/vnd.android.package-archive");

                // Asegurarse de que el intent puede iniciarse desde un servicio o actividad
                intent.AddFlags(ActivityFlags.NewTask);
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);

                // En Android 8+ se necesita iniciar desde una actividad
                Activity activity = context as Activity;
                if (activity != null)
                {
                    // Iniciar desde activity directamente es más confiable
                    activity.StartActivity(intent);
                }
                else
                {
                    // Si no es actividad, intentamos desde el contexto
                    context.StartActivity(intent);
                }

                Console.WriteLine("✅ Instalador del APK iniciado correctamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al iniciar la instalación del APK: {ex.Message}");

                // Intentar de manera alternativa
                try
                {
                    // Intento alternativo para Android más nuevos
                    var file = new Java.IO.File(apkPath);
                    var intent = new Intent(Intent.ActionInstallPackage);
                    intent.SetDataAndType(
                        AndroidX.Core.Content.FileProvider.GetUriForFile(
                            context,
                            $"{context.PackageName}.fileprovider",
                            file
                        ),
                        "application/vnd.android.package-archive"
                    );
                    intent.AddFlags(ActivityFlags.NewTask);
                    intent.AddFlags(ActivityFlags.GrantReadUriPermission);

                    context.StartActivity(intent);
                    Console.WriteLine("✅ Instalador del APK iniciado usando método alternativo.");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"❌ También falló el método alternativo: {ex2.Message}");
                }
            }
        }

        private bool IsConnectedToInternet()
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }
    }

    // Receptor para escuchar cuando se ha completado la instalación de un paquete
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { Intent.ActionPackageAdded, Intent.ActionPackageReplaced })]
    public class PackageInstalledReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action == Intent.ActionPackageAdded || intent.Action == Intent.ActionPackageReplaced)
            {
                string packageName = intent.Data.SchemeSpecificPart;
                if (packageName == context.PackageName)
                {
                    Console.WriteLine($"✅ Paquete {packageName} instalado/actualizado correctamente");

                    // Abrir la aplicación después de actualizar
                    var launchIntent = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);
                    if (launchIntent != null)
                    {
                        launchIntent.AddFlags(ActivityFlags.NewTask);
                        context.StartActivity(launchIntent);
                    }
                }
            }
        }
    }
}