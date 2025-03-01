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

namespace DISMOGT_REPORTES.Services
{
    public class UpdateService
    {
        private static readonly string repoOwner = "VonDefiant";
        private static readonly string repoName = "DISMOGT-REPORTES-NET-MAUI";
        private static readonly string apiUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
        private static readonly HttpClient httpClient = new HttpClient();

        public async Task CheckForUpdate()
        {
            try
            {
                if (!IsConnectedToInternet())
                {
                    Console.WriteLine("🚫 No hay conexión a Internet. No se pueden verificar actualizaciones.");
                    return;
                }

                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; DISMOGT_APP)");

                Console.WriteLine("🌐 Enviando solicitud a GitHub...");
                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Error en la solicitud: {response.StatusCode} - {response.ReasonPhrase}");
                    return;
                }

                string jsonString = await response.Content.ReadAsStringAsync();
                using JsonDocument json = JsonDocument.Parse(jsonString);
                string latestVersion = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
                string currentVersion = VersionTracking.CurrentVersion;

                if (string.IsNullOrEmpty(latestVersion) || !Version.TryParse(latestVersion, out Version latest) || !Version.TryParse(currentVersion, out Version current))
                {
                    Console.WriteLine("⚠️ No se pudo determinar la versión correctamente.");
                    return;
                }

                if (current >= latest)
                {
                    Console.WriteLine("✅ Ya tienes la última versión.");
                    return;
                }

                Console.WriteLine($"🚀 Nueva versión disponible: {latestVersion}");

                string apkUrl = null;
                var assets = json.RootElement.GetProperty("assets");

                // 📂 Buscar el archivo .apk sin importar el nombre exacto
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
                    return;
                }

                await DownloadAndInstallUpdate(apkUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en CheckForUpdate: {ex.Message}");
            }
        }

        private async Task DownloadAndInstallUpdate(string apkUrl)
        {
            try
            {
                string downloadPath = Path.Combine(FileSystem.CacheDirectory, "update.apk");

                Console.WriteLine($"📥 Descargando APK desde: {apkUrl}");

                using var response = await httpClient.GetAsync(apkUrl, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Error al descargar el APK: {response.StatusCode} - {response.ReasonPhrase}");
                    return;
                }

                using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fileStream);
                fileStream.Close();

                Console.WriteLine($"✅ APK descargado correctamente en: {downloadPath}");

                InstallApk(downloadPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al descargar e instalar la actualización: {ex.Message}");
            }
        }

        private void InstallApk(string apkPath)
        {
            try
            {
                var context = Android.App.Application.Context;
                var file = new Java.IO.File(apkPath);

                if (!file.Exists())
                {
                    Console.WriteLine($"❌ No se encontró el archivo APK en: {apkPath}");
                    return;
                }

                var fileUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                    context,
                    "com.dismogt.app.fileprovider",
                    file
                );

                Console.WriteLine($"📦 Iniciando instalación del APK desde: {fileUri}");

                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(fileUri, "application/vnd.android.package-archive");
                intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);

                if (Build.VERSION.SdkInt >= (BuildVersionCodes)34) // Android 14+
                {
                    Console.WriteLine("📌 Ejecutando instalación en Android 14 o superior...");
                    intent.SetFlags(ActivityFlags.GrantReadUriPermission);
                }

                if (intent.ResolveActivity(context.PackageManager) != null)
                {
                    context.StartActivity(intent);
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
