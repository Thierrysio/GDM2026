namespace GDM2026.Models;

public class OrderProductItem
{
    public OrderProductItem(string name, string? imageUrl)
    {
        Name = name;
        ImageUrl = imageUrl;
    }

    public string Name { get; }

    public string? ImageUrl { get; }
}
