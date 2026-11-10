# School Admission System

## Requirements

- Docker (Docker Desktop or Docker Engine with Compose v2)
- Nothing else. No .NET or Node install is needed to run the app.

## Run the app

```bash
docker compose up --build
```

- Frontend: http://localhost:3000
- Backend API: http://localhost:8080
- Swagger: http://localhost:8080/swagger

Stop with `Ctrl+C`, or run `docker compose down`. Data is kept.
To reset all data: `docker compose down -v`.

Optional: change the hold duration with `HOLD_DURATION_SECONDS=300 docker compose up --build`.

## Run the tests

Backend (needs Docker, because Testcontainers starts PostgreSQL):

```bash
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock \
  -v "$PWD/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test tests/Backend.Tests
```

Frontend:

```bash
docker run --rm -v "$PWD/frontend:/app" -w /app node:20-alpine sh -c "npm install && npm test"
```

## Notes

- The browser calls the API at `VITE_API_URL`, which is set at build time in `docker-compose.yml`.
- Schema is created on startup with `EnsureCreated`, so it is idempotent.
- See `ASSUMPTIONS.md` for the decisions made where the brief was unclear.
