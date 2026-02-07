namespace ClassifiedAds.Infrastructure.Storages.Firebase;

public class FirebaseOptions
{
    /// <summary>
    /// Path to the Firebase service account JSON key file.
    /// </summary>
    public string CredentialFilePath { get; set; }

    /// <summary>
    /// Firebase Storage bucket name (e.g., "your-project.appspot.com").
    /// </summary>
    public string Bucket { get; set; }

    /// <summary>
    /// Optional prefix/folder path in the bucket.
    /// </summary>
    public string Path { get; set; }
}
