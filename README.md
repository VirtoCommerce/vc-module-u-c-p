# Virto Commerce UCP Module

Модуль Virto Commerce UCP предоставляет HTTP API для Universal Commerce Protocol поверх существующих возможностей Virto Commerce Platform.

Модуль открывает публичные UCP endpoints для discovery, catalog, cart, checkout handoff и order tracking операций. Запросы преобразуются в in-process вызовы Virto Commerce XAPI и сервисов платформы без отдельного HTTP hop внутри platform process.

## Overview

`Virtocommerce.UCP` - это protocol adapter module. Он не заменяет Catalog, Cart, Orders, XAPI или Store модули. Модуль предоставляет компактный UCP-oriented HTTP surface для внешних клиентов и MCP tools, а commerce behavior делегирует существующим Virto Commerce modules.

Текущая реализация покрывает:

- UCP discovery profile: `/.well-known/ucp`.
- Catalog search через XCatalog GraphQL, выполняемый in-process.
- Product detail lookup через XCatalog GraphQL, выполняемый in-process.
- Cart assembly через XCart GraphQL: create, buyer-scoped list, get, full-state update.
- Checkout snapshot и hosted handoff с address prefill через stateless DataProtection token.
- Order tracking через Orders module services: lookup по order id/number или cart id после handoff.
- Structured UCP errors.
- Buyer context propagation из HTTP headers.
- Checkout update endpoint для address hints до hosted handoff.

Канонические публичные UCP endpoints публикуются без префикса `/api`.

## Module Structure

| Project | Назначение |
| --- | --- |
| `Virtocommerce.UCP.Core` | Protocol models, service contracts, module constants, options, errors. |
| `Virtocommerce.UCP.Data` | Provider-neutral data project; domain storage model пока не используется. |
| `Virtocommerce.UCP.Data.SqlServer` | SQL Server provider marker. |
| `Virtocommerce.UCP.Data.MySql` | MySQL provider marker. |
| `Virtocommerce.UCP.Data.PostgreSql` | PostgreSQL provider marker. |
| `Virtocommerce.UCP.ExperienceApi` | XAPI schema marker для модуля. |
| `Virtocommerce.UCP.Web` | Module entry point, controllers, services, DI registrations. |
| `Virtocommerce.UCP.Tests` | Unit tests для discovery profile, catalog, cart, checkout handoff, geography и order tracking behavior. |

## Architecture

```mermaid
flowchart LR
    Client["UCP / MCP client"]
    UcpHttp["UCP HTTP API<br/>/.well-known/ucp<br/>/ucp/v1/*"]
    Controllers["ASP.NET Core controllers<br/>UcpProfileController<br/>UcpCatalogController<br/>UcpCartController<br/>UcpCheckoutController<br/>UcpOrderController"]
    Services["UCP services<br/>UcpProfileService<br/>UcpCatalogService<br/>UcpCartService<br/>UcpCheckoutService<br/>UcpOrderService"]
    Executor["IXApiInProcessExecutor<br/>GraphQL executer"]
    XApi["Virto Commerce XAPI<br/>scoped schema: ucp"]
    Modules["Commerce modules<br/>XCatalog, XCart, Orders,<br/>Marketing, Store, Pricing, Inventory"]

    Client --> UcpHttp
    UcpHttp --> Controllers
    Controllers --> Services
    Services --> Executor
    Executor --> XApi
    XApi --> Modules
```

### Request Flow

1. Клиент вызывает канонический UCP endpoint.
2. Controller принимает HTTP request и делегирует работу UCP service.
3. Service нормализует UCP request context: store, currency, culture, pagination и buyer headers.
4. Catalog operations преобразуются в XCatalog GraphQL queries.
5. Cart operations преобразуются в XCart GraphQL queries/mutations.
6. `IXApiInProcessExecutor` выполняет GraphQL внутри текущего platform process, без отдельного HTTP call.
7. Service мапит XCatalog, XCart и Orders data обратно в UCP response models.

Buyer delegation сейчас header-based:

- `X-Buyer-User-Id`
- `X-Buyer-Organization-Id`

Service добавляет buyer claims в principal, который используется для XAPI execution. Так B2B delegated context проходит через существующие Virto Commerce authorization и context mechanisms.

### Integration Principle

UCP is an adapter layer for the existing Virto Commerce platform and storefront contracts.
It must not require legacy storefront GraphQL contracts, route contracts, or checkout UI contracts to change for UCP-specific scenarios.
When UCP and storefront naming differs, the mapping belongs in the UCP module or MCP adapter, not in the storefront contract.

## Dependencies

Module manifest объявляет runtime dependencies:

