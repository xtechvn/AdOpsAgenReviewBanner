using Microsoft.Extensions.Options;

namespace AdOpsAgenReviewBanner.Tests.Support;

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class, new()
{
    public TestOptionsMonitor(T? value = null) => CurrentValue = value ?? new T();

    public T CurrentValue { get; set; }

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
