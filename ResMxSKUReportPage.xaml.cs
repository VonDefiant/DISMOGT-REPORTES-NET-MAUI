using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Picker _picker;

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

            _picker = new Picker
            {
                Title = "Seleccione un proveedor",
                TextColor = Colors.White,
                TitleColor = Colors.White,
                FontSize = 14
            };

            InitializePage();
        }

        private void InitializePage()
        {
            try
            {
                // Encabezados
                reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddToGrid(reportGrid, CreateLabel("DESCRIPCION", true), 0, 0);
                AddToGrid(reportGrid, CreateLabel("UND", true), 1, 0);
                AddToGrid(reportGrid, CreateLabel("VENTA", true), 2, 0);
                AddToGrid(reportGrid, CreateLabel("COB", true), 3, 0);

                // Agregar datos iniciales
                PopulateGridWithData(_reportData);

                Console.WriteLine("[ResMxSKUReport] Inicializando picker de proveedores");

                // Intentar obtener opciones para el picker
                List<string> pickerOptions = null;
                try
                {
                    pickerOptions = ObtenerOpcionesParaPicker();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ResMxSKUReport] Error al obtener opciones para picker: {ex.Message}");
                }

                if (pickerOptions != null && pickerOptions.Count > 0
                    && !pickerOptions[0].Equals("No hay proveedores disponibles")
                    && !pickerOptions[0].Equals("Error al cargar proveedores"))
                {
                    Console.WriteLine($"[ResMxSKUReport] Cargando {pickerOptions.Count} proveedores en el picker");
                    _picker.ItemsSource = pickerOptions;
                    _picker.SelectedIndexChanged += OnPickerSelectedIndexChanged;

                    // Seleccionar la primera opción por defecto
                    if (_picker.Items.Count > 0)
                    {
                        _picker.SelectedIndex = 0;
                    }
                }
                else
                {
                    // Si no hay opciones o hay un error, mostrar mensaje
                    _picker.Title = "No hay proveedores disponibles";
                    _picker.IsEnabled = false;
                    Console.WriteLine("[ResMxSKUReport] No se pudieron cargar opciones para el picker");

                    // Mostrar un mensaje en la pantalla
                    var mensajeError = new Label
                    {
                        Text = "No se pudieron cargar los proveedores. Por favor verifique los datos.",
                        TextColor = Colors.OrangeRed,
                        FontSize = 14,
                        HorizontalOptions = LayoutOptions.Center
                    };

                    var mainLayout = new VerticalStackLayout
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
                            _picker,
                            mensajeError,
                            reportGrid
                        }
                    };

                    Content = new ScrollView
                    {
                        Content = mainLayout
                    };

                    return;
                }

                var contentLayout = new VerticalStackLayout
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
                        _picker,
                        reportGrid
                    }
                };

                Content = new ScrollView
                {
                    Content = contentLayout
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxSKUReport] Error en InitializePage: {ex.Message}");
                DisplayAlert("Error", $"Error al inicializar la página: {ex.Message}", "OK");
            }
        }

        private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var selectedOption = ((Picker)sender).SelectedItem;

                if (selectedOption != null)
                {
                    string clasificacionSeleccion = selectedOption.ToString();
                    Console.WriteLine($"[ResMxSKUReport] Proveedor seleccionado: {clasificacionSeleccion}");

                    try
                    {
                        var datosConsulta = _resMxSKUReportA.RealizarConsulta(_conn, _fechaBuscada, _companiadm, clasificacionSeleccion);
                        var totalClientes = _resMxSKUReportA.ObtenerTotalClientes(_conn, _fechaBuscada, _companiadm, clasificacionSeleccion);

                        // Limpiar datos del Grid pero mantener encabezados
                        LimpiarDatosManteniendoEncabezados();

                        // Población del Grid
                        PopulateGridWithData(datosConsulta, totalClientes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ResMxSKUReport] Error al obtener datos para proveedor: {ex.Message}");
                        DisplayAlert("Error", $"No se pudieron cargar los datos para el proveedor seleccionado: {ex.Message}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxSKUReport] Error en el evento de selección: {ex.Message}");
                DisplayAlert("Error", "Ocurrió un error al seleccionar el proveedor", "OK");
            }
        }

        private void PopulateGridWithData(List<SKUReportData> data, int totalClientes = 0)
        {
            try
            {
                if (data == null || data.Count == 0)
                {
                    var emptyLabel = CreateLabel("No hay datos disponibles para este proveedor", true);
                    reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    AddToGrid(reportGrid, emptyLabel, 0, 1);
                    Grid.SetColumnSpan(emptyLabel, 4);
                    return;
                }

                double totalVenta = 0;

                for (int i = 0; i < data.Count; i++)
                {
                    try
                    {
                        reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        AddToGrid(reportGrid, CreateLabel(data[i].DESCRIPCION), 0, i + 1);
                        AddToGrid(reportGrid, CreateLabel(data[i].UNIDADES.ToString()), 1, i + 1);
                        AddToGrid(reportGrid, CreateLabel(data[i].VENTA), 2, i + 1);
                        AddToGrid(reportGrid, CreateLabel(data[i].NUMERO_COBERTURAS.ToString()), 3, i + 1);

                        try
                        {
                            totalVenta += Convert.ToDouble(data[i].VENTA.Replace("Q", "").Trim());
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ResMxSKUReport] Error al convertir valor de venta: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ResMxSKUReport] Error al agregar fila {i}: {ex.Message}");
                    }
                }

                // Agregar fila de totales
                try
                {
                    var totalRow = data.Count + 1;
                    reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    AddToGrid(reportGrid, CreateLabel("TOTAL GENERAL", true), 0, totalRow);
                    AddToGrid(reportGrid, CreateLabel(""), 1, totalRow);
                    AddToGrid(reportGrid, CreateLabel($"Q {totalVenta:F2}", true), 2, totalRow);
                    AddToGrid(reportGrid, CreateLabel(totalClientes.ToString(), true), 3, totalRow);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ResMxSKUReport] Error al agregar fila de totales: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxSKUReport] Error general en PopulateGridWithData: {ex.Message}");
            }
        }

        private void LimpiarDatosManteniendoEncabezados()
        {
            try
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

                Console.WriteLine("[ResMxSKUReport] Grid limpiado correctamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxSKUReport] Error al limpiar el grid: {ex.Message}");
            }
        }

        private List<string> ObtenerOpcionesParaPicker()
        {
            try
            {
                Console.WriteLine($"[ResMxSKUReport] Consultando opciones para picker (fecha: {_fechaBuscada}, compañía: {_companiadm})");
                var opciones = _resMxSKUReportA.ObtenerDescripcionesClasificacion(_conn, _fechaBuscada, _companiadm);

                if (opciones != null && opciones.Count > 0 &&
                    !opciones[0].Equals("No hay proveedores disponibles") &&
                    !opciones[0].Equals("Error al cargar proveedores"))
                {

                    Console.WriteLine($"[ResMxSKUReport] Se obtuvieron {opciones.Count} opciones para el picker");
                    return opciones;
                }
                else
                {
                    Console.WriteLine("[ResMxSKUReport] No se encontraron opciones para el picker");
                    return new List<string> { "No hay proveedores disponibles" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxSKUReport] Error al obtener opciones para el picker: {ex.Message}");

                return new List<string> { "Error al cargar proveedores" };
            }
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
            try
            {
                Grid.SetColumn(view, column);
                Grid.SetRow(view, row);
                grid.Children.Add(view);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxSKUReport] Error al agregar vista al grid: {ex.Message}");
            }
        }
    }
}