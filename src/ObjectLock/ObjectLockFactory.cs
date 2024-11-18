using System;
using System.Diagnostics.CodeAnalysis;

namespace ReconArt.Synchronization
{
    /// <summary>
    /// Factory for creating <see cref="ObjectLock"/> instances.
    /// </summary>
    public static class ObjectLockFactory
    {
        /// <summary>
        /// Creates a new <see cref="ObjectLock"/> instance for the specified object.
        /// </summary>
        /// <typeparam name="TSource">
        /// Type of the object.
        /// </typeparam>
        /// <param name="object">
        /// Object from which to use the unique identifier for locking. This should not be mutable or modified throughout the lifetime of the lock.
        /// </param>
        /// <returns>
        /// A new <see cref="ObjectLock"/> instance.
        /// </returns>
        public static ObjectLock From<TSource>([DisallowNull] TSource @object) where
            TSource : IObjectLockIdentifier
        {
            return new ObjectLock(typeof(TSource), @object.GetUniqueLockIdentifier());
        }

        /// <summary>
        /// Creates a new <see cref="ObjectLock"/> instance for the specified object.
        /// </summary>
        /// <typeparam name="TSource">
        /// Type of the object.
        /// </typeparam>
        /// <param name="object">
        /// Object from which the lock type will be derived. Used in conjunction with the unique identifier to create a unique lock.
        /// </param>
        /// <param name="resourceIdentifier">
        /// Unique identifier for the resource being locked.
        /// </param>
        /// <returns>
        /// A new <see cref="ObjectLock"/> instance.
        /// </returns>
        public static ObjectLock From<TSource>([DisallowNull] TSource @object, [DisallowNull] string resourceIdentifier)
        {
            return new ObjectLock(typeof(TSource), resourceIdentifier);
        }

        /// <summary>
        /// Creates a new <see cref="ObjectLock"/> instance for the specified object.
        /// </summary>
        /// <param name="resourceIdentifier">
        /// Unique identifier for the resource being locked.
        /// </param>
        /// <returns>
        /// A new <see cref="ObjectLock"/> instance.
        /// </returns>
        public static ObjectLock From<TSource>([DisallowNull] string resourceIdentifier)
        {
            return new ObjectLock(typeof(TSource), resourceIdentifier);
        }
    }
}
