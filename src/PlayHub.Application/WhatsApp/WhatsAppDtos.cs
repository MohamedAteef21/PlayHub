namespace PlayHub.Application.WhatsApp;

public record WhatsAppStatusDto(
    bool Ready,
    string? SessionId,
    string? Phone,
    DateTime? ConnectedAt);

public record WhatsAppQrDto(string? Qr, string? QrBase64, bool Ready);

public record SaveWhatsAppSessionRequest(string SessionId, string? Phone);

public record SendWhatsAppTextRequest(string Phone, string Message);

public record SendOfferRequest(Guid CustomerId, Guid OfferId);

public record SendWhatsAppResultDto(bool Success, string? MessageId, string? Error);

public interface IWhatsAppService
{
    Task<WhatsAppStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<WhatsAppQrDto> GetQrAsync(CancellationToken ct = default);
    Task<WhatsAppStatusDto> SaveSessionAsync(SaveWhatsAppSessionRequest request, CancellationToken ct = default);
    Task<WhatsAppStatusDto> DisconnectAsync(CancellationToken ct = default);
    Task<SendWhatsAppResultDto> SendTextAsync(string phone, string message, CancellationToken ct = default);
    Task<SendWhatsAppResultDto> SendDocumentAsync(string phone, string caption, byte[] fileBytes, string fileName, string contentType, CancellationToken ct = default);
    Task<SendWhatsAppResultDto> SendInvoiceAsync(Guid sessionId, CancellationToken ct = default);
    Task<SendWhatsAppResultDto> SendOfferAsync(Guid customerId, Guid offerId, CancellationToken ct = default);
}
