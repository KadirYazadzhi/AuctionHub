using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;

namespace AuctionHub.Infrastructure.ModelBinders;

public class DoubleModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var valueAsString = valueProviderResult.FirstValue;

        if (string.IsNullOrEmpty(valueAsString))
        {
            return Task.CompletedTask;
        }

        // Replace comma with dot to ensure uniform parsing
        valueAsString = valueAsString.Replace(",", ".");

        if (!double.TryParse(valueAsString, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Invalid number format.");
            return Task.CompletedTask;
        }

        bindingContext.Model = result;
        return Task.CompletedTask;
    }
}

public class DoubleModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata.ModelType == typeof(double) || context.Metadata.ModelType == typeof(double?))
        {
            return new DoubleModelBinder();
        }

        return null;
    }
}
