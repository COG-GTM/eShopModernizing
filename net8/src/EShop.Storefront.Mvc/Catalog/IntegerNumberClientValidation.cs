// MVC 5 emitted data-val-number ("The field {0} must be a number.") for every numeric property, including int
// (System.Web.Mvc NumericModelValidator / ClientDataTypeModelValidatorProvider). ASP.NET Core only does it for
// float/double/decimal, so int inputs (Stock, Restock, Max stock, hidden Id) lost their client-side number check.
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EShop.Storefront.Mvc.Catalog;

public sealed class IntegerNumberClientValidatorProvider : IClientModelValidatorProvider
{
    private static readonly HashSet<Type> IntegerTypes = new()
    {
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
    };

    public void CreateValidators(ClientValidatorProviderContext context)
    {
        var type = context.ModelMetadata.UnderlyingOrModelType;
        if (!IntegerTypes.Contains(type)) return;
        context.Results.Add(new ClientValidatorItem { Validator = new IntegerNumberClientValidator(), IsReusable = true });
    }
}

public sealed class IntegerNumberClientValidator : IClientModelValidator
{
    public void AddValidation(ClientModelValidationContext context)
    {
        var accessor = context.ModelMetadata.ModelBindingMessageProvider.ValueMustBeANumberAccessor;
        var message = accessor(context.ModelMetadata.GetDisplayName());
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-number", string.Format(CultureInfo.CurrentCulture, message));
    }
}
