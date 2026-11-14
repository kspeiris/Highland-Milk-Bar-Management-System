# Database Dictionary — Highland Milk Bar Management System

SQLite database created by EF Core migrations. Money is `decimal(18,2)`, quantity
`decimal(18,3)`, unit cost `decimal(18,4)`. Enums are stored as `INTEGER`.
`NN` = not null. `PK` = primary key, `FK` = foreign key, `UK` = unique.

---

## Categories
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| Name | TEXT NN UK | |
| Description | TEXT | |
| SortOrder | INTEGER | |
| IsActive | INTEGER | |

## Products
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| Sku | TEXT NN UK | |
| Name | TEXT NN | |
| Barcode | TEXT | indexed, unique when supplied (app-enforced) |
| CategoryId | INTEGER FK → Categories | |
| Type | INTEGER | ProductType |
| Unit | TEXT | display unit |
| BaseUnitId | INTEGER FK → Units | null |
| CostPrice | decimal(18,2) | |
| SellingPrice | decimal(18,2) | |
| CurrentStock | decimal(18,3) | cached aggregate |
| ReorderLevel | decimal(18,3) | |
| TrackInventory | INTEGER | |
| IsActive | INTEGER | |
| CreatedAt | datetime | |
| UpdatedAt | datetime | null |

## Units
| Id | INTEGER PK | |
| Name | TEXT NN UK | e.g. Litre |
| Symbol | TEXT NN | e.g. L |
| Kind | INTEGER | UnitKind (Count/Weight/Volume) |
| SortOrder | INTEGER | |
| IsActive | INTEGER | |

## ProductUnits
| Id | INTEGER PK | |
| ProductId | INTEGER FK → Products | |
| UnitId | INTEGER FK → Units | |
| FactorToBase | decimal(18,4) | base unit = 1 |
| IsBaseUnit | INTEGER | |

## Suppliers
| Id | INTEGER PK | |
| Name | TEXT NN | |
| ContactPerson | TEXT | |
| Phone | TEXT | |
| Email | TEXT | |
| Address | TEXT | |
| Notes | TEXT | |
| IsActive | INTEGER | |
| CreatedAt | datetime | |

## Customers
| Id | INTEGER PK | |
| Name | TEXT NN | |
| Phone | TEXT | indexed |
| Email | TEXT | |
| Address | TEXT | |
| IsActive | INTEGER | |
| CreatedAt | datetime | |

## InventoryBatches
| Id | INTEGER PK | |
| ProductId | INTEGER FK → Products | |
| SupplierId | INTEGER FK → Suppliers | null |
| PurchaseItemId | INTEGER FK → PurchaseItems | null |
| BatchNumber | TEXT NN | |
| ManufacturedDate | datetime | null |
| ExpiryDate | datetime | indexed, null |
| QuantityReceived | decimal(18,3) | |
| QuantityRemaining | decimal(18,3) | |
| UnitCost | decimal(18,4) | |
| ReceivedAt | datetime | |
| IsActive | INTEGER | |
| Notes | TEXT | |

## StockMovements
| Id | INTEGER PK | |
| ProductId | INTEGER FK → Products | |
| InventoryBatchId | INTEGER FK → InventoryBatches | null |
| MovementType | INTEGER | StockMovementType |
| QuantityChange | decimal(18,3) | signed |
| UnitCostSnapshot | decimal(18,4) | |
| BalanceAfter | decimal(18,3) | |
| ReferenceType | INTEGER | MovementReferenceType, null |
| ReferenceId | INTEGER | null |
| Note | TEXT | |
| CreatedAt | datetime | |

## StockAdjustments
| Id | INTEGER PK | |
| ReferenceNumber | TEXT NN UK | |
| AdjustmentDate | datetime | |
| Reason | TEXT NN | |
| Notes | TEXT | |
| Status | INTEGER | StockAdjustmentStatus |
| CreatedAt | datetime | |

