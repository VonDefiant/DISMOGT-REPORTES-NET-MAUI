using Android.App;
using Android.Runtime;
using Firebase;

namespace DISMOGT_REPORTES
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();
            FirebaseApp.InitializeApp(this);
            Console.WriteLine("🔥 Firebase inicializado.");
        }


        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
