using GDM2026.Views;

namespace GDM2026
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
            Routing.RegisterRoute(nameof(SplashPage), typeof(SplashPage));
            Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
            Routing.RegisterRoute(nameof(CategoryDetailPage), typeof(CategoryDetailPage));
            Routing.RegisterRoute(nameof(OrderStatusPage), typeof(OrderStatusPage));
            Routing.RegisterRoute(nameof(ImageUploadPage), typeof(ImageUploadPage));
            Routing.RegisterRoute(nameof(ActualitePage), typeof(ActualitePage));
            Routing.RegisterRoute(nameof(MessagesPage), typeof(MessagesPage));
            Routing.RegisterRoute(nameof(PartnersPage), typeof(PartnersPage));
        }
    }
}
