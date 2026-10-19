using System;

namespace SolutionColor
{
    internal static class PackageGuids
    {
        public const string PackageString = "9e6abb40-3450-4778-a6b6-0a966d8709d6";
        public const string CommandSetString = "f90de3a5-88a6-491e-9fb9-eb9cdf3917a4";

        public static readonly Guid CommandSet = new Guid(CommandSetString);
    }

    internal static class PackageIds
    {
        public const int SetSolutionColor = 0x0100;
    }
}
