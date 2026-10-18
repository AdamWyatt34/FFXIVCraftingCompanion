using System.ComponentModel.DataAnnotations;

namespace FFXIV.Application.Feedback.DTOs;

/// <summary>Lengths mirror the Feedbacks columns (64 / 5000 / 320 / 2048).</summary>
public sealed class SubmitFeedbackRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Category is required.")]
    [MaxLength(64, ErrorMessage = "Category must be 64 characters or fewer.")]
    public string Category { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Message is required.")]
    [MaxLength(5000, ErrorMessage = "Message must be 5000 characters or less.")]
    public string Message { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email must be a valid address.")]
    [MaxLength(320, ErrorMessage = "Email must be 320 characters or less.")]
    public string? Email { get; set; }

    [MaxLength(2048, ErrorMessage = "PageUrl must be 2048 characters or less.")]
    public string? PageUrl { get; set; }
}

public record FeedbackResult(int Id, DateTimeOffset CreatedAt);
