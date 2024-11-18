using System;

namespace ReconArt.Synchronization
{
    /// <summary>
    /// Thrown when a <see cref="UnitDistributorSlim"/> is asked to return more units than it has allocated.
    /// </summary>
    public class UnitOverflowException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnitOverflowException"/> class.
        /// </summary>
        public UnitOverflowException() : base($"Returning the specified units to the {nameof(UnitDistributorSlim)} would cause it to exceed its maximum allocated supply.")
        {
        }
    }
}
