using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace GDM2026.ViewModels;

public class CategoryDetailViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private CategoryCard? _card;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _hint = string.Empty;
    private bool _isSuperCategoryPage;
    private bool _hasLoadedSuperCategories;
    private bool _sessionLoaded;
    private string _superCategoryStatus = "Chargement des super catégories…";
    private string _newSuperCategoryName = string.Empty;
    private string _newSuperCategoryDescription = string.Empty;
    private string _newSuperCategoryImage = string.Empty;
    private string _newSuperCategoryProducts = string.Empty;

    public CategoryDetailViewModel()
    {
        SuperCategories = new ObservableCollection<SuperCategory>();
        CreateSuperCategoryCommand = new Command(async () => await CreateSuperCategoryAsync(), CanCreateSuperCategory);
    }

    public ObservableCollection<SuperCategory> SuperCategories { get; }

    public ICommand CreateSuperCategoryCommand { get; }

    public CategoryCard? Card
    {
        get => _card;
        set
        {
            if (SetProperty(ref _card, value))
            {
                ApplyCard(value);
            }
        }
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Hint
    {
        get => _hint;
        set => SetProperty(ref _hint, value);
    }

    public bool IsSuperCategoryPage
    {
        get => _isSuperCategoryPage;
        set => SetProperty(ref _isSuperCategoryPage, value);
    }

    public string SuperCategoryStatus
    {
        get => _superCategoryStatus;
        set
        {
            if (SetProperty(ref _superCategoryStatus, value))
            {
                OnPropertyChanged(nameof(HasSuperCategoryStatus));
            }
        }
    }

    public bool HasSuperCategoryStatus => !string.IsNullOrWhiteSpace(SuperCategoryStatus);

    public string NewSuperCategoryName
    {
        get => _newSuperCategoryName;
        set
        {
            if (SetProperty(ref _newSuperCategoryName, value))
            {
                RefreshCreateAvailability();
            }
        }
    }

    public string NewSuperCategoryDescription
    {
        get => _newSuperCategoryDescription;
        set
        {
            if (SetProperty(ref _newSuperCategoryDescription, value))
            {
                RefreshCreateAvailability();
            }
        }
    }

    public string NewSuperCategoryImage
    {
        get => _newSuperCategoryImage;
        set
        {
            if (SetProperty(ref _newSuperCategoryImage, value))
            {
                RefreshCreateAvailability();
            }
        }
    }

    public string NewSuperCategoryProducts
    {
        get => _newSuperCategoryProducts;
        set => SetProperty(ref _newSuperCategoryProducts, value);
    }

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (!IsSuperCategoryPage)
        {
            return;
        }

        if (!_sessionLoaded)
        {
            await _sessionService.LoadAsync().ConfigureAwait(false);
            _apis.SetBearerToken(_sessionService.AuthToken);
            _sessionLoaded = true;
        }

        if (_hasLoadedSuperCategories)
        {
            return;
        }

        await LoadSuperCategoriesAsync(ct).ConfigureAwait(false);
    }

    public void ApplyCard(CategoryCard? card)
    {
        if (card == null)
        {
            Title = "Catégorie introuvable";
            Description = "Impossible de charger les détails de cette catégorie.";
            Hint = "Retournez à l'accueil et sélectionnez une catégorie.";
            IsSuperCategoryPage = false;
            return;
        }

        Title = card.Title;
        Description = card.Description;
        Hint = $"Vous êtes sur la page {card.Title}. Ajoutez ici les fonctionnalités spécifiques à cette catégorie.";
        IsSuperCategoryPage = string.Equals(card.Title, "Super categories", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Title, "Super catégories", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanCreateSuperCategory()
    {
        return !IsBusy
            && IsSuperCategoryPage
            && !string.IsNullOrWhiteSpace(NewSuperCategoryName)
            && !string.IsNullOrWhiteSpace(NewSuperCategoryDescription)
            && !string.IsNullOrWhiteSpace(NewSuperCategoryImage);
    }

    private void RefreshCreateAvailability()
    {
        (CreateSuperCategoryCommand as Command)?.ChangeCanExecute();
    }

    private async Task LoadSuperCategoriesAsync(CancellationToken ct = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SuperCategoryStatus = "Chargement des super catégories…";

            var result = await _apis.GetListAsync<SuperCategory>("/api/crud/categorieparent/list", ct).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SuperCategories.Clear();
                foreach (var item in result)
                {
                    SuperCategories.Add(item);
                }

                _hasLoadedSuperCategories = true;
                SuperCategoryStatus = SuperCategories.Any()
                    ? ""
                    : "Aucune super catégorie n'est encore configurée.";
            });
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SuperCategoryStatus = "Le chargement des super catégories a expiré.";
            });
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SuperCategoryStatus = "Impossible de récupérer les super catégories.";
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SuperCategoryStatus = "Une erreur est survenue pendant le chargement.";
            });
        }
        finally
        {
            IsBusy = false;
            RefreshCreateAvailability();
        }
    }

    private async Task CreateSuperCategoryAsync()
    {
        if (IsBusy || !IsSuperCategoryPage)
        {
            return;
        }

        try
        {
            IsBusy = true;
            RefreshCreateAvailability();
            SuperCategoryStatus = "Création de la super catégorie en cours…";

            var productIds = NewSuperCategoryProducts
                .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p =>
                {
                    if (int.TryParse(p.Trim(), out var id))
                    {
                        return (int?)id;
                    }

                    return null;
                })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            var payload = new
            {
                nom = NewSuperCategoryName,
                description = NewSuperCategoryDescription,
                image = NewSuperCategoryImage,
                produits = productIds
            };

            var created = await _apis
                .PostAsync<object, SuperCategory>("/api/crud/categorieparent/create", payload)
                .ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (created != null)
                {
                    SuperCategories.Insert(0, created);
                }

                NewSuperCategoryName = string.Empty;
                NewSuperCategoryDescription = string.Empty;
                NewSuperCategoryImage = string.Empty;
                NewSuperCategoryProducts = string.Empty;
                SuperCategoryStatus = "Super catégorie créée avec succès.";
            });

            if (created == null)
            {
                await LoadSuperCategoriesAsync().ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SuperCategoryStatus = "La création a expiré. Veuillez réessayer.";
            });
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SuperCategoryStatus = "Impossible de créer la super catégorie.";
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SuperCategoryStatus = "Une erreur est survenue lors de la création.";
            });
        }
        finally
        {
            IsBusy = false;
            RefreshCreateAvailability();
        }
    }
}
