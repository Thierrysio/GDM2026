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
            Routing.RegisterRoute(nameof(EvenementPage), typeof(EvenementPage));
            Routing.RegisterRoute(nameof(OrderStatusPage), typeof(OrderStatusPage));
            Routing.RegisterRoute(nameof(ImageUploadPage), typeof(ImageUploadPage));
            Routing.RegisterRoute(nameof(ActualitePage), typeof(ActualitePage));
            Routing.RegisterRoute(nameof(MessagesPage), typeof(MessagesPage));
            Routing.RegisterRoute(nameof(PartnersPage), typeof(PartnersPage));
            Routing.RegisterRoute(nameof(ReservationsPage), typeof(ReservationsPage));
            Routing.RegisterRoute(nameof(ProductsPage), typeof(ProductsPage));
            Routing.RegisterRoute(nameof(UsersPage), typeof(UsersPage));
            Routing.RegisterRoute(nameof(CommentsPage), typeof(CommentsPage));
            Routing.RegisterRoute(nameof(ProductsEditPage), typeof(ProductsEditPage));
            Routing.RegisterRoute(nameof(LoyaltyQrPage), typeof(LoyaltyQrPage));
        }
    }
}
