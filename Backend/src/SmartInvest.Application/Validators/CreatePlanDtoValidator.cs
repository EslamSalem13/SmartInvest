using FluentValidation;
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Validators;

public class CreatePlanDtoValidator : AbstractValidator<CreatePlanDto>
{
    public CreatePlanDtoValidator()
    {
        RuleFor(x => x.PlanName).NotEmpty().WithMessage("اسم الخطة مطلوب").MaximumLength(200);
        RuleFor(x => x.FinancialYearId).GreaterThan(0).WithMessage("يجب اختيار السنة المالية");
    }
}

public class UpdatePlanDtoValidator : AbstractValidator<UpdatePlanDto>
{
    public UpdatePlanDtoValidator()
    {
        RuleFor(x => x.PlanName).NotEmpty().WithMessage("اسم الخطة مطلوب").MaximumLength(200);
    }
}
