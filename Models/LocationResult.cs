﻿using System;
using DISMOGT_REPORTES.Services.LocationFusion;
using Microsoft.Maui.Devices.Sensors;

namespace DISMOGT_REPORTES.Models
{
    public class LocationResult
    {
        public Location Location { get; set; }
        public bool IsSuspicious { get; set; }
        public string SuspiciousReason { get; set; }

        // Nuevas propiedades para información de movimiento
        public bool IsMoving { get; set; }
        public MovementContext MovementContext { get; set; }
        public string MovementContextName { get; set; }
    }
}