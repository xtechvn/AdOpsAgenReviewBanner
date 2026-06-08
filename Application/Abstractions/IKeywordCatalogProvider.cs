using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

public interface IKeywordCatalogProvider
{
    Task<KeywordCatalog> GetCatalogAsync(CancellationToken cancellationToken = default);
}
