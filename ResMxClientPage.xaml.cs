using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace DISMOGT_REPORTES
{
    public partial class ResMxClientPage : ContentPage
    {
        private readonly List<PedidoReportData> _reportData;
        private readonly string _fechaBuscada;
        private readonly string _rutaSeleccionada;
        private readonly SQLiteConnection _conn;
        private readonly ResMxClient _resMxClient;
        private readonly Grid reportGrid;
        private readonly Picker clientePicker;
        private List<ClienteItem> _clientesDisponibles = new List<ClienteItem>();

        public ResMxClientPage(List<PedidoReportData> reportData, string fechaBuscada, SQLiteConnection conn, string rutaSeleccionada)
        {
            InitializeComponent();
            _reportData = reportData;
            _fechaBuscada = fechaBuscada;
            _rutaSeleccionada = rutaSeleccionada;
            _conn = conn;
            _resMxClient = new ResMxClient(conn.DatabasePath);

            Console.WriteLine($"ResMxClientPage - Fecha: {_fechaBuscada}, Ruta Seleccionada: {_rutaSeleccionada}");

            // Configurar Grid
            reportGrid = new Grid
            {
                RowSpacing = 5,
                ColumnSpacing = 5,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            // Picker para seleccionar cliente
            clientePicker = new Picker
            {
                Title = "Seleccione un Cliente",
                TextColor = Colors.White
            };
            clientePicker.SelectedIndexChanged += OnClienteSeleccionado;

            CargarClientes();

            var stackLayout = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = $"REPORTE DETALLADO POR CLIENTE \n {_fechaBuscada:dd/MM/yyyy} - RUTA: {_rutaSeleccionada}",
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        FontSize = 21,
                        TextColor = Colors.White
                    },
                    clientePicker,
                    reportGrid
                }
            };

            Content = new ScrollView { Content = stackLayout };

            InitializePage();
        }

        private void CargarClientes()
        {
            try
            {
                string query = @"
                SELECT DISTINCT PED.COD_CLT AS Codigo, CLIE.NOM_CLT AS Nombre
                FROM ERPADMIN_ALFAC_ENC_PED PED
                JOIN ERPADMIN_CLIENTE CLIE ON PED.COD_CLT = CLIE.COD_CLT
                WHERE FEC_PED LIKE ? || '%'
                AND ESTADO <> 'C' 
                AND TIP_DOC = '1'";

                _clientesDisponibles = _conn.Query<ClienteItem>(query, _fechaBuscada);

                clientePicker.Items.Clear();

                foreach (var cliente in _clientesDisponibles)
                {
                    Console.WriteLine($"Cliente cargado: {cliente.Codigo} - {cliente.Nombre}");
                    clientePicker.Items.Add(cliente.ToString());
                }

                Console.WriteLine($"Total clientes cargados: {_clientesDisponibles.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar los clientes con venta: {ex.Message}");
            }
        }
        //BACKUP 28-02-2025
        private void OnClienteSeleccionado(object sender, EventArgs e)
        {
            if (clientePicker.SelectedIndex == -1)
                return;

            var clienteSeleccionado = _clientesDisponibles[clientePicker.SelectedIndex];

            if (clienteSeleccionado == null)
            {
                Console.WriteLine("Error: Cliente seleccionado es nulo.");
                return;
            }

            Console.WriteLine($"Cliente seleccionado: {clienteSeleccionado.Codigo} - {clienteSeleccionado.Nombre}");

            string codigoCliente = clienteSeleccionado.Codigo;

            LimpiarDatosEnGrid();

            var datosConsulta = _resMxClient.ObtenerDatos(_fechaBuscada, codigoCliente);

            Console.WriteLine($"Registros encontrados: {datosConsulta.Count}");

            if (datosConsulta.Count == 0)
            {
                Console.WriteLine($"No se encontraron datos para el cliente: {codigoCliente} con fecha: {_fechaBuscada}");
            }

            ActualizarLista(datosConsulta);
        }

        private void InitializePage()
        {
            reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddToGrid(reportGrid, CreateLabel("ARTICULO", true), 0, 0);
            AddToGrid(reportGrid, CreateLabel("DESCRIPCION", true), 1, 0);
            AddToGrid(reportGrid, CreateLabel("UNIDADES", true), 2, 0);
            AddToGrid(reportGrid, CreateLabel("VENTAS", true), 3, 0);
        }

        private void ActualizarLista(List<PedidoReportData> datosConsulta)
        {
            double totalVenta = 0;
            for (int i = 0; i < datosConsulta.Count; i++)
            {
                reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AgregarEtiquetasAlGrid(datosConsulta[i], i + 1);

                if (double.TryParse(datosConsulta[i].VENTA.Replace("Q", ""), out double venta))
                {
                    totalVenta += venta;
                }
            }
            AgregarFilaTotales("VENTA TOTAL", totalVenta, datosConsulta.Count + 1);
        }

        private void LimpiarDatosEnGrid()
        {
            var childrenToRemove = new List<View>();

            foreach (var child in reportGrid.Children)
            {
                if (child is View view && Grid.GetRow(view) > 0) 
                {
                    childrenToRemove.Add(view);
                }
            }

            foreach (var child in childrenToRemove)
            {
                reportGrid.Children.Remove(child);
            }

            while (reportGrid.RowDefinitions.Count > 1)
            {
                reportGrid.RowDefinitions.RemoveAt(1);
            }
        }

        private void AgregarEtiquetasAlGrid(PedidoReportData data, int rowIndex)
        {
            AddToGrid(reportGrid, CreateLabel(data.ARTICULO), 0, rowIndex);
            AddToGrid(reportGrid, CreateLabel(data.DESCRIPCION), 1, rowIndex);
            AddToGrid(reportGrid, CreateLabel(data.UNIDADES.ToString()), 2, rowIndex);
            AddToGrid(reportGrid, CreateLabel(data.VENTA), 3, rowIndex);
        }

        private void AgregarFilaTotales(string label, double total, int row)
        {
            reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddToGrid(reportGrid, CreateLabel(label, true), 1, row);
            AddToGrid(reportGrid, CreateLabel($"Q {total:F2}", true), 3, row);
        }

        private Label CreateLabel(string text, bool isHeader = false)
        {
            return new Label
            {
                Text = text,
                FontAttributes = isHeader ? FontAttributes.Bold : FontAttributes.None,
                FontSize = isHeader ? 13 : 10,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            };
        }

        private void AddToGrid(Grid grid, View view, int column, int row)
        {
            Grid.SetColumn(view, column);
            Grid.SetRow(view, row);
            grid.Children.Add(view);
        }
    }

    public class ClienteItem
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }

        public override string ToString()
        {
            return $"{Codigo} - {Nombre}";
        }
    }
}
