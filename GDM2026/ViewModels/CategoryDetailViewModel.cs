using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.Controls;

namespace GDM2026.ViewModels;

public class CategoryDetailViewModel : BaseViewModel
{
    private enum ParentFormMode
    {
        None,
        Create,
        Update
    }

    private enum CategoryFormMode
    {
        None,
        Create,
        Update
    }

    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionLoaded;
    private CategoryCard? _card;
    private string _title = "Gestion des catégories";
    private string _description = "Créez ou mettez à jour vos catégories parentes et filles.";
    private string _hint = "Choisissez une action pour commencer.";

    private ParentFormMode _parentMode;
    private CategoryFormMode _categoryMode;
    private bool _isCategorySectionVisible;

    private bool _parentsLoaded;
    private bool _categoriesLoaded;
    private bool _isParentListLoading;
    private bool _isCategoryListLoading;

    private string _parentNameInput = string.Empty;
    private string _categoryNameInput = string.Empty;

    private string _parentStatusMessage = "Sélectionnez une action pour gérer les catégories parentes.";
    private string _categoryStatusMessage = "Ouvrez la gestion des catégories filles pour commencer.";
    private string _parentListMessage = "Aucune donnée chargée.";
    private string _categoryListMessage = "Aucune donnée chargée.";

    private SuperCategory? _selectedParentForEdit;
    private SuperCategory? _selectedParentForCategory;
    private SubCategory? _selectedCategoryForEdit;

    public CategoryDetailViewModel()
    {
        ParentCategories = new ObservableCollection<SuperCategory>();
        Categories = new ObservableCollection<SubCategory>();

        ShowParentCreateCommand = new Command(async () => await ToggleParentModeAsync(ParentFormMode.Create));
        ShowParentUpdateCommand = new Command(async () => await ToggleParentModeAsync(ParentFormMode.Update));
        ToggleCategorySectionCommand = new Command(async () => await ToggleCategorySectionAsync());
        ShowCategoryCreateCommand = new Command(async () => await ToggleCategoryModeAsync(CategoryFormMode.Create));
        ShowCategoryUpdateCommand = new Command(async () => await ToggleCategoryModeAsync(CategoryFormMode.Update));
        RefreshParentsCommand = new Command(async () => await LoadParentsAsync(forceReload: true));
        RefreshCategoriesCommand = new Command(async () => await LoadCategoriesAsync(forceReload: true));

        CreateParentCommand = new Command(async () => await CreateParentAsync(), CanCreateParent);
        UpdateParentCommand = new Command(async () => await UpdateParentAsync(), CanUpdateParent);
        CreateCategoryCommand = new Command(async () => await CreateCategoryAsync(), CanCreateCategory);
        UpdateCategoryCommand = new Command(async () => await UpdateCategoryAsync(), CanUpdateCategory);
        NavigateHomeCommand = new Command(async () => await NavigateHomeAsync());
    }

    #region Header
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
    #endregion

    #region Collections
    public ObservableCollection<SuperCategory> ParentCategories { get; }

    public ObservableCollection<SubCategory> Categories { get; }
    #endregion

    #region Parent bindings
    public ICommand ShowParentCreateCommand { get; }

    public ICommand ShowParentUpdateCommand { get; }

    public ICommand RefreshParentsCommand { get; }

    public ICommand CreateParentCommand { get; }

    public ICommand UpdateParentCommand { get; }

    public bool IsParentFormVisible => _parentMode != ParentFormMode.None;

    public bool IsParentCreateMode => _parentMode == ParentFormMode.Create;

    public bool IsParentUpdateMode => _parentMode == ParentFormMode.Update;

    public string ParentFormHeader => _parentMode switch
    {
        ParentFormMode.Create => "Nouvelle catégorie parente",
        ParentFormMode.Update => "Mettre à jour une catégorie parente",
        _ => string.Empty
    };

