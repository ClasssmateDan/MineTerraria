using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DimensionalTerraria.Core;

public readonly record struct CatalogEntry(int Type, string Name, string InternalName);

// No textures or Item instances are needed to search or page through the catalog.
public sealed class ItemCatalog
{
    public const int PageSize = 30;
    private readonly CatalogEntry[] entries;
    private CatalogEntry[] matches;
    public int Page { get; private set; }
    public int Count => matches.Length;
    public int Pages => Math.Max(1, (Count + PageSize - 1) / PageSize);
    public int VisibleCount => Math.Min(PageSize, Count - Page * PageSize);

    public ItemCatalog(IEnumerable<CatalogEntry> entries)
    {
        this.entries = entries.ToArray();
        matches = this.entries;
    }

    public void Search(string query)
    {
        string[] terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        matches = terms.Length == 0 ? entries : entries.Where(entry => terms.All(term =>
            entry.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            entry.InternalName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            entry.Type.ToString(CultureInfo.InvariantCulture).Equals(term, StringComparison.Ordinal))).ToArray();
        Page = 0;
    }

    public void MovePage(int offset) => Page = (int)Math.Clamp((long)Page + offset, 0, Pages - 1);

    public bool TryVisible(int slot, out CatalogEntry entry)
    {
        if (slot < 0 || slot >= VisibleCount) { entry = default; return false; }
        entry = matches[Page * PageSize + slot];
        return true;
    }
}
