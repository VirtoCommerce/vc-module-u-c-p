using GraphQL;
using VirtoCommerce.Xapi.Core.Infrastructure;
using XCartDataAssemblyMarker = VirtoCommerce.XCart.Data.DataAssemblyMarker;
using XCatalogDataAssemblyMarker = VirtoCommerce.XCatalog.Data.DataAssemblyMarker;
using XOrderDataAssemblyMarker = VirtoCommerce.XOrder.Data.DataAssemblyMarker;

namespace VirtoCommerce.UCP.Web.Services;

public sealed class XApiDocumentExecuters
{
    public XApiDocumentExecuters(
        IDocumentExecuter<ScopedSchemaFactory<XCatalogDataAssemblyMarker>> catalog,
        IDocumentExecuter<ScopedSchemaFactory<XCartDataAssemblyMarker>> cart,
        IDocumentExecuter<ScopedSchemaFactory<XOrderDataAssemblyMarker>> order)
    {
        Catalog = catalog;
        Cart = cart;
        Order = order;
    }

    public IDocumentExecuter<ScopedSchemaFactory<XCatalogDataAssemblyMarker>> Catalog { get; }
    public IDocumentExecuter<ScopedSchemaFactory<XCartDataAssemblyMarker>> Cart { get; }
    public IDocumentExecuter<ScopedSchemaFactory<XOrderDataAssemblyMarker>> Order { get; }
}
