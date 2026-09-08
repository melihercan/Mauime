using ReactiveUI.Primitives.Disposables;

namespace DemoApp.ViewModels;

/// <summary>
/// ReactiveUI 24's disposables have <see cref="DisposableBag"/> rather than Rx's
/// <c>CompositeDisposable</c>, and no <c>DisposeWith</c>. This restores the fluent form, so a
/// subscription is still tied to the view model's lifetime on the line that creates it.
/// </summary>
internal static class DisposableExtensions
{
    internal static void AddTo(this IDisposable disposable, DisposableBag bag) => bag.Add(disposable);
}
