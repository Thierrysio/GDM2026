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
            
            // S'abonner à l'événement de token expiré pour rediriger vers le login
            Apis.TokenExpired += OnTokenExpired;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        private async void OnTokenExpired(object? sender, EventArgs e)
        {
            // Effacer la session
            var sessionService = new SessionService();
            await sessionService.ClearAsync();

            // Rediriger vers la page de login sur le thread UI
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Session expirée",
                        "Votre session a expiré. Veuillez vous reconnecter.",
                        "OK");

                    await Shell.Current.GoToAsync($"//{nameof(MainPage)}", animate: false);
                }
            });
        }
    }
}
