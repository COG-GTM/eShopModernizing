using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace eShopLegacyMVC.Models.Validation
{
    /// <summary>
    /// Re-implements the subset of EF6 validate-on-save used by the catalog model: property-level
    /// <see cref="ValidationAttribute"/>s, implicit Required/MaxLength checks derived from the model facets, and
    /// type-level validation (<see cref="IValidatableObject"/>) once all properties are valid.
    /// </summary>
    internal static class EntityValidator
    {
        public static void ThrowIfInvalid(ChangeTracker changeTracker)
        {
            var failures = changeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .Select(Validate)
                .Where(r => !r.IsValid)
                .ToList();

            if (failures.Count > 0)
            {
                throw new DbEntityValidationException(failures);
            }
        }

        public static DbEntityValidationResult Validate(EntityEntry entry)
        {
            var entity = entry.Entity;
            var errors = new List<DbValidationError>();

            foreach (var property in entry.Metadata.GetProperties())
            {
                var propertyInfo = property.PropertyInfo;
                if (propertyInfo == null)
                {
                    continue;
                }

                var attributes = propertyInfo.GetCustomAttributes<ValidationAttribute>(inherit: true).ToList();

                if (!property.IsNullable && !propertyInfo.PropertyType.IsValueType && !attributes.OfType<RequiredAttribute>().Any())
                {
                    attributes.Add(new RequiredAttribute());
                }

                var maxLength = property.GetMaxLength();
                if (maxLength.HasValue && !attributes.Any(a => a is MaxLengthAttribute || a is StringLengthAttribute))
                {
                    attributes.Add(new MaxLengthAttribute(maxLength.Value));
                }

                if (attributes.Count == 0)
                {
                    continue;
                }

                var context = new ValidationContext(entity)
                {
                    MemberName = propertyInfo.Name,
                    DisplayName = propertyInfo.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? propertyInfo.Name,
                };

                var results = new List<ValidationResult>();
                Validator.TryValidateValue(entry.Property(property.Name).CurrentValue, context, results, attributes);
                errors.AddRange(results.Select(r => new DbValidationError(propertyInfo.Name, r.ErrorMessage)));
            }

            if (errors.Count == 0)
            {
                var results = new List<ValidationResult>();
                Validator.TryValidateObject(entity, new ValidationContext(entity), results, validateAllProperties: false);
                errors.AddRange(results.Select(r => new DbValidationError(r.MemberNames.FirstOrDefault(), r.ErrorMessage)));
            }

            return new DbEntityValidationResult(entity, errors);
        }
    }
}
