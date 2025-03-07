using Android.App;
using Android.Content;
using Android.OS;

namespace DISMOGT_REPORTES.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted })]
    public class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action == Intent.ActionBootCompleted)
            {
                // Iniciar el servicio al arrancar el dispositivo
                var serviceIntent = new Intent(context, typeof(LocationForegroundService));
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    context.StartForegroundService(serviceIntent);
                }
                else
                {
                    context.StartService(serviceIntent);
                }

                System.Console.WriteLine("📱 Dispositivo reiniciado: Servicio de ubicación iniciado");
            }
        }
    }
}