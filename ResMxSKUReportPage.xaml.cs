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

            reportGrid = new Grid
            {
                RowSpacing = 5,
                ColumnSpacing = 5,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };

            InitializePage();
        }

        private void InitializePage()
        {
            // Encabezados
            reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddToGrid(reportGrid, CreateLabel("DESCRIPCION", true), 0, 0);
            AddToGrid(reportGrid, CreateLabel("UND", true), 1, 0);
            AddToGrid(reportGrid, CreateLabel("VENTA", true), 2, 0);
            AddToGrid(reportGrid, CreateLabel("COB", true), 3, 0);

            // Agregar datos iniciales
            PopulateGridWithData(_reportData);

            var pickerOptions = ObtenerOpcionesParaPicker();
            var picker = new Picker
            {
                Title = "Seleccione un proveedor",
                ItemsSource = pickerOptions,
                TextColor = Colors.White,
                TitleColor = Colors.White,
                 FontSize = 14
            };
            picker.SelectedIndexChanged += OnPickerSelectedIndexChanged;

            var stackLayout = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = $"REPORTE VENTA POR ARTICULO \n {_fechaBuscada:dd/MM/yyyy} - RUTA: {_rutaSeleccionada}",
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

                // Limpiar datos del Grid pero mantener encabezados
                LimpiarDatosManteniendoEncabezados();

                // Población del Grid
                PopulateGridWithData(datosConsulta, totalClientes);
            }
        }

        private void PopulateGridWithData(List<SKUReportData> data, int totalClientes = 0)
        {
            double totalVenta = 0;

            for (int i = 0; i < data.Count; i++)
            {
                reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddToGrid(reportGrid, CreateLabel(data[i].DESCRIPCION), 0, i + 1);
                AddToGrid(reportGrid, CreateLabel(data[i].UNIDADES.ToString()), 1, i + 1);
                AddToGrid(reportGrid, CreateLabel(data[i].VENTA), 2, i + 1);
                AddToGrid(reportGrid, CreateLabel(data[i].NUMERO_COBERTURAS.ToString()), 3, i + 1);

                totalVenta += Convert.ToDouble(data[i].VENTA.Replace("Q", ""));
            }

            // Agregar fila de totales
            var totalRow = data.Count + 1;
            reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddToGrid(reportGrid, CreateLabel("TOTAL GENERAL", true), 0, totalRow);
            AddToGrid(reportGrid, CreateLabel(""), 1, totalRow);
            AddToGrid(reportGrid, CreateLabel($"Q {totalVenta:F2}", true), 2, totalRow);
            AddToGrid(reportGrid, CreateLabel(totalClientes.ToString(), true), 3, totalRow);
          
        }

        private void LimpiarDatosManteniendoEncabezados()
        {
            var childrenToRemove = new List<View>();
            foreach (var child in reportGrid.Children)
            {
                // Solo elimina las filas que no sean encabezados (fila > 0)
                if (child is View view && Grid.GetRow(view) > 0)
                {
                    childrenToRemove.Add(view);
                }
            }

            foreach (var child in childrenToRemove)
            {
                reportGrid.Children.Remove(child);
            }

            // Limpiar las definiciones de filas, pero mantén la primera fila
            while (reportGrid.RowDefinitions.Count > 1)
            {
                reportGrid.RowDefinitions.RemoveAt(1);
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
                FontSize = isHeader ? 14 : 9,
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