| Module | Version |
| --- | --- |
| `VirtoCommerce.Xapi` | `3.1001.0` |
| `VirtoCommerce.XCatalog` | `3.1000.0` |
| `VirtoCommerce.XCart` | `3.1016.0` |
| `VirtoCommerce.Store` | `3.1003.0` |
| `VirtoCommerce.Orders` | `3.1000.0` |
| `VirtoCommerce.Marketing` | `3.1000.0` |

Target framework: `.NET 10`.

## Configuration

Configuration читается из секции `UCP`:

```json
{
  "UCP": {
    "DefaultStoreId": "store-acme",
    "DefaultCurrency": "USD",
    "DefaultCultureName": "en-US",
    "UcpBaseUrl": "https://localhost:5001/ucp/v1",
    "HandoffTokenTtlMinutes": 15,
    "AnonymousCatalog": true
  }
}
```

Если `DefaultStoreId` не настроен, discovery читает открытые магазины из Store module.
Если найден один магазин, `/.well-known/ucp` возвращает его как `default_store_id`, `store` и единственный элемент `stores[]`, чтобы MCP-клиент мог выбрать store без ручного `UCP_STORE_ID`.
Если найдено несколько магазинов, `/.well-known/ucp` возвращает их в `stores[]`, но не выбирает default store автоматически: клиент должен передать явный `store_id`.
Checkout handoff URL строится из Virto Commerce Store URL (`Store.Url` / `Store.SecureUrl`) для выбранного default store.
`UCP:StorefrontOrigin` остаётся fallback для окружений без Store URL, а `UCP:HandoffUrlTemplate` можно использовать как explicit override.

Модуль также регистрирует platform setting `UCP.Enabled`.

## Web API

### Discovery

```http
GET /.well-known/ucp
```

Возвращает UCP profile: supported capabilities, default store metadata, endpoint metadata, headers, auth shape, MCP tool names, integration guidance и structured error codes.

`mcp_tools` содержит только callable tools. Planned operations остаются в `endpoints.operations`, но не рекламируются как MCP tools.

`agent_guidance` описывает checkout contract для MCP-клиента: перед hosted handoff для физических товаров нужен `shipping_address`, `billing_address` может совпадать с shipping address, а после оплаты `track_order` может использовать исходный `cart_id`.
Адрес доставки нельзя класть в `notes`: `notes` - это только order comments. Свободный текст адреса нужно маппить в `shipping_address`. Перед checkout страна разрешается через `resolve_country` / `list_countries`, а если страна имеет regions - через `list_regions`, с platform ids из UCP geography endpoints. Например `United States, Seattle, 1 Main St Apt 100` можно сначала разрешить как `US -> USA`, затем отправить `country_code: "USA"`, `country_name: "United States"`, `city: "Seattle"`, `line1: "1 Main St"`, `line2: "Apt 100"`.
Если адрес меняется после создания checkout/handoff, нужен `update_checkout` с новым адресом и повторный `handoff_checkout`, чтобы вернуть свежий `continue_url`.

### Catalog Search

```http
POST /ucp/v1/catalog/search
```

Пример request:

```json
{
  "query": "iphone",
  "context": {
    "store_id": "store-acme",
    "currency": "USD",
    "language": "en-US"
  },
  "filters": {
    "price": {
      "min": 50000,
      "max": 100000
    }
  },
  "pagination": {
    "limit": 10
  }
}
```

Цены в UCP responses и filters передаются в minor units. Например, `$700.00` представляется как `70000`.

### Product Detail

```http
GET /ucp/v1/catalog/products/{id}?store_id=store-acme&currency=USD&culture_name=en-US
```

Product response включает:

- `id`
- `code`
- `name`
- `slug`
- `image_url`
- `brand`
- `product_type`
- `price`
- `list_price`
- `availability`
- `attributes`
- `variations`

Если product не найден, endpoint возвращает structured error `product_not_found`.

### Cart Assembly

```http
POST /ucp/v1/carts
GET /ucp/v1/carts?store_id=store-acme&currency=USD&culture_name=en-US&buyer_id=user-42
GET /ucp/v1/carts/{cartId}?store_id=store-acme&currency=USD&culture_name=en-US
PUT /ucp/v1/carts/{cartId}
```

`create_cart` создаёт корзину через XCart `addItem`, затем применяет купоны через `addCoupon`.

`list_carts` - Virto extension поверх XCart `carts` query. Он требует buyer context через `X-Buyer-User-Id` или `context.buyer_id`/`buyer_id` query parameter и не возвращает общий anonymous список корзин.

Пример create request:

```json
{
  "context": {
    "store_id": "store-acme",
    "currency": "USD",
    "language": "en-US",
    "buyer_id": "user-42",
    "organization_id": "org-100"
  },
  "line_items": [
    {
      "product_id": "product-id",
      "quantity": 1
    }
  ],
  "coupons": ["SAVE10"]
}
```

