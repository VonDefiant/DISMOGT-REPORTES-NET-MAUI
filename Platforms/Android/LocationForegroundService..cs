using Android.App;
using Android.Content;
using Android.OS;
using DISMO_REPORTES.Services;
using Shiny.Jobs;
using Shiny.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Android.Content.PM;
using AndroidX.Core.App;

namespace DISMOGT_REPORTES.Platforms.Android
{
    [Service(ForegroundServiceType = ForegroundService.TypeLocation)]
    public class LocationForegroundService : Service
    {
        private Timer _timer;
        private bool _isRunning;
        private const int NOTIFICATION_ID = 1001;
        private const string CHANNEL_ID = "location_service_channel";

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (!_isRunning)
            {
                _isRunning = true;
                var notification = CreateNotification();
                StartForeground(NOTIFICATION_ID, notification);
                InitializePeriodicLocationJob();
                Console.WriteLine("⏰ Servicio de ubicación en primer plano iniciado");
            }

            return StartCommandResult.Sticky;
        }

        private void InitializePeriodicLocationJob()
        {
            _timer = new Timer(async (state) =>
            {
                try
                {
                    Console.WriteLine("⏰ Servicio en primer plano: ejecutando LocationJob automáticamente");
                    var locationJob = Host.Current.Services.GetRequiredService<LocationJob>();
                    var jobInfo = new JobInfo(
                        "LocationJob",
                        typeof(LocationJob),
                        false,
                        null,
                        InternetAccess.None,
                        false,
                        false,
                        false
                    );

                    await locationJob.Run(jobInfo, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error en servicio en primer plano: {ex.Message}");
                }
            },
            null,
            TimeSpan.FromSeconds(30),     // Primera ejecución después de 30 segundos
            TimeSpan.FromMinutes(15));    // Después cada 15 minutos
        }

        private Notification CreateNotification()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(
                    CHANNEL_ID,
                    "Servicio de ubicación",
                    NotificationImportance.Low);

                var notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);
            }

            var pendingIntent = PendingIntent.GetActivity(
                this,
                0,
                new Intent(this, typeof(MainActivity)),
                PendingIntentFlags.Immutable);

            var notification = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle("DISMOGT REPORTES")
                .SetContentText("")
                .SetSmallIcon(Resource.Drawable.notification_icon_background)
                .SetOngoing(true)
                .SetContentIntent(pendingIntent)
                .Build();

            return notification;
        }

        public override void OnDestroy()
        {
            _timer?.Dispose();
            _isRunning = false;
            base.OnDestroy();
        }
    }
}