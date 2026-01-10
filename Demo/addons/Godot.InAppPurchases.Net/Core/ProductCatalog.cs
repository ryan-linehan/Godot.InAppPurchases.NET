using Godot;
using System.Linq;

namespace Godot.InAppPurchases.Core;

/// <summary>
/// Contains all product definitions for the game.
/// This is the main data store edited in the IAP editor dock.
/// </summary>
[GlobalClass]
public partial class ProductCatalog : Resource
{
    /// <summary>
    /// All products defined in this catalog.
    /// </summary>
    [Export]
    public Godot.Collections.Array<Product> Products { get; set; } = new();

    /// <summary>
    /// Gets a product by its ID.
    /// </summary>
    /// <param name="id">The product ID to search for.</param>
    /// <returns>The product if found, null otherwise.</returns>
    public Product? GetProduct(string id)
    {
        return Products.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>
    /// Checks if a product with the given ID exists in the catalog.
    /// </summary>
    public bool HasProduct(string id)
    {
        return Products.Any(p => p.Id == id);
    }

    /// <summary>
    /// Gets a product by its platform-specific product ID.
    /// </summary>
    /// <param name="platformProductId">The platform product ID to search for.</param>
    /// <param name="providerName">The provider name (Steam, StoreKit, GooglePlay).</param>
    /// <returns>The product if found, null otherwise.</returns>
    public Product? GetProductByPlatformId(string platformProductId, string providerName)
    {
        return Products.FirstOrDefault(p => p.GetPlatformProductId(providerName) == platformProductId);
    }

    /// <summary>
    /// Adds a new product to the catalog.
    /// </summary>
    public void AddProduct(Product product)
    {
        Products.Add(product);
    }

    /// <summary>
    /// Removes a product from the catalog by its ID.
    /// </summary>
    /// <returns>True if the product was found and removed.</returns>
    public bool RemoveProduct(string id)
    {
        var product = GetProduct(id);
        if (product != null)
        {
            Products.Remove(product);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a product at the specified index.
    /// </summary>
    public void RemoveProductAt(int index)
    {
        if (index >= 0 && index < Products.Count)
        {
            Products.RemoveAt(index);
        }
    }

    /// <summary>
    /// Moves a product from one index to another.
    /// </summary>
    public void MoveProduct(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Products.Count) return;
        if (toIndex < 0 || toIndex >= Products.Count) return;
        if (fromIndex == toIndex) return;

        var product = Products[fromIndex];
        Products.RemoveAt(fromIndex);

        if (toIndex > fromIndex)
        {
            toIndex--;
        }

        Products.Insert(toIndex, product);
    }

    /// <summary>
    /// Inserts a product at the specified index.
    /// </summary>
    public void InsertProduct(int index, Product product)
    {
        if (index < 0) index = 0;
        if (index > Products.Count) index = Products.Count;
        Products.Insert(index, product);
    }

    /// <summary>
    /// Gets the index of a product by its ID.
    /// </summary>
    /// <returns>The index if found, -1 otherwise.</returns>
    public int GetProductIndex(string id)
    {
        for (int i = 0; i < Products.Count; i++)
        {
            if (Products[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Generates a unique product ID based on a base name.
    /// </summary>
    public string GenerateUniqueId(string baseName = "new_product")
    {
        if (!HasProduct(baseName))
        {
            return baseName;
        }

        int counter = 1;
        while (HasProduct($"{baseName}_{counter}"))
        {
            counter++;
        }
        return $"{baseName}_{counter}";
    }
}
