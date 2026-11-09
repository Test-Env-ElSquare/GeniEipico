namespace Application.Dtos.Alerts;

// Compatibility contract used by the EMS AlertsHub/ReceiveAlert client.
public sealed record RealtimeAlertDto(
    int Id,
    string Title,
    string Message,
    int Type,
    int Severity,
    bool IsRead,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    int? AlarmLogId,
    int? TicketId);
