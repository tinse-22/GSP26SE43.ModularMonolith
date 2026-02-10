using ClassifiedAds.Contracts.Storage.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.Storage.Services;

public interface IStorageFileGatewayService
{
    Task<StorageUploadedFileDTO> UploadAsync(StorageUploadFileRequest request, CancellationToken cancellationToken = default);
}
