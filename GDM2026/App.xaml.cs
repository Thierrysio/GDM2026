using GDM2026.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GDM2026
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            GlobalErrorHandler.Register(this);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}
