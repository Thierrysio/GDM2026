using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace GDM2026.Services;

public static class DialogService
{
    public static async Task DisplayAlertAsync(string title, string message, string cancel)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return;
        }

        await page.DisplayAlertAsync(title, message, cancel);
    }

    public static async Task<bool> DisplayConfirmationAsync(string title, string message, string accept, string cancel)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return false;
        }

        return await page.DisplayAlert(title, message, accept, cancel);
    }
}
