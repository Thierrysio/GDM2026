using GDM2026.Models;
using GDM2026.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GDM2026
{
    [QueryProperty(nameof(Status), "status")]
    public partial class OrderStatusPage : ContentPage, INotifyPropertyChanged
    {
        private readonly Apis _apis = new();
        private string? _status;
        private bool _isLoading;
        private bool _loaded;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<OrderProductItem> Products { get; } = new();

        public string? Status
        {
            get => _status;
            set
            {
                if (_status == value)
                {
                    return;
                }

                _status = value;
                OnPropertyChanged();
                _loaded = false;
                UpdateHeaders();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(IsNotLoading));
                }
            }
        }

        public bool IsNotLoading => !IsLoading;

        public OrderStatusPage()
        {
            InitializeComponent();
            BindingContext = this;
            UpdateHeaders();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_loaded)
            {
                return;
            }

            _loaded = true;
            await LoadProductsAsync().ConfigureAwait(false);
        }

        private async Task LoadProductsAsync()
        {
            if (string.IsNullOrWhiteSpace(Status))
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsLoading = true;
                MessageLabel.Text = "Chargement des produits...";
                MessageLabel.IsVisible = true;
                ProductsView.IsVisible = false;
                Products.Clear();
            });

            try
            {
                var encodedStatus = Uri.EscapeDataString(Status);
                var endpoint = $"https://dantecmarket.com/api/mobile/getCommandesParStatus?status={encodedStatus}";

                var orders = await _apis
                    .GetListAsync<OrderByStatus>(endpoint)
                    .ConfigureAwait(false);

                var productLines = orders?
                    .SelectMany(order => order?.LesCommandes ?? Enumerable.Empty<OrderLine>())
                    .Where(line => line?.LeProduit is not null)
                    .Select(line => new OrderProductItem(
                        line!.LeProduit?.NomProduit ?? "Produit",
                        BuildImageUrl(line.LeProduit)))
                    .ToList() ?? new List<OrderProductItem>();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var item in productLines)
                    {
                        Products.Add(item);
                    }

                    ProductsView.IsVisible = Products.Count > 0;
                    MessageLabel.Text = Products.Count > 0
                        ? string.Empty
                        : "Aucun produit pour cet état de commande.";
                    MessageLabel.IsVisible = string.IsNullOrWhiteSpace(MessageLabel.Text) == false;
                });
            }
            catch (TaskCanceledException)
            {
                await ShowErrorAsync("Délai dépassé lors du chargement des produits.").ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                await ShowErrorAsync("Impossible de récupérer les produits de cet état.").ConfigureAwait(false);
            }
            catch (Exception)
            {
                await ShowErrorAsync("Une erreur est survenue lors du chargement.").ConfigureAwait(false);
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = false);
            }
        }

        private async Task ShowErrorAsync(string message)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Products.Clear();
                ProductsView.IsVisible = false;
                MessageLabel.Text = message;
                MessageLabel.IsVisible = true;
            });
        }

        private void UpdateHeaders()
        {
            var statusLabel = string.IsNullOrWhiteSpace(Status) ? "" : Status.Trim();

            HeaderLabel.Text = string.IsNullOrWhiteSpace(statusLabel)
                ? "Produits de la commande"
                : $"Commandes : {statusLabel}";

            SubtitleLabel.Text = string.IsNullOrWhiteSpace(statusLabel)
                ? ""
                : $"Produits associés à l'état \"{statusLabel}\"";
        }

        private static string? BuildImageUrl(ProductSummary? product)
        {
            if (product == null)
            {
                return null;
            }

            var candidate = FirstNonEmpty(product.ImageProduit, product.ImageUrl, product.Photo, product.Descriptioncourte);

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            if (Uri.TryCreate(candidate, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            if (Uri.TryCreate(Constantes.BaseImagesAddress, UriKind.Absolute, out var baseUri))
            {
                return new Uri(baseUri, candidate.TrimStart('/')).ToString();
            }

            return candidate;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => string.IsNullOrWhiteSpace(value) == false);
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
            {
                return false;
            }

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
