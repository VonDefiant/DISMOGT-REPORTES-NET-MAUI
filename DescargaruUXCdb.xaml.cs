using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace DISMOGT_REPORTES
{
    public partial class DescargaruUXCdb : ContentPage
    {
        private string[] frases = {
            "Conectando con el servidor...",
            "Obteniendo parametros globales...",
            "Descargando sentencias...",
            "Actualizando las variables logísticas de los productos...",
            "Insertando datos...",
            "Actualización terminada"
        };
        private int currentFraseIndex = 0;
        private bool descargaEnProgreso = false;

        public DescargaruUXCdb()
        {
            InitializeComponent();
        }

        private async void DescargarArchivo_Clicked(object sender, EventArgs e)
        {
            if (descargaEnProgreso)
            {
                await DisplayAlert("En progreso", "Ya hay una descarga en curso, por favor espere", "OK");
                return;
            }

            descargaEnProgreso = true;
            progressStack.IsVisible = true;

            try
            {
                await DescargarArchivo();

                // Mostrar mensaje de éxito
                await DisplayAlert("Éxito", "Los datos se descargaron correctamente. Ya puede usar los reportes que requieren esta información.", "OK");
            }
            catch (Exception ex)
            {
                // Mostrar mensaje de error
                await DisplayAlert("Error", $"No se pudo completar la descarga: {ex.Message}", "OK");
                progressLabel.Text = "Error en la descarga. Intente nuevamente.";
            }
            finally
            {
                descargaEnProgreso = false;
            }
        }

        private async Task DescargarArchivo()
        {
            string url = "https://github.com/VonDefiant/UXCDISMOGTRESOURCES/raw/main/UXCDISMOGT.db";
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5); // Aumentar timeout a 5 minutos

            try
            {
                // Verificar que el directorio de destino existe
                string directoryPath = "/storage/emulated/0/DISMOGTREPORTES";
                if (!Directory.Exists(directoryPath))
                {
                    try
                    {
                        Directory.CreateDirectory(directoryPath);
                        Console.WriteLine($"Directorio creado: {directoryPath}");
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"No se pudo crear el directorio de destino: {ex.Message}");
                    }
                }

                // Ruta completa del archivo
                string filePath = Path.Combine(directoryPath, "UXCDISMOGT.db");

                // Verificar si el archivo ya existe y eliminarlo
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        Console.WriteLine("Archivo existente eliminado para ser reemplazado");
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"No se pudo eliminar el archivo existente: {ex.Message}");
                    }
                }

                // Mostrar primera frase antes de iniciar la descarga
                progressLabel.Text = frases[0];
                await Task.Delay(500); // Pequeña pausa para mostrar la primera frase

                using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var totalBytes = response.Content.Headers.ContentLength ?? -1;

                        // Verificar conexión estable
                        if (totalBytes <= 0)
                        {
                            throw new Exception("No se pudo determinar el tamaño del archivo. Verifique su conexión a Internet.");
                        }

                        Console.WriteLine($"Tamaño del archivo a descargar: {totalBytes} bytes");

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            var buffer = new byte[8192]; // Buffer de 8KB
                            var readBytes = 0L;
                            var bytesRead = 0;

                            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    // Escribir el contenido descargado
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);

                                    // Actualizar contador de bytes
                                    readBytes += bytesRead;

                                    // Calcular y mostrar progreso
                                    if (totalBytes > 0)
                                    {
                                        double progress = (double)readBytes / totalBytes;
                                        string progressText = ObtenerFraseDeProgreso(progress);

                                        // Solo actualizar si la frase ha cambiado
                                        if (progressLabel.Text != progressText)
                                        {
                                            progressLabel.Text = progressText;
                                            Console.WriteLine($"Progreso: {progress:P0} - {progressText}");
                                        }
                                    }
                                }

                                // Asegurar que se escriben todos los datos
                                await fileStream.FlushAsync();
                            }
                        }

                        // Verificar integridad del archivo
                        FileInfo fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length == 0)
                        {
                            throw new Exception("El archivo descargado está vacío. Por favor intente nuevamente.");
                        }

                        // Verificar que es un archivo SQLite válido
                        try
                        {
                            using (var testConn = new SQLite.SQLiteConnection(filePath))
                            {
                                // Intentar ejecutar una consulta simple para confirmar que es una BD válida
                                testConn.ExecuteScalar<int>("SELECT 1");
                                Console.WriteLine("Base de datos verificada correctamente");
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"El archivo descargado no es una base de datos válida: {ex.Message}");
                        }

                        // Mostrar frase final
                        progressLabel.Text = frases[5];
                    }
                    else
                    {
                        throw new Exception($"Error al descargar el archivo. Código de estado: {response.StatusCode}");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                throw new Exception("La descarga tomó demasiado tiempo. Verifique su conexión a Internet e intente nuevamente.");
            }
            catch (HttpRequestException httpEx)
            {
                throw new Exception($"Error de red: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error durante la descarga: {ex.Message}");
            }
        }

        private string ObtenerFraseDeProgreso(double progress)
        {
            // Determinar la frase de progreso según el valor de progress
            if (progress < 0.2)
                return frases[0];
            else if (progress < 0.4)
                return frases[1];
            else if (progress < 0.6)
                return frases[2];
            else if (progress < 0.8)
                return frases[3];
            else if (progress < 0.95)
                return frases[4];
            else
                return frases[5];
        }
    }
}