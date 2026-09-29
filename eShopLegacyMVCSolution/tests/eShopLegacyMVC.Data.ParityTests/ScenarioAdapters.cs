extern alias ported;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Legacy = eShopLegacyMVC.Models;
using LegacyServices = eShopLegacyMVC.Services;
using Ported = ported::eShopLegacyMVC.Models;
using PortedServices = ported::eShopLegacyMVC.Services;
using PortedValidation = ported::eShopLegacyMVC.Models.Validation;

namespace eShopLegacyMVC.Data.ParityTests
{
    internal sealed class ItemData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string PictureFileName { get; set; } = "dummy.png";
        public int CatalogBrandId { get; set; } = 1;
        public int CatalogTypeId { get; set; } = 1;
        public int AvailableStock { get; set; }
        public int RestockThreshold { get; set; }
        public int MaxStockThreshold { get; set; }
        public bool OnReorder { get; set; }
    }

    /// <summary>
    /// The operations the MVC and Web API controllers perform against ICatalogService, expressed with neutral types so
    /// the same scenario can be replayed against both implementations. Every call is its own unit of work (a fresh
    /// context), matching one HTTP request in the legacy app. Results are rendered as strings for comparison.
    /// </summary>
    internal interface ICatalogScenarioTarget
    {
        string Page(int pageSize, int pageIndex);
        string Find(int id);
        string Brands();
        string Types();
        string Create(ItemData item);
        string Update(ItemData item);
        string UpdateLoadedGraph(int id, string newName, int newBrandId);
        string RemoveLoaded(int id);
        string RemoveDetached(int id);
    }

    internal static class Render
    {
        public static string Item(int id, string name, string description, decimal price, string picture, int brandId, string brand, int typeId, string type, int stock, int restock, int maxStock, bool onReorder, string pictureUri) =>
            string.Join("|", id, name, description ?? "NULL", price.ToString(CultureInfo.InvariantCulture), picture, brandId, brand ?? "NULL", typeId, type ?? "NULL", stock, restock, maxStock, onReorder, pictureUri ?? "NULL");

        public static string Page(int actualPage, int itemsPerPage, long totalItems, int totalPages, IEnumerable<string> items) =>
            $"page={actualPage} size={itemsPerPage} total={totalItems} pages={totalPages}\n  " + string.Join("\n  ", items);

        public static string Exception(Exception ex)
        {
            switch (ex)
            {
                case System.Data.Entity.Validation.DbEntityValidationException legacy:
                    return Validation(legacy.Message, legacy.EntityValidationErrors.SelectMany(r => r.ValidationErrors).Select(e => (e.PropertyName, e.ErrorMessage)));
                case PortedValidation.DbEntityValidationException port:
                    return Validation(port.Message, port.EntityValidationErrors.SelectMany(r => r.ValidationErrors).Select(e => (e.PropertyName, e.ErrorMessage)));
                case System.Data.Entity.Infrastructure.DbUpdateConcurrencyException:
                case Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
                    return "DbUpdateConcurrencyException";
                case System.Data.Entity.Infrastructure.DbUpdateException:
                case Microsoft.EntityFrameworkCore.DbUpdateException:
                    return "DbUpdateException sql=" + SqlErrorNumber(ex);
                default:
                    return $"{ex.GetType().FullName}: {ex.Message}";
            }
        }

        private static string Validation(string message, IEnumerable<(string Property, string Message)> errors) =>
            "DbEntityValidationException: " + message + " [" + string.Join("; ", errors.OrderBy(e => e.Property).Select(e => $"{e.Property}: {e.Message}")) + "]";

        private static string SqlErrorNumber(Exception ex)
        {
            for (var inner = ex; inner != null; inner = inner.InnerException)
            {
                if (inner is System.Data.SqlClient.SqlException legacySql) return legacySql.Number.ToString(CultureInfo.InvariantCulture);
                if (inner is Microsoft.Data.SqlClient.SqlException sql) return sql.Number.ToString(CultureInfo.InvariantCulture);
            }
            return "none";
        }

        public static string Try(Func<string> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                return "threw " + Exception(ex);
            }
        }
    }

    internal sealed class LegacyTarget : ICatalogScenarioTarget
    {
        private readonly Legacy.CatalogItemHiLoGenerator generator;

        public LegacyTarget(Legacy.CatalogItemHiLoGenerator generator) => this.generator = generator;

        private string Run(Func<LegacyServices.CatalogService, string> action) => Render.Try(() =>
        {
            string result = null;
            LegacyHarness.WithService(generator, s => result = action(s));
            return result;
        });

        private static string R(Legacy.CatalogItem i) => i == null ? "null" :
            Render.Item(i.Id, i.Name, i.Description, i.Price, i.PictureFileName, i.CatalogBrandId, i.CatalogBrand?.Brand, i.CatalogTypeId, i.CatalogType?.Type, i.AvailableStock, i.RestockThreshold, i.MaxStockThreshold, i.OnReorder, i.PictureUri);

        private static Legacy.CatalogItem ToEntity(ItemData d) => new Legacy.CatalogItem
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            Price = d.Price,
            PictureFileName = d.PictureFileName,
            CatalogBrandId = d.CatalogBrandId,
            CatalogTypeId = d.CatalogTypeId,
            AvailableStock = d.AvailableStock,
            RestockThreshold = d.RestockThreshold,
            MaxStockThreshold = d.MaxStockThreshold,
            OnReorder = d.OnReorder,
        };

        public string Page(int pageSize, int pageIndex) => Run(s =>
        {
            var p = s.GetCatalogItemsPaginated(pageSize, pageIndex);
            return Render.Page(p.ActualPage, p.ItemsPerPage, p.TotalItems, p.TotalPages, p.Data.Select(R));
        });

        public string Find(int id) => Run(s => R(s.FindCatalogItem(id)));

        public string Brands() => Run(s => string.Join(",", s.GetCatalogBrands().OrderBy(b => b.Id).Select(b => $"{b.Id}:{b.Brand}")));

        public string Types() => Run(s => string.Join(",", s.GetCatalogTypes().OrderBy(t => t.Id).Select(t => $"{t.Id}:{t.Type}")));

        public string Create(ItemData item) => Run(s =>
        {
            var entity = ToEntity(item);
            try
            {
                s.CreateCatalogItem(entity);
            }
            catch (Exception ex)
            {
                return $"assigned id={entity.Id}, threw {Render.Exception(ex)}";
            }
            return $"created id={entity.Id}";
        });

        public string Update(ItemData item) => Run(s =>
        {
            s.UpdateCatalogItem(ToEntity(item));
            return "updated";
        });

        public string UpdateLoadedGraph(int id, string newName, int newBrandId)
        {
            Legacy.CatalogItem loaded = null;
            LegacyHarness.WithService(generator, s => loaded = s.FindCatalogItem(id));
            loaded.Name = newName;
            loaded.CatalogBrandId = newBrandId;
            return Run(s =>
            {
                s.UpdateCatalogItem(loaded);
                return "updated " + R(loaded);
            });
        }

        public string RemoveLoaded(int id) => Run(s =>
        {
            s.RemoveCatalogItem(s.FindCatalogItem(id));
            return "removed";
        });

        public string RemoveDetached(int id) => Run(s =>
        {
            s.RemoveCatalogItem(new Legacy.CatalogItem { Id = id });
            return "removed";
        });
    }

    internal sealed class PortedTarget : ICatalogScenarioTarget
    {
        private readonly Ported.CatalogItemHiLoGenerator generator;
        private readonly string database;

        public PortedTarget(Ported.CatalogItemHiLoGenerator generator, string database = SqlServer.PortedDatabaseName)
        {
            this.generator = generator;
            this.database = database;
        }

        private string Run(Func<PortedServices.CatalogService, string> action) => Render.Try(() =>
        {
            string result = null;
            PortedHarness.WithService(generator, s => result = action(s), database);
            return result;
        });

        private static string R(Ported.CatalogItem i) => i == null ? "null" :
            Render.Item(i.Id, i.Name, i.Description, i.Price, i.PictureFileName, i.CatalogBrandId, i.CatalogBrand?.Brand, i.CatalogTypeId, i.CatalogType?.Type, i.AvailableStock, i.RestockThreshold, i.MaxStockThreshold, i.OnReorder, i.PictureUri);

        private static Ported.CatalogItem ToEntity(ItemData d) => new Ported.CatalogItem
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            Price = d.Price,
            PictureFileName = d.PictureFileName,
            CatalogBrandId = d.CatalogBrandId,
            CatalogTypeId = d.CatalogTypeId,
            AvailableStock = d.AvailableStock,
            RestockThreshold = d.RestockThreshold,
            MaxStockThreshold = d.MaxStockThreshold,
            OnReorder = d.OnReorder,
        };

        public string Page(int pageSize, int pageIndex) => Run(s =>
        {
            var p = s.GetCatalogItemsPaginated(pageSize, pageIndex);
            return Render.Page(p.ActualPage, p.ItemsPerPage, p.TotalItems, p.TotalPages, p.Data.Select(R));
        });

        public string Find(int id) => Run(s => R(s.FindCatalogItem(id)));

        public string Brands() => Run(s => string.Join(",", s.GetCatalogBrands().OrderBy(b => b.Id).Select(b => $"{b.Id}:{b.Brand}")));

        public string Types() => Run(s => string.Join(",", s.GetCatalogTypes().OrderBy(t => t.Id).Select(t => $"{t.Id}:{t.Type}")));

        public string Create(ItemData item) => Run(s =>
        {
            var entity = ToEntity(item);
            try
            {
                s.CreateCatalogItem(entity);
            }
            catch (Exception ex)
            {
                return $"assigned id={entity.Id}, threw {Render.Exception(ex)}";
            }
            return $"created id={entity.Id}";
        });

        public string Update(ItemData item) => Run(s =>
        {
            s.UpdateCatalogItem(ToEntity(item));
            return "updated";
        });

        public string UpdateLoadedGraph(int id, string newName, int newBrandId)
        {
            Ported.CatalogItem loaded = null;
            PortedHarness.WithService(generator, s => loaded = s.FindCatalogItem(id), database);
            loaded.Name = newName;
            loaded.CatalogBrandId = newBrandId;
            return Run(s =>
            {
                s.UpdateCatalogItem(loaded);
                return "updated " + R(loaded);
            });
        }

        public string RemoveLoaded(int id) => Run(s =>
        {
            s.RemoveCatalogItem(s.FindCatalogItem(id));
            return "removed";
        });

        public string RemoveDetached(int id) => Run(s =>
        {
            s.RemoveCatalogItem(new Ported.CatalogItem { Id = id });
            return "removed";
        });
    }
}