## StockAdjustmentItems
| Id | INTEGER PK | |
| StockAdjustmentId | INTEGER FK → StockAdjustments | |
| ProductId | INTEGER FK → Products | |
| InventoryBatchId | INTEGER FK → InventoryBatches | null |
| QuantityChange | decimal(18,3) | |
| UnitCostSnapshot | decimal(18,4) | |
| Note | TEXT | |

## StockAllocations
| Id | INTEGER PK | |
| ProductId | INTEGER FK → Products | |
| InventoryBatchId | INTEGER FK → InventoryBatches | |
| SaleItemId | INTEGER FK → SaleItems | null |
| MovementType | INTEGER | StockMovementType |
| Quantity | decimal(18,3) | |
| UnitCostSnapshot | decimal(18,4) | |
| StockMovementId | INTEGER FK → StockMovements | null |
| CreatedAt | datetime | |

## Recipes
| Id | INTEGER PK | |
| MenuProductId | INTEGER FK → Products | |
| Name | TEXT NN | |
| YieldQuantity | decimal(18,3) | |
| YieldUnit | TEXT | |
| Version | INTEGER | |
| IsActive | INTEGER | |
| CreatedAt | datetime | |

## RecipeItems
| Id | INTEGER PK | |
| RecipeId | INTEGER FK → Recipes | |
| IngredientProductId | INTEGER FK → Products | |
| QuantityRequired | decimal(18,3) | |
| Unit | TEXT | |
| SortOrder | INTEGER | |

## Sales
| Id | INTEGER PK | |
| ReceiptNumber | TEXT NN UK | |
| SoldAt | datetime | |
| CustomerId | INTEGER FK → Customers | null |
| CashSessionId | INTEGER FK → CashSessions | null |
| Subtotal | decimal(18,2) | |
| DiscountAmount | decimal(18,2) | |
| TaxAmount | decimal(18,2) | |
| TotalAmount | decimal(18,2) | |
| CostOfGoodsSold | decimal(18,2) | |
| AmountPaid | decimal(18,2) | |
| Status | INTEGER | SaleStatus |
| Notes | TEXT | |
| CreatedAt | datetime | |

## SaleItems
| Id | INTEGER PK | |
| SaleId | INTEGER FK → Sales | |
| ProductId | INTEGER FK → Products | null |
| ItemNameSnapshot | TEXT NN | |
| Quantity | decimal(18,3) | |
| UnitPrice | decimal(18,2) | |
| UnitCostSnapshot | decimal(18,4) | |
| DiscountAmount | decimal(18,2) | |
| LineTotal | decimal(18,2) | |
| RecipeId | INTEGER FK → Recipes | null |

## Payments
| Id | INTEGER PK | |
| SaleId | INTEGER FK → Sales | |
| Method | INTEGER | PaymentMethod |
| Amount | decimal(18,2) | |
| PaidAt | datetime | |
| Reference | TEXT | |

## SalesReturns
| Id | INTEGER PK | |
| ReturnNumber | TEXT NN UK | |
| SaleId | INTEGER FK → Sales | |
| CustomerId | INTEGER FK → Customers | null |
| ReturnedAt | datetime | |
| Reason | TEXT | |
| TotalAmount | decimal(18,2) | |
| Restocked | INTEGER | |
| Status | INTEGER | SalesReturnStatus |
| Notes | TEXT | |

## SalesReturnItems
| Id | INTEGER PK | |
| SalesReturnId | INTEGER FK → SalesReturns | |
| SaleItemId | INTEGER FK → SaleItems | null |
| ProductId | INTEGER FK → Products | null |
| ItemNameSnapshot | TEXT NN | |
| Quantity | decimal(18,3) | |
| UnitPrice | decimal(18,2) | |
| Condition | INTEGER | ReturnCondition (Restock/Discard) |
| LineTotal | decimal(18,2) | |

