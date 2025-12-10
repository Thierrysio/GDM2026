using System;
using System.Collections.Generic;
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

    private bool _sessionLoaded;
    private bool _isSuperCategoryPage;
    private bool _isCategoryPage;
    private bool _isPromoCategoryPage;
    private bool _isHistoryPage;

    private bool _hasLoadedSuperCategories;
    private bool _hasLoadedSubCategories;
    private bool _hasLoadedCategories;
    private bool _hasLoadedPromoCategories;
    private bool _hasLoadedHistories;
    private bool _historyImageLibraryLoaded;

    private string _superCategoryStatus = "Chargement des super catégories…";
    private string _subCategoryStatus = "Chargement des sous-catégories…";
    private string _categoryStatus = "Chargement des catégories…";
    private string _promoCategoryStatus = "Chargement des catégories promo…";

    private string _newSuperCategoryName = string.Empty;
    private string _newSuperCategoryDescription = string.Empty;
    private string _newSuperCategoryProducts = string.Empty;
    private string _newCategoryName = string.Empty;
    private string _newPromoCategoryName = string.Empty;

    private bool _isSubCategoryMenuOpen;
    private int _selectedSubCategoryCount;
    private string _selectedSubCategorySummary = "Aucune sous-catégorie sélectionnée";
    private SuperCategory? _selectedParentCategory;

    private string _newHistoryTitle = string.Empty;
    private string _newHistoryDescription = string.Empty;
    private DateTime _historyDate = DateTime.Today;
    private string _historyStatusMessage = "Chargement des histoires…";
    private string _historyFormStatusMessage = "Renseignez les informations de l'histoire.";
    private string _historyImageLibraryMessage = "Sélectionnez une image dans la bibliothèque.";
    private string _historyImageSearchTerm = string.Empty;
    private string _selectedHistoryImageName = "Aucune image sélectionnée.";
    private string? _selectedHistoryImageUrl;
    private bool _isHistoryLoading;
    private bool _isHistorySubmitting;
    private bool _isHistoryImageLibraryLoading;
    private AdminImage? _selectedHistoryImage;

    public CategoryDetailViewModel()
    {
        SuperCategories = new ObservableCollection<SuperCategory>();
        AvailableSubCategories = new ObservableCollection<SelectableSubCategory>();
        Categories = new ObservableCollection<SubCategory>();
        PromoCategories = new ObservableCollection<PromoCategory>();
        Histories = new ObservableCollection<HistoryEntry>();
        HistoryImageLibrary = new ObservableCollection<AdminImage>();
        FilteredHistoryImages = new ObservableCollection<AdminImage>();

        CreateSuperCategoryCommand = new Command(async () => await CreateSuperCategoryAsync(), CanCreateSuperCategory);
        CreateCategoryCommand = new Command(async () => await CreateCategoryAsync(), CanCreateCategory);
        CreatePromoCategoryCommand = new Command(async () => await CreatePromoCategoryAsync(), CanCreatePromoCategory);
        CreateHistoryCommand = new Command(async () => await CreateHistoryAsync(), CanCreateHistory);
        RefreshHistoriesCommand = new Command(async () => await LoadHistoriesAsync(forceRefresh: true));
        ToggleSubCategoryMenuCommand = new Command(() => IsSubCategoryMenuOpen = !IsSubCategoryMenuOpen);
    }

    public ObservableCollection<SuperCategory> SuperCategories { get; }

    public ObservableCollection<SelectableSubCategory> AvailableSubCategories { get; }

    public ObservableCollection<SubCategory> Categories { get; }

    public ObservableCollection<PromoCategory> PromoCategories { get; }

    public ObservableCollection<HistoryEntry> Histories { get; }

    public ObservableCollection<AdminImage> HistoryImageLibrary { get; }

    public ObservableCollection<AdminImage> FilteredHistoryImages { get; }

    public ICommand CreateSuperCategoryCommand { get; }

    public ICommand CreateCategoryCommand { get; }

    public ICommand CreatePromoCategoryCommand { get; }

    public ICommand CreateHistoryCommand { get; }

    public ICommand RefreshHistoriesCommand { get; }

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

    public bool IsCategoryPage
    {
        get => _isCategoryPage;
        set => SetProperty(ref _isCategoryPage, value);
    }

    public bool IsPromoCategoryPage
    {
        get => _isPromoCategoryPage;
        set => SetProperty(ref _isPromoCategoryPage, value);
    }

    public bool IsHistoryPage
    {
        get => _isHistoryPage;
        set => SetProperty(ref _isHistoryPage, value);
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

    public string CategoryStatus
    {
        get => _categoryStatus;
        set
        {
            if (SetProperty(ref _categoryStatus, value))
            {
                OnPropertyChanged(nameof(HasCategoryStatus));
            }
        }
    }

    public bool HasCategoryStatus => !string.IsNullOrWhiteSpace(CategoryStatus);

    public string PromoCategoryStatus
    {
        get => _promoCategoryStatus;
        set
        {
            if (SetProperty(ref _promoCategoryStatus, value))
            {
                OnPropertyChanged(nameof(HasPromoCategoryStatus));
            }
        }
    }

    public bool HasPromoCategoryStatus => !string.IsNullOrWhiteSpace(PromoCategoryStatus);

    public string HistoryStatusMessage
    {
        get => _historyStatusMessage;
        set => SetProperty(ref _historyStatusMessage, value);
    }

    public string HistoryFormStatusMessage
    {
        get => _historyFormStatusMessage;
        set => SetProperty(ref _historyFormStatusMessage, value);
    }

    public DateTime HistoryDate
    {
        get => _historyDate;
        set => SetProperty(ref _historyDate, value);
    }

    public string HistoryImageLibraryMessage
    {
        get => _historyImageLibraryMessage;
        set => SetProperty(ref _historyImageLibraryMessage, value);
    }

    public string HistoryImageSearchTerm
    {
        get => _historyImageSearchTerm;
        set
        {
            if (SetProperty(ref _historyImageSearchTerm, value))
            {
                RefreshHistoryImageFilter();
            }
        }
    }

    public string SelectedHistoryImageName
    {
        get => _selectedHistoryImageName;
        set => SetProperty(ref _selectedHistoryImageName, value);
    }

    public AdminImage? SelectedHistoryImage
    {
        get => _selectedHistoryImage;
        set
        {
            if (SetProperty(ref _selectedHistoryImage, value))
            {
                ApplyHistoryImageSelection(value);
            }
        }
    }

    public bool IsHistoryImageLibraryLoading
    {
        get => _isHistoryImageLibraryLoading;
        set => SetProperty(ref _isHistoryImageLibraryLoading, value);
    }

    public bool IsHistoryLoading
    {
        get => _isHistoryLoading;
        set => SetProperty(ref _isHistoryLoading, value);
    }

    public bool IsHistorySubmitting
    {
        get => _isHistorySubmitting;
        set
        {
            if (SetProperty(ref _isHistorySubmitting, value))
            {
                (CreateHistoryCommand as Command)?.ChangeCanExecute();
            }
        }
    }

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

    public string SelectedSubCategorySummary
    {
        get => _selectedSubCategorySummary;
        private set => SetProperty(ref _selectedSubCategorySummary, value);
    }

    public bool HasParentCategories => SuperCategories.Any();

    public SuperCategory? SelectedParentCategory
    {
        get => _selectedParentCategory;
        set => SetProperty(ref _selectedParentCategory, value);
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

    public string NewCategoryName
    {
        get => _newCategoryName;
        set
        {
            if (SetProperty(ref _newCategoryName, value))
            {
                RefreshCreateAvailability();
            }
        }
    }

    public string NewPromoCategoryName
    {
        get => _newPromoCategoryName;
        set
        {
            if (SetProperty(ref _newPromoCategoryName, value))
            {
                RefreshCreateAvailability();
            }
        }
    }

    public string NewHistoryTitle
    {
        get => _newHistoryTitle;
        set
        {
            if (SetProperty(ref _newHistoryTitle, value))
            {
                RefreshCreateAvailability();
            }
        }
    }

    public string NewHistoryDescription
    {
        get => _newHistoryDescription;
        set
        {
            if (SetProperty(ref _newHistoryDescription, value))
            {
                RefreshCreateAvailability();
            }
        }
    }

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (!_sessionLoaded)
        {
            await _sessionService.LoadAsync().ConfigureAwait(false);
            _apis.SetBearerToken(_sessionService.AuthToken);
            _sessionLoaded = true;
        }

        if (IsSuperCategoryPage)
        {
            if (!_hasLoadedSuperCategories)
            {
                await LoadSuperCategoriesAsync(ct).ConfigureAwait(false);
            }

            if (!_hasLoadedSubCategories)
            {
                await LoadAvailableSubCategoriesAsync(ct).ConfigureAwait(false);
            }
        }

        if (IsCategoryPage)
        {
            if (!_hasLoadedSuperCategories)
            {
                await LoadSuperCategoriesAsync(ct).ConfigureAwait(false);
            }

            if (!_hasLoadedCategories)
            {
                await LoadCategoriesAsync(ct).ConfigureAwait(false);
            }
        }

        if (IsPromoCategoryPage && !_hasLoadedPromoCategories)
        {
            await LoadPromoCategoriesAsync(ct).ConfigureAwait(false);
        }

        if (IsHistoryPage)
        {
            if (!_hasLoadedHistories)
            {
                await LoadHistoriesAsync().ConfigureAwait(false);
            }

            if (!_historyImageLibraryLoaded)
            {
                await LoadHistoryImageLibraryAsync(ct).ConfigureAwait(false);
            }
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
            IsCategoryPage = false;
            IsPromoCategoryPage = false;
            IsHistoryPage = false;
            return;
        }

        Title = card.Title;
        Description = card.Description;

        IsSuperCategoryPage = string.Equals(card.Title, "Super categories", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Title, "Super catégories", StringComparison.OrdinalIgnoreCase);
        IsCategoryPage = string.Equals(card.Title, "Categories", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Title, "Catégories", StringComparison.OrdinalIgnoreCase);
        IsPromoCategoryPage = string.Equals(card.Title, "Promo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Title, "Catégories Evenements", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Title, "Catégories événements", StringComparison.OrdinalIgnoreCase);
        IsHistoryPage = string.Equals(card.Title, "Histoire", StringComparison.OrdinalIgnoreCase);

        if (IsSuperCategoryPage)
        {
            Hint = "Ajoutez des super catégories parentes pour organiser vos sous-catégories.";
            _hasLoadedCategories = false;
            _hasLoadedPromoCategories = false;
            return;
        }

        if (IsCategoryPage)
        {
            Hint = "Créez et organisez les catégories principales utilisées dans votre boutique.";
            _hasLoadedCategories = false;
            CategoryStatus = "Chargement des catégories…";
            return;
        }

        if (IsPromoCategoryPage)
        {
            Hint = "Gérez les catégories promo et ajoutez-en de nouvelles pour organiser vos offres.";
            _hasLoadedPromoCategories = false;
            PromoCategoryStatus = "Chargement des catégories promo…";
            return;
        }

        if (IsHistoryPage)
        {
            Hint = "Gérez les histoires présentées dans la rubrique Histoire.";
            _hasLoadedHistories = false;
            _historyImageLibraryLoaded = false;
            HistoryStatusMessage = "Chargement des histoires…";
            HistoryFormStatusMessage = "Ajoutez un titre, une description, une date et une image.";
            return;
        }

        Hint = $"Vous êtes sur la page {card.Title}. Ajoutez ici les fonctionnalités spécifiques à cette catégorie.";
    }

    private bool CanCreateSuperCategory() => !IsBusy
        && IsSuperCategoryPage
        && !string.IsNullOrWhiteSpace(NewSuperCategoryName)
        && !string.IsNullOrWhiteSpace(NewSuperCategoryDescription);

    private bool CanCreateCategory() => !IsBusy
        && IsCategoryPage
        && !string.IsNullOrWhiteSpace(NewCategoryName);

    private bool CanCreatePromoCategory() => !IsBusy
        && IsPromoCategoryPage
        && !string.IsNullOrWhiteSpace(NewPromoCategoryName);

    private bool CanCreateHistory() => IsHistoryPage
        && !IsHistorySubmitting
        && !string.IsNullOrWhiteSpace(NewHistoryTitle)
        && !string.IsNullOrWhiteSpace(NewHistoryDescription)
        && !string.IsNullOrWhiteSpace(_selectedHistoryImageUrl);

    private void RefreshCreateAvailability()
    {
        (CreateSuperCategoryCommand as Command)?.ChangeCanExecute();
        (CreateCategoryCommand as Command)?.ChangeCanExecute();
        (CreatePromoCategoryCommand as Command)?.ChangeCanExecute();
        (CreateHistoryCommand as Command)?.ChangeCanExecute();
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
                OnPropertyChanged(nameof(HasParentCategories));
                SuperCategoryStatus = SuperCategories.Any()
                    ? string.Empty
                    : "Aucune super catégorie n'est encore configurée.";

                if (_hasLoadedCategories)
                {
                    UpdateExistingCategoryParents();
                }
            });
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                SuperCategoryStatus = "Le chargement des super catégories a expiré.");
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                SuperCategoryStatus = "Impossible de récupérer les super catégories.");
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                SuperCategoryStatus = "Une erreur est survenue pendant le chargement.");
        }
        finally
        {
            IsBusy = false;
            RefreshCreateAvailability();
        }
    }

    private async Task LoadCategoriesAsync(CancellationToken ct = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            CategoryStatus = "Chargement des catégories…";
            var result = await _apis.GetListAsync<SubCategory>("/api/crud/categorie/list", ct).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Categories.Clear();
                foreach (var item in result)
                {
                    ApplyParentCategoryName(item);
                    Categories.Add(item);
                }

                _hasLoadedCategories = true;
                CategoryStatus = Categories.Any()
                    ? string.Empty
                    : "Aucune catégorie n'est encore configurée.";
            });
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                CategoryStatus = "Le chargement des catégories a expiré.");
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                CategoryStatus = "Impossible de récupérer les catégories.");
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                CategoryStatus = "Une erreur est survenue pendant le chargement." );
        }
        finally
        {
            IsBusy = false;
            RefreshCreateAvailability();
        }
    }

    private async Task LoadPromoCategoriesAsync(CancellationToken ct = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            PromoCategoryStatus = "Chargement des catégories promo…";
            var result = await _apis.GetListAsync<PromoCategory>("/api/crud/categoriepromo/list", ct).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PromoCategories.Clear();
                foreach (var item in result)
                {
                    PromoCategories.Add(item);
                }

                _hasLoadedPromoCategories = true;
                PromoCategoryStatus = PromoCategories.Any()
                    ? string.Empty
                    : "Aucune catégorie promo n'est encore configurée.";
            });
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                PromoCategoryStatus = "Le chargement des catégories promo a expiré." );
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                PromoCategoryStatus = "Impossible de récupérer les catégories promo." );
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                PromoCategoryStatus = "Une erreur est survenue pendant le chargement." );
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
                SubCategoryStatus = "Le chargement des sous-catégories a expiré." );
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                SubCategoryStatus = "Impossible de récupérer les sous-catégories." );
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                SubCategoryStatus = "Une erreur est survenue pendant le chargement." );
        }
        finally
        {
            IsBusy = false;
            RefreshCreateAvailability();
        }
    }

    private async Task CreateCategoryAsync()
    {
        if (IsBusy || !IsCategoryPage)
        {
            return;
        }

        try
        {
            IsBusy = true;
            RefreshCreateAvailability();
            CategoryStatus = "Création de la catégorie en cours…";

            var payload = new
            {
                nom = NewCategoryName,
                categorieParent = SelectedParentCategory?.Id
            };

            var created = await _apis.PostAsync<object, SubCategory>("/api/crud/categorie/create", payload).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (created != null)
                {
                    created.ParentCategoryId = SelectedParentCategory?.Id;
                    created.ParentCategoryName = SelectedParentCategory?.Name;
                    ApplyParentCategoryName(created);
                    Categories.Insert(0, created);
                }

                NewCategoryName = string.Empty;
                SelectedParentCategory = null;
                CategoryStatus = "Catégorie créée avec succès.";
            });

            if (created == null)
            {
                await LoadCategoriesAsync().ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                CategoryStatus = "La création a expiré. Veuillez réessayer." );
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                CategoryStatus = "Impossible de créer la catégorie." );
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                CategoryStatus = "Une erreur est survenue lors de la création." );
        }
        finally
        {
            IsBusy = false;
            RefreshCreateAvailability();
        }
    }

    private async Task CreatePromoCategoryAsync()
    {
        if (IsBusy || !IsPromoCategoryPage)
        {
            return;
        }

        try
        {
            IsBusy = true;
            RefreshCreateAvailability();
            PromoCategoryStatus = "Création de la catégorie promo en cours…";

            var payload = new { nom = NewPromoCategoryName };
            var created = await _apis.PostAsync<object, PromoCategory>("/api/crud/categoriepromo/create", payload).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (created != null)
                {
                    PromoCategories.Insert(0, created);
                }

                NewPromoCategoryName = string.Empty;
                PromoCategoryStatus = "Catégorie promo créée avec succès.";
            });

            if (created == null)
            {
                await LoadPromoCategoriesAsync().ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                PromoCategoryStatus = "La création a expiré. Veuillez réessayer." );
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                PromoCategoryStatus = "Impossible de créer la catégorie promo." );
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                PromoCategoryStatus = "Une erreur est survenue lors de la création." );
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
            var selectedIds = AvailableSubCategories.Where(c => c.IsSelected).Select(c => c.Id);
            var mergedIds = productIds.Concat(selectedIds).Distinct().ToList();

            var payload = new
            {
                nom = NewSuperCategoryName,
                description = NewSuperCategoryDescription,
                produits = mergedIds
            };

            var created = await _apis.PostAsync<object, SuperCategory>("/api/crud/categorieparent/create", payload).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (created != null)
                {
                    SuperCategories.Insert(0, created);
                }

                NewSuperCategoryName = string.Empty;
                NewSuperCategoryDescription = string.Empty;
                NewSuperCategoryProducts = string.Empty;
                foreach (var sub in AvailableSubCategories)
                {
                    sub.IsSelected = false;
                }
                UpdateSelectedSubCategorySelection();
                OnPropertyChanged(nameof(HasParentCategories));
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
                SuperCategoryStatus = "La création a expiré. Veuillez réessayer." );
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                SuperCategoryStatus = "Impossible de créer la super catégorie." );
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                SuperCategoryStatus = "Une erreur est survenue lors de la création." );
        }
        finally
        {
            IsBusy = false;
            RefreshCreateAvailability();
        }
    }

    private async Task LoadHistoriesAsync(bool forceRefresh = false)
    {
        if (!IsHistoryPage || IsHistoryLoading)
        {
            return;
        }

        try
        {
            IsHistoryLoading = true;
            HistoryStatusMessage = forceRefresh ? "Actualisation des histoires…" : "Chargement des histoires…";

            var items = await _apis.GetListAsync<HistoryEntry>("/api/crud/histoire/list").ConfigureAwait(false);
            var ordered = items.OrderByDescending(h => h.DateHistoire ?? DateTime.MinValue).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Histories.Clear();
                foreach (var history in ordered)
                {
                    Histories.Add(history);
                }

                _hasLoadedHistories = true;
                HistoryStatusMessage = Histories.Count == 0
                    ? "Aucune histoire à afficher."
                    : $"{Histories.Count} histoire(s) chargée(s).";
            });
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                HistoryStatusMessage = "Chargement des histoires annulé." );
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                HistoryStatusMessage = "Impossible de récupérer les histoires." );
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                HistoryStatusMessage = "Une erreur est survenue lors du chargement des histoires." );
        }
        finally
        {
            IsHistoryLoading = false;
        }
    }

    private async Task CreateHistoryAsync()
    {
        if (!IsHistoryPage || IsHistorySubmitting)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NewHistoryTitle) || string.IsNullOrWhiteSpace(NewHistoryDescription))
        {
            HistoryFormStatusMessage = "Renseignez le titre et la description de l'histoire.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedHistoryImageUrl))
        {
            HistoryFormStatusMessage = "Sélectionnez une image pour l'histoire.";
            return;
        }

        try
        {
            IsHistorySubmitting = true;
            HistoryFormStatusMessage = "Création de l'histoire…";

            var payload = new
            {
                titre = NewHistoryTitle.Trim(),
                description = NewHistoryDescription.Trim(),
                date = HistoryDate.ToString("yyyy-MM-dd"),
                image = _selectedHistoryImageUrl
            };

            var created = await _apis.PostBoolAsync("/api/crud/histoire/create", payload).ConfigureAwait(false);

            if (created)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HistoryFormStatusMessage = "Histoire créée avec succès.";
                    NewHistoryTitle = string.Empty;
                    NewHistoryDescription = string.Empty;
                    HistoryDate = DateTime.Today;
                    SelectedHistoryImage = null;
                    _selectedHistoryImageUrl = null;
                    SelectedHistoryImageName = "Aucune image sélectionnée.";
                });

                await LoadHistoriesAsync(forceRefresh: true).ConfigureAwait(false);
            }
            else
            {
                HistoryFormStatusMessage = "La création de l'histoire a échoué.";
            }
        }
        catch (TaskCanceledException)
        {
            HistoryFormStatusMessage = "Création annulée.";
        }
        catch (HttpRequestException)
        {
            HistoryFormStatusMessage = "Impossible de créer l'histoire.";
        }
        catch
        {
            HistoryFormStatusMessage = "Une erreur est survenue lors de la création de l'histoire.";
        }
        finally
        {
            IsHistorySubmitting = false;
            RefreshCreateAvailability();
        }
    }

    private async Task LoadHistoryImageLibraryAsync(CancellationToken ct)
    {
        if (_historyImageLibraryLoaded || IsHistoryImageLibraryLoading)
        {
            return;
        }

        try
        {
            IsHistoryImageLibraryLoading = true;
            HistoryImageLibraryMessage = "Chargement de la bibliothèque d'images…";

            var images = await _apis.GetListAsync<AdminImage>("/api/crud/images/list", ct).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                HistoryImageLibrary.Clear();
                foreach (var image in images)
                {
                    HistoryImageLibrary.Add(image);
                }

                _historyImageLibraryLoaded = true;
                RefreshHistoryImageFilter();
                HistoryImageLibraryMessage = HistoryImageLibrary.Count == 0
                    ? "Aucune image disponible dans l'admin."
                    : "Sélectionnez une image pour l'histoire.";
            });
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                HistoryImageLibraryMessage = "Le chargement des images a expiré." );
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                HistoryImageLibraryMessage = "Impossible de charger les images." );
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                HistoryImageLibraryMessage = "Une erreur est survenue lors du chargement des images." );
        }
        finally
        {
            IsHistoryImageLibraryLoading = false;
        }
    }

    private void RefreshHistoryImageFilter()
    {
        if (!HistoryImageLibrary.Any())
        {
            FilteredHistoryImages.Clear();
            return;
        }

        IEnumerable<AdminImage> source = HistoryImageLibrary;
        var query = HistoryImageSearchTerm?.Trim();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.ToLowerInvariant();
            var filtered = HistoryImageLibrary
                .Where(img =>
                    (!string.IsNullOrWhiteSpace(img.DisplayName) && img.DisplayName!.ToLowerInvariant().Contains(normalized))
                    || (!string.IsNullOrWhiteSpace(img.Url) && img.Url!.ToLowerInvariant().Contains(normalized)))
                .ToList();

            source = filtered;
            HistoryImageLibraryMessage = filtered.Count == 0
                ? "Aucune image ne correspond à cette recherche."
                : $"{filtered.Count} résultat(s) pour \"{query}\".";
        }
        else if (_historyImageLibraryLoaded)
        {
            HistoryImageLibraryMessage = HistoryImageLibrary.Count == 0
                ? "Aucune image disponible dans l'admin."
                : "Sélectionnez une image pour l'histoire.";
        }

        FilteredHistoryImages.Clear();
        foreach (var image in source)
        {
            FilteredHistoryImages.Add(image);
        }
    }

    private void ApplyHistoryImageSelection(AdminImage? image)
    {
        if (image is null)
        {
            _selectedHistoryImageUrl = null;
            SelectedHistoryImageName = "Aucune image sélectionnée.";
            RefreshCreateAvailability();
            return;
        }

        _selectedHistoryImageUrl = image.Url;
        SelectedHistoryImageName = $"Image sélectionnée : {image.DisplayName}";
        RefreshCreateAvailability();
    }

    private void UpdateSelectedSubCategorySelection()
    {
        var selected = AvailableSubCategories.Where(c => c.IsSelected).ToList();
        SelectedSubCategoryCount = selected.Count;

        if (!selected.Any())
        {
            SelectedSubCategorySummary = "Aucune sous-catégorie sélectionnée";
            return;
        }

        var preview = selected.Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(5).ToList();
        var suffix = selected.Count > preview.Count
            ? $" (+{selected.Count - preview.Count} autres)"
            : string.Empty;
        SelectedSubCategorySummary = $"{selected.Count} sous-catégorie(s) : {string.Join(", ", preview)}{suffix}";
    }

    private void ApplyParentCategoryName(SubCategory category)
    {
        if (category == null)
        {
            return;
        }

        var parentName = FindParentCategoryName(category.ParentCategoryId);
        if (!string.IsNullOrWhiteSpace(parentName))
        {
            category.ParentCategoryName = parentName;
        }
    }

    private void UpdateExistingCategoryParents()
    {
        foreach (var category in Categories)
        {
            ApplyParentCategoryName(category);
        }
    }

    private string? FindParentCategoryName(int? parentId)
    {
        if (!parentId.HasValue)
        {
            return null;
        }

        return SuperCategories.FirstOrDefault(sc => sc.Id == parentId.Value)?.Name;
    }

    private void DetachSubCategoryHandlers()
    {
        foreach (var item in AvailableSubCategories)
        {
            item.PropertyChanged -= OnSubCategoryPropertyChanged;
        }
    }

    private void OnSubCategoryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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
            .Select(token => token.Trim())
            .Select(token => int.TryParse(token, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }
}
