using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Maui.Controls; // Cambiado a .NET MAUI

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

        public DescargaruUXCdb()
        {
            InitializeComponent();
        }

        private async void DescargarArchivo_Clicked(object sender, EventArgs e)
        {
            progressStack.IsVisible = true;
            await DescargarArchivo();
        }

        private async Task DescargarArchivo()
        {
            string url = "https://github.com/VonDefiant/UXCDISMOGTRESOURCES/raw/main/UXCDISMOGT.db";
            var httpClient = new HttpClient();

            try
            {
                using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            var totalBytes = response.Content.Headers.ContentLength ?? -1;
                            var buffer = new byte[4096];
                            var readBytes = 0L;
                            var bytesRead = -1;
                            var filePath = "/storage/emulated/0/DISMOGTREPORTES/UXCDISMOGT.db";

                            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                while (bytesRead != 0)
                                {
                                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                    readBytes += bytesRead;

                                    // Calcular el progreso
                                    var progress = (double)readBytes / totalBytes;

                                    // Actualizar el progreso
                                    progressLabel.Text = ObtenerFraseDeProgreso(progress);

                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                }
                            }
                        }
                    }
                    else
                    {
                        await DisplayAlert("Error", $"Error al descargar el archivo. Código de estado: {response.StatusCode}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error durante la descarga del archivo: {ex.Message}", "OK");
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
            else if (progress < 0.9)
                return frases[4];
            else
                return frases[5];
        }
    }
}
