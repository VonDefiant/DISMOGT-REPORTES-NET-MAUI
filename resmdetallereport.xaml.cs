using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace DISMOGT_REPORTES
{
    public partial class ResmDetalleReport : ContentPage
    {
        private readonly List<ReporteDatadetalle> _reportData;
        private readonly string _fechaBuscada;
        private readonly string _rutaSeleccionada;

        public ResmDetalleReport(List<ReporteDatadetalle> reportData, string fechaBuscada, string rutaSeleccionada)
        {
            InitializeComponent();
            _reportData = reportData;
            _fechaBuscada = fechaBuscada;
            _rutaSeleccionada = rutaSeleccionada;

            InitializePage();
        }

        private void InitializePage()
        {
            var listView = new CollectionView
            {
                ItemsSource = _reportData,
                ItemTemplate = new DataTemplate(() =>
                {
                    var grid = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) },
                            new ColumnDefinition { Width = new GridLength(18, GridUnitType.Star) },
                            new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },
                            new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },
                            new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },
                            new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },
                            new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) }
                        }
                    };

                    var codArtLabel = CreateLabel("COD_ART");
                    var desArtLabel = CreateLabel("DES_ART");
                    var descripcionLabel = CreateLabel("DESCRIPCION");
                    var unidadesLabel = CreateLabel("UNIDADES");
                    var ventaLabel = CreateLabel("VENTA");
                    var codCltLabel = CreateLabel("COD_CLT");
                    var nombreClienteLabel = CreateLabel("NOMBRE_CLIENTE");

                    grid.Add(codArtLabel);
                    grid.Add(desArtLabel, 1, 0);
                    grid.Add(descripcionLabel, 2, 0);
                    grid.Add(unidadesLabel, 3, 0);
                    grid.Add(ventaLabel, 4, 0);
                    grid.Add(codCltLabel, 5, 0);
                    grid.Add(nombreClienteLabel, 6, 0);

                    return new ViewCell { View = grid };
                })
            };

            var headerGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(18, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) }
                }
            };

            AddHeaderToGrid(headerGrid, "ARTICULO", 0);
            AddHeaderToGrid(headerGrid, "DESCRIPCION", 1);
            AddHeaderToGrid(headerGrid, "PROVEEDOR", 2);
            AddHeaderToGrid(headerGrid, "UNIDADES", 3);
            AddHeaderToGrid(headerGrid, "VENTA", 4);
            AddHeaderToGrid(headerGrid, "COD CTL", 5);
            AddHeaderToGrid(headerGrid, "NOMBRE DEL CLIENTE", 6);

            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = $"REPORTE VENTA DETALLADA {_fechaBuscada} - RUTA: {_rutaSeleccionada}",
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        FontSize = 21,
                        TextColor = Colors.White
                    },
                    headerGrid,
                    listView
                }
            };
        }

        private Label CreateLabel(string bindingPath)
        {
            var label = new Label
            {
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            };
            label.SetBinding(Label.TextProperty, bindingPath);
            return label;
        }

        private void AddHeaderToGrid(Grid grid, string headerText, int columnIndex)
        {
            grid.Add(new Label
            {
                Text = headerText,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            }, columnIndex, 0);
        }
    }
}
