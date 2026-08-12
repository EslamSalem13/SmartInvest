using SmartInvest.Application.DTOs;
using SmartInvest.Application.Services;

namespace SmartInvest.Tests;

public class ProjectCompletionPolicyTests
{
    [Fact]
    public void Prevents_completion_without_execution_stages()
    {
        var result = Evaluate(stages: [Final(completed: true)]);
        Assert.False(result.CanCompleteProject);
        Assert.False(result.HasExecutionStages);
    }

    [Fact]
    public void Prevents_completion_with_incomplete_stage()
    {
        var result = Evaluate(stages: [Actual(100, completed: false), Final(completed: true)]);
        Assert.False(result.CanCompleteProject);
        Assert.False(result.AllStagesCompleted);
    }

    [Fact]
    public void Prevents_completion_at_99_percent()
    {
        var result = Evaluate(stages: [Actual(99), Final()]);
        Assert.False(result.CanCompleteProject);
        Assert.Equal(99m, result.PhysicalProgressTotal);
    }

    [Fact]
    public void Allows_completion_at_100_percent()
    {
        var result = Evaluate(stages: [Actual(100), Final()]);
        Assert.True(result.CanCompleteProject);
    }

    [Fact]
    public void Prevents_spending_below_contract_value()
    {
        var result = Evaluate(stageSelf: 999m, stages: [Actual(100), Final()]);
        Assert.False(result.CanCompleteProject);
    }

    [Fact]
    public void Allows_spending_equal_to_contract_value()
    {
        var result = Evaluate(stageSelf: 1000m, stages: [Actual(100), Final()]);
        Assert.True(result.CanCompleteProject);
    }

    [Fact]
    public void Allows_spending_equal_to_overrun_ceiling()
    {
        var result = Evaluate(stageSelf: 1200m, overrun: 20m, stages: [Actual(100), Final()]);
        Assert.True(result.CanCompleteProject);
        Assert.Equal(1200m, result.MaximumAllowed);
    }

    [Fact]
    public void Prevents_spending_above_overrun_ceiling()
    {
        var result = Evaluate(stageSelf: 1200.01m, overrun: 20m, stages: [Actual(100), Final()]);
        Assert.False(result.CanCompleteProject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    public void Prevents_missing_or_zero_contract_value(string? contractValueText)
    {
        var contractValue = contractValueText == null ? (decimal?)null : decimal.Parse(contractValueText);
        var result = Evaluate(contractValue: contractValue, stages: [Actual(100), Final()]);
        Assert.False(result.CanCompleteProject);
        Assert.Null(result.MinimumRequired);
    }

    [Fact]
    public void Counts_advance_payment_once()
    {
        var result = Evaluate(stageSelf: 800m, advanceDone: true, advanceSelf: 200m, stages: [Actual(100), Final()]);
        Assert.Equal(1000m, result.TotalSpent);
        Assert.True(result.CanCompleteProject);
    }

    private static ProjectCompletionEligibilityDto Evaluate(
        decimal? contractValue = 1000m,
        decimal stageSelf = 1000m,
        decimal stageBank = 0m,
        decimal overrun = 0m,
        bool advanceDone = false,
        decimal advanceSelf = 0m,
        IReadOnlyCollection<ExecutionStageCompletionFact>? stages = null) =>
        ProjectCompletionPolicy.Evaluate(new ProjectCompletionFacts(
            false, true, contractValue, overrun, stageSelf, stageBank,
            advanceDone, advanceSelf, 0m,
            stages ?? [Actual(100), Final()]));

    private static ExecutionStageCompletionFact Actual(decimal progress, bool completed = true) => new(false, completed, progress);
    private static ExecutionStageCompletionFact Final(bool completed = true) => new(true, completed, 0m);
}
