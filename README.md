# CursorPaginator

A high-performance, source-generated **Cursor-Based Pagination & Dynamic Filtering** library for .NET and Dapper.

`CursorPaginator` eliminates the performance overhead of offset-based pagination (`OFFSET / FETCH`) and reflection-based dynamic query builders. It generates strongly-typed SQL queries, high-speed binary serialization jump tables, and HMAC-SHA256 signed cursors at compile time via Roslyn Source Generators.

---

## Table of Contents

- [Architecture & Highlights](#architecture--highlights)
- [Installation](#installation)
- [Attribute Reference](#attribute-reference)
  - [`[GenerateQuery]`](#generatequery)
  - [`[Where]`](#where)
  - [`[Join]`](#join)
  - [`[JoinCondition]`](#joincondition)
  - [`[JoinWhere]`](#joinwhere)
  - [`[QueryMap]`](#querymap)
  - [`[QueryField]`](#queryfield)
  - [`[Computed]`](#computed)
- [Filter Structures & Supported Operators](#filter-structures--supported-operators)
- [Step-by-Step Implementation Patterns](#step-by-step-implementation-patterns)
  - [1. Standard Single-Table Query](#1-standard-single-table-query)
  - [2. Multi-Tenant Query](#2-multi-tenant-query)
  - [3. Query with Static Base Filters (`[Where]`)](#3-query-with-static-base-filters-where)
  - [4. Multi-Table Join Query](#4-multi-table-join-query-join-joincondition-joinwhere)
  - [5. Column Mapping & Computed Fields](#5-column-mapping--computed-fields-queryfield-computed)
  - [6. Enums Stored as Strings](#6-enums-stored-as-strings)
  - [7. API Endpoint Pattern](#7-api-endpoint-pattern)
- [Architectural Rules & Best Practices](#architectural-rules--best-practices)
- [Troubleshooting & Pitfalls](#troubleshooting--pitfalls)

---

## Architecture & Highlights

- **Roslyn Incremental Source Generator**: Generates `BuildQuery(cursor, limit)` and `BuildQuery(filter, sortBy, sortDesc, limit)` partial query handler classes at compile time.
- **Zero-Reflection Jump Tables**: Uses pre-compiled delegate arrays (`FilterReadJumpTable`, `FilterWriteJumpTable`, `CursorJumpTable`) for O(1) filter parsing and parameter binding without reflection or expression compilation.
- **Tamper-Proof HMAC-SHA256 Cursors**: Cursors are encoded into binary buffers containing type hashes, active filter state, sort directions, and tie-breaker column values, signed with HMAC-SHA256.
- **Efficient `Limit + 1` Lookups**: Queries request `Limit + 1` rows to detect `HasNextPage` in a single round-trip, avoiding expensive `COUNT(*)` overhead.
- **Provider Agnostic**: Configurable for MySQL (backtick quotes, `LIKE`) and PostgreSQL (double-quotes, `ILIKE`, `= ANY` arrays).

---

## Installation

Install both packages — the generator pulls in the runtime library automatically:

```bash
dotnet add package CursorPaginator.Generator
```

Or add both explicitly:

```xml
<PackageReference Include="CursorPaginator" Version="1.0.0" />
<PackageReference Include="CursorPaginator.Generator" Version="1.0.0"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

> [!NOTE]
> `CursorPaginator.Generator` is a Roslyn Source Generator and must be referenced with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"` in `.csproj` files that use `[GenerateQuery]`.

---

## Attribute Reference

All attributes reside in the `CursorPaginator.Core` namespace:

```csharp
using CursorPaginator.Core;
```

---

### `[GenerateQuery]`

Applied to query handler classes to trigger source generation of `BuildQuery` methods, jump tables, and cursor serializers.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public class GenerateQueryAttribute : Attribute
```

#### Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Table` | `string` | *Required* | Base table name (must match snake_case EF Core table name). |
| `Filter` | `Type` | *Required* | Filter class type implementing `ICursorFilter`. |
| `Response` | `Type` | *Required* | Response DTO class type. |
| `Sortable` | `string[]?` | `null` | Array of response property names permitted for sorting. |
| `Exclude` | `string[]?` | `null` | Response properties to exclude from `SELECT` projection. |
| `Select` | `string[]?` | `null` | Explicit list of response properties to include in `SELECT`. |
| `Identifier` | `string` | `"Id"` | Unique tie-breaker field for deterministic pagination. |
| `HasTenantFilter` | `bool` | `false` | Injects `table.tenant_id = @TenantId` in `WHERE` and count queries. |
| `EnumsAsStrings` | `bool` | `false` | Enum columns are stored as their names (e.g. EF Core `HasConversion<string>()`). Enum filter values and cursor sort values are bound as strings instead of integers. See [Enums stored as strings](#6-enums-stored-as-strings). |
| `NamingConvention` | `NamingConvention?` | `SnakeCase` | Column naming convention (`SnakeCase`, `PascalCase`, `CamelCase`). |
| `DatabaseProvider` | `DatabaseProvider?` | `MySQL` | SQL dialect provider (`MySQL`, `PostgreSQL`). |

#### Example

```csharp
[GenerateQuery(
    Table = "product",
    Sortable = ["Id", "Name", "Price", "CreatedAt"],
    Filter = typeof(ProductFilter),
    Response = typeof(ProductResponse),
    HasTenantFilter = true,
    DatabaseProvider = DatabaseProvider.PostgreSQL
)]
partial class GetProductsHandler { }
```

---

### `[Where]`

Applies static, compile-time base filter conditions to the primary `WHERE` clause. Can be declared multiple times.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class WhereAttribute : Attribute
```

#### Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Property` | `string` | *Required* | Column or property name to filter on. |
| `Operator` | `FilterOperator` | `Eq` | Comparison operator (`Eq`, `Ne`, `Gt`, `Gte`, `Lt`, `Lte`, `IsNull`, `In`, `NotIn`). |
| `Value` | `object?` | `null` | Constant value for comparison (e.g. `true`, `"ACTIVE"`, `0`). |
| `CompareToProperty` | `string?` | `null` | Target column name when comparing against another column. |
| `Table` | `string?` | `null` | Table or alias name qualifier (defaults to base table). |
| `LogicalOperator` | `LogicalOperator` | `And` | Logical conjunction (`And`, `Or`). |
| `Order` | `int` | *Declaration Order* | Order of condition in the generated `WHERE` clause. When omitted, automatically infers sequence from top-to-bottom attribute declaration order. |

#### Example

```csharp
[GenerateQuery(Table = "order", Filter = typeof(OrderFilter), Response = typeof(OrderResponse))]
[Where(Property = "is_deleted", Operator = FilterOperator.Eq, Value = false, Order = 1)]
[Where(Property = "status",     Operator = FilterOperator.Ne, Value = "Draft",  Order = 2)]
partial class GetOrdersHandler { }
```

*Generated SQL:*
```sql
SELECT ... FROM `order` WHERE `order`.`is_deleted` = false AND `order`.`status` != 'Draft' ...
```

---

### `[Join]`

Declares a table join on the query handler. Can be declared multiple times.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class JoinAttribute : Attribute
```

#### Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Table` | `string` | *Required* | Name of the table to join. |
| `Alias` | `string?` | `null` | Optional alias for the joined table. |
| `Type` | `JoinType` | `Left` | Join type (`Left`, `Inner`, `Right`, `Full`). |
| `Select` | `string[]?` | `null` | Column names from the joined table to include in `SELECT`. |
| `Order` | `int` | *Declaration Order* | Order of the `JOIN` statement. When omitted, infers from top-to-bottom source code declaration order. |

---

### `[JoinCondition]`

Defines an `ON` condition linking the joined table to the primary table or another join. Matches a `[Join]` by `JoinAlias`.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class JoinConditionAttribute : Attribute
```

#### Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `JoinAlias` | `string` | *Required* | Must match the `Alias` (or `Table`) of the corresponding `[Join]`. |
| `LeftTable` | `string` | *Required* | Left table or alias name in the `ON` condition. |
| `LeftColumn` | `string` | *Required* | Left column name. |
| `Operator` | `FilterOperator` | `Eq` | Join condition operator. |
| `RightTable` | `string` | *Required* | Right table or alias name in the `ON` condition. |
| `RightColumn` | `string` | *Required* | Right column name. |
| `LogicalOperator` | `LogicalOperator` | `And` | Logical conjunction for multiple conditions. |
| `Order` | `int` | *Declaration Order* | Order within the `ON` clause. |

---

### `[JoinWhere]`

Applies a static filter condition directly within the `ON` clause of a `JOIN`. Matches a `[Join]` by `JoinAlias`.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class JoinWhereAttribute : Attribute
```

#### Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `JoinAlias` | `string` | *Required* | Must match the `Alias` (or `Table`) of the corresponding `[Join]`. |
| `Property` | `string` | *Required* | Column name in the joined table to filter. |
| `Operator` | `FilterOperator` | `Eq` | Filter operator. |
| `Value` | `object?` | `null` | Static value for the filter condition. |
| `CompareToProperty` | `string?` | `null` | Alternative column name to compare against. |
| `LogicalOperator` | `LogicalOperator` | `And` | Logical conjunction (`And`, `Or`). |
| `Order` | `int` | *Declaration Order* | Order of execution in the join condition. |

> [!TIP]
> **Automatic Order Inference**: `Order` in `[Where]`, `[Join]`, `[JoinCondition]`, and `[JoinWhere]` is completely optional. When omitted, the Roslyn generator automatically determines execution sequence from the top-to-bottom attribute declaration order in your C# file.

---

### `[QueryMap]`

Applied at the **query handler class level** to map Response and Filter properties to joined tables or custom database columns, keeping DTOs as **100% pure POCO classes** without database attributes. Prefer this over `[QueryField]`.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class QueryMapAttribute : Attribute
```

#### Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Property` | `string` | *Required* | C# property name on the Response or Filter DTO (PascalCase). |
| `Table` | `string?` | `null` | Target table name (defaults to primary table if `Alias` is also null). |
| `Alias` | `string?` | `null` | Join table alias (e.g. `"pcl"`). |
| `Column` | `string?` | *Auto-Converted* | SQL column name. When omitted, automatically converted from `Property` via `NamingConvention`. |
| `Filterable` | `bool?` | `null` | Enables/disables filtering for this mapped property. |
| `Sortable` | `bool?` | `null` | Enables/disables sorting for this mapped property. |
| `Operators` | `FilterOperator[]?` | `null` | Restricts allowed filter operators. |
| `EnumAsString` | `bool` | *Inherits `EnumsAsStrings`* | Overrides `EnumsAsStrings` for this enum property. |
| `Order` | `int` | *Declaration Order* | Optional execution sequence index. |

#### Example

```csharp
[GenerateQuery(
    Table = "product",
    Sortable = ["Id", "CreatedAt", "Name"],
    Filter = typeof(ChannelProductFilter),
    Response = typeof(ChannelProductResponse)
)]
[Join(Table = "product_channel_listing", Alias = "pcl", Type = JoinType.Inner)]
[JoinCondition(JoinAlias = "pcl", LeftTable = "product", LeftColumn = "Id", RightTable = "pcl", RightColumn = "ProductId")]
[QueryMap(Property = nameof(ChannelProductResponse.ChannelId),    Alias = "pcl")]
[QueryMap(Property = nameof(ChannelProductResponse.IsPublished),  Alias = "pcl")]
[QueryMap(Property = nameof(ChannelProductResponse.PublishedAt),  Alias = "pcl")]
partial class GetChannelProductsHandler { }
```

---

### `[QueryField]`

Applied to **Response DTO properties** (legacy or local override) to customize database column mapping. Prefer class-level `[QueryMap]` on the query handler to keep public contracts pure.

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class QueryFieldAttribute : Attribute
```

#### Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Table` | `string?` | `null` | Table or alias name this property originates from. |
| `ColumnName` | `string?` | `null` | Explicit SQL column name overriding default naming conversion. |
| `Filterable` | `bool?` | `null` | Enables/disables dynamic filtering for this field. |
| `Sortable` | `bool?` | `null` | Enables/disables sorting for this field. |
| `Operators` | `FilterOperator[]?` | `null` | Restricts allowed filter operators. |
| `EnumAsString` | `bool` | *Inherits `EnumsAsStrings`* | Overrides `EnumsAsStrings` for this enum property. |

#### Example

```csharp
public class ProductResponse
{
    public Guid Id { get; init; }

    [QueryField(ColumnName = "product_title")]
    public string Title { get; init; } = string.Empty;

    [QueryField(Table = "cat", ColumnName = "name")]
    public string CategoryName { get; init; } = string.Empty;
}
```

---

### `[Computed]`

Applied to **Response DTO properties** to project computed SQL expressions (aggregations, arithmetic, `COALESCE`, `CASE`, etc.).

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class ComputedAttribute : Attribute
```

#### Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Expression` | `string` | *Required* | Raw SQL expression (e.g. `"price * quantity"` or `"COALESCE(discount, 0)"`). |
| `Filterable` | `bool?` | `false` | Whether filtering is supported on this computed field. |
| `Sortable` | `bool?` | `false` | Whether sorting is supported on this computed field. |

> [!NOTE]
> The source generator validates computed expressions with `SqlExpressionValidator` to prevent SQL injection and unsafe keywords (`DROP`, `DELETE`, `INSERT`, `;`, `--`).

#### Example

```csharp
public class OrderItemResponse
{
    public Guid Id { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }

    [Computed(Expression = "unit_price * quantity", Sortable = true)]
    public decimal TotalAmount { get; init; }

    [Computed(Expression = "COALESCE(discount_rate, 0)")]
    public decimal EffectiveDiscount { get; init; }
}
```

---

## Filter Structures & Supported Operators

Filter classes implement `ICursorFilter` and use strongly-typed filter structs from `CursorPaginator.Core`:

| Filter Struct | Target C# Types | Available Operators |
| :--- | :--- | :--- |
| `StringFilter` | `string` | `IsNull`, `Eq`, `Ne`, `StartsWith`, `EndsWith`, `Contains`, `In`, `NotIn` |
| `NumberFilter<T>` | `int`, `long`, `decimal`, `double`, `float`, `short`, `byte` | `IsNull`, `Eq`, `Ne`, `Gt`, `Gte`, `Lt`, `Lte`, `In`, `NotIn` |
| `DateTimeFilter` | `DateTime` | `IsNull`, `Eq`, `Ne`, `Gt`, `Gte`, `Lt`, `Lte`, `In`, `NotIn` |
| `DateOnlyFilter` | `DateOnly` | `IsNull`, `Eq`, `Ne`, `Gt`, `Gte`, `Lt`, `Lte`, `In`, `NotIn` |
| `BoolFilter` | `bool` | `IsNull`, `Eq`, `Ne` |
| `GuidFilter` | `Guid` | `IsNull`, `Eq`, `Ne`, `In`, `NotIn` |
| `EnumFilter<T>` | `TEnum : struct, Enum` | `IsNull`, `Eq`, `Ne`, `Gt`, `Gte`, `Lt`, `Lte`, `In`, `NotIn` |

---

## Step-by-Step Implementation Patterns

### 1. Standard Single-Table Query

**`CategoryFilter.cs`**
```csharp
using CursorPaginator.Core;

namespace Catalog.Categories.Api.Requests;

public class CategoryFilter : ICursorFilter
{
    public StringFilter? Name { get; init; }
    public BoolFilter? IsActive { get; init; }
    public DateTimeFilter? CreatedAt { get; init; }
}
```

**`CategoryResponse.cs`**
```csharp
namespace Catalog.Categories.Api.Responses;

public class CategoryResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastModifiedAt { get; init; }
}
```

**`GetCategoriesQuery.cs`**
```csharp
using CursorPaginator.Core;
using Catalog.Categories.Api.Requests;
using Catalog.Categories.Api.Responses;

namespace Catalog.Categories.Application.Queries;

[GenerateQuery(
    Table = "category",
    HasTenantFilter = true,
    Sortable = ["Id", "CreatedAt", "LastModifiedAt", "Name"],
    Filter = typeof(CategoryFilter),
    Response = typeof(CategoryResponse)
)]
partial class GetCategoriesHandler
{
}
```

---

### 2. Multi-Tenant Query

Add `HasTenantFilter = true` to automatically append `tenant_id = @TenantId` to all queries:

```csharp
[GenerateQuery(
    Table = "media_asset",
    HasTenantFilter = true,
    Sortable = ["Id", "CreatedAt", "FileName", "FileSize"],
    Filter = typeof(MediaAssetFilter),
    Response = typeof(MediaAssetResponse)
)]
partial class GetMediaAssetsHandler { }
```

---

### 3. Query with Static Base Filters (`[Where]`)

```csharp
[GenerateQuery(
    Table = "reorder_rule",
    Sortable = ["Id", "CreatedAt", "MinQuantity"],
    Filter = typeof(ReorderRuleFilter),
    Response = typeof(ReorderRuleResponse)
)]
[Where(Property = "is_active",   Operator = FilterOperator.Eq,     Value = true, Order = 1)]
[Where(Property = "archived_at", Operator = FilterOperator.IsNull,               Order = 2)]
partial class GetReorderRulesHandler { }
```

---

### 4. Multi-Table Join Query (`[Join]`, `[JoinCondition]`, `[JoinWhere]`)

**`GetOrdersQuery.cs`**
```csharp
using CursorPaginator.Core;
using Orders.Orders.Api.Requests;
using Orders.Orders.Api.Responses;

namespace Orders.Orders.Application.Queries.Order;

[GenerateQuery(
    Table = "order",
    HasTenantFilter = true,
    Sortable = ["Id", "OrderNumber", "OrderDate", "TotalAmount"],
    Filter = typeof(OrderFilter),
    Response = typeof(OrderListResponse)
)]
[Join(Table = "customer", Alias = "c", Type = JoinType.Left, Order = 1)]
[JoinCondition(
    JoinAlias = "c",
    LeftTable = "order", LeftColumn = "customer_id",
    Operator = FilterOperator.Eq,
    RightTable = "c",   RightColumn = "id",
    Order = 1
)]
[JoinWhere(JoinAlias = "c", Property = "is_deleted", Operator = FilterOperator.Eq, Value = false, Order = 1)]
[Where(Property = "is_deleted", Operator = FilterOperator.Eq, Value = false, Order = 1)]
partial class GetOrdersHandler { }
```

**`OrderListResponse.cs`**
```csharp
using CursorPaginator.Core;

namespace Orders.Orders.Api.Responses;

public class OrderListResponse
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public DateTime OrderDate { get; init; }
    public decimal TotalAmount { get; init; }

    [QueryField(Table = "c", ColumnName = "name")]
    public string CustomerName { get; init; } = string.Empty;

    [QueryField(Table = "c", ColumnName = "email")]
    public string CustomerEmail { get; init; } = string.Empty;
}
```

---

### 5. Column Mapping & Computed Fields (`[QueryField]`, `[Computed]`)

```csharp
using CursorPaginator.Core;

namespace Sales.Invoices.Api.Responses;

public class InvoiceItemResponse
{
    public Guid Id { get; init; }

    [QueryField(ColumnName = "item_sku")]
    public string Sku { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }

    [Computed(Expression = "unit_price * quantity", Sortable = true)]
    public decimal LineTotal { get; init; }

    [Computed(Expression = "COALESCE(tax_amount, 0)")]
    public decimal TaxTotal { get; init; }
}
```

---

### 6. Enums Stored as Strings

By default an enum filter value (and an enum sort value in a cursor) is bound as the enum itself, which Dapper sends as its **integer** value. If the column holds the enum's *name* (EF Core `HasConversion<string>()`), that comparison is wrong: MySQL coerces every non-numeric string to `0`, so `Status = 0` matches every row and `Status = 1` matches none.

Keep using `EnumFilter<T>` and opt in with `EnumsAsStrings`; values are then bound as `enum.ToString()`:

```csharp
[GenerateQuery(
    Table = "pick_list",
    EnumsAsStrings = true,          // every enum property of this query
    Filter = typeof(PickListFilter),
    Response = typeof(PickListResponse))]
partial class GetPickListsHandler { }
```

Mix string- and int-stored enums in one query by overriding per property:

```csharp
[GenerateQuery(Table = "stock_take", EnumsAsStrings = true, Filter = typeof(StockTakeFilter), Response = typeof(StockTakeResponse))]
[QueryMap(Property = nameof(StockTakeResponse.Kind), EnumAsString = false)]   // this column is an int
partial class GetStockTakesHandler { }
```

`EnumAsString` is also available on `[QueryField]`. Sorting by a string-stored enum column orders alphabetically, as the database does.

### 7. API Endpoint Pattern

Every paginated API exposes two endpoints:

1. **`GET /`** — simple scroll with query parameters `cursor` and `limit`
2. **`POST /pagination`** — dynamic filters and sorting via request body

**API Service (`CategoryApi.cs`)**
```csharp
namespace Catalog.Categories.Api;

public interface ICategoryApi
{
    Task<Outcome<PaginationResponse<CategoryResponse>>> GetAsync(
        int limit = 10,
        string? cursor = null,
        CancellationToken cancellationToken = default);

    Task<Outcome<PaginationResponse<CategoryResponse>>> GetPaginationAsync(
        PaginationRequest<CategoryFilter> query,
        CancellationToken cancellationToken = default);
}

class CategoryApi(CatalogUnitOfWork unitOfWork) : ICategoryApi
{
    private readonly CatalogUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Outcome<PaginationResponse<CategoryResponse>>> GetAsync(
        int limit = 10, string? cursor = null, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.CursorQuery<GetCategoriesHandler, CategoryFilter, CategoryResponse>(
            limit, cursor, cancellationToken);
    }

    public async Task<Outcome<PaginationResponse<CategoryResponse>>> GetPaginationAsync(
        PaginationRequest<CategoryFilter> query, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.CursorQuery<GetCategoriesHandler, CategoryFilter, CategoryResponse>(
            query, cancellationToken);
    }
}
```

**Endpoint Registration (`CategoryApiEndpoints.cs`)**
```csharp
namespace Catalog.Categories.Api;

static class CategoryApiEndpoints
{
    public static void AddCategoryApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (ICategoryApi api, string? cursor, int limit = 10, CancellationToken cancellationToken = default) =>
        {
            var result = await api.GetAsync(limit, cursor, cancellationToken);
            return result.ActionResult();
        }).RequireAuthorization(CatalogPermissions.CategoriesView)
          .WithName("GetCategories")
          .WithSummary("Get categories")
          .WithDescription("Get categories with cursor pagination");

        app.MapPost("/pagination", async ([FromBody] PaginationRequest<CategoryFilter> query, ICategoryApi api, CancellationToken cancellationToken) =>
        {
            var result = await api.GetPaginationAsync(query, cancellationToken);
            return result.ActionResult();
        }).RequireAuthorization(CatalogPermissions.CategoriesView)
          .WithName("GetCategoriesPagination")
          .WithSummary("Get categories pagination")
          .WithDescription("Get categories with dynamic filtering and pagination");
    }
}
```

---

## Architectural Rules & Best Practices

### 1. Model Type Constraints (Class vs. Record)
- **Filters & Response DTOs**: Must always be defined as **`class`**.
  - C# `record` types generate a compiler-synthesized `EqualityContract` property that interferes with Dapper query mapping at runtime.
  - Use `public class FilterName : ICursorFilter` and `public class ResponseName { }` with `{ get; init; }` properties.
- **Request DTOs**: Other request DTOs (e.g. `CreateSupplierRequest`) must be **`record`** types.
- **File Isolation**: Every filter class and response class **must reside in its own separate file** (e.g. `SupplierFilter.cs` and `SupplierResponse.cs`).
- **Use Enums, Not Strings or Ints**: Always use strongly-typed Enums in responses, requests, and filters (using `EnumFilter<T>`; set `EnumsAsStrings = true` when the column stores enum names). Global JSON options automatically serialize Enums as strings.

### 2. File & Class Naming

> [!IMPORTANT]
> **File Naming Standard**:
> - Files containing `[GenerateQuery]` must **always be named `Get[Entities]Query.cs`** (e.g. `GetCategoriesQuery.cs`, `GetShipmentsQuery.cs`).
> - **Never** name them `Get[Entities]Handler.cs` or `Get[Entities]QueryHandler.cs`.
> - The partial handler class must **omit `public`** (defaulting to `internal`): `partial class GetCategoriesHandler { }`.

### 3. Strongly Typed IDs vs Guids
- Within the **same module boundary**, always use strongly-typed ID classes (e.g. `CategoryId`, `InvoiceId`).
- Use raw `Guid` or `Guid?` **only** when referencing an aggregate root from a **different module** (cross-module domain references cannot be direct object references).

### 4. Bypass MediatR for Pagination Queries
- Do **not** use MediatR (`IMediator`) for cursor pagination or direct single-table Dapper lookups.
- Call `_unitOfWork.CursorQuery<THandler, TFilter, TResponse>` directly in the API implementation class.

### 5. Prohibit Offset Pagination & Expensive COUNT Queries
- **Never use `OFFSET / FETCH` or `LIMIT offset, limit`** — offset queries degrade at O(N) as data grows.
- **Never run `SELECT COUNT(*)`** on paginated listings — full table scans ruin latency on large datasets.
- **Always use `CursorPaginator` with `Limit + 1`** — detects `HasNextPage` in O(1) with zero extra database round-trips.
- **Signed Base64 Cursors** — pagination state (filters, sort order, tie-breaker values) is cryptographically signed with HMAC-SHA256.

### 6. Move Request-to-Command Mappings to Mappers
- Do **not** construct MediatR commands inline within API methods.
- Put all request-to-command logic in extension methods inside Mapper classes (e.g. `CategoryRequestMappers.cs` in `Application/Mappers/`).
- The API class should only call the mapper and dispatch via MediatR:
  ```csharp
  return await _mediator.Send(request.ToCommand(), cancellationToken);
  ```

---

## Troubleshooting & Pitfalls

### 1. Record Types Cause Dapper Runtime Errors
C# records generate a protected `EqualityContract` property of type `System.Type`. Dapper attempts to map all readable properties, causing runtime failures.  
**Fix**: Always use `class` for filters and response DTOs.

### 2. Table Name Mismatch
Always ensure `Table` in `[GenerateQuery(Table = "...")]` matches the EF Core snake_case convention (e.g. `"stock_movement"`, not `"StockMovements"` or `"StockMovement"`).

### 3. Roslyn Analyzer Project References
When referencing the generator in a consuming `.csproj`, use:
```xml
<PackageReference Include="CursorPaginator" Version="1.0.0" />
<PackageReference Include="CursorPaginator.Generator" Version="1.0.0"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

### 4. `[QueryMap]` vs `[QueryField]`
Prefer `[QueryMap]` on the query handler class to keep Response DTO files free of database concerns. Use `[QueryField]` only as a local override directly on a DTO property when `[QueryMap]` is not feasible.

---

## License

[MIT](LICENSE) © 2026 teranesranjith
