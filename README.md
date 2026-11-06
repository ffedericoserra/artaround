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

## Where things are

```
backend/ArtAround.Api/
  Controllers/      HTTP endpoints: auth, museums, items, visits, sessions
  Services/         PurchaseService: the transaction that moves credits
  Hubs/             SessionHub: SignalR, broadcast only
  Authorization/    The "you own this" policy
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

## Design decisions

**Purchases can't overspend or deadlock.** Buying locks the buyer and creator rows with `SELECT ... FOR UPDATE`, always in id order, inside one transaction. A second purchase by the same buyer waits, then sees the updated balance. A `CHECK (wallet_balance >= 0)` is the last line of defence. Saving a visit and paying for its items share one transaction, so neither happens without the other.

**The database has the final word on races.** Registering inserts and catches the unique violation, because checking first would leave a gap between the check and the insert. Wallet recharges add inside one `UPDATE`. Items, visits and sessions use Postgres's `xmin` column for optimistic concurrency, so a conflicting save gets a 409.

**Nested data is relational.** The app started on MongoDB with embedded arrays. They are now child tables with foreign keys, so an item used by a visit can't be deleted. Question options are a `text[]`, because they are only ever read as an ordered list.

**Authorization is deny by default.** A global filter requires a token on every action unless it says `[AllowAnonymous]`. Ownership is one resource-based policy, and answer keys are removed from responses for everyone but the host or author.

**Real-time is broadcast only.** Every change goes through REST, then SignalR tells the room. Ownership and validation rules live in one place.

**One error shape.** Every failure is ProblemDetails plus an `error` field, and `details` for validation.

**Reads avoid row explosions.** Loading several collections uses split queries, and lists avoid per-row queries.

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

## How it was built

The first version was a university team project with a Node and MongoDB backend. This backend is a rewrite in .NET and PostgreSQL, made to learn the stack. I made the design decisions and built it with coding agents, which wrote much of the code. The original API contract was captured as tests first, then used to verify the rewrite.
