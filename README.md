# Brewery ERP

Повноцінна ERP-система для крафтової пивоварні. Стек: **.NET 8 Web API** + **Angular 17** + **MySQL 8**.

---

## Зміст

- [Архітектура](#архітектура)
- [Вимоги](#вимоги)
- [Налаштування бази даних](#налаштування-бази-даних)
- [Запуск бекенду](#запуск-бекенду)
- [Запуск фронтенду](#запуск-фронтенду)
- [Тестування](#тестування)
- [SQL-утиліти](#sql-утиліти)
- [Структура проекту](#структура-проекту)

---

## Архітектура

```
BreweryERP/
├── backend/
│   ├── BreweryERP.Api/        # .NET 8 Web API (Controllers, Services, EF Core)
│   └── BreweryERP.Tests/      # xUnit тести (unit + structural + ACID)
├── frontend/
│   └── BreweryERP.Web/        # Angular 17 SPA
├── seed_brewery.sql            # Тестові дані для БД
├── queries_and_reports.sql     # Аналітичні запити + Views (Етап 6)
├── create_activity.sql         # Таблиця журналу активності
├── desc_tables.sql             # Утиліта: опис таблиць
└── show_tables.sql             # Утиліта: список таблиць
```

**Бізнес-патерни, реалізовані в БД:**
- **Складений ключ** — `InvoiceItem(invoice_id, ingredient_id)`, `RecipeItem(recipe_id, ingredient_id)`, `OrderItem(order_id, sku_id)`
- **Головний-підлеглий** — `SupplyInvoice → InvoiceItems`, `SalesOrder → OrderItems`
- **Версіонування** — `Recipe.IsActive` (архівація застарілих версій рецептів)

---

## Вимоги

| Компонент | Версія |
|-----------|--------|
| .NET SDK | 8.0+ |
| Node.js | 18+ |
| MySQL Server | 8.0+ |
| Angular CLI | 17+ |

---

## Налаштування бази даних

### 1. Створити базу даних

```sql
CREATE DATABASE craft_brewery CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

### 2. Налаштувати Connection String

Відкрити `backend/BreweryERP.Api/appsettings.json` і вказати свої параметри:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=3306;Database=craft_brewery;User=root;Password=YOUR_PASSWORD;CharSet=utf8mb4;"
  }
}
```

> **За замовчуванням**: `Server=127.0.0.1`, `Port=3306`, `User=root`, `Password=` (пусто).

### 3. Застосувати міграції EF Core

```powershell
cd backend/BreweryERP.Api
dotnet ef database update
```

### 4. Заповнити тестовими даними (опціонально)

```bash
mysql -u root -p craft_brewery < seed_brewery.sql
```

---

## Запуск бекенду

```powershell
cd backend/BreweryERP.Api
dotnet run
```

API буде доступний за адресою: **http://localhost:5124**  
Swagger UI: **http://localhost:5124/swagger**

### Перший користувач (Admin)

При першому запуску EF автоматично застосовує міграції та сідить дані.  
За замовчуванням API піднімається і можна зареєструватись через `/api/auth/register`.

### JWT Authentication

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@brewery.local",
  "password": "Admin@12345"
}
```

Отриманий `token` передавати в заголовку: `Authorization: Bearer <token>`

---

## Запуск фронтенду

```powershell
cd frontend/BreweryERP.Web
npm install
npm start
```

Додаток відкриється за адресою: **http://localhost:4200**

> Для зміни URL бекенду — відредагувати `src/environments/environment.ts`:
> ```ts
> export const environment = {
>   apiUrl: 'http://localhost:5124/api'
> };
> ```

---

## Тестування

> **IDE показує помилки у тестах, але build проходить?**
> Це стара Roslyn/OmniSharp cache. Щоб скинути: `Ctrl+Shift+P → "Restart Language Server"` або перезапусти VS Code.
> Перевір реальний стан командою `dotnet build` у терміналі.

### Передумови

Тести використовують **EF Core InMemory** — MySQL не потрібен. Просто `dotnet test`.

### Запуск всіх тестів

```powershell
# З кореня репо
dotnet test backend\BreweryERP.Tests\BreweryERP.Tests.csproj

# Або з папки тестів
cd backend\BreweryERP.Tests
dotnet test
```

### Корисні флаги

```powershell
# Детальний вивід (показує кожен тест)
dotnet test --logger "console;verbosity=detailed"

# Тільки один клас тестів
dotnet test --filter "FullyQualifiedName~SalesOrderServiceTests"
dotnet test --filter "FullyQualifiedName~BatchServiceTests"
dotnet test --filter "FullyQualifiedName~SupplyInvoiceServiceTests"
dotnet test --filter "FullyQualifiedName~ImportParserTests"

# Тільки структурні тести
dotnet test --filter "DisplayName~StructuralTest"

# Тільки ACID тести
dotnet test --filter "DisplayName~ACID"

# Без rebuild (якщо щойно будував)
dotnet test --no-build
```

### Очікуваний результат

```
Всього тестів: 86
     Пройдено: 86
Загальний час: ~2 сек
```

### Покриття тестів

| Файл | Що перевіряється | К-сть тестів |
|------|-----------------|--------------|
| `BatchServiceTests.cs` | Списання запасів, статус-переходи, ACID rollback при нестачі | 10 |
| `SupplyInvoiceServiceTests.cs` | CRUD накладних, TotalStock increment/rollback | 10 |
| `SalesOrderServiceTests.cs` | Master-Detail замовлення, FK/PK через EF metadata, ACID | 16 |
| `ImportParserTests.cs` | Unit-тести парсера Excel (CSV, заголовки, типи) | ~50 |

#### Типи тестів у `SalesOrderServiceTests`

| Категорія | Тести |
|-----------|-------|
| **Functional** | CreateAsync (валідація, ціна, маржа), UpdateStatus, GetById |
| **Structural** | `StructuralTest_OrderItem_HasCompositeKey` — composite PK `(order_id, sku_id)` |
| | `StructuralTest_InvoiceItem_HasCompositeKey` — composite PK `(invoice_id, ingredient_id)` |
| | `StructuralTest_RecipeItem_HasCompositeKey` — composite PK `(recipe_id, ingredient_id)` |
| | `StructuralTest_SalesOrder_ClientId_IsNotNullable` — NOT NULL constraint |
| | `StructuralTest_SalesOrder_FK_Client_IsRestrict` — DeleteBehavior.Restrict |
| | `StructuralTest_OrderItem_FK_Order_IsCascade` — DeleteBehavior.Cascade |
| **ACID** | `ACID_ShipWithZeroStock_ThrowsAndStockRemainsZero` — сток не стає від'ємним |

> **Примітка**: InMemory provider ігнорує реальні транзакції на рівні DB-engine.
> ACID перевіряється на рівні бізнес-логіки (guard clauses + exception handling).
> Реальна транзакційна ізоляція тестується вручну через SQL або integration-тести з MySQL.

---

## SQL-утиліти

| Файл | Призначення |
|------|-------------|
| `seed_brewery.sql` | Тестові дані: 10 стилів, 20 інгредієнтів, 8 рецептів, 10 партій, 10 замовлень |
| `queries_and_reports.sql` | 8 аналітичних запитів + 2 VIEW |
| `create_activity.sql` | Таблиця `activity_logs` для журналювання |
| `desc_tables.sql` | `DESCRIBE` для всіх таблиць |
| `show_tables.sql` | `SHOW TABLES` |

### Ключові звіти (`queries_and_reports.sql`)

| # | Запит |
|---|-------|
| 1 | Топ-5 клієнтів за сумою замовлень |
| 2 | Оборот по місяцях |
| 3 | Залишки складу готової продукції |
| 4 | Прибутковість партій |
| 5 | Найпопулярніші SKU |
| 6 | Витрати сировини по типах |
| 7 | Постачальники — обсяг та середня ціна |
| 8 | Структурна перевірка цілісності даних |

---

## Структура проекту

```
backend/BreweryERP.Api/
├── Controllers/     # REST контролери (Auth, Batch, Recipe, SalesOrder, ...)
├── Data/            # ApplicationDbContext + EF Fluent API конфігурація
├── DTOs/            # Request/Response records
├── Migrations/      # EF Core міграції
├── Models/          # Entity-класи (Entities.cs)
├── Services/        # Бізнес-логіка (BatchService, SalesOrderService, ...)
└── Program.cs       # DI, JWT, CORS, Swagger, Identity

frontend/BreweryERP.Web/src/app/
├── core/            # HTTP interceptors, guards, auth service
├── features/        # Компоненти за доменами (batches, orders, recipes, ...)
└── shared/          # Спільні компоненти та моделі
```
