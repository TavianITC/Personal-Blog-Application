namespace Personal_Blog_Application.ViewModels
{
    public class AvatarRenderViewModel
    {
        public string? AvatarUrl { get; set; }
        public string UserName { get; set; } = string.Empty;

        // Tailwind utility classes the call site picks per layout. SizeClasses
        // controls both the image and the initials-fallback bubble (must include
        // width + height). TextSize only applies to the initials fallback.
        public string SizeClasses { get; set; } = "h-8 w-8";
        public string TextSize { get; set; } = "text-[0.7rem]";
    }
}
