using System.ComponentModel.DataAnnotations;

namespace Personal_Blog_Application.ViewModels
{
    public class AvatarUploadViewModel
    {
        [Required(ErrorMessage = "Please choose an image.")]
        public IFormFile? File { get; set; }
    }
}
