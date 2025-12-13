using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class HomePageViewModel : BaseViewModel
{
    private readonly SessionService _sessionService = new();

    public HomePageViewModel()
    {
        OrderStatuses = new ObservableCollection<OrderStatusDisplay>();
        Categories = new ObservableCollection<CategoryCard>();

        OrderStatusSelectedCommand = new Command<OrderStatusDisplay>(async status => await NavigateToOrderStatusAsync(status));
        CategorySelectedCommand = new Command<CategoryCard>(async card => await NavigateToCategoryAsync(card));
    }

    public ObservableCollection<OrderStatusDisplay> OrderStatuses { get; }

    public ObservableCollection<CategoryCard> Categories { get; }

    public ICommand OrderStatusSelectedCommand { get; }

    public ICommand CategorySelectedCommand { get; }

    public string WelcomeText { get; private set; } = "Bienvenue";

    public async Task InitializeAsync()
    {
        await LoadWelcomeTextAsync().ConfigureAwait(false);
        await LoadOrderStatusesAsync().ConfigureAwait(false);
        await LoadCategoriesAsync().ConfigureAwait(false);
    }

    private async Task LoadWelcomeTextAsync()
    {
        if (await _sessionService.LoadAsync().ConfigureAwait(false) && _sessionService.User != null)
        {
            var user = _sessionService.User;
            var name = user?.Prenom ?? user?.Nom ?? user?.UserIdentifier;
            WelcomeText = string.IsNullOrWhiteSpace(name) ? "Bienvenue" : $"Bienvenue {name}";
            OnPropertyChanged(nameof(WelcomeText));
        }
    }

    private Task LoadOrderStatusesAsync()
    {
        if (OrderStatuses.Count == 0)
        {
            OrderStatuses.Add(new OrderStatusDisplay("Confirmée", 0));
            OrderStatuses.Add(new OrderStatusDisplay("En cours de traitement", 0));
            OrderStatuses.Add(new OrderStatusDisplay("Traitée", 0));
            OrderStatuses.Add(new OrderStatusDisplay("Livrée", 0));
            OrderStatuses.Add(new OrderStatusDisplay("A confirmer", 0));
        }

        return Task.CompletedTask;
    }

    private Task LoadCategoriesAsync()
    {
        if (Categories.Count == 0)
        {
            Categories.Add(new CategoryCard("Produits", "Gérez les produits et promotions"));
            Categories.Add(new CategoryCard("Commandes", "Consultez les commandes par état"));
            Categories.Add(new CategoryCard("Réservations", "Suivez les réservations en cours"));
            Categories.Add(new CategoryCard("Utilisateurs", "Administrer les comptes clients"));
        }

        return Task.CompletedTask;
    }

    private static Task NavigateToOrderStatusAsync(OrderStatusDisplay? status)
    {
        if (status == null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync($"//{nameof(OrderStatusPage)}?status={Uri.EscapeDataString(status.Status)}");
    }

    private static Task NavigateToCategoryAsync(CategoryCard? card)
    {
        if (card == null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync($"//{nameof(CategoryDetailPage)}", new Dictionary<string, object>
        {
            ["card"] = card
        });
    }
}
