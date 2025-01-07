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
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // COD
                    new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) }, // DESCRIPCION
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // UNIDADES
                    new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }, // VENTA
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }  // COBERTURAS
                },
                BackgroundColor = Colors.Transparent,
                RowSpacing = 2, // Espaciado reducido entre encabezado y datos
                Padding = 0 // Sin relleno adicional
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
                RowSpacing = 4, // Espaciado entre las filas de datos
                Padding = 0 // Sin relleno adicional
            };

            // Agregar los datos
            double totalVenta = 0;
            int totalCob = 0;

            for (int i = 0; i < _reportData.Count; i++)
            {
                AddDataToGrid(dataGrid, _reportData[i].COD_FAM, i + 1, 0, 13);
                AddDataToGrid(dataGrid, _reportData[i].DESCRIPCION, i + 1, 1, 11);
                AddDataToGrid(dataGrid, _reportData[i].UNIDADES.ToString(), i + 1, 2, 13);
                AddDataToGrid(dataGrid, _reportData[i].VENTA, i + 1, 3, 12);
                AddDataToGrid(dataGrid, _reportData[i].NUMERO_CLIENTES.ToString(), i + 1, 4, 13);

                totalVenta += Convert.ToDouble(_reportData[i].VENTA.Replace("Q", ""));
                totalCob += _reportData[i].TotalClientes;
            }

            // Fila vacía para separar totales
            var totalRow = _reportData.Count + 1;
            var separator = new BoxView
            {
                HeightRequest = 5, // Altura reducida del separador
                BackgroundColor = Colors.Transparent
            };
            dataGrid.Add(separator, 0, totalRow);
            Grid.SetColumnSpan(separator, 5); // Especifica el alcance en las columnas

            // Agregar totales
            AddDataToGrid(dataGrid, "VENTA TOTAL", totalRow + 1, 1, 14, isBold: true);
            AddDataToGrid(dataGrid, $"Q {totalVenta:F2}", totalRow + 1, 3, 14, isBold: true);
            AddDataToGrid(dataGrid, totalCob.ToString(), totalRow + 1, 4, 14, isBold: true);

            // Contenedor principal
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Spacing = 5, // Espaciado reducido entre elementos principales
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
                TextColor = Colors.White,
                Margin = new Thickness(0) // Sin margen adicional
            }, columnIndex, 0);
        }

        private void AddDataToGrid(Grid grid, string text, int rowIndex, int columnIndex, int fontSize, bool isBold = false)
        {
            grid.Add(new Label
            {
                Text = text,
                FontAttributes = isBold ? FontAttributes.Bold : FontAttributes.None,
                FontSize = fontSize,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White,
                Margin = new Thickness(0, 2, 0, 2) // Margen mínimo vertical para datos
            }, columnIndex, rowIndex);
        }
    }
}
