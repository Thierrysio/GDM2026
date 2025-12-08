using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class MessagesViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionPrepared;
    private bool _isLoading;
    private bool _isSubmitting;
    private DateTime _messageDate = DateTime.Today;
    private string _messageText = string.Empty;
    private string _responseText = string.Empty;
    private string _statusText = string.Empty;
    private string _feedbackMessage = "Consultez et créez des messages.";
    private Color _feedbackColor = Colors.Gold;

    public ObservableCollection<MessageEntry> Messages { get; } = new();

    public ICommand RefreshCommand { get; }

    public ICommand SubmitCommand { get; }

    public MessagesViewModel()
    {
        RefreshCommand = new Command(async () => await LoadMessagesAsync());
        SubmitCommand = new Command(async () => await SubmitAsync(), CanSubmit);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsSubmitting
    {
        get => _isSubmitting;
        set
        {
            if (SetProperty(ref _isSubmitting, value))
            {
                (SubmitCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public DateTime MessageDate
    {
        get => _messageDate;
        set => SetProperty(ref _messageDate, value);
    }

    public string MessageText
    {
        get => _messageText;
        set
        {
            if (SetProperty(ref _messageText, value))
            {
                (SubmitCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public string ResponseText
    {
        get => _responseText;
        set => SetProperty(ref _responseText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string FeedbackMessage
    {
        get => _feedbackMessage;
        set => SetProperty(ref _feedbackMessage, value);
    }

    public Color FeedbackColor
    {
        get => _feedbackColor;
        set => SetProperty(ref _feedbackColor, value);
    }

    public async Task InitializeAsync()
    {
        if (!_sessionPrepared)
        {
            await PrepareSessionAsync();
        }

        if (Messages.Count == 0)
        {
            await LoadMessagesAsync();
        }
    }

    private bool CanSubmit()
    {
        return !IsSubmitting && !string.IsNullOrWhiteSpace(_messageText);
    }

    private async Task PrepareSessionAsync()
    {
        try
        {
            await _sessionService.LoadAsync();
            _apis.SetBearerToken(_sessionService.AuthToken);
            _sessionPrepared = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MESSAGES] Session non préparée : {ex}");
            _sessionPrepared = true;
        }
    }

    private async Task LoadMessagesAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            FeedbackMessage = "Chargement des messages…";
            FeedbackColor = Colors.Gold;

            var items = await _apis.GetListAsync<MessageEntry>("/api/crud/messages/list");

            Messages.Clear();
            foreach (var item in items)
            {
                Messages.Add(item);
            }

            FeedbackMessage = Messages.Count == 0
                ? "Aucun message à afficher pour le moment."
                : $"{Messages.Count} message(s) chargé(s).";
            FeedbackColor = Colors.LightGreen;
        }
        catch (OperationCanceledException)
        {
            FeedbackMessage = "Chargement annulé.";
            FeedbackColor = Colors.Orange;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            FeedbackMessage = "Accès refusé. Veuillez vous reconnecter.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] 401 lors du chargement : {ex}");
        }
        catch (Exception ex)
        {
            FeedbackMessage = "Impossible de charger les messages.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] Erreur de chargement : {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SubmitAsync()
    {
        if (IsSubmitting)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_messageText))
        {
            FeedbackMessage = "Ajoutez le contenu du message avant d'enregistrer.";
            FeedbackColor = Colors.OrangeRed;
            return;
        }

        try
        {
            IsSubmitting = true;
            FeedbackMessage = "Enregistrement en cours…";
            FeedbackColor = Colors.Gold;

            if (!_sessionPrepared)
            {
                await PrepareSessionAsync();
            }

            var payload = new
            {
                date_message = _messageDate.ToString("yyyy-MM-dd"),
                message = _messageText.Trim(),
                reponse = string.IsNullOrWhiteSpace(_responseText) ? null : _responseText.Trim(),
                etat = string.IsNullOrWhiteSpace(_statusText) ? null : _statusText.Trim()
            };

            var created = await _apis.PostBoolAsync("/api/crud/messages/create", payload);

            if (created)
            {
                FeedbackMessage = "Message créé avec succès.";
                FeedbackColor = Colors.LightGreen;

                MessageText = string.Empty;
                ResponseText = string.Empty;
                StatusText = string.Empty;
                MessageDate = DateTime.Today;

                await LoadMessagesAsync();
            }
            else
            {
                FeedbackMessage = "La création du message a échoué.";
                FeedbackColor = Colors.OrangeRed;
            }
        }
        catch (OperationCanceledException)
        {
            FeedbackMessage = "Création annulée.";
            FeedbackColor = Colors.Orange;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            FeedbackMessage = "Session expirée. Veuillez vous reconnecter.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] 401 lors de la création : {ex}");
        }
        catch (Exception ex)
        {
            FeedbackMessage = "Impossible de créer le message.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] Erreur de création : {ex}");
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}
