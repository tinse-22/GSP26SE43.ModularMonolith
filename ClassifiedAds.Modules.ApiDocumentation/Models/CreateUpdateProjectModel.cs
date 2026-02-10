using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class CreateUpdateProjectModel
{
    [Required(ErrorMessage = "Tên project là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tên project không được vượt quá 200 ký tự.")]
    public string Name { get; set; }

    [StringLength(2000, ErrorMessage = "Mô tả không được vượt quá 2000 ký tự.")]
    public string Description { get; set; }

    [StringLength(500, ErrorMessage = "URL cơ sở không được vượt quá 500 ký tự.")]
    public string BaseUrl { get; set; }
}
