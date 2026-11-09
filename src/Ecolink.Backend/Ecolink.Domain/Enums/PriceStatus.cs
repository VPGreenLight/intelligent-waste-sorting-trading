namespace Ecolink.Domain.Enums;

/// <summary>
/// Trạng thái dòng giá thu mua của Recycler (RecyclerPriceList.status: ACTIVE / INACTIVE).
/// Inactive = Recycler tạm ngưng mua loại phế liệu này (không xóa cứng, theo BR-MTH-07).
/// </summary>
public enum PriceStatus
{
    Active = 1,
    Inactive = 2
}
