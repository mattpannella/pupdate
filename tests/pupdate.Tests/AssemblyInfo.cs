// Disable cross-class parallelism in this assembly. Reasons:
//  1) Integration tests share a single WireMockServer (via HttpStateCollection). xUnit's
//     [Collection] serializes tests *within* a collection but parallelizes across collections,
//     and some test runners (notably VS Code's Test Explorer) parallelize aggressively
//     enough that another collection's class ctor can call _mock.Reset() while a
//     HttpStateful test is mid-flight, wiping its stubs and yielding spurious 404s.
//  2) Several unit tests mutate process-global state (Directory.SetCurrentDirectory,
//     static URL constants on services) that doesn't tolerate cross-class parallelism.
// 124 tests at ~2-4s serial is acceptable; correctness over micro-optimization.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
