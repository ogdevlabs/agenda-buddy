using Xunit;

namespace Identity.Tests;

// Prevents test classes that mutate process-level env vars from running concurrently
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection { }
