# Invoices App — OCR & Fiscal Intelligence

Automated extraction, storage and management of Brazilian electronic invoices (NF-e/NFC-e). Users upload PDFs, the system runs OCR via a Go worker to extract structured data (CNPJ, items, totals, dates, access key), stores everything in PostgreSQL, and provides a Blazor WebAssembly frontend for viewing, editing, and exporting.

## Architecture

```
[Browser — Blazor WASM + MudBlazor]
        │ HTTP (REST JSON)
        ▼
[Caddy Reverse Proxy :80/:443]
        │ reverse_proxy
        ▼
[invoices-api — ASP.NET Core 10 :8080]  ◄──►  [PostgreSQL :5432]
        │                                      [Prometheus :9090]
        │ AMQP — queue "invoices_to_process"   [Grafana :3000]
        ▼                                      [Loki :3100]
[RabbitMQ :5672]
        │ AMQP — worker consumes
        ▼
[invoices-ocr — Go Worker]
    PDF → grayscale image → Tesseract OCR (pt-BR) → regex parsing → validation
        │
        │ AMQP — queue "processed_invoices"
        ▼
[RabbitMQ]
        │ InvoiceConsumer (BackgroundService)
        ▼
[invoices-api]
    Resolve/create Establishment → save Invoice + Items to PostgreSQL
        │
        ▼ HTTP (REST JSON)
[Blazor WASM]  →  user sees invoice in list/detail
```

## Stack

| Layer | Technology |
|---|---|
| **Frontend** | Blazor WebAssembly, MudBlazor v4, C# (.NET 10) |
| **API** | ASP.NET Core 10, Entity Framework Core, Npgsql, Serilog |
| **OCR Worker** | Go, Tesseract, go-fitz (PDF), RabbitMQ |
| **Database** | PostgreSQL 18, EF Core |
| **Messaging** | RabbitMQ (async processing queue) |
| **Auth** | JWT + Refresh Tokens, BCrypt |
| **Reverse Proxy** | Caddy (automatic HTTPS) |
| **Observability** | Prometheus (metrics), Loki (logs), Grafana (dashboards) |
| **Orchestration** | Docker Compose |

## Services

| Service | Container | Port(s) | Role |
|---|---|---|---|
| `rabbitmq` | rabbitmq:3-management | 5672 / 15672 | Message broker |
| `postgres` | postgres:18-alpine | 5433 (host) | Primary database |
| `invoices-api` | .NET 10 Web API | 5152 (host) | REST API, EFCore, auth |
| `invoices-ocr` | Go 1.24 worker | — | OCR processing (Tesseract) |
| `caddy` | caddy:2-alpine | 80 / 443 | Reverse proxy |
| `prometheus` | prom/prometheus | 9090 | Metrics |
| `loki` | grafana/loki | 3100 | Log aggregation |
| `grafana` | grafana/grafana | 3000 | Dashboards |

## Data Flow

1. **Upload** — User drops a PDF in the Blazor UI → `POST /api/invoices/process`
2. **Queue** — API stores the raw file in PostgreSQL and publishes to `invoices_to_process`
3. **OCR** — Go worker consumes the message, renders PDF to image, runs Tesseract, parses with regex (CNPJ, access key, items, totals, dates), validates checksums
4. **Result** — Worker publishes `ParsedInvoice` to `processed_invoices`
5. **Persist** — API's `InvoiceConsumer` background service resolves/creates the establishment and saves the full invoice + items
6. **View** — Blazor WASM polls/refreshes and displays the result

## Getting Started

```bash
# Start all services
docker compose up --build

# The app is available at http://localhost
# Grafana at http://localhost:3000 (admin/admin)
# RabbitMQ management at http://localhost:15672 (guest/guest)

# Default admin credentials
# Username: admin
# Password: Admin@123
```

## Project Structure

```
├── backend-go/                    # Go OCR worker
│   ├── main.go                    # Entry point, consumer loop, graceful shutdown
│   └── internal/
│       ├── config/                # Env-based configuration
│       ├── imaging/               # PDF → grayscale image
│       ├── messaging/             # RabbitMQ serialization
│       ├── models/                # RawInvoice, ParsedInvoice
│       ├── ocr/                   # Image preprocessing + Tesseract wrapper
│       └── parser/                # Regex patterns + parsing logic
├── invoices-dotnet/
│   ├── invoices.api/              # ASP.NET Core Web API
│   │   ├── Controllers/           # Auth, Invoices, Establishments
│   │   ├── Services/              # InvoiceService, AuthService, InvoiceConsumer
│   │   └── Data/                  # EF Core context, repositories, migrations
│   ├── invoices.core/             # Shared models, DTOs, service interfaces
│   ├── invoices.front.blazor/     # Blazor WASM + MudBlazor UI
│   │   ├── Components/Pages/      # Login, InvoiceList, InvoiceDetail, InvoiceUpload
│   │   ├── Components/Theme/      # Custom MudBlazor theme
│   │   └── Services/              # HTTP clients, auth state provider
│   └── invoices.tests/            # xUnit tests (WebApplicationFactory, SQLite in-memory)
├── infrastructure/
│   ├── caddy/                     # Caddyfile reverse proxy config
│   ├── grafana/                   # Auto-provisioned datasources
│   ├── loki/                      # Log storage config
│   ├── prometheus/                # Scrape config
│   └── promtail/                  # Docker log shipper config
└── docker-compose.yml             # All 9 services orchestrated
```

## API Endpoints

| Method | Route | Description |
|---|---|---|
| POST | `/api/auth/login` | Login, returns JWT + refresh token |
| POST | `/api/auth/refresh` | Rotate refresh token |
| POST | `/api/auth/logout` | Revoke refresh token |
| GET | `/api/invoices` | Paginated list with search/filter/sort |
| GET | `/api/invoices/{id}` | Single invoice with items |
| GET | `/api/invoices/groups` | Year-month groups with counts |
| GET | `/api/invoices/export` | XLSX export by month or selection |
| POST | `/api/invoices/process` | Submit PDF for OCR processing |
| PUT | `/api/invoices/{id}` | Update invoice metadata/items |
| DELETE | `/api/invoices/{id}` | Delete single invoice |
| POST | `/api/invoices/batch-delete` | Delete multiple invoices |
| GET | `/api/establishments/search` | Autocomplete by name/CNPJ |

## Design Decisions

- **Go for OCR** — Native concurrency with goroutines, lightweight workers, fast PDF rendering via go-fitz
- **RabbitMQ for async** — Decouples upload from processing; worker can crash and messages survive; easy to scale workers independently
- **Blazor WASM + MudBlazor** — Rich component library, warm custom theme (earth tones), SPA without JavaScript fatigue
- **Caddy** — Automatic HTTPS via Let's Encrypt, zero-config reverse proxy
- **Observability** — Prometheus metrics + Loki logs + Grafana dashboards for production monitoring
