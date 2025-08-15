# ODTE.GoScore.Tests

- **Target**: xUnit test stubs for GoScore decision gating and policy integration.
- **Policy file**: `../../GoScore.policy.json` (edit the path in tests if your checkout differs).

## Contents
- `GoScore.cs` — minimal stubs (`GoInputs`, `GoPolicy`, `GoScorer`, calculators) to allow compilation.
- `GoScoreTests.cs` — score monotonicity & decision-tiers.
- `InputCalculatorTests.cs` — PoE, PoT, Sigmoid sanity.
- `SelectorPolicyTests.cs` — JSON policy load and threshold behavior.
- `LedgerSerializationTests.cs` — ensures all ledger fields exist and serialize.

## Use
1. Add this project to your solution: `dotnet sln add ODTE.GoScore.Tests.csproj`
2. Adjust `GoPolicy.Load` path if needed.
3. Replace stubs with references to your production library when available.
4. Run: `dotnet test`

_Generated: 2025-08-15T05:29:56.856586Z_
