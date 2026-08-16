# Contributing

Issues and pull requests are welcome for the Community Edition.

## Ground rules

- Keep the modular monolith: Domain, Application, Infrastructure, Web
- Do not add MediatR, CQRS, or generic repositories unless a current problem
  requires them
- Authorize privileged work in application services, not only in Razor
- Use `TimeProvider` instead of `DateTime.Now` / `UtcNow`
- Use `decimal` for money
- Do not commit secrets, connection strings with passwords, or real client,
  invoice, or payment data
- Demo and development passwords in the repo are published placeholders only

## Workflow

1. `dotnet test` from the repository root
2. Follow existing naming and file layout
3. Update the durable document that matches the change (`docs/`)
4. Keep README current only when the operator-facing story changes

The Community Edition is AGPLv3. By contributing you agree to license your
work under the same terms. See [LICENSE](../LICENSE).

Do not send BillFoundry Pro features here. Multi-organization tenancy, a
hosted control plane, and similar work stay out of this repository.
