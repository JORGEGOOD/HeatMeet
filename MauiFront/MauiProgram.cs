using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Core.Hosting;

namespace MauiFront
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("NDMwNzcwMUAzMjMyMmUzMDJlMzBGcFZCUCtpYkY4Mmw2NlBYL25jVDVFNjZjSCtLcmFVUWl2bUNJckJ6MUVRPQ==");

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureSyncfusionCore()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}