namespace PushpakClasses.Application.Common.Interfaces;

public interface IPdfCertificateService
{
    Task<(byte[] FileBytes, string FileName)> GenerateCertificateAsync(Guid attemptId, CancellationToken cancellationToken = default);
}
