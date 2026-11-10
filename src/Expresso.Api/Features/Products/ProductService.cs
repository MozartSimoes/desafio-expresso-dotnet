using Expresso.Api.Data;
using Expresso.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Expresso.Api.Features.Products;

public class ProductService(ExpressoDbContext db)
{
    public async Task<IReadOnlyList<ProductResponse>> ListAsync(
        ProductCategory? category, bool? available, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (category is not null)
            query = query.Where(p => p.Category == category);
        if (available is not null)
            query = query.Where(p => p.Available == available);

        var products = await query.ToListAsync(ct);
        return products.OrderForMenu().Select(ProductResponse.From).ToList();
    }

    public async Task<ProductResponse> GetAsync(int id, CancellationToken ct) =>
        ProductResponse.From(await FindAsync(id, ct));

    public async Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken ct)
    {
        await EnsureUniqueAsync(request, currentId: null, ct);

        var product = new Product(request.Name, request.Category!.Value, request.Size, request.Price, request.Available);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return ProductResponse.From(product);
    }

    public async Task<ProductResponse> UpdateAsync(int id, ProductRequest request, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        await EnsureUniqueAsync(request, currentId: id, ct);

        product.Update(request.Name, request.Category!.Value, request.Size, request.Price, request.Available);
        await db.SaveChangesAsync(ct);
        return ProductResponse.From(product);
    }

    private async Task<Product> FindAsync(int id, CancellationToken ct) =>
        await db.Products.FindAsync([id], ct) ?? throw new NotFoundException("Produto", id);

    // Friendly 409 for the common case; the unique index still guards against concurrent inserts.
    private async Task EnsureUniqueAsync(ProductRequest request, int? currentId, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var exists = await db.Products.AnyAsync(
            p => p.Name == name && p.Size == request.Size && p.Id != currentId, ct);
        if (exists)
            throw new ConflictException($"Já existe o produto '{name}' com esse tamanho.");
    }
}
