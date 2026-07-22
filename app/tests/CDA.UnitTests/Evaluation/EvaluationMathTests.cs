using CDA.Domain.Evaluation;

namespace CDA.UnitTests.Evaluation;

public class EvaluationMathTests
{
    [Fact]
    public void The_total_is_weight_times_score_summed()
    {
        var total = EvaluationMath.Total([(Weight: 5, Score: 4), (Weight: 2, Score: 3), (Weight: 0, Score: 5)]);

        Assert.Equal(26, total);
    }

    [Fact]
    public void A_requirement_weighted_zero_contributes_nothing_however_well_it_scores()
    {
        // "This does not matter to me" has to mean it cannot influence the result.
        Assert.Equal(0, EvaluationMath.Total([(Weight: 0, Score: 5)]));
    }

    [Fact]
    public void The_percentage_is_measured_against_a_perfect_answer_under_the_same_weights()
    {
        // Two requirements, weights 5 and 1, scored 5 and 5: the best possible is 30, and a
        // perfect answer therefore scores 100%.
        Assert.Equal(100, EvaluationMath.Percentage([(5, 5), (1, 5)]));

        // Half marks on both.
        Assert.Equal(50, EvaluationMath.Percentage([(5, 0), (5, 5)]));
    }

    [Fact]
    public void The_percentage_is_what_makes_two_alternatives_comparable()
    {
        // Raw totals depend on how many requirements there are; the percentage does not, which
        // is why the comparison shows both.
        var few = EvaluationMath.Percentage([(3, 4)]);
        var many = EvaluationMath.Percentage([(3, 4), (3, 4), (3, 4), (3, 4)]);

        Assert.Equal(few, many);
    }

    [Fact]
    public void With_every_weight_at_zero_there_is_no_percentage_to_report()
    {
        // Nothing matters, so nothing can be scored — reporting 0% would imply a judgement
        // that was never made, and dividing by zero is not an option either.
        Assert.Null(EvaluationMath.Percentage([(0, 5), (0, 3)]));
    }

    [Fact]
    public void An_empty_evaluation_scores_nothing()
    {
        Assert.Equal(0, EvaluationMath.Total([]));
        Assert.Null(EvaluationMath.Percentage([]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void A_weight_outside_the_scale_is_refused(int weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RequirementWeight(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), weight, DateTime.UtcNow));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void A_score_outside_the_scale_is_refused(int score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RequirementScore(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), score, DateTime.UtcNow));
    }

    [Fact]
    public void Changing_a_weight_revalidates_it()
    {
        var weight = new RequirementWeight(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, DateTime.UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(() => weight.ChangeTo(9, DateTime.UtcNow));
        Assert.Equal(3, weight.Weight);
    }
}
