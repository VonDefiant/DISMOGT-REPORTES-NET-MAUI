using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls; // Cambié a Microsoft.Maui.Controls para MAUI

namespace DISMOGT_REPORTES // Cambié el espacio de nombres a tu estructura de MAUI
{
    public partial class resmdetallereport : ContentPage
    {
        private readonly List<ReporteDatadetalle> _reportData;
        private readonly string _fechaBuscada;
        private readonly string _rutaSeleccionada;

        public resmdetallereport(List<ReporteDatadetalle> reportData, string fechaBuscada, string rutaSeleccionada)
        {
            InitializeComponent();
            _reportData = reportData;
            _fechaBuscada = fechaBuscada;
            _rutaSeleccionada = rutaSeleccionada;

            InitializePage();
        }

        private void InitializePage()
        {
            var listView = new ListView
            {
                ItemsSource = _reportData,
                RowHeight = 50,
                ItemTemplate = new DataTemplate(() =>
                {
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });

                    var codArtLabel = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White };
                    var desArtLabel = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White };
                    var descripcionLabel = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White };
                    var unidadesLabel = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White };
                    var ventaLabel = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White };
                    var codCltLabel = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White };
                    var nombreClienteLabel = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White };

                    codArtLabel.SetBinding(Label.TextProperty, "COD_ART");
                    desArtLabel.SetBinding(Label.TextProperty, "DES_ART");
                    descripcionLabel.SetBinding(Label.TextProperty, "DESCRIPCION");
                    unidadesLabel.SetBinding(Label.TextProperty, "UNIDADES");
                    ventaLabel.SetBinding(Label.TextProperty, "VENTA");
                    codCltLabel.SetBinding(Label.TextProperty, "COD_CLT");
                    nombreClienteLabel.SetBinding(Label.TextProperty, "NOMBRE_CLIENTE");

                    grid.Children.Add(codArtLabel);
                    grid.Children.Add(desArtLabel, 1, 0);
                    grid.Children.Add(descripcionLabel, 2, 0);
                    grid.Children.Add(unidadesLabel, 3, 0);
                    grid.Children.Add(ventaLabel, 4, 0);
                    grid.Children.Add(codCltLabel, 5, 0);
                    grid.Children.Add(nombreClienteLabel, 6, 0);

                    return new ViewCell { View = grid };
                })
            };

            listView.SelectionMode = ListViewSelectionMode.None;

            var scrollViewHorizontal = new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                Content = listView
            };

            var headerLabels = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                Children =
                {
                    new Label { Text = "ARTICULO", FontAttributes = FontAttributes.Bold, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White },
                    new Label { Text = "DESCRIPCION", FontAttributes = FontAttributes.Bold, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White },
                    new Label { Text = "PROVEEDOR", FontAttributes = FontAttributes.Bold, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White },
                    new Label { Text = "UNIDADES", FontAttributes = FontAttributes.Bold, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White },
                    new Label { Text = "VENTA", FontAttributes = FontAttributes.Bold, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White },
                    new Label { Text = "COD CTL", FontAttributes = FontAttributes.Bold, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White },
                    new Label { Text = "NOMBRE DEL CLIENTE", FontAttributes = FontAttributes.Bold, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White }
                }
            };

            var headerView = new StackLayout
            {
                Children = { headerLabels }
            };

            Content = new StackLayout
            {
                Children = {
                    new Label { Text = $"REPORTE VENTA DETALLADA {_fechaBuscada} - RUTA: {_rutaSeleccionada} ", FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, FontSize = 21, TextColor = Colors.White },
                    headerView,
                    scrollViewHorizontal
                }
            };
        }
    }
}
