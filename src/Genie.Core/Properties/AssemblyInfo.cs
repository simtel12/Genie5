using System.Runtime.CompilerServices;

// Exposes internal types (e.g. ScriptExpression) to the unit-test assembly so
// tests can drive the script-language evaluator directly. Test-only; no effect
// on shipped behavior.
[assembly: InternalsVisibleTo("Genie.Core.Tests")]
