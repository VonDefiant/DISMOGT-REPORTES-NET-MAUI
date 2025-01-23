using SQLite;
using System.IO;
using DISMOGT_REPORTES.Models;
using System;

namespace DISMO_REPORTES.Services
{
    public class DatabaseService
    {
        private static SQLiteConnection _database;

        public static SQLiteConnection Database
        {
            get
            {
                if (_database == null)
                {
                    InitializeDatabase();
                }
                return _database;
            }
        }

        public static void InitializeDatabase()
        {
            try
            {
                // Obtiene el path de la base de datos utilizando el almacenamiento local de MAUI
                var dbPath = Path.Combine(FileSystem.Current.AppDataDirectory, "DM.db");

                // Inicializa la conexión SQLite y crea la tabla si no existe
                _database = new SQLiteConnection(dbPath);
                _database.CreateTable<PendingLocation>();

                Console.WriteLine("Base de datos inicializada y tabla PendingLocation creada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al inicializar la base de datos: {ex.Message}");
            }
        }
    }
}
