using Microsoft.Maui.Devices.Sensors;
using SQLite;
using System;

namespace DISMOGT_REPORTES.Services.LocationFusion
{
    /// <summary>
    /// Enum para representar el contexto de movimiento del usuario
    /// </summary>
    public enum MovementContext
    {
        Unknown,
        Stationary,
        Walking,
        Vehicle,
        Indoor
    }

    /// <summary>
    /// Clase para almacenar métricas de rendimiento de la fusión de ubicación
    /// </summary>
    public class LocationPerformanceMetric
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public double OriginalAccuracy { get; set; }
        public double FinalAccuracy { get; set; }
        public double AccuracyImprovement { get; set; }
        public double ProcessingTimeMs { get; set; }
        public string MovementContext { get; set; }
        public bool WasSuspicious { get; set; }
        public string SuspiciousReason { get; set; }
    }

}