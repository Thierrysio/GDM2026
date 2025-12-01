using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace GDM2026.Services;

public interface INavigationService
{
    Task GoToAsync(string route, IDictionary<string, object>? parameters = null, bool absolute = false, bool animate = true);
    bool IsShellRoute(string route);
}

public class NavigationService : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null, bool absolute = false, bool animate = true)
    {
        if (string.IsNullOrWhiteSpace(route) || Shell.Current is null)
        {
            return Task.CompletedTask;
        }

        var finalRoute = absolute ? $"//{route}" : route;
        return parameters is null
            ? Shell.Current.GoToAsync(finalRoute, animate)
            : Shell.Current.GoToAsync(finalRoute, parameters);
    }

    public bool IsShellRoute(string route)
    {
        if (Shell.Current is null || string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        foreach (var shellItem in Shell.Current.Items)
        {
            if (RouteEquals(shellItem.Route, route))
            {
                return true;
            }

            foreach (var shellSection in shellItem.Items)
            {
                if (RouteEquals(shellSection.Route, route))
                {
                    return true;
                }

                foreach (var shellContent in shellSection.Items)
                {
                    if (RouteEquals(shellContent.Route, route))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool RouteEquals(string? candidate, string route) =>
        !string.IsNullOrWhiteSpace(candidate) &&
        string.Equals(candidate, route, StringComparison.Ordinal);
}
