-- Idempotent schema + seed for the eShopCoreModernized catalog database.
-- Mirrors the EF Core model in src/eShopCoreModernized/Models/CatalogDBContext.cs
-- plus the catalog_hilo sequence used by CatalogItemHiLoGenerator.

IF DB_ID(N'$(DB_NAME)') IS NULL
    CREATE DATABASE [$(DB_NAME)];
GO

USE [$(DB_NAME)];
GO

SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.CatalogBrand', N'U') IS NULL
    CREATE TABLE dbo.CatalogBrand (
        Id    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CatalogBrand PRIMARY KEY,
        Brand NVARCHAR(100)     NOT NULL
    );
GO

IF OBJECT_ID(N'dbo.CatalogType', N'U') IS NULL
    CREATE TABLE dbo.CatalogType (
        Id   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CatalogType PRIMARY KEY,
        Type NVARCHAR(100)     NOT NULL
    );
GO

-- Catalog.Id is assigned by the app from the catalog_hilo sequence, so it is not an IDENTITY column.
IF OBJECT_ID(N'dbo.Catalog', N'U') IS NULL
    CREATE TABLE dbo.Catalog (
        Id                INT            NOT NULL CONSTRAINT PK_Catalog PRIMARY KEY,
        Name              NVARCHAR(50)   NOT NULL,
        Description       NVARCHAR(MAX)  NULL,
        Price             DECIMAL(18,2)  NOT NULL,
        PictureFileName   NVARCHAR(MAX)  NOT NULL,
        TempImageName     NVARCHAR(MAX)  NULL,
        CatalogTypeId     INT            NOT NULL CONSTRAINT FK_Catalog_CatalogType_CatalogTypeId
                                                  REFERENCES dbo.CatalogType (Id) ON DELETE CASCADE,
        CatalogBrandId    INT            NOT NULL CONSTRAINT FK_Catalog_CatalogBrand_CatalogBrandId
                                                  REFERENCES dbo.CatalogBrand (Id) ON DELETE CASCADE,
        AvailableStock    INT            NOT NULL,
        RestockThreshold  INT            NOT NULL,
        MaxStockThreshold INT            NOT NULL,
        OnReorder         BIT            NOT NULL
    );
GO

IF OBJECT_ID(N'dbo.catalog_hilo', N'SO') IS NULL
    CREATE SEQUENCE dbo.catalog_hilo AS BIGINT START WITH 101 INCREMENT BY 10;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CatalogBrand)
BEGIN
    SET IDENTITY_INSERT dbo.CatalogBrand ON;
    INSERT INTO dbo.CatalogBrand (Id, Brand) VALUES
        (1, N'Azure'), (2, N'.NET'), (3, N'Visual Studio'), (4, N'SQL Server'), (5, N'Other');
    SET IDENTITY_INSERT dbo.CatalogBrand OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CatalogType)
BEGIN
    SET IDENTITY_INSERT dbo.CatalogType ON;
    INSERT INTO dbo.CatalogType (Id, Type) VALUES
        (1, N'Mug'), (2, N'T-Shirt'), (3, N'Sheet'), (4, N'USB Memory Stick');
    SET IDENTITY_INSERT dbo.CatalogType OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Catalog)
    INSERT INTO dbo.Catalog
        (Id, CatalogTypeId, CatalogBrandId, AvailableStock, RestockThreshold, MaxStockThreshold, OnReorder, Description, Name, Price, PictureFileName)
    VALUES
        (1,  2, 2, 100, 10, 200, 0, N'.NET Bot Black Hoodie',  N'.NET Bot Black Hoodie',  19.50, N'1.png'),
        (2,  1, 2, 100, 10, 200, 0, N'.NET Black & White Mug', N'.NET Black & White Mug',  8.50, N'2.png'),
        (3,  2, 5, 100, 10, 200, 0, N'Prism White T-Shirt',    N'Prism White T-Shirt',    12.00, N'3.png'),
        (4,  2, 2, 100, 10, 200, 0, N'.NET Foundation T-shirt', N'.NET Foundation T-shirt', 12.00, N'4.png'),
        (5,  3, 5, 100, 10, 200, 0, N'Roslyn Red Sheet',       N'Roslyn Red Sheet',        8.50, N'5.png'),
        (6,  2, 2, 100, 10, 200, 0, N'.NET Blue Hoodie',       N'.NET Blue Hoodie',       12.00, N'6.png'),
        (7,  2, 5, 100, 10, 200, 0, N'Roslyn Red T-Shirt',     N'Roslyn Red T-Shirt',     12.00, N'7.png'),
        (8,  2, 5, 100, 10, 200, 0, N'Kudu Purple Hoodie',     N'Kudu Purple Hoodie',      8.50, N'8.png'),
        (9,  1, 5, 100, 10, 200, 0, N'Cup<T> White Mug',       N'Cup<T> White Mug',       12.00, N'9.png'),
        (10, 3, 2, 100, 10, 200, 0, N'.NET Foundation Sheet',  N'.NET Foundation Sheet',  12.00, N'10.png'),
        (11, 3, 2, 100, 10, 200, 0, N'Cup<T> Sheet',           N'Cup<T> Sheet',            8.50, N'11.png'),
        (12, 2, 5, 100, 10, 200, 0, N'Prism White TShirt',     N'Prism White TShirt',     12.00, N'12.png');
GO
