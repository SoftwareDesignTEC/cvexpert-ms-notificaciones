using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace Notificaciones.Api.Services.Channels;

/// <summary>Envío de correo, aislado del SDK de Azure Communication Services para poder probar el canal.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, ComposedEmail email, CancellationToken cancellationToken);
}

/// <summary>Arma el correo de una notificación. Se reemplaza por las plantillas editables cuando existan.</summary>
public interface IEmailComposer
{
    ComposedEmail Compose(NotificationDelivery delivery);
}

public record ComposedEmail(string Subject, string Html, string PlainText);

/// <summary>
/// Correo al usuario del directorio, en su idioma. Sin entrada en el directorio no hay a quién enviarlo.
/// Solo para los tipos de <see cref="EmailTypes"/>.
/// </summary>
public class EmailNotificationChannel(
    IEmailSender sender,
    IEmailComposer composer,
    ILogger<EmailNotificationChannel> logger) : INotificationChannel
{
    /// <summary>
    /// Tipos que se envían por correo. job.new queda fuera hasta decidir las preferencias por canal
    /// (pregunta abierta 7 de docs/contratos/README.md): puede ser muy frecuente.
    /// </summary>
    public static readonly IReadOnlySet<string> EmailTypes = new HashSet<string>
    {
        "application.status_changed",
        "cv.generated",
        "course.recommended",
    };

    public string Name => "email";

    public async Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        var notification = delivery.Notification;
        if (!EmailTypes.Contains(notification.Type)) return;

        if (delivery.Recipient is null)
        {
            logger.LogInformation(
                "{UserId} no está en el directorio: la notificación {NotificationId} no se envía por correo",
                notification.UserId, notification.Id);
            return;
        }

        await sender.SendAsync(delivery.Recipient.Email, composer.Compose(delivery), cancellationToken);
        logger.LogInformation(
            "Correo de la notificación {NotificationId} ({Type}) enviado a {UserId}", notification.Id, notification.Type, notification.UserId);
    }
}

/// <summary>Asunto = title; cuerpo HTML simple con title, body y el pie "CVExpert", más una versión en texto plano.</summary>
public class DefaultEmailComposer : IEmailComposer
{
    // Escapa < > & y comillas, pero deja las tildes y la ñ legibles.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    public ComposedEmail Compose(NotificationDelivery delivery)
    {
        var (title, body) = delivery.Text;

        // title y body incluyen datos de otros servicios (jobTitle, company...): se escapan para HTML.
        var html = $"""
            <!doctype html>
            <html lang="{Html.Encode(delivery.Language)}">
            <body style="margin:0;padding:24px;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
              <div style="max-width:560px;margin:0 auto;">
                <h1 style="font-size:20px;margin:0 0 12px;">{Html.Encode(title)}</h1>
                <p style="font-size:15px;line-height:1.5;margin:0 0 24px;">{Html.Encode(body)}</p>
                <hr style="border:none;border-top:1px solid #e5e7eb;margin:0 0 12px;">
                <p style="font-size:12px;color:#6b7280;margin:0;">CVExpert</p>
              </div>
            </body>
            </html>
            """;
        var plainText = $"{title}\n\n{body}\n\n--\nCVExpert";

        return new ComposedEmail(title, html, plainText);
    }
}
