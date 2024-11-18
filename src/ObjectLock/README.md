# ReconArt.Synchronization.ObjectLock

## Overview

`ReconArt.Synchronization.ObjectLock` is a .NET library that provides a synchronization primitive that limits the number of threads that can access an object-scoped resource concurrently.

## Features

- Object-scoped synchronization using unique identifiers
- Support for both synchronous and asynchronous locking mechanisms
- Configurable concurrency limits for locks
- Compatible with .NET 9, .NET 8, .NET Standard 2.1, and .NET Standard 2.0

## Installation

To install the `ReconArt.Synchronization.ObjectLock` package, use the NuGet Package Manager or the Package Manager Console with the following command:

```powershell
Install-Package ReconArt.Synchronization.ObjectLock
```

## Usage

### Creating an Object Lock

You can create an `ObjectLock` instance by utilizing the overloads found in `ObjectLockFactory`:

```csharp
using ReconArt.Synchronization;

// Using an object, that implements IObjectLockIdentifier
var obj = new MyClass();
using var objectLock = ObjectLockFactory.From(obj);

// Using an object, that does not implement IObjectLockIdentifier
using var simpleObjectLock = ObjectLockFactory.From(new object(), "my unique identifier");

// Using a type and a unique identifier
using var typeLock = ObjectLockFactory.From<MyClass>("my unique identifier");
```

**Note:** To ensure effective locking, make sure the unique identifier is unique within the scope of the object and will not be mutated throughout the lifetime of the lock. The underlying implementation will use a copy of this identifier - any changes made to it, will not be reflected.

### Waiting for a Lock

You can wait for a lock synchronously or asynchronously:

```csharp
// Synchronous wait
objectLock.Wait();

// Asynchronous wait
await objectLock.WaitAsync();
```

You can also specify a timeout and a cancellation token:

```csharp
try
{

    // Synchronous wait with milliseconds timeout
    bool entered = objectLock.Wait(10 * 1000);

    // Synchronous wait with timeout
    bool entered = objectLock.Wait(TimeSpan.FromSeconds(10));

    // Asynchronous wait with timeout and cancellation token.
    bool enteredAsync = await objectLock.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
}
catch (OperationCanceledException)
{
    // Handle cancellation
}
```

### Releasing a Lock

To release a lock, call the `Release` method:

```csharp
objectLock.Release();
```

### Disposing an Object Lock

To release all resources used by the `ObjectLock`, call the `Dispose` method:

```csharp
objectLock.Dispose();
```

## Background Disposal

To avoid contention in performance-critical paths such as the creation and disposal of an `ObjectLock`, the library delays disposing of the underlying resources until one of the following is true:
- 24 hours have passed since the last cleanup operation
- There are more than 50,000 locks in the global dictionary of locks

The above conditions are checked every 30 seconds in a background thread, and are non-configurable.

## Contributing

If you'd like to contribute to the project, please reach out to the [ReconArt/synchronization-primitives](https://github.com/orgs/ReconArt/teams/synchronization-primitives) team.

## Support

If you encounter any issues or require assistance, please file an issue in the [GitHub Issues](https://github.com/ReconArt/synchronization-primitives/issues) section of the repository.

## Authors and Acknowledgments

Developed by [ReconArt, Inc.](https://reconart.com/).
