# ER Diagram — Highland Milk Bar Management System

The diagram below reflects the EF Core model (`AppDbContext` + configurations). Attribute lists
are abbreviated to key columns; see `DATABASE_DICTIONARY.md` for the full column list.

```mermaid
erDiagram
    CATEGORIES ||--o{ PRODUCTS : "contains"
    UNITS ||--o{ PRODUCTUNITS : "measured by"
    PRODUCTS ||--o{ PRODUCTUNITS : "has"
    PRODUCTS ||--o{ INVENTORYBATCHES : "stocked as"
    SUPPLIERS ||--o{ INVENTORYBATCHES : "supplies"
    PURCHASEITEMS ||--o{ INVENTORYBATCHES : "received as"
    PRODUCTS ||--o{ STOCKMOVEMENTS : "moves"
    INVENTORYBATCHES ||--o{ STOCKMOVEMENTS : "records"
    PRODUCTS ||--o{ STOCKALLOCATIONS : "allocates"
    INVENTORYBATCHES ||--o{ STOCKALLOCATIONS : "allocated from"
    SALEITEMS ||--o{ STOCKALLOCATIONS : "consumes"
    STOCKMOVEMENTS ||--o{ STOCKALLOCATIONS : "justifies"

    CUSTOMERS ||--o{ SALES : "places"
    CASHSESSIONS ||--o{ SALES : "contains"
    SALES ||--o{ SALEITEMS : "has"
    PRODUCTS ||--o{ SALEITEMS : "sold as"
    SALES ||--o{ PAYMENTS : "settled by"
    SALES ||--o{ SALESRETURNS : "returned by"
    CUSTOMERS ||--o{ SALESRETURNS : "requests"
    SALEITEMS ||--o{ SALESRETURNSITEMS : "returned in"
    PRODUCTS ||--o{ SALESRETURNSITEMS : "returned as"
    SALESRETURNS ||--o{ SALESRETURNSITEMS : "contains"

    SUPPLIERS ||--o{ PURCHASES : "invoices"
    PURCHASES ||--o{ PURCHASEITEMS : "has"
    PRODUCTS ||--o{ PURCHASEITEMS : "purchased as"
    PURCHASES ||--o{ SUPPLIERPAYMENTS : "paid by"

    PRODUCTS ||--o{ RECIPES : "prepared by"
    RECIPES ||--o{ RECIPEITEMS : "contains"
    PRODUCTS ||--o{ RECIPEITEMS : "ingredient"
    RECIPES ||--o{ SALEITEMS : "used for"

    EXPENSECATEGORIES ||--o{ EXPENSES : "classifies"
    CASHSESSIONS ||--o{ EXPENSES : "pays"
    CASHSESSIONS ||--o{ CASHMOVEMENTS : "records"

    CATEGORIES {
        int Id PK
        string Name UK
        int SortOrder
        bool IsActive
    }
    PRODUCTS {
        int Id PK
        string Sku UK
        string Name
        string Barcode
        int CategoryId FK
        int BaseUnitId FK
        decimal CostPrice
        decimal SellingPrice
        decimal CurrentStock
        decimal ReorderLevel
        bool TrackInventory
        bool IsActive
    }
    UNITS {
        int Id PK
        string Name UK
        string Symbol
        int Kind
    }
    PRODUCTUNITS {
        int Id PK
        int ProductId FK
        int UnitId FK
        decimal FactorToBase
        bool IsBaseUnit
    }
    INVENTORYBATCHES {
        int Id PK
        int ProductId FK
        string BatchNumber
        datetime ExpiryDate
        decimal QuantityReceived
        decimal QuantityRemaining
        decimal UnitCost
    }
    STOCKMOVEMENTS {
        int Id PK
        int ProductId FK
        int InventoryBatchId FK
        int MovementType
        decimal QuantityChange
        decimal BalanceAfter
    }
    STOCKALLOCATIONS {
        int Id PK
        int ProductId FK
        int InventoryBatchId FK
        int SaleItemId FK
        int MovementType
        decimal Quantity
        decimal UnitCostSnapshot
    }
    RECIPES {
        int Id PK
        int MenuProductId FK
        string Name
        decimal YieldQuantity
        int Version
        bool IsActive
    }
    RECIPEITEMS {
        int Id PK
        int RecipeId FK
        int IngredientProductId FK
        decimal QuantityRequired
        string Unit
    }
    SALES {
        int Id PK
        string ReceiptNumber UK
        int CustomerId FK
        int CashSessionId FK
        decimal Subtotal
        decimal DiscountAmount
        decimal TotalAmount
        decimal CostOfGoodsSold
        decimal AmountPaid
        int Status
    }
    SALEITEMS {
        int Id PK
        int SaleId FK
        int ProductId FK
        int RecipeId FK
        string ItemNameSnapshot
        decimal Quantity
        decimal UnitPrice
        decimal UnitCostSnapshot
        decimal LineTotal
    }
    PAYMENTS {
        int Id PK
        int SaleId FK
        int Method
        decimal Amount
    }
    SALESRETURNS {
        int Id PK
        string ReturnNumber UK
        int SaleId FK
        datetime ReturnedAt
        decimal TotalAmount
        bool Restocked
        int Status
    }
    SALESRETURNSITEMS {
        int Id PK
        int SalesReturnId FK
        int SaleItemId FK
        int ProductId FK
        decimal Quantity
        int Condition
        decimal LineTotal
    }
    PURCHASES {
        int Id PK
        string InvoiceNumber
        int SupplierId FK
        decimal TotalAmount
        decimal AmountPaid
        int Status
    }
    PURCHASEITEMS {
        int Id PK
        int PurchaseId FK
        int ProductId FK
        decimal Quantity
        decimal UnitCost
        string BatchNumber
        datetime ExpiryDate
    }
    SUPPLIERPAYMENTS {
        int Id PK
        int PurchaseId FK
        decimal Amount
        int Method
    }
    SUPPLIERS {
        int Id PK
        string Name
        string Phone
    }
    CUSTOMERS {
        int Id PK
        string Name
        string Phone
    }
    EXPENSECATEGORIES {
        int Id PK
        string Name UK
    }
    EXPENSES {
        int Id PK
        int ExpenseCategoryId FK
        int CashSessionId FK
        string Description
        decimal Amount
        datetime ExpenseDate
    }
    CASHSESSIONS {
        int Id PK
        datetime OpenedAt
        decimal OpeningFloat
        datetime ClosedAt
        int Status
    }
    CASHMOVEMENTS {
        int Id PK
        int CashSessionId FK
        int MovementType
        int Direction
        decimal Amount
    }
    APPSETTINGS {
        int Id PK
        string Key UK
        string Value
    }
    AUDITLOGS {
        int Id PK
        datetime Timestamp
        string Action
        string EntityType
        int EntityId
    }
```