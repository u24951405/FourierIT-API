# SQL Server database objects

This project adds exactly two database-layer objects for the complexity requirements:

- `dbo.GetInstitutionComplianceSummary`: a stored procedure that joins institutions, enquiry requests, requested document lines, target users/departments, document types, and documents. Its conditional aggregation returns request counts, required document-type counts, submitted/approved counts, missing counts, and missing type names for an institution. This belongs in SQL Server because the rollup is set-based across related tables and can be produced in one round trip for every application caller.
- `dbo.TR_Documents_SetLastModifiedDate`: an `AFTER UPDATE` trigger on `Documents` that sets `LastModifiedDate` to `SYSUTCDATETIME()`. This belongs in the database because modification metadata is an integrity rule that must hold for C#, migrations, direct SQL, and any future writer.

The EF migration `AddInstitutionComplianceProcedureAndDocumentLastModifiedTrigger` creates both objects and removes them on rollback. The API endpoint `GET /api/compliance/institutions/{institutionId}/summary` invokes the procedure through EF Core `Database.SqlQueryRaw<T>()`.

No third procedure or trigger was added: the two objects above are the strongest defensible database-layer candidates in this schema.
