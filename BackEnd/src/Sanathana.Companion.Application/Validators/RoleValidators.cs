using FluentValidation;
using Sanathana.Companion.Application.DTOs.Roles;

namespace Sanathana.Companion.Application.Validators;

public class CreateRoleValidator : AbstractValidator<CreateRoleDto>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.RoleName).NotEmpty().WithMessage("Role name is required.").MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public class UpdateRoleValidator : AbstractValidator<UpdateRoleDto>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.RoleName).NotEmpty().WithMessage("Role name is required.").MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(250);
    }
}