    public string ParentHelperMessage => _parentMode switch
    {
        ParentFormMode.Create => "Renseignez le nom de la nouvelle catégorie parente.",
        ParentFormMode.Update when SelectedParentForEdit is null => "Sélectionnez une catégorie parente à modifier.",
        ParentFormMode.Update => $"Modification de la catégorie #{SelectedParentForEdit?.Id}.",
        _ => string.Empty
    };

    public ICommand ParentActionCommand => IsParentUpdateMode ? UpdateParentCommand : CreateParentCommand;

    public string ParentActionButtonText => IsParentUpdateMode ? "Mettre à jour" : "Créer";

    public string ParentNameInput
    {
        get => _parentNameInput;
        set
        {
            if (SetProperty(ref _parentNameInput, value))
            {
                RefreshParentCommands();
            }
        }
    }

    public string ParentStatusMessage
    {
        get => _parentStatusMessage;
        set => SetProperty(ref _parentStatusMessage, value);
    }

    public string ParentListMessage
    {
        get => _parentListMessage;
        set => SetProperty(ref _parentListMessage, value);
    }

    public bool IsParentListLoading
    {
        get => _isParentListLoading;
        private set => SetProperty(ref _isParentListLoading, value);
    }

    public SuperCategory? SelectedParentForEdit
    {
        get => _selectedParentForEdit;
        set
        {
            if (SetProperty(ref _selectedParentForEdit, value))
            {
                ParentNameInput = value?.Name ?? string.Empty;
                RefreshParentCommands();
                OnPropertyChanged(nameof(HasParentSelection));
            }
        }
    }

    public bool HasParentSelection => SelectedParentForEdit is not null;
    #endregion

    #region Category bindings
    public ICommand ToggleCategorySectionCommand { get; }

    public ICommand ShowCategoryCreateCommand { get; }

    public ICommand ShowCategoryUpdateCommand { get; }

    public ICommand RefreshCategoriesCommand { get; }

    public ICommand CreateCategoryCommand { get; }

    public ICommand UpdateCategoryCommand { get; }

    public ICommand NavigateHomeCommand { get; }

    public bool IsCategorySectionVisible
    {
        get => _isCategorySectionVisible;
        set => SetProperty(ref _isCategorySectionVisible, value);
    }

    public bool IsCategoryFormVisible => _categoryMode != CategoryFormMode.None;

    public bool IsCategoryCreateMode => _categoryMode == CategoryFormMode.Create;

    public bool IsCategoryUpdateMode => _categoryMode == CategoryFormMode.Update;

    public string CategoryFormHeader => _categoryMode switch
    {
        CategoryFormMode.Create => "Nouvelle catégorie",
        CategoryFormMode.Update => "Mettre à jour une catégorie",
        _ => string.Empty
    };

    public string CategoryHelperMessage => _categoryMode switch
    {
        CategoryFormMode.Create => "Renseignez le nom de la catégorie et assignez un parent.",
        CategoryFormMode.Update when SelectedCategoryForEdit is null => "Sélectionnez une catégorie existante.",
        CategoryFormMode.Update => $"Modification de la catégorie #{SelectedCategoryForEdit?.Id}.",
        _ => string.Empty
    };

    public ICommand CategoryActionCommand => IsCategoryUpdateMode ? UpdateCategoryCommand : CreateCategoryCommand;

    public string CategoryActionButtonText => IsCategoryUpdateMode ? "Mettre à jour" : "Créer";

    public string CategoryNameInput
    {
        get => _categoryNameInput;
        set
        {
            if (SetProperty(ref _categoryNameInput, value))
            {
                RefreshCategoryCommands();
            }
        }
    }

    public string CategoryStatusMessage
    {
        get => _categoryStatusMessage;
        set => SetProperty(ref _categoryStatusMessage, value);
    }

    public string CategoryListMessage
    {
        get => _categoryListMessage;
        set => SetProperty(ref _categoryListMessage, value);
    }

    public bool IsCategoryListLoading
    {
        get => _isCategoryListLoading;
        private set => SetProperty(ref _isCategoryListLoading, value);
    }

