namespace FourierIT_API.DTOs.Audit;

public class AuditLogEntry
{
    public string UserId { get; set; }
    public string InstitutionId { get; set; }
    public string Timestamp { get; set; }
    public AuditEventType ActionType { get; set; }
}

public enum AuditEventType
{
    OTP_SENT,
    OTP_VERIFIED,
    LOGIN_SUCCESS,
    LOGIN_FAILURE,
    LOGOUT,
}
