using FluentValidation;
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Validators;

public class CreateMainProjectDtoValidator : AbstractValidator<CreateMainProjectDto>
{
    public CreateMainProjectDtoValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(50);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المشروع الرئيسي مطلوب")
            .MaximumLength(250);

        RuleFor(x => x.SubProgramId)
            .GreaterThan(0).WithMessage("يجب اختيار البرنامج الفرعي");
    }
}
