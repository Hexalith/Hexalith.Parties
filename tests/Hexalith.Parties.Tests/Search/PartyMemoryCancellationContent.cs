using System.Text;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Cancels after a qualified response has been consumed and its scope disposed.</summary>
internal sealed class PartyMemoryCancellationContent(Action cancel)
    : StringContent("{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}", Encoding.UTF8, "application/json")
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            cancel();
        }
    }
}
