using Microsoft.Extensions.Logging;

using Syncfusion.Maui.Core.Hosting;

namespace MauiFront
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCekx3QXxbf1x2ZFZMZFtbQXZPMyBoS35RcEVnWXhec3BSQ2BcWER2VEFe");

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureSyncfusionCore() 
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}