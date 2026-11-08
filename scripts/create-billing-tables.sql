SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE BillingDb;
GO

IF OBJECT_ID(N'dbo.Invoices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Invoices
    (
        Id UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Invoices_Id DEFAULT NEWSEQUENTIALID(),
        Number BIGINT NOT NULL,
        -- 1 = Aberta, 2 = Fechada
        Status TINYINT NOT NULL
            CONSTRAINT DF_Invoices_Status DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_Invoices_CreatedAt DEFAULT SYSUTCDATETIME(),
        ClosedAt DATETIME2 NULL,
        UpdatedAt DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,

        CONSTRAINT PK_Invoices PRIMARY KEY (Id),
        CONSTRAINT UQ_Invoices_Number UNIQUE (Number),
        CONSTRAINT CK_Invoices_Number CHECK (Number > 0),
        CONSTRAINT CK_Invoices_Status CHECK (Status IN (1, 2)),
        CONSTRAINT CK_Invoices_Status_ClosedAt
            CHECK
            (
                (Status = 1 AND ClosedAt IS NULL)
                OR (Status = 2 AND ClosedAt IS NOT NULL)
            )
    );
END;
GO

IF OBJECT_ID(N'dbo.InvoiceItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InvoiceItems
    (
        Id UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_InvoiceItems_Id DEFAULT NEWSEQUENTIALID(),
        InvoiceId UNIQUEIDENTIFIER NOT NULL,
        ProductId UNIQUEIDENTIFIER NOT NULL,
        ProductCode NVARCHAR(50) NOT NULL,
        ProductDescription NVARCHAR(200) NOT NULL,
        Quantity INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_InvoiceItems_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_InvoiceItems PRIMARY KEY (Id),
        CONSTRAINT FK_InvoiceItems_Invoices
            FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoices (Id),
        CONSTRAINT CK_InvoiceItems_ProductCode_NotBlank
            CHECK (LEN(LTRIM(RTRIM(ProductCode))) > 0),
        CONSTRAINT CK_InvoiceItems_ProductDescription_NotBlank
            CHECK (LEN(LTRIM(RTRIM(ProductDescription))) > 0),
        CONSTRAINT CK_InvoiceItems_Quantity CHECK (Quantity > 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_InvoiceItems_InvoiceId_ProductId'
      AND object_id = OBJECT_ID(N'dbo.InvoiceItems')
)
BEGIN
    CREATE UNIQUE INDEX UX_InvoiceItems_InvoiceId_ProductId
        ON dbo.InvoiceItems (InvoiceId, ProductId);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_InvoiceItems_ProductId'
      AND object_id = OBJECT_ID(N'dbo.InvoiceItems')
)
BEGIN
    CREATE INDEX IX_InvoiceItems_ProductId
        ON dbo.InvoiceItems (ProductId);
END;
GO

IF OBJECT_ID(N'dbo.InvoiceSequence', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InvoiceSequence
    (
        Id INT NOT NULL,
        CurrentValue BIGINT NOT NULL
            CONSTRAINT DF_InvoiceSequence_CurrentValue DEFAULT 0,
        UpdatedAt DATETIME2 NULL,

        CONSTRAINT PK_InvoiceSequence PRIMARY KEY (Id),
        CONSTRAINT CK_InvoiceSequence_SingleRow CHECK (Id = 1),
        CONSTRAINT CK_InvoiceSequence_CurrentValue CHECK (CurrentValue >= 0)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.InvoiceSequence WHERE Id = 1)
BEGIN
    INSERT INTO dbo.InvoiceSequence (Id, CurrentValue) VALUES (1, 0);
END;
GO
