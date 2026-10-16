namespace Regression.Tests;

// Loc.Mode is shared by the plugin. Tests that change it must not run at the
// same time: otherwise a Russian assertion can observe a German neighbour.
[CollectionDefinition("Language", DisableParallelization = true)]
public sealed class LanguageCollection;
