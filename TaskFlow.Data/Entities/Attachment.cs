
namespace TaskFlow.Data.Entities;

public class Attachment
{
    // Primary Key
    public Guid Id { get; set; } = Guid.NewGuid();

    // Related Task
    public Guid TaskId { get; set; }

    // Optional Related Comment
    public Guid? TaskCommentId { get; set; }

    // User Who Uploaded the File
    public string UploadedByUserId { get; set; } = string.Empty;

    // File Information
    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    // Upload Information
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public Task Task { get; set; } = null!;

    public TaskComment? TaskComment { get; set; }

    public ApplicationUser UploadedByUser { get; set; } = null!;
}
