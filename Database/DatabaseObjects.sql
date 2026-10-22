-- DocuVault database objects: run against the FourierIT database (safe to re-run).
-- See DATABASE-OBJECTS.md for why each object lives in SQL Server.

-- Stored procedure: compliance summary for one institution.
-- Returns whether every document type the institution requested has an
-- approved/verified/compliant document from the target user or department.
CREATE OR ALTER PROCEDURE dbo.GetInstitutionComplianceSummary
    @InstitutionId int
AS
BEGIN
    SET NOCOUNT ON;

    WITH RequiredLines AS
    (
        SELECT
            r.EnquiryRequestId,
            r.InstitutionId,
            i.InstitutionName,
            r.TargetUserId,
            r.TargetDepartmentId,
            rdt.DocumentTypeId,
            dt.TypeName AS DocumentTypeName
        FROM InstitutionEnquiryRequests AS r
        INNER JOIN Institutions AS i ON i.InstitutionId = r.InstitutionId
        INNER JOIN InstitutionRequestedDocumentTypes AS rdt ON rdt.EnquiryRequestId = r.EnquiryRequestId
        INNER JOIN DocumentTypes AS dt ON dt.DocumentTypeId = rdt.DocumentTypeId
        WHERE r.InstitutionId = @InstitutionId
          AND r.Status NOT IN ('Denied', 'Revoked')
    ),
    LineStatus AS
    (
        SELECT
            line.*,
            CASE WHEN EXISTS
            (
                SELECT 1
                FROM Documents AS d
                LEFT JOIN AspNetUsers AS targetUser ON targetUser.Id = d.UserId
                WHERE d.DocumentTypeId = line.DocumentTypeId
                  AND d.CurrentStatus <> 'Deleted'
                  AND d.CurrentStatus IN ('Approved', 'Verified', 'Compliant')
                  AND (d.UserId = line.TargetUserId
                       OR (line.TargetDepartmentId IS NOT NULL
                           AND targetUser.DepartmentId = line.TargetDepartmentId))
            ) THEN 1 ELSE 0 END AS IsSubmittedOrApproved
        FROM RequiredLines AS line
    )
    SELECT
        InstitutionId,
        InstitutionName,
        CASE WHEN SUM(CASE WHEN IsSubmittedOrApproved = 0 THEN 1 ELSE 0 END) = 0
             THEN 'Compliant' ELSE 'Non-Compliant' END AS ComplianceStatus,
        COUNT(DISTINCT EnquiryRequestId) AS RequestCount,
        COUNT(*) AS RequiredDocumentTypeCount,
        SUM(IsSubmittedOrApproved) AS SubmittedOrApprovedDocumentTypeCount,
        SUM(CASE WHEN IsSubmittedOrApproved = 0 THEN 1 ELSE 0 END) AS MissingDocumentTypeCount,
        COALESCE(STRING_AGG(CASE WHEN IsSubmittedOrApproved = 0 THEN DocumentTypeName END, ', '), '') AS MissingDocumentTypes
    FROM LineStatus
    GROUP BY InstitutionId, InstitutionName;
END
GO

-- Trigger: keep Documents.LastModifiedDate current on every update,
-- unless the update already set it explicitly.
CREATE OR ALTER TRIGGER dbo.TR_Documents_SetLastModifiedDate
ON Documents
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(LastModifiedDate) RETURN;

    UPDATE d
    SET LastModifiedDate = SYSUTCDATETIME()
    FROM Documents AS d
    INNER JOIN inserted AS insertedRow ON insertedRow.DocumentId = d.DocumentId;
END
GO