    public SuperCategory? SelectedParentForCategory
    {
        get => _selectedParentForCategory;
        set
        {
            if (SetProperty(ref _selectedParentForCategory, value))
            {
                RefreshCategoryCommands();
            }
        }
    }

    public SubCategory? SelectedCategoryForEdit
    {
        get => _selectedCategoryForEdit;
        set
        {
            if (SetProperty(ref _selectedCategoryForEdit, value))
            {
                CategoryNameInput = value?.Name ?? string.Empty;
                if (value?.ParentCategoryId is int parentId)
                {
                    SelectedParentForCategory = ParentCategories.FirstOrDefault(p => p.Id == parentId);
                }
                else
                {
                    SelectedParentForCategory = null;
                }

                RefreshCategoryCommands();
                OnPropertyChanged(nameof(HasCategorySelection));
            }
        }
    }

    public bool HasCategorySelection => SelectedCategoryForEdit is not null;
    #endregion

    #region Initialization
    public Task EnsureInitializedAsync() => Task.CompletedTask;

    public void ApplyCard(CategoryCard? card)
    {
        if (card is null)
        {
            Title = "Gestion des catégories";
            Description = "Créez ou mettez à jour vos catégories parentes et filles.";
            Hint = "Choisissez l'action à effectuer.";
            return;
        }

        Title = card.Title;
        Description = card.Description;
        Hint = "Utilisez les actions ci-dessous pour gérer cette section.";
    }
    #endregion

    #region Parent workflow
    private async Task ToggleParentModeAsync(ParentFormMode mode)
    {
        if (_parentMode == mode)
        {
            _parentMode = ParentFormMode.None;
            OnParentModeChanged();
            return;
        }

        _parentMode = mode;
        OnParentModeChanged();

        if (mode == ParentFormMode.Create)
        {
            ClearParentForm();
            return;
        }

        if (mode == ParentFormMode.Update)
        {
            await LoadParentsAsync(forceReload: !_parentsLoaded);
        }
    }

    private void OnParentModeChanged()
    {
        OnPropertyChanged(nameof(IsParentFormVisible));
        OnPropertyChanged(nameof(IsParentCreateMode));
        OnPropertyChanged(nameof(IsParentUpdateMode));
        OnPropertyChanged(nameof(ParentFormHeader));
        OnPropertyChanged(nameof(ParentHelperMessage));
        OnPropertyChanged(nameof(ParentActionCommand));
        OnPropertyChanged(nameof(ParentActionButtonText));
        RefreshParentCommands();
    }

