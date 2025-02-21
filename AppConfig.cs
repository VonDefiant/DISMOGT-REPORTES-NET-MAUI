using System;
using System.IO;
using Android.OS; // Asegura que este using esté presente

namespace DISMO_REPORTES.Services
{
    public static class AppConfig
    {
        private static readonly string FolderPath = Path.Combine(global::Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "DISMOGTREPORTES");
        private static readonly string TxtFilePath = Path.Combine(FolderPath, "RutaID.txt");

        public static string IdRuta
        {
            get => LeerRutaDesdeTxt();
            set => GuardarRutaEnTxt(value);
        }

        private static string LeerRutaDesdeTxt()
        {
            try
            {
                CrearCarpetaSiNoExiste();

                if (File.Exists(TxtFilePath))
                {
                    return File.ReadAllText(TxtFilePath).Trim();
                }
                else
                {
                    Console.WriteLine($"[AppConfig] Archivo no encontrado: {TxtFilePath}");
                    return string.Empty;
                }
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
                CrearCarpetaSiNoExiste();

                File.WriteAllText(TxtFilePath, idRuta);
                Console.WriteLine($"[AppConfig] ID de ruta guardado correctamente en {TxtFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppConfig] Error al guardar la ruta en el archivo TXT: {ex.Message}");
            }
        }

        private static void CrearCarpetaSiNoExiste()
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
                Console.WriteLine($"[AppConfig] Carpeta creada: {FolderPath}");
            }
        }
    }
}
