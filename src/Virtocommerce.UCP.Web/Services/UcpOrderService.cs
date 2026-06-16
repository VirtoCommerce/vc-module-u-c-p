using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.OrdersModule.Core.Services;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Services;

public class UcpOrderService : IUcpOrderService
{
    private const int RecentOrderLookupLimit = 50;
    private static readonly string OrderResponseGroup = CustomerOrderResponseGroup.Full.ToString();

    private readonly ICustomerOrderService _customerOrderService;
    private readonly ICustomerOrderSearchService _customerOrderSearchService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UcpOptions _options;

    public UcpOrderService(
        ICustomerOrderService customerOrderService,
        ICustomerOrderSearchService customerOrderSearchService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<UcpOptions> options)
    {
        _customerOrderService = customerOrderService;
        _customerOrderSearchService = customerOrderSearchService;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public virtual async Task<UcpOrderResponse> TrackOrderAsync(UcpOrderTrackingRequest request, CancellationToken cancellationToken = default)
    {
        request ??= new UcpOrderTrackingRequest();
        var orderRequest = NormalizeRequest(request);

        CustomerOrder orderModel;
        if (!string.IsNullOrWhiteSpace(orderRequest.CartId))
        {
            orderModel = await FindOrderByCartIdAsync(orderRequest);
        }
        else
        {
            orderModel = await GetOrderByIdOrNumberAsync(orderRequest);
        }

        if (orderModel == null)
        {
            var lookup = FirstNotEmpty(orderRequest.OrderId, orderRequest.OrderNumber, orderRequest.CartId);
            throw CreateException(ModuleConstants.ErrorCodes.OrderNotFound, $"Order '{lookup}' was not found.", StatusCodes.Status404NotFound);
        }

        var order = ReadOrder(orderModel);
        return new UcpOrderResponse
        {
            Ucp = CreateMetadata("success", "dev.ucp.shopping.order.track"),
            Order = order,
            Messages = order.Messages,
        };
    }

    protected virtual OrderExecutionRequest NormalizeRequest(UcpOrderTrackingRequest request)
    {
        var orderId = FirstNotEmpty(request.OrderId, request.OrderNumber);
        if (string.IsNullOrWhiteSpace(orderId) && string.IsNullOrWhiteSpace(request.CartId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "order_id, order_number, or cart_id is required.");
        }

        return new OrderExecutionRequest
        {
            OrderId = request.OrderId,
            OrderNumber = request.OrderNumber,
            CartId = request.CartId,
            CultureName = FirstNotEmpty(request.Context?.Language, _options.DefaultCultureName),
            UserId = FirstNotEmpty(GetBuyerUserId(), request.Context?.BuyerId),
            OrganizationId = FirstNotEmpty(GetBuyerOrganizationId(), request.Context?.OrganizationId),
        };
    }

    protected virtual async Task<CustomerOrder> GetOrderByIdOrNumberAsync(OrderExecutionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.OrderId))
        {
            var orders = await _customerOrderService.GetAsync([request.OrderId], OrderResponseGroup, clone: false);
            var order = orders.FirstOrDefault();
            if (order != null)
            {
                return order;
            }
        }

        var number = FirstNotEmpty(request.OrderNumber, request.OrderId);
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var result = await _customerOrderSearchService.SearchAsync(new CustomerOrderSearchCriteria
        {
            Number = number,
            Take = 1,
            ResponseGroup = OrderResponseGroup,
        }, clone: false);

