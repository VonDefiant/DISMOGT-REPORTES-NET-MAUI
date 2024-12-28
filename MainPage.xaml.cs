using SQLite;
using System;
using System.Data;
using System.IO;

namespace DISMOGT_REPORTES
{
    public partial class MainPage : ContentPage
    {
        private efectivreport efectReport;
        private ResMxFamReport resMxFamReport;
        private ResMxSKUReportA resMxSKUReport;
        private ResMxPedidoReport resMxPEDReport;
        private SQLiteConnection _conn;
        private ResMxClient resMxClientReport;
        private ResMxCajasclasA resMxCajasReport;
        private resmdetallereportA resDetalleReport;
        public string rutaSeleccionada;
        public static string otraDbPath = "/storage/emulated/0/DISMOGTREPORTES/UXCDISMOGT.db";
        private string txtFilePath = "/storage/emulated/0/DISMOGTREPORTES/RutaID.txt";

        public MainPage()
        {
            InitializeComponent();

            string filePath = "/storage/emulated/0/FRM600.db";

            if (!File.Exists(filePath))
            {
                filePath = "/storage/emulated/0/Android/data/com.softland.fr.droid/files/FRM600.db";
            }

            try
            {
                _conn = new SQLiteConnection(filePath);
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine("Error al abrir la conexión SQLite: " + ex.Message);
            }

            efectReport = new efectivreport(filePath);
            resMxFamReport = new ResMxFamReport(filePath);
            resMxSKUReport = new ResMxSKUReportA(filePath);
            resMxPEDReport = new ResMxPedidoReport(filePath);
            resMxClientReport = new ResMxClient(filePath);
            resMxCajasReport = new ResMxCajasclasA(filePath);
            resDetalleReport = new resmdetallereportA(filePath);

            // Obtener la ruta desde la base de datos
            rutaSeleccionada = ObtenerRutaDesdeBD();

            // Guardar el valor obtenido en un archivo de texto
            GuardarRutaEnTxt(rutaSeleccionada);
        }

        public string ObtenerRutaDesdeBD()
        {
            try
            {
                // Consulta SQL para obtener la ruta
                string consulta = "SELECT RUTA FROM ERPADMIN_RUTA_CFG LIMIT 1";

                var rutaSeleccionada = _conn.ExecuteScalar<string>(consulta);

                Console.WriteLine("RUTA DE VENTA " + rutaSeleccionada);

                return rutaSeleccionada;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener la ruta desde la base de datos: " + ex.Message);
                return null;
            }
        }

        private void GuardarRutaEnTxt(string ruta)
        {
            try
            {
                // Si el archivo ya existe, se borra para reemplazarlo
                if (File.Exists(txtFilePath))
                {
                    File.Delete(txtFilePath);
                }

                // Escribir la nueva ruta en el archivo de texto
                File.WriteAllText(txtFilePath, ruta);
                Console.WriteLine("Ruta guardada en archivo TXT: " + txtFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al guardar la ruta en el archivo TXT: " + ex.Message);
            }
        }

        private async void OnGenerarButtonClicked(object sender, EventArgs e)
        {
            // Obtener la fecha seleccionada del DatePicker
            DateTime fechaSeleccionada = FechaDatePicker.Date;

            // Formatear la fecha seleccionada como "M/d/yyyy"
            string fechaBuscada = fechaSeleccionada.ToString("M/d/yyyy");

            // Obtener el tipo de informe seleccionado
            string tipoInforme = TipoInformePicker.SelectedItem?.ToString();
            string companiadm = "DISMOGT";
            DataLabel.Text = string.Empty;

            if (!string.IsNullOrEmpty(tipoInforme))
            {
                // Seleccionar el informe según el tipo
                switch (tipoInforme)
                {
                    case "Efectividad":
                        efectReport.ActualizarDatos(fechaBuscada, DataLabel, ErrorLabel, tipoInforme);
                        break;
                    case "Venta por proveedor":
                        var resultFamilia = resMxFamReport.ObtenerDatos(fechaBuscada, companiadm);
                        await Navigation.PushAsync(new ResMxFamReportPage(resultFamilia, fechaBuscada, rutaSeleccionada));
                        break;
                    case "Venta por SKU":
                        var resultSKUA = resMxSKUReport.ObtenerDatos(fechaBuscada, companiadm);
                        await Navigation.PushAsync(new ResMxSKUReport(resultSKUA, fechaBuscada, _conn, companiadm, rutaSeleccionada));
                        break;
                    case "Venta por pedido":
                        var resultPedido = resMxPEDReport.ObtenerDatos(fechaBuscada, companiadm);
                        await Navigation.PushAsync(new ResMxPEDReportPage(resultPedido, fechaBuscada, rutaSeleccionada));
                        break;
                    case "Venta detallada por cliente":
                        var resultpedclient = resMxClientReport.ObtenerDatos(fechaBuscada, companiadm);
                        await Navigation.PushAsync(new ResMxClientPage(resultpedclient, fechaBuscada, _conn, rutaSeleccionada));
                        break;
                    case "Venta X cajas y clasificación":
                        var CAJASReportData = resMxCajasReport.ObtenerDatos(fechaBuscada, companiadm);
                        await Navigation.PushAsync(new ResMxCAJASReport(CAJASReportData, fechaBuscada, _conn, companiadm, rutaSeleccionada));
                        break;
                    case "Venta Detallada":
                        var resultDetalle = resDetalleReport.ObtenerDatos(fechaBuscada, companiadm);
                        await Navigation.PushAsync(new resmdetallereport(resultDetalle, fechaBuscada, rutaSeleccionada));
                        break;
                    case "Actualizar datos":
                        await Navigation.PushAsync(new DescargaruUXCdb());
                        break;
                }
            }
            else
            {
                // Manejar el caso en que no se ha seleccionado un tipo de informe
                ErrorLabel.Text = "Seleccione un tipo de informe";
            }
        }
    }
}
