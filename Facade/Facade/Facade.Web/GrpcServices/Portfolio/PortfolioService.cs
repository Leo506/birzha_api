﻿using Calabonga.OperationResults;
using Facade.Web.Application;
using Facade.Web.GrpcServices.Portfolio.Aggregation;
using Facade.Web.GrpcServices.Product;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Orders;
using PortfolioGrpc;
using PortfolioServiceGrpc;
using ProductGrpc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Facade.Web.GrpcServices.Portfolio;

[Authorize]
public class PortfolioService : PortfolioServiceGrpc.PortfolioService.PortfolioServiceBase
{
    private readonly ILogger<PortfolioService> _logger;
    private readonly ServiceUrls _serviceUrls;

    public PortfolioService(
        ILogger<PortfolioService> logger,
        IOptionsMonitor<ServiceUrls> optionsMonitor)
    { 
        _logger = logger;
        _serviceUrls = optionsMonitor.CurrentValue;
    }

    public override async Task<PortfolioServiceGrpc.GetPortfolioResponse> GetPortfolio(PortfolioServiceGrpc.GetPortfolioRequest request, ServerCallContext context)
    {
        var httpHandler = new HttpClientHandler();
            httpHandler.ServerCertificateCustomValidationCallback = 
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    
        var channelPortfolio = GrpcChannel.ForAddress(_serviceUrls.PortfolioService, new GrpcChannelOptions { HttpHandler = httpHandler });
        var channelProduct = GrpcChannel.ForAddress(_serviceUrls.ProductService, new GrpcChannelOptions { HttpHandler = httpHandler });
        var channelOrders = GrpcChannel.ForAddress(_serviceUrls.OrdersService, new GrpcChannelOptions { HttpHandler = httpHandler });

        var portfolioClient = new PortfolioGrpc.PortfolioService.PortfolioServiceClient(channelPortfolio);
        var productClient = new ProductGrpc.ProductService.ProductServiceClient(channelProduct);
        var ordersClient = new OrdersService.OrdersServiceClient(channelOrders);
        var userId = context.GetHttpContext().User.Claims.FirstOrDefault(claim => claim.Type == "id")!.Value;
        var responsePortfolio = await TryGetPortfolio(portfolioClient, productClient, ordersClient, context, userId);

        if (responsePortfolio.Ok)
        {
            return responsePortfolio.Result;
        }

        return new GetPortfolioResponse()
        {
            Error = new PortfolioServiceGrpc.Error()
            {
                ErrorMessage = responsePortfolio.Exception == null ? "Failed to request" : responsePortfolio.Exception.Message,
                StackTrace = responsePortfolio.Exception == null ? new Exception().StackTrace : responsePortfolio.Exception.StackTrace
            }
        };
    }

    private async Task<OperationResult<GetPortfolioResponse>> TryGetPortfolio(
        PortfolioGrpc.PortfolioService.PortfolioServiceClient portfolioClient, 
        ProductGrpc.ProductService.ProductServiceClient productClient,
        OrdersService.OrdersServiceClient ordersClient,
        ServerCallContext context,
        string id)
    {
        var responsePortfolio = await TryGetAssets(context, portfolioClient, id);
        var assetsArray = responsePortfolio.Result.AssetArray.Assets;
        
        _logger.LogInformation($"Is assetArray == null ? {assetsArray == null}");
        _logger.LogInformation($"Get {assetsArray.Count} assets");

        var responseProduct = await TryGetAllProducts(productClient);
        var productsArray = responseProduct.Result.ProductArray.Products;

        _logger.LogInformation($"Products array == null ? {productsArray == null}");
        _logger.LogInformation($"Products array has {productsArray.Count} elements");

        var responseOrders = await TryGetOrders(context, ordersClient, productsArray, id);
        var userProductsInfo = responseOrders.Result.Success.Products;
        
        _logger.LogInformation($"userProductsInfo == null {userProductsInfo == null}");
        _logger.LogInformation($"UserProductsInfo contains {userProductsInfo.Count} elements");

        var result = OperationResult.CreateResult<GetPortfolioResponse>();
        
        GetPortfolioResponse portfolio = new GetPortfolioResponse()
        {
            Portfolio = new PortfolioServiceGrpc.Portfolio()
            {
                Total = new PortfolioServiceGrpc.Portfolio.Types.Total()
            }
        };
        try
        {
            var productsInPortfolio = PortfolioAggregator.AggregateProducts(portfolio, assetsArray, productsArray, userProductsInfo).Portfolio.Products;
            result.Result = PortfolioAggregator.AggregateTotal(portfolio, productsInPortfolio);
            if (result.Result == null)
            {
                result.AddError(new Exception("Failed to request"));
            }
        }
        catch(Exception e)
        {
            _logger.LogError(e.Message);
            result.AddError(e);
        }

        return result;
    }

    private async Task<OperationResult<GetAllProductsResponse>> TryGetAllProducts(ProductGrpc.ProductService.ProductServiceClient client)
    {
        var result = OperationResult.CreateResult<GetAllProductsResponse>();

        try
        {
            result.Result = await client.GetAllProductsAsync(new GetAllProductsRequest());
            if (result.Result == null)
            {
                result.AddError(new Exception("Failed to request"));
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            result.AddError(e);
        }

        return result;
    }

    private async Task<OperationResult<GetAllAssetsResponse>> TryGetAssets(ServerCallContext context, PortfolioGrpc.PortfolioService.PortfolioServiceClient client, string id)
    {
        var result = OperationResult.CreateResult<GetAllAssetsResponse>();
        
        try
        {
            result.Result = await client.GetAllAssetsAsync(new GetAllAssetsRequest { Id = id });
            if (result.Result == null)
            {
                result.AddError(new Exception("Failed to request"));
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            result.AddError(e);
        }

        return result;
    }

    private async Task<OperationResult<UserProductsResponse>> TryGetOrders(ServerCallContext context, OrdersService.OrdersServiceClient client,
        Google.Protobuf.Collections.RepeatedField<ProductArray.Types.Product> productsArray,
        string id)
    {
        var result = OperationResult.CreateResult<UserProductsResponse>();
        
        try
        {
            UserProductsRequest userProductsRequest = new UserProductsRequest();

            userProductsRequest.InvestorId = id;
            foreach (var product in productsArray) 
            { 
                userProductsRequest.ProductsId.Add(product.Id); 
            }
           

            result.Result = await client.GetUserProductsInfoAsync(userProductsRequest);
            if (result.Result == null)
            {
                result.AddError(new Exception("Failed to request"));
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            result.AddError(e);
        }

        return result;
    }
}

