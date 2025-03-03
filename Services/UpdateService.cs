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

namespace DISMOGT_REPORTES.Services
{
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string ApkUrl { get; set; }
    }

    public class UpdateService
    {
        private static readonly string repoOwner = "VonDefiant";
        private static readonly string repoName = "DISMOGT-REPORTES-NET-MAUI";
        private static readonly string apiUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
        private static readonly HttpClient httpClient = new HttpClient();

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
                return new UpdateInfo { HasUpdate = true, ApkUrl = apkUrl };
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
                string downloadPath = Path.Combine(FileSystem.CacheDirectory, "update.apk");
                ProgressDialog progressDialog = new ProgressDialog(activity);
                progressDialog.SetTitle("Descargando actualización");
                progressDialog.SetMessage("Espere mientras se descarga la nueva versión...");
                progressDialog.SetProgressStyle(ProgressDialogStyle.Horizontal);
                progressDialog.SetCancelable(false);
                progressDialog.Show();

                Console.WriteLine($"📥 Descargando APK desde: {apkUrl}");

                using var response = await httpClient.GetAsync(apkUrl, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Error al descargar el APK: {response.StatusCode} - {response.ReasonPhrase}");
                    progressDialog.Dismiss();
                    return;
                }

                using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var buffer = new byte[8192];
                int bytesRead;
                long totalRead = 0;

                using var responseStream = await response.Content.ReadAsStreamAsync();
                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        int progress = (int)((totalRead * 100) / totalBytes);
                        progressDialog.Progress = progress;
                    }
                }

                fileStream.Close();
                progressDialog.Dismiss();

                Console.WriteLine($"✅ APK descargado correctamente en: {downloadPath}");

                InstallApk(downloadPath, activity);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al descargar e instalar la actualización: {ex.Message}");
            }
        }

        private void InstallApk(string apkPath, Activity activity)
        {
            try
            {
                var context = activity.ApplicationContext;
                var file = new Java.IO.File(apkPath);

                if (!file.Exists())
                {
                    Console.WriteLine($"❌ No se encontró el archivo APK en: {apkPath}");
                    return;
                }

                // 🔹 Corrección del uso de FileProvider
                var fileUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                    context,
                    "com.dismogt.app.fileprovider",
                    file
                );

                Console.WriteLine($"📦 Iniciando instalación del APK desde: {fileUri}");

                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(fileUri, "application/vnd.android.package-archive");
                intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
                intent.SetFlags(ActivityFlags.NoHistory | ActivityFlags.ClearWhenTaskReset); 

                if (Build.VERSION.SdkInt >= (BuildVersionCodes)34)
                {
                    Console.WriteLine("📌 Ejecutando instalación en Android 14 o superior...");
                    intent.SetFlags(ActivityFlags.GrantReadUriPermission);
                }

                if (intent.ResolveActivity(context.PackageManager) != null)
                {
                    activity.StartActivity(intent);
                    Console.WriteLine("✅ Instalador del APK iniciado correctamente.");
                }
                else
                {
                    Console.WriteLine("❌ No se encontró una aplicación para manejar la instalación del APK.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al iniciar la instalación del APK: {ex.Message}");
            }
        }

        private bool IsConnectedToInternet()
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }
    }
}
