using Android.Hardware;
using Android.Runtime;
using System;

namespace DISMOGT_REPORTES.Services.LocationFusion
{
    /// <summary>
    /// Listener para el sensor de acelerómetro
    /// </summary>
    public class FusionAccelerometerListener : Java.Lang.Object, ISensorEventListener
    {
        public event Action<float[]> SensorChanged;

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            // No es necesario implementar
        }

        public void OnSensorChanged(SensorEvent e)
        {
            if (e.Sensor.Type == SensorType.Accelerometer)
            {
                SensorChanged?.Invoke(e.Values.ToArray());
            }
        }
    }

    /// <summary>
    /// Listener para el sensor de magnetómetro
    /// </summary>
    public class FusionMagnetometerListener : Java.Lang.Object, ISensorEventListener
    {
        public event Action<float[]> SensorChanged;

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            // No es necesario implementar
        }

        public void OnSensorChanged(SensorEvent e)
        {
            if (e.Sensor.Type == SensorType.MagneticField)
            {
                SensorChanged?.Invoke(e.Values.ToArray());
            }
        }
    }

    /// <summary>
    /// Listener para el sensor de giroscopio
    /// </summary>
    public class FusionGyroscopeListener : Java.Lang.Object, ISensorEventListener
    {
        public event Action<float[]> SensorChanged;

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            // No es necesario implementar
        }

        public void OnSensorChanged(SensorEvent e)
        {
            if (e.Sensor.Type == SensorType.Gyroscope)
            {
                SensorChanged?.Invoke(e.Values.ToArray());
            }
        }
    }
}