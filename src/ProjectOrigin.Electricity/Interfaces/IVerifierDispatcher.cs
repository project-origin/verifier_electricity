using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProjectOrigin.Electricity.Interfaces;

public interface IVerifierDispatcher
{
    Task<VerificationResult> Verify(Registry.V1.Transaction transaction, IEnumerable<Registry.V1.Transaction> stream);
}
