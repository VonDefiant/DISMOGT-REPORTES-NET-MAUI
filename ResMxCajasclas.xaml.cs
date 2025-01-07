using SQLite;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace DISMOGT_REPORTES
{
    public partial class ResMxCAJASReport : ContentPage
    {
        private readonly List<CAJASReportData> _reportData;
        private readonly string _fechaBuscada;
        private readonly SQLiteConnection _conn;
        private readonly string _companiadm;
        private readonly ResMxCajasclasA _resMxCAJASReportA;
        private readonly Grid reportGrid;
        private readonly string _rutaSeleccionada;

        public ResMxCAJASReport(List<CAJASReportData> reportData, string fechaBuscada, SQLiteConnection conn, string companiadm, string rutaSeleccionada)
        {
            InitializeComponent();
            _reportData = reportData;
            _fechaBuscada = fechaBuscada;
            _conn = conn;
            _companiadm = companiadm;
            _resMxCAJASReportA = new ResMxCajasclasA(conn.DatabasePath);
            _rutaSeleccionada = rutaSeleccionada;

            reportGrid = new Grid
            {
                RowSpacing = 5,
                ColumnSpacing = 5,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(3.3, GridUnitType.Star) }, // DESCRIPCION
                    new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) }, // COB
                    new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },   // VENTA
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },  // UNIDADES
                    new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }   // CAJAS
                }
            };

            InitializePage();
        }

        private void InitializePage()
        {
            // Agregar encabezados
            reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddToGrid(reportGrid, CreateLabel("DESCRIPCION", true), 0, 0);
            AddToGrid(reportGrid, CreateLabel("COB", true), 1, 0);
            AddToGrid(reportGrid, CreateLabel("VENTA", true), 2, 0);
            AddToGrid(reportGrid, CreateLabel("UND", true), 3, 0);
            AddToGrid(reportGrid, CreateLabel("CAJAS", true), 4, 0);

            // Agregar datos iniciales
            PopulateGridWithData(_reportData);

            var picker = new Picker
            {
                Title = "Seleccione un proveedor",
                ItemsSource = ObtenerOpcionesParaPicker(),
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
                        Text = $"REPORTE VENTA POR CAJAS \n {_fechaBuscada:dd/MM/yyyy} - RUTA: {_rutaSeleccionada}",
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        FontSize = 21,
                        TextColor = Colors.White
                    },
                    picker,
                    reportGrid
                }
            };

            Content = new ScrollView { Content = stackLayout };
        }

        private void PopulateGridWithData(List<CAJASReportData> data)
        {
            double totalVenta = 0;
            double totalCajas = 0;

            for (int i = 0; i < data.Count; i++)
            {
                reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                AddToGrid(reportGrid, CreateLabel(data[i].AGRUPACION), 0, i + 1);
                AddToGrid(reportGrid, CreateLabel(data[i].NUMERO_COBERTURAS.ToString()), 1, i + 1);
                AddToGrid(reportGrid, CreateLabel(data[i].VENTA), 2, i + 1);
                AddToGrid(reportGrid, CreateLabel(data[i].UNIDADES.ToString()), 3, i + 1);
                AddToGrid(reportGrid, CreateLabel(data[i].CAJAS.ToString()), 4, i + 1);

                totalVenta += Convert.ToDouble(data[i].VENTA.Replace("Q", ""));
                totalCajas += data[i].CAJAS;
            }

            // Fila de totales
            var totalRow = data.Count + 1;
            reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddToGrid(reportGrid, CreateLabel("TOTAL GENERAL", true), 0, totalRow);
            AddToGrid(reportGrid, CreateLabel(data.Count.ToString(), true), 1, totalRow);
            AddToGrid(reportGrid, CreateLabel($"Q {totalVenta:F2}", true), 2, totalRow);
            AddToGrid(reportGrid, CreateLabel(""), 3, totalRow);
            AddToGrid(reportGrid, CreateLabel(totalCajas.ToString(), true), 4, totalRow);
        }

        private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedOption = ((Picker)sender).SelectedItem;

            if (selectedOption != null)
            {
                string clasificacionSeleccion = selectedOption.ToString();

                var datosConsulta = _resMxCAJASReportA.RealizarConsulta(_conn, _fechaBuscada, _companiadm, clasificacionSeleccion);
                var totalClientes = _resMxCAJASReportA.ObtenerTotalClientes(_conn, _fechaBuscada, _companiadm, clasificacionSeleccion);

                LimpiarDatosManteniendoEncabezados();

                PopulateGridWithData(datosConsulta);
            }
        }

        private void LimpiarDatosManteniendoEncabezados()
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

        private List<string> ObtenerOpcionesParaPicker()
        {
            return _resMxCAJASReportA.ObtenerDescripcionesClasificacion(_conn, _fechaBuscada, _companiadm);
        }
    }
}
