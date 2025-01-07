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
        public string IdRuta { get; set; }
        public double BatteryLevel { get; set; }

    }
}