`update_cart` следует UCP replacement semantics: request передаёт желаемое итоговое состояние корзины, а adapter вычисляет diff и вызывает XCart mutations:

- `addItem`
- `changeCartItemQuantity`
- `removeCartItem`
- `addCoupon`
- `removeCoupon`

Чтобы удалить позицию, нужно исключить её из `line_items` или передать существующий `line_items[].id` с `quantity: 0`.

Cart response включает:

- `id`
- `status`
- `store_id`
- `currency`
- `buyer_id`
- `organization_id`
- `line_items`
- `totals`
- `coupons`
- `addresses`
- `shipments`
- `payments`
- `continue_url`
- `messages`

Денежные значения возвращаются в minor units.

### Geography

```http
GET /ucp/v1/geography/countries?query=United%20States&limit=10
GET /ucp/v1/geography/countries/resolve?query=KZ
GET /ucp/v1/geography/countries/{countryId}/regions
```

Geography endpoints являются thin adapter поверх platform `ICountriesService`.

- `list_countries` возвращает platform countries и поддерживает простой поиск по `id` / `name`.
- `resolve_country` принимает ISO2, ISO3 или platform country name и возвращает platform country id, например `KZ -> KAZ`.
- `list_regions` возвращает platform regions/provinces для country id, если они есть в справочнике.
- `city` не резолвится через справочник и остаётся текстовым полем checkout address.

MCP-клиент использует эти endpoints перед checkout, когда страна или область пришли натуральным языком. Это убирает догадки и сохраняет старый storefront/XCart address contract.

### Checkout Handoff

```http
POST /ucp/v1/checkouts
PATCH /ucp/v1/checkouts/{checkoutId}
GET /ucp/v1/checkouts/{checkoutId}/payment-handlers
POST /ucp/v1/checkouts/{checkoutId}/handoff
POST /ucp/v1/internal/handoff/restore
```

Текущий checkout flow hosted-only:

- `create_checkout` создаёт checkout snapshot из cart.
- Если request содержит `shipping_address` или `billing_address`, модуль применяет их к XCart перед созданием snapshot.
- `update_checkout` меняет address hints до оплаты и применяет адреса к XCart.
- `handoff_checkout` возвращает `continue_url` с защищённым `ucp_session`.
- После `update_checkout` нужно повторно вызвать `handoff_checkout`, чтобы получить новый `continue_url` с актуальным address snapshot.
- `continue_url` использует storefront origin из Store URL, например `https://localhost:3000/checkout?ucp_session=...`.
- `storefront_restore` валидирует `ucp_session` и возвращает cart/checkout context для storefront.
- Shipping method и payment details завершаются в storefront checkout.

Request model уже содержит optional hints для следующего шага:

- `buyer`
- `shipping_address`
- `billing_address`
- `shipping_method_id`
- `payment_handler`
- `notes`

`shipping_address` и `billing_address` применяются к cart через XCart `addOrUpdateCartAddress` и также сохраняются в handoff token. Перед записью адреса UCP нормализует `country_code` через платформенный `ICountriesService`: вход может содержать ISO2 вроде `KZ`, но предпочтительный flow - сначала вызвать `resolve_country` и передать platform country id вроде `KAZ`. Если для страны в платформенном справочнике есть regions, `region` / `region_id` нормализуются через `GetCountryRegionsAsync`; предпочтительно выбрать region через `list_regions`. `city` остаётся текстовым полем. `shipping_method_id` и `payment_handler` пока сохраняются как hints в token; выбор конкретного delivery/payment method требует валидных методов из storefront/XCart available methods после адреса.

Если `shipping_address` или `billing_address` передаётся в UCP, `first_name` и `last_name` обязательны: модуль возвращает `invalid_request`, если recipient name не указан. Для hosted checkout address form также нужно передавать `postal_code`: без него storefront сможет показать адрес строкой, но при редактировании адреса попросит дозаполнить ZIP / Postal code и не даст сохранить форму. Для лучшего guest checkout UX желательно передавать `email`; если он не был получен от пользователя, storefront попросит дозаполнить contact поля.

`ucp_session` сейчас является stateless DataProtection token, а не записью в UCP database. Token содержит checkout/cart context, address snapshot, payment hint и `expires_at`; при restore модуль расшифровывает token, проверяет срок действия и заново читает cart из XCart. Для production multi-node окружения нужен persistent shared ASP.NET Data Protection key ring для всех platform instances. Потеря key ring инвалидирует активные handoff links. Если потребуется revocation, one-time session или отсутствие payload в URL/logs, следующий production-hardening шаг - заменить token на opaque session id с server-side storage и TTL.

`notes` не применяются как delivery address. Если request содержит адрес только в `notes`, response вернёт warning `shipping_address_not_notes`; следующий `update_checkout` или `handoff_checkout` должен содержать заполненный `shipping_address`.

