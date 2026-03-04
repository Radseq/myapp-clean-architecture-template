namespace MyApp.BuildingBlocks.Domain.Common;

public enum ErrorKind
{
    Unknown = 0,

    // 4xx
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,

    // 5xx (zależności / upstream)
    DependencyFailure = 6,
    DependencyTimeout = 7,

    // 500
    Unexpected = 8
}