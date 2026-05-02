using System.Text.RegularExpressions;
using PowerPilot.Core.Models;

namespace PowerPilot.P1Reader;

public static class DsmrP1Parser
{
    private static readonly Regex EquipmentIdRegex = new(@"0-0:96\.1\.1\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex Tariff1DeliveredRegex = new(@"1-0:1\.8\.1\((\d+\.\d+)\*kWh\)", RegexOptions.Compiled);
    private static readonly Regex Tariff2DeliveredRegex = new(@"1-0:1\.8\.2\((\d+\.\d+)\*kWh\)", RegexOptions.Compiled);
    private static readonly Regex Tariff1ReturnedRegex = new(@"1-0:2\.8\.1\((\d+\.\d+)\*kWh\)", RegexOptions.Compiled);
    private static readonly Regex Tariff2ReturnedRegex = new(@"1-0:2\.8\.2\((\d+\.\d+)\*kWh\)", RegexOptions.Compiled);
    private static readonly Regex CurrentTariffRegex = new(@"0-0:96\.14\.0\((\d+)\)", RegexOptions.Compiled);
    private static readonly Regex CurrentPowerUsageRegex = new(@"1-0:1\.7\.0\((\d+\.\d+)\*kW\)", RegexOptions.Compiled);
    private static readonly Regex CurrentPowerDeliveryRegex = new(@"1-0:2\.7\.0\((\d+\.\d+)\*kW\)", RegexOptions.Compiled);
    private static readonly Regex GasDeliveredRegex = new(@"0-1:24\.2\.1\(\d+[WS]\)\((\d+\.\d+)\*m3\)", RegexOptions.Compiled);

    public static P1Telegram? Parse(string rawTelegram)
    {
        if (string.IsNullOrWhiteSpace(rawTelegram))
            return null;

        var telegram = new P1Telegram { Timestamp = DateTime.UtcNow };
        telegram.EquipmentIdentifier = ExtractString(EquipmentIdRegex, rawTelegram);
        telegram.ElectricityDeliveredTariff1 = ExtractDecimal(Tariff1DeliveredRegex, rawTelegram);
        telegram.ElectricityDeliveredTariff2 = ExtractDecimal(Tariff2DeliveredRegex, rawTelegram);
        telegram.ElectricityReturnedTariff1 = ExtractDecimal(Tariff1ReturnedRegex, rawTelegram);
        telegram.ElectricityReturnedTariff2 = ExtractDecimal(Tariff2ReturnedRegex, rawTelegram);
        telegram.CurrentTariff = (int)ExtractDecimal(CurrentTariffRegex, rawTelegram);
        telegram.CurrentPowerUsage = ExtractDecimal(CurrentPowerUsageRegex, rawTelegram);
        telegram.CurrentPowerDelivery = ExtractDecimal(CurrentPowerDeliveryRegex, rawTelegram);
        telegram.GasDelivered = ExtractDecimal(GasDeliveredRegex, rawTelegram);
        return telegram;
    }

    private static string? ExtractString(Regex regex, string input)
    {
        var match = regex.Match(input);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static decimal ExtractDecimal(Regex regex, string input)
    {
        var match = regex.Match(input);
        if (!match.Success) return 0m;
        return decimal.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }
}
