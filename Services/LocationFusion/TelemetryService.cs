using SQLite;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DISMOGT_REPORTES.Services.LocationFusion
{
    /// <summary>
    /// Servicio encargado de recopilar y analizar métricas de rendimiento
    /// </summary>
    public class TelemetryService
    {
        private readonly string _dbPath;
        private bool _metricsEnabled = true;
        private List<LocationPerformanceMetric> _recentMetrics = new List<LocationPerformanceMetric>();
        private int _consecutiveImprovements = 0;
        private int _consecutiveWorsenings = 0;

        /// <summary>
        /// Constructor del servicio de telemetría
        /// </summary>
        public TelemetryService(string storagePath)
        {
            _dbPath = Path.Combine(storagePath, "location_metrics.db");
            InitializeDatabase();
        }

        /// <summary>
        /// Inicializa la base de datos para métricas
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                var directory = Path.GetDirectoryName(_dbPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var connection = new SQLiteConnection(_dbPath))
                {
                    connection.CreateTable<LocationPerformanceMetric>();
                    Console.WriteLine("✅ Base de datos de métricas inicializada en: " + _dbPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al inicializar base de datos de métricas: {ex.Message}");
                _metricsEnabled = false;
            }
        }

        /// <summary>
        /// Registra una nueva métrica de rendimiento
        /// </summary>
        public void RecordPerformanceMetric(
            double originalAccuracy,
            double finalAccuracy,
            double processingTimeMs,
            bool wasSuspicious,
            string suspiciousReason,
            MovementContext context)
        {
            if (!_metricsEnabled)
                return;

            try
            {
                // Calcular mejora de precisión
                double accuracyImprovement = originalAccuracy - finalAccuracy;

                // Crear la métrica
                var metric = new LocationPerformanceMetric
                {
                    Timestamp = DateTime.Now,
                    OriginalAccuracy = originalAccuracy,
                    FinalAccuracy = finalAccuracy,
                    AccuracyImprovement = accuracyImprovement,
                    ProcessingTimeMs = processingTimeMs,
                    MovementContext = context.ToString(),
                    WasSuspicious = wasSuspicious,
                    SuspiciousReason = suspiciousReason
                };

                // Guardar en la base de datos
                using (var connection = new SQLiteConnection(_dbPath))
                {
                    connection.Insert(metric);
                }

                // Mantener un registro de métricas recientes en memoria
                _recentMetrics.Add(metric);
                if (_recentMetrics.Count > 50) // Limitar a las últimas 50
                {
                    _recentMetrics.RemoveAt(0);
                }

                // Actualizar contadores de consecutivos para ajuste de parámetros
                if (accuracyImprovement > 0)
                {
                    _consecutiveImprovements++;
                    _consecutiveWorsenings = 0;
                }
                else if (accuracyImprovement < 0)
                {
                    _consecutiveImprovements = 0;
                    _consecutiveWorsenings++;
                }
                else
                {
                    // Sin cambio, mantener contadores
                }

                // Log de la métrica para depuración
                Console.WriteLine($"📊 Métrica registrada - Mejora: {accuracyImprovement:F2}m, " +
                             $"Tiempo: {processingTimeMs:F1}ms, Contexto: {context}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al registrar métrica: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene estadísticas de rendimiento
        /// </summary>
        public Dictionary<string, double> GetPerformanceStats()
        {
            var stats = new Dictionary<string, double>();

            try
            {
                if (_recentMetrics.Count == 0)
                    return stats;

                // Estadísticas generales
                stats["AverageImprovement"] = _recentMetrics.Average(m => m.AccuracyImprovement);
                stats["MedianImprovement"] = GetMedian(_recentMetrics.Select(m => m.AccuracyImprovement).ToList());
                stats["AverageProcessingTime"] = _recentMetrics.Average(m => m.ProcessingTimeMs);
                stats["SuccessRate"] = _recentMetrics.Count(m => m.AccuracyImprovement > 0) * 100.0 / _recentMetrics.Count;
                stats["SuspiciousRate"] = _recentMetrics.Count(m => m.WasSuspicious) * 100.0 / _recentMetrics.Count;

                // Estadísticas por contexto
                var contexts = _recentMetrics.Select(m => m.MovementContext).Distinct();
                foreach (var context in contexts)
                {
                    var contextMetrics = _recentMetrics.Where(m => m.MovementContext == context).ToList();
                    if (contextMetrics.Count > 0)
                    {
                        stats[$"Improvement_{context}"] = contextMetrics.Average(m => m.AccuracyImprovement);
                        stats[$"ProcessingTime_{context}"] = contextMetrics.Average(m => m.ProcessingTimeMs);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al calcular estadísticas: {ex.Message}");
            }

            return stats;
        }

        /// <summary>
        /// Obtiene los contadores de mejoras y empeoramientos consecutivos
        /// </summary>
        public (int improvements, int worsenings) GetConsecutiveCounters()
        {
            return (_consecutiveImprovements, _consecutiveWorsenings);
        }

        /// <summary>
        /// Calcula la mediana de una lista de valores
        /// </summary>
        private double GetMedian(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0;

            var sortedValues = values.OrderBy(v => v).ToList();
            int count = sortedValues.Count;

            if (count % 2 == 0)
            {
                // Si es par, promedio de los dos del medio
                return (sortedValues[count / 2 - 1] + sortedValues[count / 2]) / 2;
            }
            else
            {
                // Si es impar, el del medio
                return sortedValues[count / 2];
            }
        }
    }
}