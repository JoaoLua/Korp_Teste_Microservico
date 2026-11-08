SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE StockDb;
GO

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        Id UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Products_Id DEFAULT NEWSEQUENTIALID(),
        Code NVARCHAR(50) NOT NULL,
        Description NVARCHAR(200) NOT NULL,
        Balance INT NOT NULL
            CONSTRAINT DF_Products_Balance DEFAULT 0,
        MinimumStock INT NOT NULL
            CONSTRAINT DF_Products_MinimumStock DEFAULT 0,
        Category NVARCHAR(100) NULL,
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_Products_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,

        CONSTRAINT PK_Products PRIMARY KEY (Id),
        CONSTRAINT UQ_Products_Code UNIQUE (Code),
        CONSTRAINT CK_Products_Code_NotBlank
            CHECK (LEN(LTRIM(RTRIM(Code))) > 0),
        CONSTRAINT CK_Products_Description_NotBlank
            CHECK (LEN(LTRIM(RTRIM(Description))) > 0),
        CONSTRAINT CK_Products_Balance CHECK (Balance >= 0),
        CONSTRAINT CK_Products_MinimumStock CHECK (MinimumStock >= 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.StockMovements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockMovements
    (
        Id UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_StockMovements_Id DEFAULT NEWSEQUENTIALID(),
        ProductId UNIQUEIDENTIFIER NOT NULL,
        Quantity INT NOT NULL,
        -- 1 = Entrada, 2 = Saida, 3 = Estorno
        MovementType TINYINT NOT NULL,
        Reason NVARCHAR(200) NULL,
        InvoiceId UNIQUEIDENTIFIER NULL,
        IdempotencyKey NVARCHAR(100) NULL,
        OriginalMovementId UNIQUEIDENTIFIER NULL,
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_StockMovements_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_StockMovements PRIMARY KEY (Id),
        CONSTRAINT FK_StockMovements_Products
            FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
        CONSTRAINT FK_StockMovements_OriginalMovement
            FOREIGN KEY (OriginalMovementId) REFERENCES dbo.StockMovements (Id),
        CONSTRAINT CK_StockMovements_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_StockMovements_MovementType
            CHECK (MovementType IN (1, 2, 3)),
        CONSTRAINT CK_StockMovements_Reversal
            CHECK
            (
                (MovementType = 3 AND OriginalMovementId IS NOT NULL)
                OR (MovementType IN (1, 2) AND OriginalMovementId IS NULL)
            ),
        CONSTRAINT CK_StockMovements_IdempotencyKey_NotBlank
            CHECK
            (
                IdempotencyKey IS NULL
                OR LEN(LTRIM(RTRIM(IdempotencyKey))) > 0
            )
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_StockMovements_ProductId_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.StockMovements')
)
BEGIN
    CREATE INDEX IX_StockMovements_ProductId_CreatedAt
        ON dbo.StockMovements (ProductId, CreatedAt DESC)
        INCLUDE (Quantity, MovementType, InvoiceId);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_StockMovements_InvoiceId'
      AND object_id = OBJECT_ID(N'dbo.StockMovements')
)
BEGIN
    CREATE INDEX IX_StockMovements_InvoiceId
        ON dbo.StockMovements (InvoiceId)
        WHERE InvoiceId IS NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_StockMovements_Invoice_Product_Movement'
      AND object_id = OBJECT_ID(N'dbo.StockMovements')
)
BEGIN
    CREATE UNIQUE INDEX UX_StockMovements_Invoice_Product_Movement
        ON dbo.StockMovements (InvoiceId, ProductId, MovementType)
        WHERE InvoiceId IS NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_StockMovements_Idempotency_Product'
      AND object_id = OBJECT_ID(N'dbo.StockMovements')
)
BEGIN
    CREATE UNIQUE INDEX UX_StockMovements_Idempotency_Product
        ON dbo.StockMovements (IdempotencyKey, ProductId)
        WHERE IdempotencyKey IS NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_StockMovements_OriginalMovementId'
      AND object_id = OBJECT_ID(N'dbo.StockMovements')
)
BEGIN
    CREATE UNIQUE INDEX UX_StockMovements_OriginalMovementId
        ON dbo.StockMovements (OriginalMovementId)
        WHERE OriginalMovementId IS NOT NULL;
END;
GO

-- Apenas baixas concluidas sao gravadas. A unicidade da nota evita nova baixa em uma repeticao.
IF OBJECT_ID(N'dbo.StockDeductionOperations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockDeductionOperations
    (
        Id UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_StockDeductionOperations_Id DEFAULT NEWSEQUENTIALID(),
        IdempotencyKey NVARCHAR(100) NOT NULL,
        InvoiceId UNIQUEIDENTIFIER NOT NULL,
        -- Mantido para compatibilidade com registros da primeira versao.
        Status TINYINT NOT NULL
            CONSTRAINT DF_StockDeductionOperations_Status DEFAULT 2,
        RequestPayload NVARCHAR(MAX) NOT NULL,
        RequestHash NVARCHAR(64) NOT NULL,
        ResponsePayload NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_StockDeductionOperations_CreatedAt DEFAULT SYSUTCDATETIME(),
        CompletedAt DATETIME2 NULL,

        CONSTRAINT PK_StockDeductionOperations PRIMARY KEY (Id),
        CONSTRAINT UQ_StockDeductionOperations_IdempotencyKey UNIQUE (IdempotencyKey),
        CONSTRAINT UQ_StockDeductionOperations_InvoiceId UNIQUE (InvoiceId),
        CONSTRAINT CK_StockDeductionOperations_IdempotencyKey_NotBlank
            CHECK (LEN(LTRIM(RTRIM(IdempotencyKey))) > 0),
        CONSTRAINT CK_StockDeductionOperations_Status CHECK (Status = 2),
        CONSTRAINT CK_StockDeductionOperations_RequestHash CHECK (LEN(RequestHash) = 64),
        CONSTRAINT CK_StockDeductionOperations_RequestPayload_Json CHECK (ISJSON(RequestPayload) = 1),
        CONSTRAINT CK_StockDeductionOperations_ResponsePayload_Json
            CHECK (ResponsePayload IS NULL OR ISJSON(ResponsePayload) = 1),
        CONSTRAINT CK_StockDeductionOperations_Completion CHECK (Status = 2 AND CompletedAt IS NOT NULL)
    );
END;
GO
