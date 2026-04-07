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

        // Standardize: Replace comma with dot and remove any whitespace
        string standardizedValue = value.Replace(",", ".").Trim();

        // If there are multiple dots (e.g. from thousand separators), keep only the last one
        int lastDotIndex = standardizedValue.LastIndexOf('.');
        if (lastDotIndex != -1)
        {
            string integerPart = standardizedValue.Substring(0, lastDotIndex).Replace(".", "");
            string fractionalPart = standardizedValue.Substring(lastDotIndex + 1);
            standardizedValue = integerPart + "." + fractionalPart;
        }

        if (!decimal.TryParse(standardizedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
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
