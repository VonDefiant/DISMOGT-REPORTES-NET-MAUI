using SQLite;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace DISMOGT_REPORTES
{
    public partial class ResMxClientPage : ContentPage
    {
        private readonly List<PedidoReportData> _reportData;
        private readonly string _fechaBuscada;
        private readonly SQLiteConnection _conn;
        private readonly ResMxClient _resMxClient;
        private readonly Grid reportGrid;
        private readonly Entry productoBuscadoEntry;
        private readonly string _rutaSeleccionada;
        private Label labelCliente;

        public ResMxClientPage(List<PedidoReportData> reportData, string fechaBuscada, SQLiteConnection conn, string rutaSeleccionada)
        {
            InitializeComponent();
            _reportData = reportData;
            _fechaBuscada = fechaBuscada;
            _conn = conn;
            _resMxClient = new ResMxClient(conn.DatabasePath);
            _rutaSeleccionada = rutaSeleccionada;

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

            // Entrada para buscar cliente
            productoBuscadoEntry = new Entry
            {
                Placeholder = "Ingrese el código del cliente",
                TextColor = Colors.White,
                PlaceholderColor = Colors.White
            };
            productoBuscadoEntry.Completed += OnClienteBuscadoEntryCompleted;

            labelCliente = ObtenerLabelCliente(_reportData.FirstOrDefault()?.NOMBRE);

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
                    productoBuscadoEntry,
                    labelCliente,
                    reportGrid
                }
            };

            Content = new ScrollView
            {
                Content = stackLayout
            };

            InitializePage();
        }

        private void InitializePage()
        {
            // Encabezados
            reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddToGrid(reportGrid, CreateLabel("ARTICULO", true), 0, 0);
            AddToGrid(reportGrid, CreateLabel("DESCRIPCION", true), 1, 0);
            AddToGrid(reportGrid, CreateLabel("UNIDADES", true), 2, 0);
            AddToGrid(reportGrid, CreateLabel("VENTAS", true), 3, 0);

            InicializarDatosEnGrid();
        }

        private void InicializarDatosEnGrid()
        {
            double totalVenta = 0;

            for (int i = 0; i < _reportData.Count; i++)
            {
                reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AgregarEtiquetasAlGrid(_reportData[i], i + 1);

                if (double.TryParse(_reportData[i].VENTA.Replace("Q", ""), out double venta))
                {
                    totalVenta += venta;
                }
            }

            // Agregar fila de totales
            AgregarFilaTotales("VENTA TOTAL", totalVenta, _reportData.Count + 1);
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

        private void OnClienteBuscadoEntryCompleted(object sender, EventArgs e)
        {
            ActualizarDatos();
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

        private void ActualizarDatos()
        {
            string clienteBuscado = productoBuscadoEntry.Text;
            var datosConsulta = _resMxClient.RealizarConsulta(_conn, _fechaBuscada, clienteBuscado);

            LimpiarDatosEnGrid();

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

            labelCliente.Text = datosConsulta.FirstOrDefault()?.NOMBRE ?? "Cliente no encontrado";
        }

        private Label ObtenerLabelCliente(string nombre)
        {
            return new Label
            {
                Text = nombre ?? string.Empty,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center
            };
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
}
