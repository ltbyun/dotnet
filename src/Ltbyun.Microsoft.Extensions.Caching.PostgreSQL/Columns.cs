namespace Ltbyun.Microsoft.Extensions.Caching.PostgreSQL;

internal static class Columns
{
    public static class Names
    {
        public const string CacheItemId = "Id";
        public const string CacheItemValue = "Value";
        public const string ExpiresAtTime = "ExpiresAtTime";
        public const string SlidingExpirationInSeconds = "SlidingExpirationInSeconds";
        public const string AbsoluteExpiration = "AbsoluteExpiration";
    }
}
