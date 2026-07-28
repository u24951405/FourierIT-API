namespace FourierIT_API.Services
{
    public static class ComplianceEvaluationEngine
    {
        public static int CalculateCompliancePercentage(int totalRequired, int compliant, int missing, int expired, int rejected, int pending)
        {
            if (totalRequired <= 0)
                return 0;

            var denominator = Math.Max(1, totalRequired);
            var validCount = Math.Max(0, compliant);
            var invalidCount = Math.Max(0, missing + expired + rejected + pending);
            var totalEvaluated = validCount + invalidCount;

            if (totalEvaluated <= 0)
                return 0;

            return (int)Math.Round((double)validCount / totalEvaluated * 100);
        }

        public static string ResolveOverallStatus(int totalRequired, int missing, int expired, int rejected, int pending, int valid)
        {
            if (totalRequired <= 0)
                return "Pending";

            if (missing > 0 || expired > 0 || rejected > 0)
                return "Non-Compliant";

            if (pending > 0)
                return "Review-Required";

            return valid >= totalRequired ? "Compliant" : "Partial";
        }
    }
}
