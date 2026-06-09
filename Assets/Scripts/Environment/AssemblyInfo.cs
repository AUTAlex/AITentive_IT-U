using System.Runtime.CompilerServices;

// This grants access to your Test assembly
[assembly: InternalsVisibleTo("EnvironmentTests")]

// Just in case Unity appends .Editor to the assembly name
[assembly: InternalsVisibleTo("EnvironmentTests.Editor")]