namespace PurePrep.Application;

/// <summary>
/// Provides a stable, anonymous device identifier (a GUID generated on first install and persisted
/// in secure storage). The backend tracks Smart Credits against this id — no user accounts required.
/// </summary>
public interface IDeviceIdentity
{
    Task<Guid> GetDeviceIdAsync(CancellationToken cancellationToken = default);
}
