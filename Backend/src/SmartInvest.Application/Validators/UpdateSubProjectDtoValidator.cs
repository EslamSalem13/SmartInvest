using FluentValidation;
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Validators;

public class UpdateSubProjectDtoValidator : AbstractValidator<UpdateSubProjectDto>
{
    public UpdateSubProjectDtoValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(50);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المشروع الفرعي مطلوب")
            .MaximumLength(250);

        RuleFor(x => x.ProjectLevelId)
            .GreaterThan(0).WithMessage("المستوى مطلوب");

        RuleFor(x => x.MarkazId)
            .GreaterThan(0).WithMessage("يجب اختيار المركز");

        RuleFor(x => x.PriorityId)
            .GreaterThan(0).WithMessage("يجب اختيار الأولوية");

        RuleFor(x => x.StatusId)
            .GreaterThan(0).WithMessage("يجب اختيار حالة المشروع");

        RuleFor(x => x.ProjectNature)
            .Must(v => v == "توريدات" || v == "مقاولات").WithMessage("نوع المشروع يجب أن يكون توريدات أو مقاولات");

        RuleFor(x => x.BankFunding)
            .GreaterThanOrEqualTo(0).WithMessage("التمويل البنكي لا يمكن أن يكون سالبًا");

        RuleFor(x => x.SelfFunding)
            .GreaterThanOrEqualTo(0).WithMessage("التمويل الذاتي لا يمكن أن يكون سالبًا");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
            .WithMessage("خط العرض غير صحيح");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
            .WithMessage("خط الطول غير صحيح");
    }
}
