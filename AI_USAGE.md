# AI Usage

## Tools used

- <tool name and version>

## Where AI was used

- Scaffolding the project files and Dockerfiles
- Drafting the EF Core query for seat counting
- Drafting the React countdown component
- Writing the test cases (I reviewed and adjusted them)

## Where I did NOT use AI

- Designing the concurrency approach (row lock) and the expiry logic
- Choosing the data model and status rules

## Mistakes the AI made, and how I caught them

- Example: it first used `FOR UPDATE` inside `FromSql`, which EF wraps in a subquery and fails in PostgreSQL. I replaced it with an `UPDATE ... WHERE Id = ...` row lock, then confirmed it with the concurrency test.
- <add the real ones you hit>

## How I verified the AI output

- Ran the 3 backend tests and the frontend test
- Ran `docker compose up --build` from a fresh clone
- Checked every endpoint against the contract in §1.4 using Swagger
