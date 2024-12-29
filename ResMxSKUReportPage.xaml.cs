using SQLite;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace DISMOGT_REPORTES
{
    public partial class ResMxSKUReport : ContentPage
    {
        private readonly List<SKUReportData> _reportData;
        private readonly string _fechaBuscada;
        private readonly SQLiteConnection _conn;
        private readonly string _companiadm;
        private readonly ResMxSKUReportA _resMxSKUReportA;
        private readonly Grid reportGrid;
        private readonly string _rutaSeleccionada;

        public ResMxSKUReport(List<SKUReportData> reportData, string fechaBuscada, SQLiteConnection conn, string companiadm, string rutaSeleccionada)
        {
            InitializeComponent();
            _reportData = reportData;
            _fechaBuscada = fechaBuscada;
            _conn = conn;
            _companiadm = companiadm;
            _resMxSKUReportA = new ResMxSKUReportA(conn.DatabasePath);
            _rutaSeleccionada = rutaSeleccionada;

            int totalClientes = _resMxSKUReportA.ObtenerTotalClientes(conn, fechaBuscada, companiadm, "%");
            _reportData.ForEach(d => d.TotalClientes = totalClientes);

            ReportListView.ItemsSource = _reportData;

            reportGrid = new Grid();

            InitializePage();
        }

        private void InitializePage()
        {
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            reportGrid.RowSpacing = 5;

            AddToGrid(reportGrid, CreateLabel("DESCRIPCION", true), 0, 0);
            AddToGrid(reportGrid, CreateLabel("UND", true), 1, 0);
            AddToGrid(reportGrid, CreateLabel("VENTA", true), 2, 0);
            AddToGrid(reportGrid, CreateLabel("COB", true), 3, 0);

            double totalVenta = 0;
            int totalClientes = 0;

            for (int i = 0; i < _reportData.Count; i++)
            {
                AddToGrid(reportGrid, CreateLabel(_reportData[i].DESCRIPCION), 0, i + 1);
                AddToGrid(reportGrid, CreateLabel(_reportData[i].UNIDADES.ToString()), 1, i + 1);
                AddToGrid(reportGrid, CreateLabel(_reportData[i].VENTA), 2, i + 1);
                AddToGrid(reportGrid, CreateLabel(_reportData[i].NUMERO_COBERTURAS.ToString()), 3, i + 1);

                totalVenta += Convert.ToDouble(_reportData[i].VENTA.Replace("Q", ""));
                totalClientes = _reportData[i].TotalClientes;
            }

            AddToGrid(reportGrid, CreateLabel("TOTAL GENERAL", true), 0, _reportData.Count + 1);
            AddToGrid(reportGrid, CreateLabel(""), 1, _reportData.Count + 1);
            AddToGrid(reportGrid, CreateLabel($"Q {totalVenta:F2}"), 2, _reportData.Count + 1);
            AddToGrid(reportGrid, CreateLabel(totalClientes.ToString()), 3, _reportData.Count + 1);

            var pickerOptions = ObtenerOpcionesParaPicker();
            var picker = new Picker
            {
                Title = "Seleccione un proveedor",
                ItemsSource = pickerOptions,
                TextColor = Colors.White,
                TitleColor = Colors.White
            };
            picker.SelectedIndexChanged += OnPickerSelectedIndexChanged;

            var stackLayout = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = $"REPORTE VENTA POR ARTICULO {_fechaBuscada:dd/MM/yyyy} - RUTA: {_rutaSeleccionada}",
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        FontSize = 21,
                        TextColor = Colors.White
                    },
                    picker,
                    reportGrid
                }
            };

            Content = new ScrollView
            {
                Content = stackLayout
            };
        }

        private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedOption = ((Picker)sender).SelectedItem;

            if (selectedOption != null)
            {
                string clasificacionSeleccion = selectedOption.ToString();

                var datosConsulta = _resMxSKUReportA.RealizarConsulta(_conn, _fechaBuscada, _companiadm, clasificacionSeleccion);
                var totalClientes = _resMxSKUReportA.ObtenerTotalClientes(_conn, _fechaBuscada, _companiadm, clasificacionSeleccion);

                LimpiarDatosEnGrid();

                double totalVenta = 0;

                for (int i = 0; i < datosConsulta.Count; i++)
                {
                    AddToGrid(reportGrid, CreateLabel(datosConsulta[i].DESCRIPCION), 0, i + 1);
                    AddToGrid(reportGrid, CreateLabel(datosConsulta[i].UNIDADES.ToString()), 1, i + 1);
                    AddToGrid(reportGrid, CreateLabel(datosConsulta[i].VENTA), 2, i + 1);
                    AddToGrid(reportGrid, CreateLabel(datosConsulta[i].NUMERO_COBERTURAS.ToString()), 3, i + 1);

                    totalVenta += Convert.ToDouble(datosConsulta[i].VENTA.Replace("Q", ""));
                }

                AddToGrid(reportGrid, CreateLabel("TOTAL GENERAL", true), 0, datosConsulta.Count + 1);
                AddToGrid(reportGrid, CreateLabel(""), 1, datosConsulta.Count + 1);
                AddToGrid(reportGrid, CreateLabel($"Q {totalVenta:F2}"), 2, datosConsulta.Count + 1);
                AddToGrid(reportGrid, CreateLabel(totalClientes.ToString()), 3, datosConsulta.Count + 1);
            }
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

        private List<string> ObtenerOpcionesParaPicker()
        {
            return _resMxSKUReportA.ObtenerDescripcionesClasificacion(_conn, _fechaBuscada, _companiadm);
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
