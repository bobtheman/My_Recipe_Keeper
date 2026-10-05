using System.Diagnostics;

namespace My_Recipe_Keeper
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Otherwise escaped managed exceptions show up as nothing but a silent kill in logcat.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Debug.WriteLine($"[UNHANDLED] {e.ExceptionObject}");

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Debug.WriteLine($"[UNOBSERVED TASK] {e.Exception}");
                e.SetObserved();
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "My Recipe Keeper" };
        }
    }
}
