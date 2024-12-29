using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace DISMOGT_REPORTES
{
    public partial class ResMxFamReportPage : ContentPage
    {
        private readonly List<ReporteData> _reportData;
        private readonly string _fechaBuscada;
        private readonly string _rutaSeleccionada;
        private readonly Grid reportGrid;

        public ResMxFamReportPage(List<ReporteData> reportData, string fechaBuscada, string rutaSeleccionada)
        {
            InitializeComponent();
            _reportData = reportData;
            _fechaBuscada = fechaBuscada;
            _rutaSeleccionada = rutaSeleccionada;

            reportGrid = new Grid();

            InitializePage();
        }

        private void InitializePage()
        {
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // COD
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) }); // DESCRIPCION
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // UNIDADES
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); // VENTA
            reportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // COBERTURAS

            reportGrid.RowSpacing = 5;

            AddToGrid(reportGrid, CreateLabel("COD", true), 0, 0);
            AddToGrid(reportGrid, CreateLabel("DESCRIPCION", true), 1, 0);
            AddToGrid(reportGrid, CreateLabel("UND", true), 2, 0);
            AddToGrid(reportGrid, CreateLabel("VENTA", true), 3, 0);
            AddToGrid(reportGrid, CreateLabel("COB", true), 4, 0);

            double totalVenta = 0;

            for (int i = 0; i < _reportData.Count; i++)
            {
                AddToGrid(reportGrid, CreateLabel(_reportData[i].COD_FAM), 0, i + 1);
                AddToGrid(reportGrid, CreateLabel(_reportData[i].DESCRIPCION), 1, i + 1);
                AddToGrid(reportGrid, CreateLabel(_reportData[i].UNIDADES.ToString()), 2, i + 1);
                AddToGrid(reportGrid, CreateLabel(_reportData[i].VENTA), 3, i + 1);
                AddToGrid(reportGrid, CreateLabel(_reportData[i].NUMERO_CLIENTES.ToString()), 4, i + 1);

                totalVenta += Convert.ToDouble(_reportData[i].VENTA.Replace("Q", ""));
            }

            AddToGrid(reportGrid, CreateLabel("VENTA TOTAL", true), 1, _reportData.Count + 1);
            AddToGrid(reportGrid, CreateLabel($"Q {totalVenta:F2}"), 3, _reportData.Count + 1);
            AddToGrid(reportGrid, CreateLabel(_reportData.Sum(r => r.TotalClientes).ToString(), true), 4, _reportData.Count + 1);

            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = $"REPORTE VENTA POR PROVEEDOR {_fechaBuscada} - RUTA: {_rutaSeleccionada}",
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        FontSize = 21,
                        TextColor = Colors.White
                    },
                    reportGrid
                }
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
