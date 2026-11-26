using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface ISourceParser
{
    string ParserType { get; }
    Task<List<RawSignal>> FetchSignalsAsync(ScoutSource source, CancellationToken ct = default);
}
