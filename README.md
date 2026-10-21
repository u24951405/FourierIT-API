# FourierIT API database objects

The migration `AddInstitutionComplianceProcedureAndDocumentLastModifiedTrigger` adds exactly two SQL Server objects. `dbo.GetInstitutionComplianceSummary` performs a set-based rollup across institutions, requests, requested document lines, target users/departments, document types, and submitted document statuses; it belongs in SQL Server because related-table aggregation is most consistent and efficient in one round trip. `dbo.TR_Documents_SetLastModifiedDate` maintains `Documents.LastModifiedDate` after updates; it belongs in the database because the integrity rule applies to every writer, including C#, migrations, and direct SQL. The endpoint `GET /api/compliance/institutions/{institutionId}/summary` invokes the procedure through EF Core `FromSqlRaw`. No third procedure or trigger was added because no other candidate was as strong or necessary.

## Local secrets

Passwords are not kept in tracked files. Each developer sets them once with User Secrets, from this folder:

```powershell
dotnet user-secrets set "SuperAdmin:Password" "<a strong password>"   # the seeded Super Admin account
dotnet user-secrets set "EmailSettings:Password" "<Gmail app password>" # outgoing email
```

On startup the Super Admin account is created with that password, or reset to it if it changed. Without it, the account is not created and a warning is logged.

## Before a release

Work through [SMOKE_TEST.md](SMOKE_TEST.md): `tools/smoke-test.ps1` signs in as every role and checks their pages load, and the checklist covers what needs a person clicking through.

