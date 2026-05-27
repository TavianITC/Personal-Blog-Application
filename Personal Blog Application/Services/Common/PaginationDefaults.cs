namespace Personal_Blog_Application.Services.Common
{
    // Shared pagination defaults so services don't cross-reference each other
    // just to grab a page-size constant. Override at the call site if a feature
    // needs a different page size.
    public static class PaginationDefaults
    {
        public const int PageSize = 10;
    }
}
