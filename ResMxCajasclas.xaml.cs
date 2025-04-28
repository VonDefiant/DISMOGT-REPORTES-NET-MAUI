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
        private readonly string _uxcDbPath; // Añadido para guardar la ruta

        public ResMxCAJASReport(List<CAJASReportData> reportData, string fechaBuscada, SQLiteConnection conn, string companiadm, string rutaSeleccionada)
        {
            InitializeComponent();
            _reportData = reportData;
            _fechaBuscada = fechaBuscada;
            _conn = conn;
            _companiadm = companiadm;
            _resMxCAJASReportA = new ResMxCajasclasA(conn.DatabasePath);
            _rutaSeleccionada = rutaSeleccionada;

            // Definir la ruta a la base de datos UXCDISMOGT
            _uxcDbPath = "/storage/emulated/0/DISMOGTREPORTES/UXCDISMOGT.db";

            // Verificar si existe y buscar alternativas si es necesario
            if (!System.IO.File.Exists(_uxcDbPath))
            {
                string[] alternativePaths = new string[]
                {
                    "/storage/emulated/0/Android/data/com.dismogt.app/files/UXCDISMOGT.db",
                    "/storage/emulated/0/Download/UXCDISMOGT.db",
                    "/data/data/com.dismogt.app/files/UXCDISMOGT.db"
                };

                foreach (string path in alternativePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        _uxcDbPath = path;
                        Console.WriteLine($"[ResMxCAJASReport] Base de datos UXC encontrada en ruta alternativa: {_uxcDbPath}");
                        break;
                    }
                }
            }

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
            try
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
                    TextColor = Colors.White,
                    TitleColor = Colors.White,
                    FontSize = 14
                };

                try
                {
                    // Obtener opciones para el picker con verificación
                    var pickerOptions = ObtenerOpcionesParaPicker();

                    if (pickerOptions != null && pickerOptions.Count > 0)
                    {
                        Console.WriteLine($"[ResMxCAJASReport] Cargando {pickerOptions.Count} proveedores en picker");
                        picker.ItemsSource = pickerOptions;
                        picker.SelectedIndexChanged += OnPickerSelectedIndexChanged;
                    }
                    else
                    {
                        picker.Title = "No hay proveedores disponibles";
                        picker.IsEnabled = false;
                        Console.WriteLine("[ResMxCAJASReport] No se obtuvieron opciones para el picker");

                        // Mostrar mensaje de error al usuario
                        var errorLabel = new Label
                        {
                            Text = "No se pudieron cargar los proveedores. Verifique la base de datos UXCDISMOGT.",
                            TextColor = Colors.OrangeRed,
                            HorizontalOptions = LayoutOptions.Center,
                            FontSize = 14
                        };

                        var layout = new VerticalStackLayout
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
                                errorLabel,
                                reportGrid
                            }
                        };

                        Content = new ScrollView { Content = layout };
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ResMxCAJASReport] Error al obtener opciones para picker: {ex.Message}");
                    picker.Title = "Error al cargar proveedores";
                    picker.IsEnabled = false;
                }

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
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxCAJASReport] Error en InitializePage: {ex.Message}");
                DisplayAlert("Error", $"Error al inicializar la página: {ex.Message}", "OK");
            }
        }

        private void PopulateGridWithData(List<CAJASReportData> data)
        {
            try
            {
                if (data == null || data.Count == 0)
                {
                    var emptyLabel = CreateLabel("No hay datos disponibles", true);
                    reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    AddToGrid(reportGrid, emptyLabel, 0, 1);
                    Grid.SetColumnSpan(emptyLabel, 5);
                    return;
                }

                double totalVenta = 0;
                double totalCajas = 0;

                for (int i = 0; i < data.Count; i++)
                {
                    try
                    {
                        reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        AddToGrid(reportGrid, CreateLabel(data[i].AGRUPACION), 0, i + 1);
                        AddToGrid(reportGrid, CreateLabel(data[i].NUMERO_COBERTURAS.ToString()), 1, i + 1);
                        AddToGrid(reportGrid, CreateLabel(data[i].VENTA), 2, i + 1);
                        AddToGrid(reportGrid, CreateLabel(data[i].UNIDADES.ToString()), 3, i + 1);
                        AddToGrid(reportGrid, CreateLabel(data[i].CAJAS.ToString()), 4, i + 1);

                        try
                        {
                            totalVenta += Convert.ToDouble(data[i].VENTA.Replace("Q", "").Trim());
                            totalCajas += data[i].CAJAS;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ResMxCAJASReport] Error al convertir valores: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ResMxCAJASReport] Error al agregar fila {i}: {ex.Message}");
                    }
                }

                // Fila de totales
                try
                {
                    var totalRow = data.Count + 1;
                    reportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    AddToGrid(reportGrid, CreateLabel("TOTAL GENERAL", true), 0, totalRow);
                    AddToGrid(reportGrid, CreateLabel(data.Count.ToString(), true), 1, totalRow);
                    AddToGrid(reportGrid, CreateLabel($"Q {totalVenta:F2}", true), 2, totalRow);
                    AddToGrid(reportGrid, CreateLabel(""), 3, totalRow);
                    AddToGrid(reportGrid, CreateLabel(totalCajas.ToString("F2"), true), 4, totalRow);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ResMxCAJASReport] Error al agregar fila de totales: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxCAJASReport] Error general en PopulateGridWithData: {ex.Message}");
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
                    Console.WriteLine($"[ResMxCAJASReport] Proveedor seleccionado: {clasificacionSeleccion}");

                    try
                    {
                        // Pasar la ruta a UXCDISMOGT.db como quinto parámetro
                        var datosConsulta = _resMxCAJASReportA.RealizarConsulta(_conn, _fechaBuscada, _companiadm, clasificacionSeleccion, _uxcDbPath);
                        var totalClientes = _resMxCAJASReportA.ObtenerTotalClientes(_conn, _fechaBuscada, _companiadm, clasificacionSeleccion);

                        LimpiarDatosManteniendoEncabezados();
                        PopulateGridWithData(datosConsulta);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ResMxCAJASReport] Error al obtener datos para proveedor: {ex.Message}");
                        DisplayAlert("Error", $"No se pudieron cargar los datos: {ex.Message}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxCAJASReport] Error en OnPickerSelectedIndexChanged: {ex.Message}");
                DisplayAlert("Error", "Ocurrió un error al seleccionar el proveedor", "OK");
            }
        }

        private void LimpiarDatosManteniendoEncabezados()
        {
            try
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

                Console.WriteLine("[ResMxCAJASReport] Grid limpiado correctamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxCAJASReport] Error al limpiar grid: {ex.Message}");
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
            try
            {
                Grid.SetColumn(view, column);
                Grid.SetRow(view, row);
                grid.Children.Add(view);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxCAJASReport] Error al agregar vista al grid: {ex.Message}");
            }
        }

        private List<string> ObtenerOpcionesParaPicker()
        {
            try
            {
                // Pasar la ruta a UXCDISMOGT.db como está guardada en la propiedad
                List<string> opciones = _resMxCAJASReportA.ObtenerDescripcionesClasificacion(_conn, _fechaBuscada, _companiadm);

                if (opciones != null && opciones.Count > 0)
                {
                    Console.WriteLine($"[ResMxCAJASReport] Se obtuvieron {opciones.Count} opciones para el picker");
                    return opciones;
                }

                Console.WriteLine("[ResMxCAJASReport] La consulta no devolvió opciones para el picker");
                return new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxCAJASReport] Error al obtener opciones para picker: {ex.Message}");
                return new List<string>();
            }
        }
    }
}