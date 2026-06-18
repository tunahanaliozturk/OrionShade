namespace Moongazing.OrionShade.Tests;

using Xunit;

/// <summary>
/// Serial xUnit collection for every test class that touches the process-global
/// <c>Moongazing.OrionShade</c> meter. <see cref="ShadeDiagnostics"/> publishes a single
/// meter named by a shared constant, so a <see cref="System.Diagnostics.Metrics.MeterListener"/>
/// in one class would otherwise observe measurements emitted by other classes running in parallel.
/// Disabling parallelization across these classes keeps meter-based assertions deterministic; the
/// listener also filters by instrument identity as a second line of defence.
/// </summary>
[CollectionDefinition(nameof(MeterSerial), DisableParallelization = true)]
public sealed class MeterSerial
{
}