        return result.Results.FirstOrDefault();
    }

    protected virtual async Task<CustomerOrder> FindOrderByCartIdAsync(OrderExecutionRequest request)
    {
        var criteria = new CustomerOrderSearchCriteria
        {
            CustomerId = request.UserId,
            OrganizationId = request.OrganizationId,
            Take = RecentOrderLookupLimit,
            Sort = "CreatedDate:desc",
            ResponseGroup = OrderResponseGroup,
        };

        var order = await FindOrderByCartIdAsync(criteria, request.CartId);
        if (order != null || string.IsNullOrWhiteSpace(request.UserId) && string.IsNullOrWhiteSpace(request.OrganizationId))
        {
            return order;
        }

        criteria.CustomerId = null;
        criteria.OrganizationId = null;

        return await FindOrderByCartIdAsync(criteria, request.CartId);
    }

    protected virtual async Task<CustomerOrder> FindOrderByCartIdAsync(CustomerOrderSearchCriteria criteria, string cartId)
    {
        var result = await _customerOrderSearchService.SearchAsync(criteria, clone: false);
        return result.Results.FirstOrDefault(order => string.Equals(order.ShoppingCartId, cartId, StringComparison.OrdinalIgnoreCase));
    }

    protected virtual UcpOrder ReadOrder(CustomerOrder orderModel)
    {
        var currency = orderModel.Currency;
        var order = new UcpOrder
        {
            Id = orderModel.Id,
            Number = orderModel.Number,
            Status = orderModel.Status,
            StatusDisplayValue = orderModel.Status,
            CreatedAt = orderModel.CreatedDate.ToString("O"),
            CartId = orderModel.ShoppingCartId,
            StoreId = orderModel.StoreId,
            BuyerId = orderModel.CustomerId,
            CustomerName = orderModel.CustomerName,
            Currency = currency,
            Totals = new UcpOrderTotals
            {
                Subtotal = CreateMoney(orderModel.SubTotal, currency),
                Total = CreateMoney(orderModel.Total, currency),
                TaxTotal = CreateMoney(orderModel.TaxTotal, currency),
                DiscountTotal = CreateMoney(orderModel.DiscountTotal, currency),
                ShippingSubtotal = CreateMoney(orderModel.ShippingSubTotal, currency),
                ShippingTotal = CreateMoney(orderModel.ShippingTotal, currency),
            },
            LineItems = ReadLineItems(orderModel.Items, currency),
            Shipments = ReadShipments(orderModel.Shipments, currency),
            Payments = ReadPayments(orderModel.InPayments, currency),
        };

        order.Messages = ReadMessages(order);
        return order;
    }

    protected virtual IList<UcpOrderLineItem> ReadLineItems(IEnumerable<LineItem> items, string currency)
    {
        return (items ?? [])
            .Select(item => new UcpOrderLineItem
            {
                Id = item.Id,
                ProductId = item.ProductId,
                Sku = item.Sku,
                Name = item.Name,
                ImageUrl = item.ImageUrl,
                Quantity = item.Quantity,
                UnitPrice = CreateMoney(item.Price, FirstNotEmpty(item.Currency, currency)),
                PlacedPrice = CreateMoney(item.PlacedPrice, FirstNotEmpty(item.Currency, currency)),
                LineTotal = CreateMoney(item.ExtendedPrice, FirstNotEmpty(item.Currency, currency)),
                DiscountTotal = CreateMoney(item.DiscountTotal, FirstNotEmpty(item.Currency, currency)),
                TaxTotal = CreateMoney(item.TaxTotal, FirstNotEmpty(item.Currency, currency)),
            })
            .ToList();
    }

    protected virtual IList<UcpOrderShipment> ReadShipments(IEnumerable<Shipment> shipments, string currency)
    {
        return (shipments ?? [])
            .Select(shipment => new UcpOrderShipment
            {
                ShipmentMethodCode = shipment.ShipmentMethodCode,
                ShipmentMethodOption = shipment.ShipmentMethodOption,
                TrackingNumber = shipment.TrackingNumber,
                TrackingUrl = shipment.TrackingUrl,
                Price = CreateMoney(shipment.Price, FirstNotEmpty(shipment.Currency, currency)),
                DiscountAmount = CreateMoney(shipment.DiscountAmount, FirstNotEmpty(shipment.Currency, currency)),
                DeliveryAddress = ReadAddress(shipment.DeliveryAddress),
            })
            .ToList();
    }

    protected virtual IList<UcpOrderPayment> ReadPayments(IEnumerable<PaymentIn> payments, string currency)
    {
        return (payments ?? [])
            .Select(payment => new UcpOrderPayment
            {
                Id = payment.Id,
                Number = payment.Number,
                GatewayCode = payment.GatewayCode,
                Approved = payment.IsApproved,
                MethodCode = payment.PaymentMethod?.Code,
                MethodName = payment.PaymentMethod?.Name,
                BillingAddress = ReadAddress(payment.BillingAddress),
            })
            .ToList();
    }

    protected virtual UcpOrderAddress ReadAddress(Address address)
    {
        if (address == null)
        {
            return null;
        }

        return new UcpOrderAddress
        {
            Id = address.Key,
            Name = address.Name,
            Organization = address.Organization,
            FirstName = address.FirstName,
            LastName = address.LastName,
            Line1 = address.Line1,
            Line2 = address.Line2,
            City = address.City,
            Region = address.RegionName,
            RegionId = address.RegionId,
            PostalCode = FirstNotEmpty(address.PostalCode, address.Zip),
            CountryCode = address.CountryCode,
            CountryName = address.CountryName,
            Phone = address.Phone,
            Email = address.Email,
        };
    }

    protected virtual IList<UcpMessage> ReadMessages(UcpOrder order)
    {
        var messages = new List<UcpMessage>();

        if (order.Shipments.Any(shipment => !string.IsNullOrWhiteSpace(shipment.TrackingNumber) || !string.IsNullOrWhiteSpace(shipment.TrackingUrl)))
        {
            messages.Add(new UcpMessage
            {
                Type = "info",
                Code = "shipment_tracking_available",
                Content = "Shipment tracking information is available on this order.",
                Severity = "info",
            });
        }

        return messages;
    }

    protected virtual UcpMoney CreateMoney(decimal amount, string currency)
    {
        return new UcpMoney
        {
            Amount = ToMinorUnits(amount),
            Currency = currency,
        };
    }

    protected virtual string GetBuyerUserId()
    {
        return GetHeader(ModuleConstants.Headers.BuyerUserId);
    }

    protected virtual string GetBuyerOrganizationId()
    {
        return GetHeader(ModuleConstants.Headers.BuyerOrganizationId);
    }

    protected virtual string GetHeader(string name)
    {
        var headers = _httpContextAccessor.HttpContext?.Request.Headers;
        return headers != null && headers.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;
    }

    protected virtual string GetCorrelationId()
    {
        return FirstNotEmpty(GetHeader(ModuleConstants.Headers.CorrelationId), _httpContextAccessor.HttpContext?.TraceIdentifier);
    }

    protected virtual UcpException CreateException(string code, string message, int statusCode = StatusCodes.Status400BadRequest)
    {
        return new UcpException(code, message, statusCode)
        {
            Error = new UcpError
            {
                Code = code,
                Message = message,
                CorrelationId = GetCorrelationId(),
            },
        };
    }

    protected virtual UcpResponseMetadata CreateMetadata(string status, string capability)
    {
        return new UcpResponseMetadata
        {
            Version = ModuleConstants.UcpVersion,
            Status = status,
            CorrelationId = GetCorrelationId(),
            Capabilities =
            {
                [capability] =
                [
                    new UcpCapabilityVersion { Version = ModuleConstants.UcpVersion },
                ],
            },
        };
    }

    protected static long ToMinorUnits(decimal amount)
    {
        return Convert.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    protected static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    protected class OrderExecutionRequest
    {
        public string OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string CartId { get; set; }
        public string CultureName { get; set; }
        public string UserId { get; set; }
        public string OrganizationId { get; set; }
    }
}
