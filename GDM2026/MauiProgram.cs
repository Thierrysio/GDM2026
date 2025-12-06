using GDM2026.Services;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;

namespace GDM2026
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", true);

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton(sp => AppHttpClientFactory.Create());

            builder.Services.AddSingleton<IApis, Apis>();
            builder.Services.AddSingleton<ImageUploadService>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
