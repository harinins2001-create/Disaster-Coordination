using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Core.Interfaces;

namespace Core.Infrastructure.Persistence.Services;

public class EmailService : IEmailService
{
    private readonly string _fromEmail;

    public EmailService()
    {
        _fromEmail = Environment.GetEnvironmentVariable("SES_FROM_EMAIL") ?? string.Empty;
    }

    public async Task<bool> SendDisasterApprovedEmail(string toEmail, string recipientName, string disasterTitle, string slug, CancellationToken ct = default)
    {
        var subject = $"Your disaster report has been approved: {disasterTitle}";
        var bodyText = $"Hi {recipientName},\n\nYour disaster report \"{disasterTitle}\" has been approved and is now visible to the public.\n\nSlug: {slug}\n\nThank you for your contribution.\n— DRCS";
        var bodyHtml = $"<p>Hi {System.Net.WebUtility.HtmlEncode(recipientName)},</p><p>Your disaster report <strong>{System.Net.WebUtility.HtmlEncode(disasterTitle)}</strong> has been approved and is now visible to the public.</p><p>Slug: <code>{System.Net.WebUtility.HtmlEncode(slug)}</code></p><p>Thank you for your contribution.<br/>— DRCS</p>";
        return await Send(toEmail, subject, bodyText, bodyHtml, ct);
    }

    public async Task<bool> SendDisasterRejectedEmail(string toEmail, string recipientName, string disasterTitle, string reason, CancellationToken ct = default)
    {
        var subject = $"Your disaster report was not approved: {disasterTitle}";
        var bodyText = $"Hi {recipientName},\n\nYour disaster report \"{disasterTitle}\" was not approved.\n\nReason: {reason}\n\nYou may revise and submit a new report at any time.\n— DRCS";
        var bodyHtml = $"<p>Hi {System.Net.WebUtility.HtmlEncode(recipientName)},</p><p>Your disaster report <strong>{System.Net.WebUtility.HtmlEncode(disasterTitle)}</strong> was not approved.</p><p><strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(reason)}</p><p>You may revise and submit a new report at any time.<br/>— DRCS</p>";
        return await Send(toEmail, subject, bodyText, bodyHtml, ct);
    }

    private async Task<bool> Send(string toEmail, string subject, string bodyText, string bodyHtml, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_fromEmail) || string.IsNullOrWhiteSpace(toEmail)) return false;

        try
        {
            using var client = new AmazonSimpleEmailServiceClient(Amazon.RegionEndpoint.APSouth1);
            var req = new SendEmailRequest
            {
                Source = _fromEmail,
                Destination = new Destination { ToAddresses = new List<string> { toEmail } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Text = new Content(bodyText),
                        Html = new Content(bodyHtml)
                    }
                }
            };
            await client.SendEmailAsync(req, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
