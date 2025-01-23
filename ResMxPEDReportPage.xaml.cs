using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace DISMOGT_REPORTES
{
    public partial class ResMxPEDReportPage : ContentPage
    {
        private readonly List<ReportePedidos> _reportData;
        private readonly string _fechaBuscada;
        private readonly string _rutaSeleccionada;

        public ResMxPEDReportPage(List<ReportePedidos> reportData, string fechaBuscada, string rutaSeleccionada)
        {
            InitializeComponent();
            _reportData = reportData;
            _fechaBuscada = fechaBuscada;
            _rutaSeleccionada = rutaSeleccionada;
            InitializePage();
        }

        private void InitializePage()
        {
            // Crear la grilla
            var reportGrid = new Grid
            {
                RowSpacing = 5,
                ColumnSpacing = 5,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto }, // NUM_PED
                    new ColumnDefinition { Width = GridLength.Auto }, // COD_CLT
                    new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) }, // NOM_CLT
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } // MONTO
                }
            };

            // Encabezados
            reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddToGrid(reportGrid, CreateLabel("NUM PED", true), 0, 0);
            AddToGrid(reportGrid, CreateLabel("CODIGO", true), 1, 0);
            AddToGrid(reportGrid, CreateLabel("CLIENTE", true), 2, 0);
            AddToGrid(reportGrid, CreateLabel("VENTA", true), 3, 0);

            double totalVenta = 0;

            // Agregar datos al grid
            for (int i = 0; i < _reportData.Count; i++)
            {
                reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddToGrid(reportGrid, CreateLabel(_reportData[i].NUM_PED), 0, i + 1);
                AddToGrid(reportGrid, CreateLabel(_reportData[i].COD_CLT), 1, i + 1);
                AddToGrid(reportGrid, CreateLabel(_reportData[i].NOM_CLT), 2, i + 1);
                AddToGrid(reportGrid, CreateLabel(_reportData[i].MONTO), 3, i + 1);

                totalVenta += Convert.ToDouble(_reportData[i].MONTO.Replace("Q", ""));
            }

            // Agregar fila de totales
            var totalRow = _reportData.Count + 1;
            reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddToGrid(reportGrid, CreateLabel("TOTAL", true), 0, totalRow);
            AddToGrid(reportGrid, CreateLabel("PEDIDOS", true), 1, totalRow);
            AddToGrid(reportGrid, CreateLabel($"{_reportData.Count}"), 2, totalRow);
            AddToGrid(reportGrid, CreateLabel($"Q {totalVenta:F2}"), 3, totalRow);

            // Contenido principal
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        new Label
                        {
                            Text = $"REPORTE DE PEDIDOS\n{_fechaBuscada} - RUTA: {_rutaSeleccionada}",
                            FontAttributes = FontAttributes.Bold,
                            HorizontalTextAlignment = TextAlignment.Center,
                            FontSize = 21,
                            TextColor = Colors.White
                        },
                        reportGrid
                    }
                }
            };
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
            if (grid.RowDefinitions.Count <= row)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            Grid.SetColumn(view, column);
            Grid.SetRow(view, row);
            grid.Children.Add(view);
        }
    }
}
