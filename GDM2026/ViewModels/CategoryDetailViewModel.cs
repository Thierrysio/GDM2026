using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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
    private bool _hasLoadedSubCategories;
    private bool _sessionLoaded;
    private string _superCategoryStatus = "Chargement des super catégories…";
    private string _subCategoryStatus = "Chargement des sous-catégories…";
    private string _newSuperCategoryName = string.Empty;
    private string _newSuperCategoryDescription = string.Empty;
    private string _newSuperCategoryProducts = string.Empty;
    private bool _isSubCategoryMenuOpen;
    private int _selectedSubCategoryCount;

    public CategoryDetailViewModel()
    {
        SuperCategories = new ObservableCollection<SuperCategory>();
        AvailableSubCategories = new ObservableCollection<SelectableSubCategory>();
        CreateSuperCategoryCommand = new Command(async () => await CreateSuperCategoryAsync(), CanCreateSuperCategory);
        ToggleSubCategoryMenuCommand = new Command(() =>
        {
            IsSubCategoryMenuOpen = !IsSubCategoryMenuOpen;
        });
    }

    public ObservableCollection<SuperCategory> SuperCategories { get; }

    public ObservableCollection<SelectableSubCategory> AvailableSubCategories { get; }

    public ICommand CreateSuperCategoryCommand { get; }

    public ICommand ToggleSubCategoryMenuCommand { get; }

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

    public string SubCategoryStatus
    {
        get => _subCategoryStatus;
        set
        {
            if (SetProperty(ref _subCategoryStatus, value))
            {
                OnPropertyChanged(nameof(HasSubCategoryStatus));
            }
        }
    }

    public bool HasSubCategoryStatus => !string.IsNullOrWhiteSpace(SubCategoryStatus);

    public bool IsSubCategoryMenuOpen
    {
        get => _isSubCategoryMenuOpen;
        set
        {
            if (SetProperty(ref _isSubCategoryMenuOpen, value))
            {
                OnPropertyChanged(nameof(SubCategoryMenuLabel));
            }
        }
    }

    public string SubCategoryMenuLabel => IsSubCategoryMenuOpen
        ? "Masquer les sous-catégories"
        : "Afficher les sous-catégories disponibles";

    public int SelectedSubCategoryCount
    {
        get => _selectedSubCategoryCount;
        private set => SetProperty(ref _selectedSubCategoryCount, value);
    }

    private string _selectedSubCategorySummary = "Aucune sous-catégorie sélectionnée";

    public string SelectedSubCategorySummary
    {
        get => _selectedSubCategorySummary;
        private set => SetProperty(ref _selectedSubCategorySummary, value);
    }

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

        if (_hasLoadedSuperCategories && _hasLoadedSubCategories)
        {
            return;
        }

        if (!_hasLoadedSuperCategories)
        {
            await LoadSuperCategoriesAsync(ct).ConfigureAwait(false);
        }

        if (!_hasLoadedSubCategories)
        {
            await LoadAvailableSubCategoriesAsync(ct).ConfigureAwait(false);
        }
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
        IsSuperCategoryPage = string.Equals(card.Title, "Super categories", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Title, "Super catégories", StringComparison.OrdinalIgnoreCase);
        Hint = IsSuperCategoryPage
            ? "Ajoutez des super catégories parentes pour organiser vos sous-catégories (ex. Épicerie sucrée > Chocolat)."
            : $"Vous êtes sur la page {card.Title}. Ajoutez ici les fonctionnalités spécifiques à cette catégorie.";
    }

    private bool CanCreateSuperCategory()
    {
        return !IsBusy
            && IsSuperCategoryPage
            && !string.IsNullOrWhiteSpace(NewSuperCategoryName)
            && !string.IsNullOrWhiteSpace(NewSuperCategoryDescription);
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

    private async Task LoadAvailableSubCategoriesAsync(CancellationToken ct = default)
    {
        if (IsBusy || _hasLoadedSubCategories)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SubCategoryStatus = "Chargement des sous-catégories…";

            var result = await _apis.GetListAsync<SubCategory>("/api/crud/categorie/list", ct).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                DetachSubCategoryHandlers();
                AvailableSubCategories.Clear();
                foreach (var item in result)
                {
                    var selectable = new SelectableSubCategory(item);
                    selectable.PropertyChanged += OnSubCategoryPropertyChanged;
                    AvailableSubCategories.Add(selectable);
                }

                _hasLoadedSubCategories = true;
                UpdateSelectedSubCategorySelection();
                SubCategoryStatus = AvailableSubCategories.Any()
                    ? "Sélectionnez les sous-catégories à inclure."
                    : "Aucune sous-catégorie n'est encore disponible.";
            });
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SubCategoryStatus = "Le chargement des sous-catégories a expiré.";
            });
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SubCategoryStatus = "Impossible de récupérer les sous-catégories.";
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SubCategoryStatus = "Une erreur est survenue pendant le chargement.";
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

            var productIds = ParseProductIds();
            var selectedSubCategoryIds = AvailableSubCategories
                .Where(c => c.IsSelected)
                .Select(c => c.Id);

            var mergedIds = productIds
                .Concat(selectedSubCategoryIds)
                .Distinct()
                .ToList();

            var payload = new
            {
                nom = NewSuperCategoryName,
                description = NewSuperCategoryDescription,
                produits = mergedIds
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
                NewSuperCategoryProducts = string.Empty;
                foreach (var subCategory in AvailableSubCategories)
                {
                    subCategory.IsSelected = false;
                }
                UpdateSelectedSubCategorySelection();
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

    private void UpdateSelectedSubCategorySelection()
    {
        var selected = AvailableSubCategories
            .Where(c => c.IsSelected)
            .ToList();

        SelectedSubCategoryCount = selected.Count;

        if (SelectedSubCategoryCount == 0)
        {
            SelectedSubCategorySummary = "Aucune sous-catégorie sélectionnée";
            return;
        }

        var previewNames = selected
            .Select(c => c.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Take(5)
            .ToList();

        var hasMore = SelectedSubCategoryCount > previewNames.Count;
        var joinedNames = string.Join(", ", previewNames);
        var suffix = hasMore ? $" (+{SelectedSubCategoryCount - previewNames.Count} autres)" : string.Empty;

        SelectedSubCategorySummary = $"{SelectedSubCategoryCount} sous-catégorie(s) : {joinedNames}{suffix}";
    }

    private void DetachSubCategoryHandlers()
    {
        foreach (var subCategory in AvailableSubCategories)
        {
            subCategory.PropertyChanged -= OnSubCategoryPropertyChanged;
        }
    }

    private void OnSubCategoryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableSubCategory.IsSelected))
        {
            UpdateSelectedSubCategorySelection();
        }
    }

    private List<int> ParseProductIds()
    {
        return NewSuperCategoryProducts
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
    }
}
