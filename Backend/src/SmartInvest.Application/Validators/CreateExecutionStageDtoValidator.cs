using FluentValidation;
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Validators;

public class CreateExecutionStageDtoValidator : AbstractValidator<CreateExecutionStageDto>
{
    public CreateExecutionStageDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المرحلة مطلوب")
            .MaximumLength(250);

        RuleFor(x => x.Deadline)
            .NotEmpty().WithMessage("الموعد النهائي للمرحلة مطلوب");

        RuleFor(x => x.SelfFundingSpent)
            .GreaterThanOrEqualTo(0).WithMessage("المصروف الذاتي لا يمكن أن يكون سالبًا");

        RuleFor(x => x.BankFundingSpent)
            .GreaterThanOrEqualTo(0).WithMessage("المصروف البنكي لا يمكن أن يكون سالبًا");

        RuleFor(x => x.PhysicalProgressPercent)
            .InclusiveBetween(0, 100).WithMessage("نسبة التنفيذ العيني يجب أن تكون بين 0 و100");

        RuleFor(x => x.SelfFundingProofFile)
            .NotNull().WithMessage("إثبات الصرف الذاتي مطلوب عند تسجيل مبلغ ذاتي")
            .When(x => x.SelfFundingSpent > 0);

        RuleFor(x => x.BankFundingProofFile)
            .NotNull().WithMessage("إثبات الصرف البنكي مطلوب عند تسجيل مبلغ بنكي")
            .When(x => x.BankFundingSpent > 0);

        RuleFor(x => x.PhysicalProgressProofFile)
            .NotNull().WithMessage("إثبات التنفيذ العيني مطلوب عند تسجيل نسبة تنفيذ")
            .When(x => x.PhysicalProgressPercent > 0);
    }
}
