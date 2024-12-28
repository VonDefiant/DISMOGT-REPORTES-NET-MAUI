using System;
using System.IO;

public static class AppConfig
{
    private static readonly string txtFilePath = "/storage/emulated/0/DISMOGTREPORTES/RutaID.txt";

    public static string IdRuta
    {
        get
        {
            return LeerRutaDesdeTxt();
        }
    }

    private static string LeerRutaDesdeTxt()
    {
        try
        {
            if (File.Exists(txtFilePath))
            {
                return File.ReadAllText(txtFilePath).Trim();
            }
            else
            {
                Console.WriteLine("El archivo de texto no existe: " + txtFilePath);
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error al leer la ruta desde el archivo TXT: " + ex.Message);
            return null;
        }
    }
}
