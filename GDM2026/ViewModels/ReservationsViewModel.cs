using System;
using System.Linq;

namespace GDM2026.ViewModels;

public class ReservationsViewModel : OrderStatusPageViewModel
{
    public ReservationsViewModel()
    {
        IsReservationMode = true;
        StartDate = DateTime.Today.AddDays(-7);
        EndDate = DateTime.Today;
        Status = "Confirmée";

        if (ReservationStatuses.Count > 0)
        {
            ReservationStatuses[0].IsSelected = true;
        }
    }

    /// <summary>
    /// Sélectionne un statut par son nom (utilisé lors de la navigation depuis la page d'accueil)
    /// </summary>
    public void SelectStatusByName(string statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
            return;

        // Désélectionner tous les statuts
        foreach (var tile in ReservationStatuses)
        {
            tile.IsSelected = false;
        }

        // Sélectionner le statut correspondant
        var matchingStatus = ReservationStatuses.FirstOrDefault(s =>
            string.Equals(s.Status, statusName, StringComparison.OrdinalIgnoreCase));

        if (matchingStatus != null)
        {
            matchingStatus.IsSelected = true;
            Status = matchingStatus.Status;
        }
        else
        {
            // Si le statut n'existe pas encore dans les tuiles, on le définit quand même
            Status = statusName;
        }
    }
}
