using PowerPilot.Core.Models;
using PowerPilot.Core.Interfaces;

namespace PowerPilot.Web.Services;

/// <summary>
/// Singleton service that manages energy monitoring notifications.
/// Provides thread-safe notification management and event-based updates for the UI.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly List<EnergyNotification> _notifications = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<NotificationService> _logger;

    /// <summary>
    /// Event raised when a new notification is received.
    /// UI components can subscribe to this event to display notifications in real-time.
    /// </summary>
    public event EventHandler<EnergyNotification>? NotificationReceived;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds a new notification and raises the NotificationReceived event.
    /// </summary>
    /// <param name="notification">The notification to add.</param>
    public async Task AddNotificationAsync(EnergyNotification notification)
    {
        await _lock.WaitAsync();
        try
        {
            _notifications.Add(notification);

            // Keep only the most recent 50 notifications to prevent memory growth
            if (_notifications.Count > 50)
            {
                _notifications.RemoveRange(0, _notifications.Count - 50);
            }

            _logger.LogInformation(
                "New energy notification added: {Message} (Surplus: {Surplus} kW, Agents: {AgentCount})",
                notification.Message,
                notification.EnergySurplusKw,
                notification.AgentContributions.Count);
        }
        finally
        {
            _lock.Release();
        }

        // Raise event outside the lock to prevent deadlocks
        NotificationReceived?.Invoke(this, notification);
    }

    /// <summary>
    /// Gets the most recent notifications, optionally filtering by read status.
    /// </summary>
    /// <param name="count">Maximum number of notifications to return.</param>
    /// <param name="unreadOnly">If true, only returns unread notifications.</param>
    /// <returns>List of recent notifications.</returns>
    public async Task<List<EnergyNotification>> GetRecentNotificationsAsync(int count = 10, bool unreadOnly = false)
    {
        await _lock.WaitAsync();
        try
        {
            var query = _notifications.AsEnumerable();

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            return query
                .OrderByDescending(n => n.Timestamp)
                .Take(count)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    /// <param name="notificationId">The ID of the notification to mark as read.</param>
    public async Task MarkAsReadAsync(string notificationId)
    {
        await _lock.WaitAsync();
        try
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                _logger.LogDebug("Notification {Id} marked as read", notificationId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    public async Task MarkAllAsReadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var notification in _notifications)
            {
                notification.IsRead = true;
            }
            _logger.LogDebug("All notifications marked as read");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets the count of unread notifications.
    /// </summary>
    public async Task<int> GetUnreadCountAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _notifications.Count(n => !n.IsRead);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Clears all notifications.
    /// </summary>
    public async Task ClearAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _notifications.Clear();
            _logger.LogInformation("All notifications cleared");
        }
        finally
        {
            _lock.Release();
        }
    }
}
