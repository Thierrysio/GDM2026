using System.Collections.Generic;

namespace GDM2026.Services;

public static class OrderStatusDeltaTracker
{
    private static readonly object _sync = new();
    private static readonly Dictionary<string, int> _deltas = new();

    public static void RecordChange(string? previousStatus, string newStatus)
    {
        if (string.IsNullOrWhiteSpace(newStatus))
        {
            return;
        }

        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(previousStatus))
            {
                _deltas[previousStatus] = GetDelta(previousStatus) - 1;
                RemoveIfZero(previousStatus);
            }

            _deltas[newStatus] = GetDelta(newStatus) + 1;
            RemoveIfZero(newStatus);
        }
    }

    public static IReadOnlyDictionary<string, int> GetDeltas()
    {
        lock (_sync)
        {
            return new Dictionary<string, int>(_deltas);
        }
    }

    public static void Clear()
    {
        lock (_sync)
        {
            _deltas.Clear();
        }
    }

    private static int GetDelta(string status) => _deltas.TryGetValue(status, out var value) ? value : 0;

    private static void RemoveIfZero(string status)
    {
        if (_deltas.TryGetValue(status, out var value) && value == 0)
        {
            _deltas.Remove(status);
        }
    }
}
