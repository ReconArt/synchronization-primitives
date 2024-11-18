namespace ReconArt.Synchronization
{
    /// <summary>
    /// Interface for objects that can provide a unique identifier for locking.
    /// </summary>
    public interface IObjectLockIdentifier
    {
        /// <summary>
        /// Gets a unique identifier for the object.
        /// </summary>
        /// <remarks>
        /// Implementations should ensure that this identifier is unique for the object and will not change change throughout the lifetime of the lock.
        /// </remarks>
        /// <returns></returns>
        string GetUniqueLockIdentifier();
    }
}
