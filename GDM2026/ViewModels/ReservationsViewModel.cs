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
}
