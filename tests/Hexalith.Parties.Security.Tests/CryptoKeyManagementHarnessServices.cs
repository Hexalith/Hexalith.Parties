using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;

namespace Hexalith.Parties.Security.Tests;

internal sealed class CryptoKeyManagementHarnessServices
{
    private readonly CapturingLogger<PartyKeyLifecycleService> _lifecycleLogger;

    private readonly CapturingLogger<DecryptionCircuitBreaker> _circuitLogger;

    private readonly CapturingLogger<PartyPayloadProtectionService> _protectionLogger;

    private readonly CapturingLogger<ErasureVerificationService> _verificationLogger;

    public CryptoKeyManagementHarnessServices(
        LocalDevKeyStorageBackend backend,
        IPartyKeyManagementService keyManagementService,
        PartyKeyLifecycleService lifecycleService,
        DecryptionCircuitBreaker circuitBreaker,
        PartyPayloadProtectionService protectionService,
        CapturingLogger<PartyKeyLifecycleService> lifecycleLogger,
        CapturingLogger<DecryptionCircuitBreaker> circuitLogger,
        CapturingLogger<PartyPayloadProtectionService> protectionLogger,
        CapturingLogger<ErasureVerificationService> verificationLogger)
    {
        Backend = backend;
        KeyManagementService = keyManagementService;
        LifecycleService = lifecycleService;
        CircuitBreaker = circuitBreaker;
        ProtectionService = protectionService;
        VerificationLogger = verificationLogger;
        _lifecycleLogger = lifecycleLogger;
        _circuitLogger = circuitLogger;
        _protectionLogger = protectionLogger;
        _verificationLogger = verificationLogger;
    }

    public LocalDevKeyStorageBackend Backend { get; }

    public IPartyKeyManagementService KeyManagementService { get; }

    public PartyKeyLifecycleService LifecycleService { get; }

    public DecryptionCircuitBreaker CircuitBreaker { get; }

    public PartyPayloadProtectionService ProtectionService { get; }

    public CapturingLogger<ErasureVerificationService> VerificationLogger { get; }

    public IEnumerable<string> CapturedMessages =>
        _lifecycleLogger.Messages
            .Concat(_circuitLogger.Messages)
            .Concat(_protectionLogger.Messages)
            .Concat(_verificationLogger.Messages);
}
