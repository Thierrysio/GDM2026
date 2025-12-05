using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Storage;

namespace GDM2026.Services;

/// <summary>
/// Centralise la gestion des exceptions non interceptées pour éviter les crashs en production.
/// </summary>
public static class GlobalErrorHandler
{
    private const string CrashLogFileName = "last-crash.log";

    public static void Register(Application? app)
    {
        try
        {
            if (app?.Dispatcher != null)
            {
                app.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
            }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }
        catch
        {
            // L'initialisation du handler ne doit jamais interrompre le démarrage de l'app.
        }
    }

    private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        HandleException(e.Exception, "Dispatcher");
        e.Handled = true;
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        HandleException(e.ExceptionObject as Exception, "AppDomain");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleException(e.Exception, "TaskScheduler");
        e.SetObserved();
    }

    private static async void HandleException(Exception? exception, string source)
    {
        if (exception == null)
        {
            return;
        }

        try
        {
            var logPath = Path.Combine(FileSystem.AppDataDirectory, CrashLogFileName);
            var logEntry = $"[{DateTimeOffset.Now:u}] ({source}) {exception}";

            File.WriteAllText(logPath, logEntry);
            Debug.WriteLine(logEntry);

            if (Application.Current?.Dispatcher != null)
            {
                await Application.Current.Dispatcher.DispatchAsync(async () =>
                {
                    var page = Application.Current.Windows.FirstOrDefault()?.Page;
                    if (page != null)
                    {
                        await page.DisplayAlert("Erreur inattendue", "Une erreur est survenue. L'application continue à fonctionner.", "OK");
                    }
                });
            }
        }
        catch
        {
            // En dernier recours, on évite toute remontée d'exception supplémentaire.
        }
    }
}
