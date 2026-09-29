using System;
using System.Collections.Generic;
using System.Linq;

namespace eShopLegacyMVC.Models.Validation
{
    /// <summary>
    /// Mirrors System.Data.Entity.Validation.DbEntityValidationException, which EF6 threw from SaveChanges when
    /// an Added or Modified entity failed validation. EF Core has no built-in equivalent.
    /// </summary>
    public class DbEntityValidationException : Exception
    {
        public const string DefaultMessage = "Validation failed for one or more entities. See 'EntityValidationErrors' property for more details.";

        public DbEntityValidationException(IEnumerable<DbEntityValidationResult> entityValidationResults)
            : base(DefaultMessage)
        {
            EntityValidationErrors = entityValidationResults.ToList();
        }

        public IEnumerable<DbEntityValidationResult> EntityValidationErrors { get; }
    }

    public class DbEntityValidationResult
    {
        public DbEntityValidationResult(object entity, IEnumerable<DbValidationError> validationErrors)
        {
            Entity = entity;
            ValidationErrors = validationErrors.ToList();
        }

        public object Entity { get; }

        public ICollection<DbValidationError> ValidationErrors { get; }

        public bool IsValid => ValidationErrors.Count == 0;
    }

    public class DbValidationError
    {
        public DbValidationError(string propertyName, string errorMessage)
        {
            PropertyName = propertyName;
            ErrorMessage = errorMessage;
        }

        public string PropertyName { get; }

        public string ErrorMessage { get; }
    }
}
