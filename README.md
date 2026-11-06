*Note: this repository is a personal mirror of the [ArtAround](https://github.com/ffedericoserra/tec-web) project developed my Matteo Mazzetti, Andrea Ziccone, and me (Federico Serra) for the Web Technologies course at University of Bologna. I'm using it to gain knowledge with new technologies.*

# ArtAround

ArtAround is a web app for guided museum tours, with two frontends that share one backend:

- **Marketplace**: authors write descriptions of artworks ("items") in three tones and arrange them into tours ("visits"). Items can be bought with a credit wallet.
- **Navigator**: visitors pick a museum and a visit, then follow it stop by stop on their phone, with text-to-speech, voice commands and a museum map.

Visits can also run as **group visits**: a host shares a session code, participants follow in sync, with chat, questions and a live activity feed.

## Run it

You only need Docker:

```bash
docker compose up --build
```

- Navigator: http://localhost:8000
- Marketplace: http://localhost:8000/marketplace

Log in as `autore1`, `autore2`, `visitatore1`, `visitatore2` or `docente1`, all with password `12345678`. See [docs/demo-users.md](docs/demo-users.md) for what each visit shows.

The first start creates the database and fills it with two museums and demo data. To start over:

```bash
docker compose down -v
```

## Repo Structure

```
backend/ArtAround.Api/
  Controllers/      HTTP endpoints: auth, museums, items, visits, sessions
  Services/         PurchaseService: the transaction that moves credits
  Hubs/             SessionHub: SignalR, broadcast only
  Authorization/    'Owning' policies
  Domain/           EF entities
  Dtos/             JSON shapes and request validation
  Data/             DbContext, table mappings, migrations
  Infrastructure/   Error handling and small helpers
  Seeding/          Demo data loader
contract-tests/     API tests that run over real HTTP against a running server
frontend-marketplace/   Plain HTML, CSS and JS
frontend-navigator/     React + Vite
data/               Museum and demo data, loaded by the seed
uploads/            Images
```

## Stack

C# / ASP.NET Core 10 with controllers, PostgreSQL 17, EF Core, SignalR, JWT, xUnit, Docker. The frontends are vanilla JS and React 18.


## Development

Run the database in Docker and everything else locally:

```bash
docker compose up -d db
```

```bash
cd backend && dotnet tool restore && dotnet ef database update --project ArtAround.Api
```

```bash
cd backend/ArtAround.Api && dotnet user-secrets set "Jwt:Secret" "any-string-of-at-least-32-characters"
```

```bash
cd backend/ArtAround.Api && dotnet run -- seed && dotnet run
```

```bash
cd frontend-navigator && npm ci && npm run dev
```

The API listens on port 8000, and Vite proxies to it from port 5173. Without a built navigator, port 8000 serves only the API and the marketplace.

## Tests

With the app running on port 8000:

```bash
dotnet test contract-tests/ArtAround.ContractTests
```

They exercise the API over real HTTP and SignalR, including concurrent purchases, recharges and saves. CI runs them on every push against the full Docker setup.

