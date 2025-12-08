namespace KSignal.API.Models;

public enum MarketSort
{
    Volume24H = 0,
    TotalVolume = 1,
    OpenDate = 2,
    ClosingSoon = 3,
    ClosingFarFuture = 4,
    YesPrice = 5,
    NoPrice = 6
}

public enum SortDirection
{
    Asc,
    Desc
}
