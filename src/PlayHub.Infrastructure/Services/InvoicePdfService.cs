using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Alerts;
using PlayHub.Application.Common;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class InvoicePdfService : IInvoicePdfService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;

    static InvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public InvoicePdfService(PlayHubDbContext db, TenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<byte[]> BuildSessionInvoicePdfAsync(Guid sessionId, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var session = await _db.Sessions
            .Include(s => s.Device)
            .Include(s => s.Room)
            .Include(s => s.PricingPlan)
            .Include(s => s.OpenedByUser)
            .Include(s => s.ClosedByUser)
            .Include(s => s.Customer)
            .Include(s => s.CafeteriaLines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.Invoice).ThenInclude(i => i!.Payments)
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Session not found.");

        if (session.Invoice is null)
            throw new InvalidOperationException("Session has no invoice yet.");

        var invoice = session.Invoice;
        var payment = invoice.Payments.FirstOrDefault();
        var lines = session.CafeteriaLines
            .Where(l => l.Quantity - l.ReturnedQuantity > 0)
            .ToList();

        var guest = session.IsQuickGuest
            ? session.QuickGuestName
            : session.Customer?.Name;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("PlayHub Invoice / فاتورة").Bold().FontSize(16);
                    col.Item().Text(session.Branch.Name).FontSize(11);
                    col.Item().Text($"#{invoice.InvoiceNumber}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Text($"Device: {session.Device.Name} · {session.Room?.Name ?? "—"}");
                    col.Item().Text($"Mode: {(session.SessionMode == SessionMode.Gaming ? "Gaming" : "Watching")} · {session.PricingPlan.Name}");
                    if (!string.IsNullOrWhiteSpace(guest))
                        col.Item().Text($"Customer: {guest}");
                    col.Item().Text($"Started: {FormatEgypt(session.StartedAt)}");
                    col.Item().Text($"Closed: {FormatEgypt(session.ClosedAt)}");

                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    var segments = SessionBillingSegments.Read(session);
                    if (segments.Count > 0)
                    {
                        col.Item().PaddingTop(6).Text("Billing detail / تفصيل الحساب").Bold();
                        foreach (var seg in segments)
                        {
                            var people = seg.PeopleCount ?? 0;
                            var formula = seg.QuantityUnit switch
                            {
                                "match" => $"تمن المباراة {seg.Rate:0.##} × {seg.Quantity} مباريات = {seg.Amount:0.##}",
                                "guest" => $"تمن الفرد {seg.Rate:0.##} × {seg.Quantity} أفراد = {seg.Amount:0.##}",
                                "hour" when people > 0 =>
                                    $"تمن الفرد {seg.Rate:0.##} × {people} أفراد × {seg.Quantity:0.####} ساعة = {seg.Amount:0.##}",
                                "min" when people > 0 =>
                                    $"تمن الفرد {seg.Rate:0.##} × {people} أفراد × {seg.Quantity:0.##} دقيقة = {seg.Amount:0.##}",
                                "hour" => $"سعر الساعة {seg.Rate:0.##} × {seg.Quantity:0.####} ساعة = {seg.Amount:0.##}",
                                "min" => $"السعر {seg.Rate:0.##} × {seg.Quantity:0.##} دقيقة = {seg.Amount:0.##}",
                                _ => $"{seg.Rate:0.##} × {seg.Quantity} = {seg.Amount:0.##}"
                            };
                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text(formula);
                                r.ConstantItem(80).AlignRight().Text($"{seg.Amount:0.00}");
                            });
                        }
                    }
                    else
                    {
                        col.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Text("Time / الوقت");
                            r.ConstantItem(80).AlignRight().Text($"{session.TimeCost:0.00}");
                        });
                    }
                    if (session.RoomSurchargeCost > 0)
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Room (VIP) / رسم الغرفة");
                            r.ConstantItem(80).AlignRight().Text($"{session.RoomSurchargeCost:0.00}");
                        });
                    }
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Cafeteria / كافتيريا");
                        r.ConstantItem(80).AlignRight().Text($"{session.CafeteriaCost:0.00}");
                    });
                    if (session.DiscountAmount > 0)
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Discount / خصم");
                            r.ConstantItem(80).AlignRight().Text($"-{session.DiscountAmount:0.00}");
                        });
                    }

                    if (lines.Count > 0)
                    {
                        col.Item().PaddingTop(8).Text("Items").Bold();
                        foreach (var l in lines)
                        {
                            var qty = l.Quantity - l.ReturnedQuantity;
                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text($"{l.CafeteriaItem.Name} × {qty}");
                                r.ConstantItem(80).AlignRight().Text($"{l.UnitPrice * qty:0.00}");
                            });
                        }
                    }

                    col.Item().PaddingTop(10).Row(r =>
                    {
                        r.RelativeItem().Text("Total / الإجمالي").Bold().FontSize(12);
                        r.ConstantItem(80).AlignRight().Text($"{invoice.Total:0.00} EGP").Bold().FontSize(12);
                    });

                    if (payment is not null)
                        col.Item().Text($"Payment: {payment.PaymentMethod}");
                });

                page.Footer().AlignCenter().Text("شكراً لزيارتكم · Thank you").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    private static string FormatEgypt(DateTime? utc)
    {
        if (utc is null) return "—";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo");
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), tz);
        return local.ToString("yyyy-MM-dd hh:mm tt");
    }
}
