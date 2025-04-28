using System;
using System.IO;
using Android.OS;

namespace DISMO_REPORTES.Services
{
    public static class AppConfig
    {
        private static readonly string FolderPath = Path.Combine(global::Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "DISMOGTREPORTES");
        private static readonly string TxtFilePath = Path.Combine(FolderPath, "RutaID.txt");
        private static string _cachedIdRuta = null; // Cache en memoria para evitar lecturas frecuentes

        public static string IdRuta
        {
            get => ObtenerRuta();
            set => GuardarRutaEnTxt(value);
        }

        private static string ObtenerRuta()
        {
            // Si ya tenemos un valor en caché y no es vacío, lo devolvemos
            if (!string.IsNullOrEmpty(_cachedIdRuta))
            {
                return _cachedIdRuta;
            }

            try
            {
                // Asegurar que la carpeta existe primero
                CrearCarpetaSiNoExiste();

                if (File.Exists(TxtFilePath))
                {
                    string rutaId = File.ReadAllText(TxtFilePath).Trim();

                    // Si hay un valor válido, lo guardamos en caché
                    if (!string.IsNullOrEmpty(rutaId))
                    {
                        _cachedIdRuta = rutaId;
                        Console.WriteLine($"[AppConfig] Ruta leída del archivo: {rutaId}");
                        return rutaId;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppConfig] Error al leer la ruta desde el archivo TXT: {ex.Message}");
                return string.Empty;
            }
        }

        private static void GuardarRutaEnTxt(string idRuta)
        {
            try
            {
                if (string.IsNullOrEmpty(idRuta))
                {
                    Console.WriteLine("[AppConfig] No se puede guardar una ruta vacía");
                    return;
                }

                // Guardar en caché primero para respuestas rápidas
                _cachedIdRuta = idRuta;

                // Asegurar que la carpeta existe primero
                CrearCarpetaSiNoExiste();

                // Intentar escribir el archivo
                try
                {
                    // Usar WriteAllText para abrir, escribir y cerrar el archivo automáticamente
                    File.WriteAllText(TxtFilePath, idRuta);
                    Console.WriteLine($"[AppConfig] ID de ruta '{idRuta}' guardado correctamente en {TxtFilePath}");
                }
                catch (Exception writeEx)
                {
                    Console.WriteLine($"[AppConfig] Error al escribir en archivo: {writeEx.Message}");

                    // Intentar con un método alternativo de bajo nivel
                    try
                    {
                        // Crear archivo con FileStream para mayor control
                        using (FileStream fs = new FileStream(TxtFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(idRuta);
                            fs.Write(bytes, 0, bytes.Length);
                            fs.Flush();
                        }
                        Console.WriteLine($"[AppConfig] ID de ruta guardado con método alternativo");
                    }
                    catch (Exception fsEx)
                    {
                        Console.WriteLine($"[AppConfig] Error también con método alternativo: {fsEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppConfig] Error general al guardar la ruta: {ex.Message}");
            }
        }

        private static void CrearCarpetaSiNoExiste()
        {
            try
            {
                if (!Directory.Exists(FolderPath))
                {
                    Directory.CreateDirectory(FolderPath);
                    Console.WriteLine($"[AppConfig] Carpeta creada: {FolderPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppConfig] Error al crear carpeta: {ex.Message}");

            }
        }
    }
}