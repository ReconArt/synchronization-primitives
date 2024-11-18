# ReconArt.Synchronization.UnitDistributorSlim

## Overview

`ReconArt.Synchronization.UnitDistributorSlim` is a .NET library that allows multi-threaded access to a shared resource with a limited supply by distributing that supply fairly. It supports priority-based distribution and is optimized for working with 6 priorities (from 0 to 5).

## Features

- Fair distribution of limited resources
- Support for priority-based distribution
- Optimized for 6 priorities (0 to 5)
- Compatible with .NET 9 and .NET 8

## Installation

To install the `ReconArt.Synchronization.UnitDistributorSlim` package, use the NuGet Package Manager or the Package Manager Console with the following command:

```powershell
Install-Package ReconArt.Synchronization.UnitDistributorSlim
```

## Usage

### Creating a Unit Distributor

You can create a `UnitDistributorSlim` instance by specifying the total supply:

```csharp
using ReconArt.Synchronization;

var unitDistributor = new UnitDistributorSlim(100); // 100 units available
```

### Taking Units

You can attempt to take units asynchronously, with optional priority and timeout:

```csharp
// Asynchronous take with priority
await unitDistributor.TryTakeAsync(10, 2); // 10 units with priority 2

// Asynchronous take with priority and timeout
bool success = await unitDistributor.TryTakeAsync(10, 2, TimeSpan.FromSeconds(10));
```

### Returning Units

To return units to the distributor, call the `Return` method:

```csharp
unitDistributor.Return(10); // Return 10 units
```

### Handling Unit Overflow

If you attempt to return more units than the distributor's maximum supply, a `UnitOverflowException` will be thrown:

```csharp
try
{
    unitDistributor.Return(200); // Attempt to return 200 units
}
catch (UnitOverflowException ex)
{
    Console.WriteLine(ex.Message);
}
```

## Contributing

If you'd like to contribute to the project, please reach out to the [ReconArt/synchronization-primitives](https://github.com/orgs/ReconArt/teams/synchronization-primitives) team.

## Support

If you encounter any issues or require assistance, please file an issue in the [GitHub Issues](https://github.com/ReconArt/synchronization-primitives/issues) section of the repository.

## Authors and Acknowledgments

Developed by [ReconArt, Inc.](https://reconart.com/).
