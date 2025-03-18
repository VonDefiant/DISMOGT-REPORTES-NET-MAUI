using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DISMOGT_REPORTES.Models
{
    public class PendingLocation
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Timestamp { get; set; }
        public bool IsSuspicious { get; set; }

        [NotNull]
        public string IdRuta { get; set; }
        public double BatteryLevel { get; set; }

        // Nuevo campo para almacenar los datos del reporte serializados como JSON
        public string ReportDataJson { get; set; }

        // Nuevo campo para almacenar el motivo de la sospecha
        public string SuspiciousReason { get; set; }

        // Nuevos campos para estado de movimiento
        public bool IsMoving { get; set; }
        public string MovementContext { get; set; }
    }
}