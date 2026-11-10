# Assumptions

1. **Identity:** No login. The `X-Student-Id` header (17 digits) is the student's identity. Anyone who knows the number can act on that student's application. Real systems need authentication.
2. **Seat accounting:** A class's used seats = active HELD holds (not expired) + CONFIRMED holds. Expired holds release the seat at the moment they expire, with no API call needed.
3. **Expiry:** Computed from `expiresAt` at read time. The stored status stays `HELD`, and the API reports `EXPIRED`. No background job is needed.
4. **Hold duration:** Default 600 seconds, set by `HOLD_DURATION_SECONDS`.
5. **One active application per student per class:** A student may apply to different classes (for example Morning and Day shift) at the same time, but only one HELD or CONFIRMED application per class.
6. **Confirm = payment succeeded:** No real payment gateway, as the brief says. Confirm is idempotent. Confirmed holds never expire.
7. **Cancel:** Allowed only for HELD applications. Confirmed admissions return 409. Cancelling an expired or already cancelled hold returns 204 (idempotent).
8. **Ownership:** Accessing another student's hold returns 404, so the existence of the hold is not leaked.
9. **Concurrency:** Each seat-changing operation takes a row lock on the class inside a DB transaction. This is correct even with several backend processes sharing one database.
10. **Errors:** All errors return JSON `{ "error": "<CODE>", "message": "..." }`. Invalid input returns 400, never 500.
11. **Timezone:** The API uses UTC ISO-8601 timestamps. The UI displays them in local time.
12. **Persistence:** PostgreSQL uses a named Docker volume, so data survives `docker compose down` and `up`.
13. **Schema:** Created with `EnsureCreated` for simplicity. A production system would use EF migrations.
14. **Class names:** Duplicates are allowed. No pagination, since class counts are small.
15. **Admin:** No auth for the admin screen, per the brief.
