using eShop.Catalog.Core.Models;

namespace eShop.Catalog.Infrastructure.Data
{
    /// <summary>
    /// Optional hook run by the SQL <see cref="CatalogService"/> right before SaveChanges for added/modified items,
    /// standing in for EF6's DbContext.ValidateOnSaveEnabled (EF Core does not validate on save). Not registered by
    /// default, so apps that need no save-time validation keep the plain EF Core behaviour.
    /// </summary>
    public interface ICatalogSaveValidator
    {
        /// <summary>Throws <see cref="CatalogItemValidationException"/> when the item must not be written.</summary>
        void Validate(CatalogItem catalogItem);
    }

    /// <summary>Counterpart of EF6's DbEntityValidationException: the item was rejected and nothing was written.</summary>
    public sealed class CatalogItemValidationException : Exception
    {
        public CatalogItemValidationException(IReadOnlyList<string> errors)
            : base("Validation failed for one or more entities. See 'Errors' property for more details: " + string.Join(" ", errors))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }
}
