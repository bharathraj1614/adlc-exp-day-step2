using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace OuterloopLabApi.Models;

public sealed class ConvertCurrencyRequest
{
    public decimal Amount { get; set; }
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;

    public bool IsValid(out ValidationProblemDetails problemDetails)
    {
        problemDetails = new ValidationProblemDetails();

        if (Amount <= 0)
        {
            problemDetails.Errors["amount"] = new[] { "amount must be greater than 0" };
        }

        var currencyRegex = new Regex("^[A-Z]{3}$", RegexOptions.Compiled);
        if (!currencyRegex.IsMatch(FromCurrency ?? string.Empty))
        {
            problemDetails.Errors["fromCurrency"] = new[] { "fromCurrency must be a 3-letter uppercase ISO code" };
        }

        if (!currencyRegex.IsMatch(ToCurrency ?? string.Empty))
        {
            problemDetails.Errors["toCurrency"] = new[] { "toCurrency must be a 3-letter uppercase ISO code" };
        }

        return problemDetails.Errors.Count == 0;
    }
}
