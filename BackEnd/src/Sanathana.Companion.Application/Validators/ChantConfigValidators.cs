using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.ChantConfigs;

namespace Sanathana.Companion.Application.Validators;

public class CreateChantConfigValidator : AbstractValidator<CreateChantConfigDto>
{
    public CreateChantConfigValidator()
    {
        RuleFor(x => x.ChantId).NotEmpty().WithMessage("Select a chant category.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Chant name is required.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.TimeDescription).MaximumLength(200);
        RuleFor(x => x.AudioFileName).MaximumLength(255);
        // 10 MB decodes from ~14 MB of base64; reject oversized before decoding.
        RuleFor(x => x.AudioBase64).MaximumLength(14_000_000).WithMessage("Audio is too large (max 10 MB).");

        // Length is checked first (and stops the chain) so a huge body is rejected before the
        // sanitizer runs on it. An "empty" rich-text body still arrives as markup ("<p><br></p>").
        RuleFor(x => x.ChantText)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(200_000).WithMessage("Chant text is too long.")
            .Must(t => !string.IsNullOrWhiteSpace(HtmlSanitizer.ToPlainText(t)))
            .WithMessage("Chant text is required.");

        RuleFor(x => x.ToTime)
            .Must((dto, to) => dto.FromTime is null || to is null || dto.FromTime < to)
            .WithMessage("From time must be earlier than To time.");
    }
}

public class UpdateChantConfigValidator : AbstractValidator<UpdateChantConfigDto>
{
    public UpdateChantConfigValidator()
    {
        RuleFor(x => x.ChantId).NotEmpty().WithMessage("Select a chant category.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Chant name is required.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.TimeDescription).MaximumLength(200);
        RuleFor(x => x.AudioFileName).MaximumLength(255);
        RuleFor(x => x.AudioBase64).MaximumLength(14_000_000).WithMessage("Audio is too large (max 10 MB).");

        RuleFor(x => x.ChantText)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(200_000).WithMessage("Chant text is too long.")
            .Must(t => !string.IsNullOrWhiteSpace(HtmlSanitizer.ToPlainText(t)))
            .WithMessage("Chant text is required.");

        RuleFor(x => x.ToTime)
            .Must((dto, to) => dto.FromTime is null || to is null || dto.FromTime < to)
            .WithMessage("From time must be earlier than To time.");
    }
}
