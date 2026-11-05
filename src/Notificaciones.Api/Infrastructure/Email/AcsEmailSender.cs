using Azure;
using Azure.Communication.Email;
using Notificaciones.Api.Services.Channels;

namespace Notificaciones.Api.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> con Azure Communication Services. No espera a que el correo se entregue
/// (<see cref="WaitUntil.Started"/>): basta con que ACS lo acepte. El cliente se crea la primera vez que se usa,
/// para que una cadena mal formada falle en el envío y no al arrancar.
/// </summary>
public class AcsEmailSender(string connectionString, string senderAddress) : IEmailSender
{
    private readonly Lazy<EmailClient> _client = new(() => new EmailClient(connectionString));

    public Task SendAsync(string to, ComposedEmail email, CancellationToken cancellationToken)
    {
        var content = new EmailContent(email.Subject) { Html = email.Html, PlainText = email.PlainText };
        return _client.Value.SendAsync(WaitUntil.Started, new EmailMessage(senderAddress, to, content), cancellationToken);
    }
}
