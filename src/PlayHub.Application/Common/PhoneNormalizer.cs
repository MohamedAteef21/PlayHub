using System.Text;

namespace PlayHub.Application.Common;

public static class PhoneNormalizer
{
    /// <summary>Keep digits only. Leading 0 for Egyptian-length numbers is preserved.</summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var sb = new StringBuilder(phone.Length);
        foreach (var ch in phone)
        {
            if (char.IsDigit(ch))
                sb.Append(ch);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Egyptian mobile stored as 01xxxxxxxxx (11 digits) → WhatsApp expects 201xxxxxxxxx.
    /// </summary>
    public static string ToWhatsAppNumber(string? normalizedOrRaw)
    {
        var digits = Normalize(normalizedOrRaw);
        if (digits.Length == 11 && digits.StartsWith('0'))
            return "20" + digits[1..];
        return digits;
    }
}
