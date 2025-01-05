using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;

namespace DISMOGT_REPORTES
{
    public partial class ResMxFamReportPage : ContentPage
    {
        private readonly List<ReporteData> _reportData;
        private readonly string _fechaBuscada;
        private readonly string _rutaSeleccionada;

        public ResMxFamReportPage(List<ReporteData> reportData, string fechaBuscada, string rutaSeleccionada)
        {
            InitializeComponent();
            _reportData = reportData;
            _fechaBuscada = fechaBuscada;
            _rutaSeleccionada = rutaSeleccionada;

            InitializePage();
        }

        private void InitializePage()
        {
            // Crear encabezados
            var headerGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }, // COD
                    new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) }, // DESCRIPCION
                    new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }, // UNIDADES
                    new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }, // VENTA
                    new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }  // COBERTURAS
                },
                BackgroundColor = Colors.Transparent,
                RowSpacing = 2,
                Padding = 5
            };

            AddHeaderToGrid(headerGrid, "COD", 0);
            AddHeaderToGrid(headerGrid, "DESCRIPCION", 1);
            AddHeaderToGrid(headerGrid, "UND", 2);
            AddHeaderToGrid(headerGrid, "VENTA", 3);
            AddHeaderToGrid(headerGrid, "COB", 4);

            // Crear el contenido del reporte
            var dataGrid = new Grid
            {
                ColumnDefinitions = headerGrid.ColumnDefinitions,
                RowSpacing = 2,
                Padding = 5
            };

            // Agregar los datos
            double totalVenta = 0;
            int totalCob = 0;

            for (int i = 0; i < _reportData.Count; i++)
            {
                AddDataToGrid(dataGrid, _reportData[i].COD_FAM, i + 1, 0);
                AddDataToGrid(dataGrid, _reportData[i].DESCRIPCION, i + 1, 1);
                AddDataToGrid(dataGrid, _reportData[i].UNIDADES.ToString(), i + 1, 2);
                AddDataToGrid(dataGrid, _reportData[i].VENTA, i + 1, 3);
                AddDataToGrid(dataGrid, _reportData[i].NUMERO_CLIENTES.ToString(), i + 1, 4);

                totalVenta += Convert.ToDouble(_reportData[i].VENTA.Replace("Q", ""));
                totalCob += _reportData[i].NUMERO_CLIENTES;
            }

            // Agregar totales
            var totalRow = _reportData.Count + 1;
            AddDataToGrid(dataGrid, "VENTA TOTAL", totalRow, 1, isBold: true);
            AddDataToGrid(dataGrid, $"Q {totalVenta:F2}", totalRow, 3, isBold: true);
            AddDataToGrid(dataGrid, totalCob.ToString(), totalRow, 4, isBold: true);

            // Contenedor principal
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        new Label
                        {
                            Text = $"REPORTE VENTA POR PROVEEDOR\n {_fechaBuscada} - RUTA: {_rutaSeleccionada}",
                            FontAttributes = FontAttributes.Bold,
                            HorizontalTextAlignment = TextAlignment.Center,
                            FontSize = 20,
                            TextColor = Colors.White
                        },
                        headerGrid,
                        dataGrid
                    }
                }
            };
        }

        private void AddHeaderToGrid(Grid grid, string text, int columnIndex)
        {
            grid.Add(new Label
            {
                Text = text,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            }, columnIndex, 0);
        }

        private void AddDataToGrid(Grid grid, string text, int rowIndex, int columnIndex, bool isBold = false)
        {
            grid.Add(new Label
            {
                Text = text,
                FontAttributes = isBold ? FontAttributes.Bold : FontAttributes.None,
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            }, columnIndex, rowIndex);
        }
    }
}
