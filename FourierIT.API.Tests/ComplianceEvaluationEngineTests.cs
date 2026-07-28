using FourierIT_API.Services;

namespace FourierIT.API.Tests;

public class ComplianceEvaluationEngineTests
{
    [Fact]
    public void CalculateCompliancePercentage_ReturnsExpectedValue()
    {
        var result = ComplianceEvaluationEngine.CalculateCompliancePercentage(8, 2, 1, 1, 1, 1);

        Assert.Equal(33, result);
    }

    [Fact]
    public void ResolveOverallStatus_ReturnsReviewRequired_WhenPendingVerificationExists()
    {
        var status = ComplianceEvaluationEngine.ResolveOverallStatus(10, 0, 0, 0, 2, 8);

        Assert.Equal("Review-Required", status);
    }

    [Fact]
    public void ResolveOverallStatus_ReturnsNonCompliant_WhenMissingOrExpiredExists()
    {
        var status = ComplianceEvaluationEngine.ResolveOverallStatus(5, 0, 2, 1, 0, 0);

        Assert.Equal("Non-Compliant", status);
    }
}
