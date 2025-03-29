using System;
using System.Threading.Tasks;
using Android.App;
using Microsoft.Maui.Devices.Sensors;
using DISMOGT_REPORTES.Models;
using DISMOGT_REPORTES.Services.LocationFusion;
using Android.Content;
using System.Collections.Generic;

namespace DISMOGT_REPORTES.Services
{
    /// <summary>
    /// Servicio principal que actúa como interfaz con el sistema modular de fusión de ubicación
    /// </summary>
    public class LocationFusionService : IDisposable
    {
        private readonly LocationFusion.LocationFusionService _fusionService;
        private readonly Context _context;
        private bool _isInitialized = false;

        // New sampling frequency property
        public double SamplingFrequencyMs { get; set; } = 500; // 250ms (4 samples per second)

        /// <summary>
        /// Constructor del servicio de fusión
        /// </summary>
        public LocationFusionService(Context context)
        {
            _context = context;

            try
            {
                // Inicializar el servicio de fusión modular
                _fusionService = new LocationFusion.LocationFusionService(context);

                // Configure ultramínimo filtering and set sampling frequency
                _fusionService.SetFilteringLevel(0.02);  // Only 5% of normal filtering intensity
                _fusionService.SetSamplingFrequency(SamplingFrequencyMs);

                _isInitialized = true;
                Console.WriteLine($"✅ Servicio de fusión de ubicación inicializado con filtrado ultramínimo (5%) y muestreo cada {SamplingFrequencyMs}ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al inicializar servicio de fusión: {ex.Message}");
            }
        }

        /// <summary>
        /// Método principal para obtener una ubicación mejorada con fusión de datos
        /// </summary>
        public async Task<LocationResult> GetFusedLocationAsync(Location location)
        {
            if (!_isInitialized || location == null)
                return new LocationResult { Location = location, IsSuspicious = false };

            try
            {
                // Set the latest sampling frequency in case it was changed
                _fusionService.SetSamplingFrequency(SamplingFrequencyMs);

                return await _fusionService.GetFusedLocationAsync(location);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en fusión de ubicación: {ex.Message}");
                return new LocationResult { Location = location, IsSuspicious = false };
            }
        }

        /// <summary>
        /// Configura el nivel de alerta para la detección de anomalías
        /// </summary>
        public void ConfigureAnomalyAlerts(AnomalyAlertLevel level)
        {
            if (_isInitialized)
            {
                _fusionService.ConfigureAnomalyAlerts(level);
            }
        }

        /// <summary>
        /// Configura el nivel de filtrado (0.0 a 1.0, donde 0.0 es prácticamente sin filtrado)
        /// </summary>
        public void SetFilteringLevel(double level)
        {
            if (_isInitialized)
            {
                _fusionService.SetFilteringLevel(level);
                Console.WriteLine($"🔧 Nivel de filtrado establecido a: {level:P0}");
            }
        }

        /// <summary>
        /// Obtiene estadísticas de rendimiento del sistema de fusión
        /// </summary>
        public Dictionary<string, double> GetPerformanceStats()
        {
            if (_isInitialized)
            {
                return _fusionService.GetPerformanceStats();
            }
            return new Dictionary<string, double>();
        }

        /// <summary>
        /// Libera los recursos y cancela las suscripciones a sensores
        /// </summary>
        public void Dispose()
        {
            if (_isInitialized)
            {
                _fusionService?.Dispose();
            }
        }
    }
}