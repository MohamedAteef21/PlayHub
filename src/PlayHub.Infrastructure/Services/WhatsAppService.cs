using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlayHub.Application.Alerts;
using PlayHub.Application.Common;
using PlayHub.Application.WhatsApp;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IAuditService _audit;
    private readonly IInvoicePdfService _pdfService;

    public WhatsAppService(
        PlayHubDbContext db,
        TenantContext tenantContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IAuditService audit,
        IInvoicePdfService pdfService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _audit = audit;
        _pdfService = pdfService;
    }

    public async Task<WhatsAppStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var tenant = await GetTenantAsync(ct);
        var clientId = ClientId(tenant.Id);

        try
        {
            var client = CreateClient(ResolveBaseUrl(tenant), clientId, tenant.WhatsAppSessionId);
            // Lazy-start Chromium for this tenant
            using var ensureRes = await client.PostAsync("ensure", content: null, ct);
            using var statusRes = await client.GetAsync("status", ct);
            if (!statusRes.IsSuccessStatusCode)
                return StoredStatus(tenant, gatewayReady: false);

            await using var stream = await statusRes.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var ready = ReadBool(root, "ready")
                || string.Equals(ReadString(root, "status"), "ready", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ReadString(root, "status"), "AUTHENTICATED", StringComparison.OrdinalIgnoreCase);

            var gatewaySessionId = ReadString(root, "sessionId");
            var gatewayPhone = ReadString(root, "phone")
                ?? ReadString(root, "phoneNumber");

            // Auto-persist when gateway becomes ready
            if (ready && !string.IsNullOrWhiteSpace(gatewaySessionId))
            {
                await PersistSessionIfChangedAsync(tenant.Id, gatewaySessionId, gatewayPhone, ct);
                tenant = await GetTenantAsync(ct);
            }

            return new WhatsAppStatusDto(
                ready,
                gatewaySessionId ?? tenant.WhatsAppSessionId,
                gatewayPhone ?? tenant.WhatsAppConnectedPhone,
                ready ? (tenant.WhatsAppConnectedAt ?? DateTime.UtcNow) : tenant.WhatsAppConnectedAt);
        }
        catch
        {
            return StoredStatus(tenant, gatewayReady: false);
        }
    }

    public async Task<WhatsAppQrDto> GetQrAsync(CancellationToken ct = default)
    {
        var tenant = await GetTenantAsync(ct);
        var client = CreateClient(ResolveBaseUrl(tenant), ClientId(tenant.Id), tenant.WhatsAppSessionId);

        using var ensureRes = await client.PostAsync("ensure", content: null, ct);
        using var res = await client.GetAsync("qr", ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"WhatsApp QR request failed: {err}");
        }

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var qr = ReadString(root, "qr") ?? ReadString(root, "code");
        var qrBase64 = ReadString(root, "qrBase64")
            ?? ReadString(root, "qr_base64")
            ?? ReadString(root, "base64")
            ?? (qr is not null && qr.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? qr : null);
        var ready = ReadBool(root, "ready");

        if (ready)
        {
            var sessionId = ReadString(root, "sessionId");
            var phone = ReadString(root, "phone") ?? ReadString(root, "phoneNumber");
            if (!string.IsNullOrWhiteSpace(sessionId))
                await PersistSessionIfChangedAsync(tenant.Id, sessionId, phone, ct);
        }

        return new WhatsAppQrDto(qr, qrBase64, ready);
    }

    public async Task<WhatsAppStatusDto> SaveSessionAsync(SaveWhatsAppSessionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            throw new InvalidOperationException("Session id is required.");

        await PersistSessionIfChangedAsync(
            _tenantContext.TenantId,
            request.SessionId.Trim(),
            request.Phone,
            ct,
            force: true);

        var tenant = await GetTenantAsync(ct);
        return new WhatsAppStatusDto(true, tenant.WhatsAppSessionId, tenant.WhatsAppConnectedPhone, tenant.WhatsAppConnectedAt);
    }

    public async Task<WhatsAppStatusDto> DisconnectAsync(CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstAsync(t => t.Id == _tenantContext.TenantId, ct);
        try
        {
            var client = CreateClient(ResolveBaseUrl(tenant), ClientId(tenant.Id), tenant.WhatsAppSessionId);
            using var res = await client.PostAsync("disconnect", content: null, ct);
            _ = await res.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            // Gateway may be down — still clear DB link
        }

        tenant.WhatsAppSessionId = null;
        tenant.WhatsAppConnectedPhone = null;
        tenant.WhatsAppConnectedAt = null;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("WhatsApp.Disconnected", "Tenant", tenant.Id, null, ct: ct);

        return new WhatsAppStatusDto(false, null, null, null);
    }

    public async Task<SendWhatsAppResultDto> SendTextAsync(string phone, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new InvalidOperationException("Message is required.");

        var waPhone = PhoneNormalizer.ToWhatsAppNumber(phone);
        if (string.IsNullOrWhiteSpace(waPhone))
            throw new InvalidOperationException("Phone is required.");

        var tenant = await GetTenantAsync(ct);
        if (string.IsNullOrWhiteSpace(tenant.WhatsAppSessionId))
        {
            // Try live gateway session
            var live = await GetStatusAsync(ct);
            if (!live.Ready || string.IsNullOrWhiteSpace(live.SessionId))
                throw new InvalidOperationException("WhatsApp is not connected for this venue. Scan the QR first.");
            tenant = await GetTenantAsync(ct);
        }

        return await PostSendAsync(tenant, waPhone, message.Trim(), ct);
    }

    public async Task<SendWhatsAppResultDto> SendDocumentAsync(
        string phone, string caption, byte[] fileBytes, string fileName, string contentType, CancellationToken ct = default)
    {
        if (fileBytes.Length == 0)
            throw new InvalidOperationException("File is empty.");

        var waPhone = PhoneNormalizer.ToWhatsAppNumber(phone);
        if (string.IsNullOrWhiteSpace(waPhone))
            throw new InvalidOperationException("Phone is required.");

        var tenant = await GetTenantAsync(ct);
        if (string.IsNullOrWhiteSpace(tenant.WhatsAppSessionId))
            throw new InvalidOperationException("WhatsApp is not connected for this venue.");

        var client = CreateClient(ResolveBaseUrl(tenant), ClientId(tenant.Id), tenant.WhatsAppSessionId);
        var payload = new
        {
            phone = waPhone,
            caption,
            message = caption,
            base64 = Convert.ToBase64String(fileBytes),
            fileName,
            mimeType = contentType,
            sessionId = tenant.WhatsAppSessionId
        };

        using var res = await client.PostAsJsonAsync("send-document", payload, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (res.IsSuccessStatusCode)
        {
            string? messageId = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                messageId = ReadString(doc.RootElement, "messageId")
                    ?? ReadString(doc.RootElement, "id");
            }
            catch { /* ignore */ }
            return new SendWhatsAppResultDto(true, messageId, null);
        }

        return await PostSendAsync(tenant, waPhone,
            $"{caption}\n\n(PDF attachment unavailable — print from PlayHub if needed)", ct);
    }

    public async Task<SendWhatsAppResultDto> SendInvoiceAsync(Guid sessionId, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var session = await _db.Sessions
            .Include(s => s.Customer)
            .Include(s => s.Invoice)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Session not found.");

        if (session.CustomerId is null || session.Customer is null)
            throw new InvalidOperationException("WhatsApp invoices can only be sent to registered customers.");

        if (string.IsNullOrWhiteSpace(session.Customer.Phone))
            throw new InvalidOperationException("Customer has no phone number.");

        if (session.Invoice is null)
            throw new InvalidOperationException("Session has no invoice yet. Close the session first.");

        var invoiceNumber = session.Invoice.InvoiceNumber;
        var total = session.Invoice.Total;
        var customerName = session.Customer.Name;

        var caption = new StringBuilder()
            .AppendLine($"مرحباً {customerName}")
            .AppendLine($"فاتورتك مرفقة PDF / Your invoice PDF is attached")
            .AppendLine($"رقم الفاتورة / Invoice: {invoiceNumber}")
            .AppendLine($"الإجمالي / Total: {total:0.00}")
            .AppendLine("شكراً لزيارتكم")
            .ToString()
            .Trim();

        byte[]? pdf = null;
        try
        {
            pdf = await _pdfService.BuildSessionInvoicePdfAsync(sessionId, ct);
        }
        catch
        {
            // fall back to text
        }

        SendWhatsAppResultDto result;
        if (pdf is { Length: > 0 })
        {
            result = await SendDocumentAsync(
                session.Customer.Phone,
                caption,
                pdf,
                $"invoice-{invoiceNumber}.pdf",
                "application/pdf",
                ct);
        }
        else
        {
            result = await SendTextAsync(session.Customer.Phone, caption, ct);
        }

        await _audit.LogAsync("WhatsApp.InvoiceSent", "Session", session.Id, new
        {
            invoiceNumber,
            total,
            session.CustomerId,
            WithPdf = pdf is { Length: > 0 }
        }, ct: ct);
        return result;
    }

    public async Task<SendWhatsAppResultDto> SendOfferAsync(Guid customerId, Guid offerId, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.IsActive, ct)
            ?? throw new KeyNotFoundException("Customer not found.");

        if (string.IsNullOrWhiteSpace(customer.Phone))
            throw new InvalidOperationException("Customer has no phone number.");

        var offer = await _db.CustomerOffers.FirstOrDefaultAsync(o => o.Id == offerId && o.IsActive, ct)
            ?? throw new KeyNotFoundException("Offer not found.");

        var message = $"{offer.Title}\n\n{offer.Message}";
        var result = await SendTextAsync(customer.Phone, message, ct);
        await _audit.LogAsync("WhatsApp.OfferSent", "Customer", customer.Id, new
        {
            offerId,
            offer.Title
        }, ct: ct);
        return result;
    }

    private async Task PersistSessionIfChangedAsync(
        Guid tenantId, string sessionId, string? phone, CancellationToken ct, bool force = false)
    {
        var tenant = await _db.Tenants.FirstAsync(t => t.Id == tenantId, ct);
        var normalizedPhone = string.IsNullOrWhiteSpace(phone) ? null : PhoneNormalizer.Normalize(phone);

        if (!force
            && tenant.WhatsAppSessionId == sessionId
            && (normalizedPhone is null || tenant.WhatsAppConnectedPhone == normalizedPhone))
            return;

        tenant.WhatsAppSessionId = sessionId;
        if (normalizedPhone is not null)
            tenant.WhatsAppConnectedPhone = normalizedPhone;
        tenant.WhatsAppConnectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("WhatsApp.SessionSaved", "Tenant", tenant.Id, new
        {
            tenant.WhatsAppSessionId,
            tenant.WhatsAppConnectedPhone
        }, ct: ct);
    }

    private static WhatsAppStatusDto StoredStatus(Domain.Entities.Tenant tenant, bool gatewayReady) =>
        new(gatewayReady && !string.IsNullOrWhiteSpace(tenant.WhatsAppSessionId),
            tenant.WhatsAppSessionId,
            tenant.WhatsAppConnectedPhone,
            tenant.WhatsAppConnectedAt);

    private async Task<SendWhatsAppResultDto> PostSendAsync(
        Domain.Entities.Tenant tenant, string waPhone, string message, CancellationToken ct)
    {
        var sessionId = tenant.WhatsAppSessionId
            ?? throw new InvalidOperationException("WhatsApp is not connected for this venue.");

        var client = CreateClient(ResolveBaseUrl(tenant), ClientId(tenant.Id), sessionId);
        using var res = await client.PostAsJsonAsync("send", new
        {
            phone = waPhone,
            number = waPhone,
            message,
            sessionId
        }, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            return new SendWhatsAppResultDto(false, null, string.IsNullOrWhiteSpace(body) ? res.ReasonPhrase : body);

        string? messageId = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            messageId = ReadString(doc.RootElement, "messageId")
                ?? ReadString(doc.RootElement, "id")
                ?? ReadString(doc.RootElement, "message_id");
        }
        catch
        {
            // Non-JSON success body is fine.
        }

        return new SendWhatsAppResultDto(true, messageId, null);
    }

    private async Task<Domain.Entities.Tenant> GetTenantAsync(CancellationToken ct) =>
        await _db.Tenants.AsNoTracking().FirstAsync(t => t.Id == _tenantContext.TenantId, ct);

    private static string ClientId(Guid tenantId) => tenantId.ToString("N");

    private string ResolveBaseUrl(Domain.Entities.Tenant tenant)
    {
        var configured = tenant.WhatsAppApiBaseUrl;
        if (string.IsNullOrWhiteSpace(configured))
            configured = _configuration["WhatsApp:ApiBaseUrl"];

        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("WhatsApp API base URL is not configured.");

        return configured.TrimEnd('/') + "/";
    }

    private HttpClient CreateClient(string baseUrl, string clientId, string? sessionId)
    {
        var client = _httpClientFactory.CreateClient("WhatsApp");
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Remove("X-Client-Id");
        client.DefaultRequestHeaders.Add("X-Client-Id", clientId);
        client.DefaultRequestHeaders.Remove("X-Session-Id");
        if (!string.IsNullOrWhiteSpace(sessionId))
            client.DefaultRequestHeaders.Add("X-Session-Id", sessionId);
        return client;
    }

    private static string? ReadString(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;
        if (!el.TryGetProperty(name, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    private static bool ReadBool(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var prop))
            return false;
        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(prop.GetString(), out var b) && b,
            _ => false
        };
    }
}
