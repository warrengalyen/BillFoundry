# Audit trail

BillFoundry records a durable **business audit trail** for important
administrative and financial activity. These rows are separate from diagnostic
application logs and from the Created/Updated timestamps on ordinary entities.

## Architecture

`AuditEvent` is an append-only domain entity. Application services call
`IAuditRecorder.Record` **before** `SaveChanges`. The audit row is added to the
same EF Core context as the business change, so a failed database transaction
does not leave a misleading event behind.

`IAuditService` is the read model:

- `SearchAsync` is limited to the Administrator policy.
- `ListForEntityAsync` uses the same policy a user needs to view that record
  (invoices, estimates, clients, or catalog items). Organization and user
  timelines stay administrator-only.
- There is no update or delete API. An interceptor rejects modified or deleted
  `AuditEvent` entries.

Row timestamps (`IAuditable` / `AuditableInterceptor`) still record who last
saved a row. That is not a history of business actions.

## Captured events

| Action | When |
| --- | --- |
| Organization updated / logo uploaded / logo removed | Organization settings |
| Client created / updated / activated / deactivated | Client profile and contacts |
| Service item created / updated / activated / deactivated | Catalog |
| Estimate created / updated / status changed / duplicated | Estimate workflow |
| Invoice created / updated / sent / voided / duplicated | Invoice workflow |
| Invoice created from estimate | Conversion (also records the estimate status change) |
| Payment recorded / reversed | Invoice receipts |
| Password changed / password reset completed / account locked out | Account security |

Reads, PDF downloads, report CSV exports, and ordinary successful sign-ins are
not audited.

Descriptions are written for administrators ("Marked invoice INV-0001 as sent"),
not internal method names.

## Privacy

Audit rows never store passwords, secrets, authentication tokens, or connection
strings. Metadata keys that look like those values are dropped before JSON is
saved. Payment notes, password-reset tokens, and raw request payloads are not
captured. User identity is stored as the signed-in user id and email.

## Audit events vs diagnostic logs

| | Audit events | Diagnostic logs |
| --- | --- | --- |
| Purpose | Prove who changed business records | Troubleshoot runtime behavior |
| Storage | SQL Server `AuditEvents` table | Logging providers / console |
| Lifetime | Durable, append-only | Rotated or discarded |
| Audience | Administrators | Operators and developers |
| Content | Action, entity, description, limited metadata | Exceptions, request traces |

Do not treat log files as the financial audit trail.
