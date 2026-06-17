namespace Core.Interfaces;

public interface IEmailService
{
    Task<bool> SendDisasterApprovedEmail(string toEmail, string recipientName, string disasterTitle, string slug, CancellationToken ct = default);
    Task<bool> SendDisasterRejectedEmail(string toEmail, string recipientName, string disasterTitle, string reason, CancellationToken ct = default);
}
