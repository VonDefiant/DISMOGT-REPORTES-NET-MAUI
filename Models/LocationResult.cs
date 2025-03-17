using System;
using Microsoft.Maui.Devices.Sensors;

namespace DISMOGT_REPORTES.Models
{
    public class LocationResult
    {
        public Location Location { get; set; }
        public bool IsSuspicious { get; set; }
        public string SuspiciousReason { get; set; }
    }
}