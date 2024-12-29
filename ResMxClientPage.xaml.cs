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

            ReportListView.ItemsSource = _reportData;

            reportGrid = new Grid();

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
                Children = {
                    new Label
                    {
                        Text = $"REPORTE DETALLADO POR CLIENTE {_fechaBuscada:dd/MM/yyyy} - RUTA: {_rutaSeleccionada}",
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        FontSize = 21,
                        TextColor = Colors.White
                    },
                    productoBuscadoEntry,
                    labelCliente,
                    reportGrid,
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
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            reportGrid.RowSpacing = 5;

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
                AgregarEtiquetasAlGrid(_reportData[i], i + 1);
                if (double.TryParse(_reportData[i].VENTA.Replace("Q", ""), out double venta))
                {
                    totalVenta += venta;
                }
            }

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
            AddToGrid(reportGrid, CreateLabel(label, true), 1, row);
            AddToGrid(reportGrid, CreateLabel($"Q {total:F2}"), 3, row);
        }

        private void OnClienteBuscadoEntryCompleted(object sender, EventArgs e)
        {
            ActualizarDatos();
        }

        private void LimpiarDatosEnGrid()
        {
            for (int i = reportGrid.Children.Count - 1; i >= 0; i--)
            {
                var child = reportGrid.Children[i];

                if (child is View view)
                {
                    var row = Grid.GetRow(view);

                    if (row > 0)
                    {
                        reportGrid.Children.Remove(view);
                    }
                }
            }
        }

        private void ActualizarDatos()
        {
            string productoBuscado = productoBuscadoEntry.Text;
            var datosConsulta = _resMxClient.RealizarConsulta(_conn, _fechaBuscada, productoBuscado);

            LimpiarDatosEnGrid();

            double totalVenta = 0;

            for (int i = 0; i < datosConsulta.Count; i++)
            {
                AgregarEtiquetasAlGrid(datosConsulta[i], i + 1);
                if (double.TryParse(datosConsulta[i].VENTA.Replace("Q", ""), out double venta))
                {
                    totalVenta += venta;
                }
            }

            AgregarFilaTotales("VENTA TOTAL", totalVenta, datosConsulta.Count + 1);

            labelCliente.Text = datosConsulta.FirstOrDefault()?.NOMBRE;
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
                FontSize = isHeader ? 14 : 12,
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
