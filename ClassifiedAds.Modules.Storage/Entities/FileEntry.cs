using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Storage.Entities;

/// <summary>
/// File storage entity for API specs, reports, exports.
/// </summary>
public class FileEntry : Entity<Guid>, IAggregateRoot
{
    // === Legacy properties (kept for backward compatibility) ===
    public string Name { get; set; }

    public string Description { get; set; }

    public long Size { get; set; }

    public DateTimeOffset UploadedTime { get; set; }

    public string FileName { get; set; }

    public string FileLocation { get; set; }

    public bool Encrypted { get; set; }

    public string EncryptionKey { get; set; }

    public string EncryptionIV { get; set; }

    public bool Archived { get; set; }

    public DateTimeOffset? ArchivedDate { get; set; }

    public bool Deleted { get; set; }

    public DateTimeOffset? DeletedDate { get; set; }

    // === New properties from ERD ===

    /// <summary>
    /// User who uploaded this file.
    /// </summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// MIME type (e.g., application/json, application/pdf).
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// File size in bytes (alias for Size).
    /// </summary>
    public long FileSize
    {
        get => Size;
        set => Size = value;
    }

    /// <summary>
    /// Relative path to file on disk (alias for FileLocation).
    /// </summary>
    public string StoragePath
    {
        get => FileLocation;
        set => FileLocation = value;
    }

    /// <summary>
    /// File category: ApiSpec, Report, Export, Attachment.
    /// </summary>
    public FileCategory FileCategory { get; set; }

    /// <summary>
    /// Soft delete flag (alias for Deleted).
    /// </summary>
    public bool IsDeleted
    {
        get => Deleted;
        set => Deleted = value;
    }

    /// <summary>
    /// When file was soft deleted (alias for DeletedDate).
    /// </summary>
    public DateTimeOffset? DeletedAt
    {
        get => DeletedDate;
        set => DeletedDate = value;
    }

    /// <summary>
    /// Optional expiration time for auto-deletion (temp files).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class DeletedFileEntry : Entity<Guid>, IAggregateRoot
{
    public Guid FileEntryId { get; set; }
}

/// <summary>
/// File category for organization.
/// </summary>
public enum FileCategory
{
    /// <summary>
    /// OpenAPI/Postman/Swagger specification files.
    /// </summary>
    ApiSpec = 0,

    /// <summary>
    /// Generated PDF/CSV/HTML reports.
    /// </summary>
    Report = 1,

    /// <summary>
    /// Exported test results.
    /// </summary>
    Export = 2,

    /// <summary>
    /// General user attachments.
    /// </summary>
    Attachment = 3
}
