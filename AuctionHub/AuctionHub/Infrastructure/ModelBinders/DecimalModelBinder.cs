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

        if (string.IsNullOrEmpty(value))
        {
            return Task.CompletedTask;
        }

        // Replace comma with dot to standardize parsing
        // If both . and , are present, assume the last one is the decimal separator
        // and others are thousand separators.
        
        int lastComma = value.LastIndexOf(',');
        int lastDot = value.LastIndexOf('.');
        
        if (lastComma > lastDot)
        {
            // Comma is the decimal separator. Remove all dots, and all commas except the last one.
            string beforeLastComma = value.Substring(0, lastComma);
            string afterLastComma = value.Substring(lastComma + 1);
            value = beforeLastComma.Replace(".", "").Replace(",", "") + "." + afterLastComma;
        }
        else if (lastDot > lastComma)
        {
            // Dot is the decimal separator. Remove all commas, and all dots except the last one.
            string beforeLastDot = value.Substring(0, lastDot);
            string afterLastDot = value.Substring(lastDot + 1);
            value = beforeLastDot.Replace(",", "").Replace(".", "") + "." + afterLastDot;
        }
        else
        {
            // No separators or just one type. Standard replacement.
            value = value.Replace(",", ".");
        }

        if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
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