    private async Task LoadParentsAsync(bool forceReload)
    {
        if (IsParentListLoading)
        {
            return;
        }

        if (!forceReload && _parentsLoaded)
        {
            return;
        }

        try
        {
            IsParentListLoading = true;
            ParentListMessage = "Chargement des catégories parentes…";

            var parents = await _apis.GetListAsync<SuperCategory>("/api/crud/categorieparent/list");
            ParentCategories.Clear();
            foreach (var parent in parents.OrderBy(p => p.Name))
            {
                ParentCategories.Add(parent);
            }

            _parentsLoaded = true;
            ApplyParentNamesToCategories();
            ParentListMessage = ParentCategories.Count == 0
                ? "Aucune catégorie parente n'est disponible."
                : "Sélectionnez une catégorie parente pour la modifier.";
        }
        catch (TaskCanceledException)
        {
            ParentListMessage = "Chargement des catégories parentes annulé.";
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[CATEGORIES] Parent list error: {ex}");
            ParentListMessage = "Impossible de récupérer les catégories parentes.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CATEGORIES] Parent load error: {ex}");
            ParentListMessage = "Erreur inconnue lors du chargement des parents.";
        }
        finally
        {
            IsParentListLoading = false;
        }
    }

    private bool CanCreateParent()
    {
        return IsParentCreateMode
            && !IsBusy
            && !string.IsNullOrWhiteSpace(ParentNameInput);
    }

    private bool CanUpdateParent()
    {
        return IsParentUpdateMode
            && !IsBusy
            && SelectedParentForEdit is not null
            && !string.IsNullOrWhiteSpace(ParentNameInput);
    }

    private async Task CreateParentAsync()
    {
        if (!CanCreateParent())
        {
            ParentStatusMessage = "Renseignez le nom de la catégorie parente.";
            return;
        }

        try
        {
            IsBusy = true;
            RefreshParentCommands();
            ParentStatusMessage = "Création en cours…";

            if (!await EnsureAuthenticationAsync())
            {
                ParentStatusMessage = "Connexion requise pour créer une catégorie.";
                return;
            }

            var payload = new { nom = ParentNameInput.Trim() };
            var created = await _apis.PostBoolAsync("/api/crud/categorieparent/create", payload);

            ParentStatusMessage = created
                ? "Catégorie parente créée avec succès."
                : "La création a échoué.";

            if (created)
            {
                ClearParentForm();
                await LoadParentsAsync(forceReload: true);
            }
        }
        catch (HttpRequestException ex)
        {
            ParentStatusMessage = $"Impossible de créer la catégorie ({ex.Message}).";
        }
        catch (Exception ex)
        {
            ParentStatusMessage = $"Erreur inattendue : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshParentCommands();
        }
    }

    private async Task UpdateParentAsync()
    {
        if (!CanUpdateParent())
        {
            ParentStatusMessage = "Sélectionnez une catégorie parente et renseignez son nom.";
            return;
        }

        try
        {
            IsBusy = true;
            RefreshParentCommands();
            ParentStatusMessage = "Mise à jour en cours…";

            if (!await EnsureAuthenticationAsync())
            {
                ParentStatusMessage = "Connexion requise pour mettre à jour.";
                return;
            }

            var payload = new
            {
                id = SelectedParentForEdit!.Id,
                nom = ParentNameInput.Trim()
            };

            var updated = await _apis.PostBoolAsync("/api/crud/categorieparent/update", payload);
            ParentStatusMessage = updated
                ? "Catégorie parente mise à jour."
                : "La mise à jour a échoué.";

            if (updated)
            {
                await LoadParentsAsync(forceReload: true);
                SelectedParentForEdit = null;
                ParentNameInput = string.Empty;
            }
        }
        catch (HttpRequestException ex)
        {
            ParentStatusMessage = $"Impossible de mettre à jour ({ex.Message}).";
        }
        catch (Exception ex)
        {
            ParentStatusMessage = $"Erreur inattendue : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshParentCommands();
        }
    }

    private void ClearParentForm()
    {
        ParentNameInput = string.Empty;
        SelectedParentForEdit = null;
    }
    #endregion

    #region Category workflow
    private async Task ToggleCategorySectionAsync()
    {
        IsCategorySectionVisible = !IsCategorySectionVisible;

        if (IsCategorySectionVisible)
        {
            await LoadParentsAsync(forceReload: !_parentsLoaded);
        }
        else
        {
            _categoryMode = CategoryFormMode.None;
            OnCategoryModeChanged();
        }
    }

    private async Task ToggleCategoryModeAsync(CategoryFormMode mode)
    {
        if (!IsCategorySectionVisible)
        {
            IsCategorySectionVisible = true;
        }

        if (_categoryMode == mode)
        {
            _categoryMode = CategoryFormMode.None;
            OnCategoryModeChanged();
            return;
        }

        _categoryMode = mode;
        OnCategoryModeChanged();

        if (mode == CategoryFormMode.Create)
        {
            ClearCategoryForm();
            await LoadParentsAsync(forceReload: !_parentsLoaded);
            return;
        }

        if (mode == CategoryFormMode.Update)
        {
            await LoadParentsAsync(forceReload: !_parentsLoaded);
            await LoadCategoriesAsync(forceReload: !_categoriesLoaded);
        }
    }

    private void OnCategoryModeChanged()
    {
        OnPropertyChanged(nameof(IsCategoryFormVisible));
        OnPropertyChanged(nameof(IsCategoryCreateMode));
        OnPropertyChanged(nameof(IsCategoryUpdateMode));
        OnPropertyChanged(nameof(CategoryFormHeader));
        OnPropertyChanged(nameof(CategoryHelperMessage));
        OnPropertyChanged(nameof(CategoryActionCommand));
        OnPropertyChanged(nameof(CategoryActionButtonText));
        RefreshCategoryCommands();
    }

    private async Task LoadCategoriesAsync(bool forceReload)
    {
        if (IsCategoryListLoading)
        {
            return;
        }

        if (!forceReload && _categoriesLoaded)
        {
            return;
        }

        try
        {
            IsCategoryListLoading = true;
            CategoryListMessage = "Chargement des catégories…";

            var categories = await _apis.GetListAsync<SubCategory>("/api/crud/categorie/list");
            Categories.Clear();
            foreach (var category in categories.OrderBy(c => c.Name))
            {
                Categories.Add(category);
            }

            _categoriesLoaded = true;
            ApplyParentNamesToCategories();
            CategoryListMessage = Categories.Count == 0
                ? "Aucune catégorie n'est disponible."
                : "Sélectionnez une catégorie pour la modifier.";
        }
        catch (TaskCanceledException)
        {
            CategoryListMessage = "Chargement des catégories annulé.";
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[CATEGORIES] Sub list error: {ex}");
            CategoryListMessage = "Impossible de récupérer les catégories.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CATEGORIES] Sub load error: {ex}");
            CategoryListMessage = "Erreur inconnue lors du chargement des catégories.";
        }
        finally
        {
            IsCategoryListLoading = false;
        }
    }

    private bool CanCreateCategory()
    {
        return IsCategoryCreateMode
            && !IsBusy
            && !string.IsNullOrWhiteSpace(CategoryNameInput)
            && SelectedParentForCategory is not null;
    }

    private bool CanUpdateCategory()
    {
        return IsCategoryUpdateMode
            && !IsBusy
            && SelectedCategoryForEdit is not null
            && !string.IsNullOrWhiteSpace(CategoryNameInput)
            && SelectedParentForCategory is not null;
    }

    private async Task CreateCategoryAsync()
    {
        if (!CanCreateCategory())
        {
            CategoryStatusMessage = "Renseignez le nom et sélectionnez un parent.";
            return;
        }

        try
        {
            IsBusy = true;
            RefreshCategoryCommands();
            CategoryStatusMessage = "Création en cours…";

            if (!await EnsureAuthenticationAsync())
            {
                CategoryStatusMessage = "Connexion requise pour créer.";
                return;
            }

            var payload = new
            {
                nom = CategoryNameInput.Trim(),
                lacategorieParentId = SelectedParentForCategory!.Id
            };

            var created = await _apis.PostBoolAsync("/api/crud/categorie/create", payload);
            CategoryStatusMessage = created
                ? "Catégorie créée avec succès."
                : "La création a échoué.";

            if (created)
            {
                ClearCategoryForm();
                await LoadCategoriesAsync(forceReload: true);
            }
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = $"Impossible de créer ({ex.Message}).";
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"Erreur inattendue : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshCategoryCommands();
        }
    }

    private async Task UpdateCategoryAsync()
    {
        if (!CanUpdateCategory())
        {
            CategoryStatusMessage = "Sélectionnez une catégorie et renseignez les champs.";
            return;
        }

        try
        {
            IsBusy = true;
            RefreshCategoryCommands();
            CategoryStatusMessage = "Mise à jour en cours…";

            if (!await EnsureAuthenticationAsync())
            {
                CategoryStatusMessage = "Connexion requise pour mettre à jour.";
                return;
            }

            var payload = new
            {
                id = SelectedCategoryForEdit!.Id,
                nom = CategoryNameInput.Trim(),
                lacategorieParentId = SelectedParentForCategory!.Id
            };

            var updated = await _apis.PostBoolAsync("/api/crud/categorie/update", payload);
            CategoryStatusMessage = updated
                ? "Catégorie mise à jour."
                : "La mise à jour a échoué.";

            if (updated)
            {
                await LoadCategoriesAsync(forceReload: true);
                SelectedCategoryForEdit = null;
                ClearCategoryForm();
            }
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = $"Impossible de mettre à jour ({ex.Message}).";
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"Erreur inattendue : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshCategoryCommands();
        }
    }

    private void ClearCategoryForm()
    {
        CategoryNameInput = string.Empty;
        SelectedParentForCategory = null;
        SelectedCategoryForEdit = null;
    }
    #endregion

    #region Helpers
    private void RefreshParentCommands()
    {
        (CreateParentCommand as Command)?.ChangeCanExecute();
        (UpdateParentCommand as Command)?.ChangeCanExecute();
    }

    private void RefreshCategoryCommands()
    {
        (CreateCategoryCommand as Command)?.ChangeCanExecute();
        (UpdateCategoryCommand as Command)?.ChangeCanExecute();
    }

    private void ApplyParentNamesToCategories()
    {
        if (!Categories.Any() || !ParentCategories.Any())
        {
            return;
        }

        var parentLookup = ParentCategories.ToDictionary(p => p.Id, p => p.Name);
        foreach (var category in Categories)
        {
            if (category.ParentCategoryId.HasValue && parentLookup.TryGetValue(category.ParentCategoryId.Value, out var name))
            {
                category.ParentCategoryName = name;
            }
            else
            {
                category.ParentCategoryName = null;
            }
        }

        OnPropertyChanged(nameof(Categories));
    }

    private async Task<bool> EnsureAuthenticationAsync()
    {
        if (!_sessionLoaded)
        {
            _sessionLoaded = true;
            await _sessionService.LoadAsync();
        }

        if (!string.IsNullOrWhiteSpace(_sessionService.AuthToken))
        {
            _apis.SetBearerToken(_sessionService.AuthToken);
            return true;
        }

        if (!await PromptInlineLoginAsync())
        {
            return false;
        }

        _apis.SetBearerToken(_sessionService.AuthToken);
        return true;
    }

    private async Task<bool> PromptInlineLoginAsync()
    {
        var credentials = await RequestCredentialsAsync();
        if (credentials is null)
        {
            return false;
        }

        try
        {
            var user = await AuthenticateAsync(credentials.Value.username, credentials.Value.password);
            if (user is null)
            {
                ParentStatusMessage = "Identifiants invalides.";
                CategoryStatusMessage = "Identifiants invalides.";
                return false;
            }

            await _sessionService.SaveAsync(user, user.Token);
            return true;
        }
        catch (HttpRequestException)
        {
            ParentStatusMessage = "Impossible de contacter le serveur d'authentification.";
            CategoryStatusMessage = "Impossible de contacter le serveur d'authentification.";
            return false;
        }
    }

    private static async Task<(string username, string password)?> RequestCredentialsAsync()
    {
        var shell = Shell.Current;
        if (shell is null)
        {
            return null;
        }

        var username = await shell.DisplayPromptAsync(
            "Connexion requise",
            "Identifiez-vous pour continuer.",
            accept: "Continuer",
            cancel: "Annuler");

        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var password = await shell.DisplayPromptAsync(
            "Mot de passe",
            "Entrez votre mot de passe",
            accept: "Valider",
            cancel: "Annuler",
            placeholder: "Mot de passe",
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return (username.Trim(), password);
    }

    private async Task<User?> AuthenticateAsync(string username, string password)
    {
        var loginData = new
        {
            Email = username,
            Password = password
        };

        try
        {
            return await _apis.PostAsync<object, User>("/api/mobile/GetFindUser", loginData);
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[AUTH] error: {ex}");
            return null;
        }
    }

    private Task NavigateHomeAsync()
    {
        return Shell.Current is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync("//HomePage", animate: false);
    }
    #endregion
}