## Purchases
| Id | INTEGER PK | |
| InvoiceNumber | TEXT NN | indexed |
| SupplierId | INTEGER FK → Suppliers | null |
| PurchaseDate | datetime | |
| Subtotal | decimal(18,2) | |
| DiscountAmount | decimal(18,2) | |
| TotalAmount | decimal(18,2) | |
| AmountPaid | decimal(18,2) | |
| Status | INTEGER | PurchaseStatus |
| Notes | TEXT | |
| CreatedAt | datetime | |
| ReceivedAt | datetime | null |

## PurchaseItems
| Id | INTEGER PK | |
| PurchaseId | INTEGER FK → Purchases | |
| ProductId | INTEGER FK → Products | |
| Quantity | decimal(18,3) | |
| UnitCost | decimal(18,4) | |
| LineTotal | decimal(18,2) | |
| BatchNumber | TEXT | |
| ManufacturedDate | datetime | null |
| ExpiryDate | datetime | null |
| ReceivedQuantity | decimal(18,3) | |

## SupplierPayments
| Id | INTEGER PK | |
| PurchaseId | INTEGER FK → Purchases | |
| Amount | decimal(18,2) | |
| Method | INTEGER | PaymentMethod |
| PaidAt | datetime | |
| Reference | TEXT | |
| Notes | TEXT | |

## ExpenseCategories
| Id | INTEGER PK | |
| Name | TEXT NN UK | |
| IsActive | INTEGER | |

## Expenses
| Id | INTEGER PK | |
| ExpenseCategoryId | INTEGER FK → ExpenseCategories | null |
| Description | TEXT NN | |
| Amount | decimal(18,2) | |
| ExpenseDate | datetime | indexed |
| PaymentMethod | INTEGER | PaymentMethod |
| ReferenceNumber | TEXT | |
| Notes | TEXT | |
| CashSessionId | INTEGER FK → CashSessions | null |
| CreatedAt | datetime | |

## CashSessions
| Id | INTEGER PK | |
| OpenedAt | datetime | |
| OpeningFloat | decimal(18,2) | |
| ClosedAt | datetime | null |
| ClosingCounted | decimal(18,2) | null |
| Status | INTEGER | CashSessionStatus |
| Notes | TEXT | |

## CashMovements
| Id | INTEGER PK | |
| CashSessionId | INTEGER FK → CashSessions | |
| MovementType | INTEGER | CashMovementType |
| Direction | INTEGER | CashDirection |
| Amount | decimal(18,2) | |
| ReferenceType | INTEGER | MovementReferenceType, null |
| ReferenceId | INTEGER | null |
| Note | TEXT | |
| CreatedAt | datetime | |

## AppSettings
| Id | INTEGER PK | |
| Key | TEXT NN UK | |
| Value | TEXT | |
| UpdatedAt | datetime | |

## AuditLogs
| Id | INTEGER PK | |
| Timestamp | datetime | indexed |
| Action | TEXT NN | |
| EntityType | TEXT | |
| EntityId | INTEGER | null |
| Details | TEXT | |

---

## Enum reference

| Enum | Values |
|---|---|
| ProductType | Packaged, Ingredient, PreparedItem |
| PaymentMethod | Cash, Card, BankTransfer, Other |
| SaleStatus | Completed, Voided, PartiallyReturned, Returned |
| StockMovementType | Opening, Purchase, Sale, SaleReturn, AdjustmentIn, AdjustmentOut, Waste, Expired, RecipeConsumption |
| PurchaseStatus | Draft, Received, PartiallyPaid, Paid, Cancelled |
| StockAdjustmentStatus | Draft, Applied, Cancelled |
| SalesReturnStatus | Completed, Voided |
| ReturnCondition | Restock, Discard |
| CashSessionStatus | Open, Closed |
| CashMovementType | OpeningFloat, Sale, Refund, Expense, PayIn, Payout, Closing |
| CashDirection | In, Out |
| MovementReferenceType | Opening, Purchase, Sale, SaleReturn, Adjustment, Recipe, Manual |
| UnitKind | Count, Weight, Volume |
| AllocationPolicy | Fifo, Fefo |