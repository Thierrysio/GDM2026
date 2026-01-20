using GDM2026.Views;

namespace GDM2026
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Ne pas enregistrer MainPage, SplashPage, HomePage ici
            // car ils sont d�j� d�clar�s dans AppShell.xaml via ShellContent

            Routing.RegisterRoute(nameof(CategoryDetailPage), typeof(CategoryDetailPage));
            Routing.RegisterRoute(nameof(EvenementPage), typeof(EvenementPage));
            Routing.RegisterRoute(nameof(ImageUploadPage), typeof(ImageUploadPage));
            Routing.RegisterRoute(nameof(ActualitePage), typeof(ActualitePage));
            Routing.RegisterRoute(nameof(MessagesPage), typeof(MessagesPage));
            Routing.RegisterRoute(nameof(PartnersPage), typeof(PartnersPage));
            Routing.RegisterRoute(nameof(ReservationsPage), typeof(ReservationsPage));
            Routing.RegisterRoute(nameof(ProductsPage), typeof(ProductsPage));
            Routing.RegisterRoute(nameof(CataloguePage), typeof(CataloguePage));
            Routing.RegisterRoute(nameof(UsersPage), typeof(UsersPage));
            Routing.RegisterRoute(nameof(CommentsPage), typeof(CommentsPage));
            Routing.RegisterRoute(nameof(ProductsEditPage), typeof(ProductsEditPage));
            Routing.RegisterRoute(nameof(PromoPage), typeof(PromoPage));
            Routing.RegisterRoute(nameof(PlanningPage), typeof(PlanningPage));
            Routing.RegisterRoute(nameof(HistoirePage), typeof(HistoirePage));
            Routing.RegisterRoute(nameof(QrCodeScannerPage), typeof(QrCodeScannerPage));
            Routing.RegisterRoute(nameof(UtiliserPointsFidelitePage), typeof(UtiliserPointsFidelitePage));
            Routing.RegisterRoute(nameof(AjouterPointsFidelitePage), typeof(AjouterPointsFidelitePage));
        }
    }
}
