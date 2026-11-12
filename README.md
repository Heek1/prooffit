# ProofFit

**Does your resume prove what the vacancy asks for?**

ProofFit compares a resume with a job posting and backs every verdict with evidence: a
requirement counts as met only when it can point to a line in the resume or a fact from the
candidate's GitHub. The language model extracts and matches; plain code gathers the evidence
and computes the score, so the same input always gives the same result.

## Status

The project is built in four stages. Stage 1 is in progress.

| Stage | Scope | State |
|---|---|---|
| 1. Skeleton | API, database, resume upload, vacancy input, tests, CI, Docker | In progress: GitHub sign-in and deployment remain |
| 2. Analysis | Requirement and claim extraction, evidence-backed matching, score, evaluation set | Not started |
| 3. Report | GitHub evidence collector, report page with live progress, history | Not started |
| 4. Polish | Demo mode with sample data, cost dashboard, screenshots | Not started |

What works today: upload a PDF resume, paste a vacancy, and both are stored and shown back
line by line. Those line numbers are what the match report will cite.

## Stack

- **Backend:** ASP.NET Core 10 minimal APIs, EF Core, PostgreSQL, PdfPig
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS
- **Tests:** xUnit; integration tests run the real API against PostgreSQL in Testcontainers
- **Infrastructure:** Docker Compose, GitHub Actions

## Run it locally

You need the .NET 10 SDK, Node.js 22 and Docker.

Start the database:

```bash
docker compose up -d db
```

Start the API on http://localhost:5080. It applies database migrations on startup in
development:

```bash
dotnet run --project backend/src/ProofFit.Api
```

Start the frontend on http://localhost:5173:

```bash
cd frontend && npm install && npm run dev
```

To run everything in containers instead, with the app on http://localhost:8080:

```bash
docker compose up --build
```

## Tests

The integration tests start their own PostgreSQL container, so Docker has to be running.

```bash
cd backend && dotnet test
```

```bash
cd frontend && npm run lint && npm run build
```

## API

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/resumes` | Upload a PDF (multipart field `file`, up to 5 MB); returns the extracted text |
| `GET` | `/api/resumes/{id}` | A stored resume |
| `POST` | `/api/vacancies` | Save a vacancy: `{ "title", "text" }`, text of 50 to 20,000 characters |
| `GET` | `/api/vacancies/{id}` | A stored vacancy |
| `GET` | `/health/live` | The process is up |
| `GET` | `/health/ready` | The process is up and can reach the database |

Errors are returned as [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem details.
In development the OpenAPI document is served at `/openapi/v1.json`.

## Layout

```
backend/
  src/ProofFit.Api/
    Domain/        entities and shared input limits
    Data/          EF Core context and migrations
    Features/      one folder per feature: endpoints and the code only they use
  tests/ProofFit.Api.Tests/
frontend/
  src/
    api.ts         typed client for the API
    components/    the two document sheets and their parts
docker-compose.yml
.github/workflows/ci.yml
```
