using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DISMOGT_REPORTES.Models;
using SQLite;

namespace DISMOGT_REPORTES.Services
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
                // Verifica el path de la base de datos
                var dbPath = "/storage/emulated/0/DISMOGTREPORTES/pending_locations.db";

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
