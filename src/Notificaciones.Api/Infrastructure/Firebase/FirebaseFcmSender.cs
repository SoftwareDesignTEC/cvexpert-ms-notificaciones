using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Notificaciones.Api.Services.Channels;

namespace Notificaciones.Api.Infrastructure.Firebase;

/// <summary>
/// <see cref="IFcmSender"/> con el SDK de Firebase Admin. La app de Firebase se crea la primera vez que se usa,
/// para que una credencial mal formada falle en el envío (y el canal lo registre) y no al arrancar.
/// </summary>
public class FirebaseFcmSender(string serviceAccountJson) : IFcmSender
{
    // Límite de tokens por llamada de SendEachForMulticastAsync.
    private const int MaxTokensPerCall = 500;

    private readonly Lazy<FirebaseMessaging> _messaging = new(() =>
    {
        var credential = CredentialFactory.FromJson<ServiceAccountCredential>(serviceAccountJson).ToGoogleCredential();
        var app = FirebaseApp.Create(new AppOptions { Credential = credential }, "cvexpert-notificaciones");
        return FirebaseMessaging.GetMessaging(app);
    });

    public async Task<IReadOnlyList<FcmSendResult>> SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var results = new List<FcmSendResult>(tokens.Count);
        foreach (var chunk in tokens.Chunk(MaxTokensPerCall))
        {
            var response = await _messaging.Value.SendEachForMulticastAsync(new MulticastMessage
            {
                // FirebaseAdmin 3.7 marca Tokens como obsoleto en favor de Fids (Firebase Installation IDs), pero sigue
                // funcionando. Pasar a FIDs cambia lo que registra Android en POST /notifications/devices: es un cambio
                // de contrato que se acuerda con el equipo.
#pragma warning disable CS0618
                Tokens = chunk,
#pragma warning restore CS0618
                Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
                Data = data,
            }, cancellationToken);

            for (var i = 0; i < chunk.Length; i++)
                results.Add(ToResult(chunk[i], response.Responses[i]));
        }
        return results;
    }

    private static FcmSendResult ToResult(string token, SendResponse response)
    {
        if (response.IsSuccess) return new FcmSendResult(token, FcmSendStatus.Sent);

        var code = response.Exception.MessagingErrorCode;
        return code == MessagingErrorCode.Unregistered
            ? new FcmSendResult(token, FcmSendStatus.Unregistered, code.ToString())
            : new FcmSendResult(token, FcmSendStatus.Failed, code?.ToString() ?? response.Exception.Message);
    }
}