Пример handoff request с address prefill:

```json
{
  "cart_id": "cart-1",
  "context": {
    "store_id": "store-acme",
    "currency": "USD",
    "language": "en-US",
    "buyer_id": "ucp-anonymous-123"
  },
  "buyer": {
    "email": "buyer@example.com"
  },
  "shipping_address": {
    "first_name": "Ada",
    "last_name": "Buyer",
    "line1": "1 Main St",
    "city": "Seattle",
    "region_id": "WA",
    "region": "Washington",
    "postal_code": "98101",
    "country_code": "US",
    "country_name": "United States",
    "phone": "555-0100",
    "email": "buyer@example.com"
  },
  "billing_address": {
    "first_name": "Ada",
    "last_name": "Buyer",
    "line1": "1 Main St",
    "city": "Seattle",
    "region_id": "WA",
    "region": "Washington",
    "postal_code": "98101",
    "country_code": "US",
    "country_name": "United States",
    "phone": "555-0100",
    "email": "buyer@example.com"
  },
  "payment_handler": "hosted_checkout"
}
```

### Order Tracking

```http
GET /ucp/v1/orders/{orderId}?buyer_id=user-42&culture_name=en-US
GET /ucp/v1/orders?cart_id={cartId}&buyer_id=user-42&culture_name=en-US
```

`track_order` возвращает order status, order number, totals, line items, shipment snapshot, payment snapshot и shipment tracking поля, если они уже есть в order data.

Order tracking snapshot включает:

- order `status` / `status_display_value`
- line item `status`
- payment `status`, gateway, method, approval flag и billing address
- shipment `status`, approval flag, delivery date, delivery address, tracking number/url

После hosted handoff клиент обычно ещё не знает `order_id`, поэтому основной путь - lookup по исходному `cart_id`. Lookup выполняется по `CustomerOrder.ShoppingCartId` через Orders module services. Если buyer context изменился во время guest checkout, endpoint повторяет поиск без buyer filter и всё равно матчится строго по `cart_id`.

Если заказ ещё не создан storefront checkout flow или не найден среди recent orders, endpoint возвращает structured error `order_not_found`.

## Error Model

Известные UCP error codes:

- `invalid_request`
- `missing_store_id`
- `product_not_found`
- `cart_not_found`
- `order_not_found`
- `xapi_execution_failed`

Responses включают correlation id, если он доступен. Модуль читает `X-Correlation-Id` и fallback-ится на ASP.NET Core trace identifier.

## Build and Test

```powershell
dotnet build C:\Source\vc-modules\vc-module-u-c-p\Virtocommerce.UCP.sln
dotnet test C:\Source\vc-modules\vc-module-u-c-p\Virtocommerce.UCP.sln --no-build
```

Текущий ожидаемый статус:

- Build passes.
- Unit tests pass.
- Возможны `NU1903` warnings из provider dependency chains для `System.Security.Cryptography.Xml`.

## Installation Notes

Для local platform testing нужно устанавливать этот модуль, а не старый временный draft `vc-module-ucp`. Target module id:

```text
Virtocommerce.UCP
```

После установки стоит проверить:

1. В module list есть `Virtocommerce.UCP`.
2. `GET /.well-known/ucp` возвращает UCP profile.
3. `POST /ucp/v1/catalog/search` возвращает catalog results для настроенного store.
4. `GET /ucp/v1/catalog/products/{id}` возвращает product details или structured `product_not_found`.
5. `POST /ucp/v1/carts` создаёт XCart-backed корзину.
6. `GET /ucp/v1/carts` возвращает buyer-scoped список корзин.
7. `PUT /ucp/v1/carts/{cartId}` обновляет итоговое состояние корзины.
8. `POST /ucp/v1/checkouts` создаёт checkout snapshot.
9. `POST /ucp/v1/checkouts/{checkoutId}/handoff` возвращает hosted checkout `continue_url`.
10. `GET /ucp/v1/geography/countries/resolve?query=KZ` возвращает platform country id для checkout address.
11. `GET /ucp/v1/geography/countries/{countryId}/regions` возвращает regions, если они есть в platform dictionary.
12. После оформления на storefront `GET /ucp/v1/orders?cart_id={cartId}&buyer_id={buyerId}` возвращает order tracking snapshot.

## Roadmap

Ближайшие области реализации:

- Delivery/payment method selection after address-based available methods are known.
- Full carrier-level shipment tracking events, если появится carrier integration.
- Faceted/catalog filter schema для более богатого product discovery.
- OAuth2/OIDC buyer delegation вместо header-only context.

## License

Copyright (c) Virto Solutions LTD. All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

<https://virtocommerce.com/open-source-license>

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
