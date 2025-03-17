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
        private const int DATABASE_VERSION = 2; // Incrementar la versión cuando se modifica la estructura

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

                    // Obtener la versión actual de la base de datos
                    var currentVersion = GetDatabaseVersion();

                    // Crea las tablas si no existen
                    _database.CreateTable<PendingLocation>();
                    _database.CreateTable<UniqueToken>();

                    // Si la versión es anterior a la actual, realizar migraciones
                    if (currentVersion < DATABASE_VERSION)
                    {
                        PerformMigrations(currentVersion);
                    }

                    Console.WriteLine("Base de datos inicializada correctamente.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al inicializar la base de datos: {ex.Message}");
            }
        }

        private static int GetDatabaseVersion()
        {
            try
            {
                // Crear tabla de versiones si no existe
                _database.Execute("CREATE TABLE IF NOT EXISTS VersionInfo (Version INTEGER)");

                // Obtener la versión actual
                var versionInfo = _database.Query<VersionInfo>("SELECT Version FROM VersionInfo");
                if (versionInfo.Count == 0)
                {
                    // Si no hay versión, asumimos que es la versión 1
                    _database.Execute("INSERT INTO VersionInfo (Version) VALUES (1)");
                    return 1;
                }
                return versionInfo[0].Version;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener versión de la base de datos: {ex.Message}");
                return 1;
            }
        }

        private static void PerformMigrations(int currentVersion)
        {
            // Migración de la versión 1 a la 2 (agregar ReportDataJson a PendingLocation)
            if (currentVersion < 2)
            {
                try
                {
                    Console.WriteLine("Migrando base de datos a la versión 2...");

                    // Intentar agregar la columna ReportDataJson a la tabla PendingLocation
                    try
                    {
                        _database.Execute("ALTER TABLE PendingLocation ADD COLUMN ReportDataJson TEXT");
                        Console.WriteLine("Columna ReportDataJson agregada correctamente a PendingLocation");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al agregar columna: {ex.Message}");

                        // Si falla, es posible que necesitemos recrear la tabla
                        RecreateTable();
                    }

                    // Actualizar la versión en la base de datos
                    _database.Execute("UPDATE VersionInfo SET Version = 2");
                    Console.WriteLine("Base de datos migrada correctamente a la versión 2");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en la migración: {ex.Message}");
                }
            }
        }

        private static void RecreateTable()
        {
            try
            {
                // Crear una tabla temporal con la nueva estructura
                _database.Execute(@"
                    CREATE TABLE PendingLocation_Temp (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Latitude REAL,
                        Longitude REAL,
                        Timestamp TEXT,
                        IsSuspicious INTEGER,
                        IdRuta TEXT NOT NULL,
                        BatteryLevel REAL,
                        ReportDataJson TEXT
                    )");

                // Copiar datos de la tabla antigua a la nueva
                _database.Execute(@"
                    INSERT INTO PendingLocation_Temp (Id, Latitude, Longitude, Timestamp, IsSuspicious, IdRuta, BatteryLevel)
                    SELECT Id, Latitude, Longitude, Timestamp, IsSuspicious, IdRuta, BatteryLevel FROM PendingLocation");

                // Eliminar la tabla antigua
                _database.Execute("DROP TABLE PendingLocation");

                // Renombrar la tabla temporal
                _database.Execute("ALTER TABLE PendingLocation_Temp RENAME TO PendingLocation");

                Console.WriteLine("Tabla PendingLocation recreada correctamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al recrear tabla: {ex.Message}");
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

    public class VersionInfo
    {
        public int Version { get; set; }
    }
}