using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.Subscription.Models;

public class CreateUpdatePlanModel
{
    [Required(ErrorMessage = "Tên gói cước là bắt buộc.")]
    [StringLength(50, ErrorMessage = "Tên gói cước không được vượt quá 50 ký tự.")]
    public string Name { get; set; }

    [Required(ErrorMessage = "Tên hiển thị là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên hiển thị không được vượt quá 100 ký tự.")]
    public string DisplayName { get; set; }

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
    public string Description { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Giá theo tháng phải lớn hơn hoặc bằng 0.")]
    public decimal? PriceMonthly { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Giá theo năm phải lớn hơn hoặc bằng 0.")]
    public decimal? PriceYearly { get; set; }

    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "Mã tiền tệ phải có đúng 3 ký tự (ví dụ: USD, VND).")]
    public string Currency { get; set; } = "USD";

    public bool IsActive { get; set; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "Thứ tự hiển thị phải lớn hơn hoặc bằng 0.")]
    public int SortOrder { get; set; }

    public List<PlanLimitModel> Limits { get; set; } = new List<PlanLimitModel>();
}
