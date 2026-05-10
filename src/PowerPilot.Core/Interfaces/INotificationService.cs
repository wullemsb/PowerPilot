using PowerPilot.Core.Models;

namespace PowerPilot.Core.Interfaces;

/// <summary>
/// Service for managing energy monitoring notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Event raised when a new notification is received.
    /// </summary>
    event EventHandler<EnergyNotification>? NotificationReceived;

    /// <summary>
    /// Adds a new notification and raises the NotificationReceived event.
    /// </summary>
    Task AddNotificationAsync(EnergyNotification notification);

    /// <summary>
    /// Gets the most recent notifications.
    /// </summary>
    Task<List<EnergyNotification>> GetRecentNotificationsAsync(int count = 10, bool unreadOnly = false);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    Task MarkAsReadAsync(string notificationId);

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    Task MarkAllAsReadAsync();

    /// <summary>
    /// Gets the count of unread notifications.
    /// </summary>
    Task<int> GetUnreadCountAsync();

    /// <summary>
    /// Clears all notifications.
    /// </summary>
    Task ClearAllAsync();
}
