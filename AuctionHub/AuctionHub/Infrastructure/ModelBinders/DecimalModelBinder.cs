using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;

namespace AuctionHub.Infrastructure.ModelBinders;

public class DecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;

        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.CompletedTask;
        }

        // Extremely robust parsing for Bulgarian/International formats
        // 1. Remove all spaces and currency symbols
        string cleanValue = value.Replace(" ", "").Replace("€", "").Replace("$", "").Replace("\u00A0", "").Trim();
        
        // 2. Identify the decimal separator.
        // We look for the LAST separator (dot or comma).
        int lastComma = cleanValue.LastIndexOf(',');
        int lastDot = cleanValue.LastIndexOf('.');
        
        // Determine which one is the decimal separator (the one that appears last)
        if (lastComma > lastDot)
        {
            // Comma is the decimal separator (European/BG style)
            // Remove all dots (thousand separators) and replace comma with dot for InvariantCulture
            cleanValue = cleanValue.Replace(".", "").Replace(",", ".");
        }
        else if (lastDot > lastComma)
        {
            // Dot is the decimal separator (US/International style)
            // Remove all commas (thousand separators)
            cleanValue = cleanValue.Replace(",", "");
        }
        // If they are equal (both -1), cleanValue is just the number string already.

        if (!decimal.TryParse(cleanValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Invalid decimal value.");
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(result);
        return Task.CompletedTask;
    }
}

public class DecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata.ModelType == typeof(decimal) || context.Metadata.ModelType == typeof(decimal?))
        {
            return new DecimalModelBinder();
        }

        return null;
    }
}
