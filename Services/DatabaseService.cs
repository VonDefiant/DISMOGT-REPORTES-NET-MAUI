using SQLite;
using System.IO;
using DISMOGT_REPORTES.Models;
using System;

namespace DISMOGT_REPORTES.Services
{
    public class DatabaseService
    {
        private static SQLiteConnection _database;
        private static readonly object _dbLock = new object();

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
                lock (_dbLock)
                {
                    var dbPath = Path.Combine(FileSystem.Current.AppDataDirectory, "DM.db");

                    // Asegurar que el directorio de la BD existe
                    Directory.CreateDirectory(Path.GetDirectoryName(dbPath));

                    // Inicializa la base de datos SQLite
                    _database = new SQLiteConnection(dbPath);

                    // Crea las tablas si no existen
                    _database.CreateTable<PendingLocation>();
                    _database.CreateTable<UniqueToken>();

                    Console.WriteLine("Base de datos inicializada correctamente.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al inicializar la base de datos: {ex.Message}");
            }
        }

        public static string GetOrCreateUniqueToken()
        {
            try
            {
                lock (_dbLock)
                {
                    var tokenEntry = Database.Find<UniqueToken>("Token");

                    if (tokenEntry == null)
                    {
                        string newToken = Guid.NewGuid().ToString();
                        Database.Insert(new UniqueToken { Token = newToken });

                        Console.WriteLine($" Token único generado: {newToken}");
                        return newToken;
                    }

                    return tokenEntry.Token;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error al obtener o crear el token único: {ex.Message}");
                return string.Empty;
            }
        }

    }

    public class UniqueToken
    {
        [PrimaryKey]
        public string Token { get; set; }
    }
}
