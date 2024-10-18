using Convey.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Convey.WebApi;

public class EndpointsBuilder : IEndpointsBuilder
{
    private const string Query = "query";
    private const string Body = "body";

    private static readonly string[] HeadMethod = ["HEAD"];
    
    private readonly WebApiEndpointDefinitions _definitions;
    private readonly IEndpointRouteBuilder _routeBuilder;

    public EndpointsBuilder(IEndpointRouteBuilder routeBuilder, WebApiEndpointDefinitions definitions)
    {
        _routeBuilder = routeBuilder;
        _definitions = definitions;
    }

    public IEndpointsBuilder Get(
        string path,
        Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
    {
        var builder = _routeBuilder.MapGet(path, ctx => context?.Invoke(ctx) ?? Task.CompletedTask);
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition(HttpMethods.Get, path);

        return this;
    }

    public IEndpointsBuilder Get<TRequest>(
        string path,
        Func<TRequest, HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
        where TRequest : class
    {
        var builder = _routeBuilder.MapGet(path, ctx => BuildQueryContext(ctx, context));
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition<TRequest>(HttpMethods.Get, path);

        return this;
    }

    public IEndpointsBuilder Get<TRequest, TResult>(
        string path,
        Func<TRequest, HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
        where TRequest : class
    {
        var builder = _routeBuilder.MapGet(path, ctx => BuildQueryContext(ctx, context));
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition<TRequest, TResult>(HttpMethods.Get, path);

        return this;
    }

    public IEndpointsBuilder Head(
        string path,
        Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
    {
        var builder = _routeBuilder.MapMethods(path, HeadMethod, ctx => context?.Invoke(ctx) ?? Task.CompletedTask);
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition(HttpMethods.Head, path);

        return this;
    }

    public IEndpointsBuilder Head<TRequest>(
        string path,
        Func<TRequest, HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
        where TRequest : class
    {
        var builder = _routeBuilder.MapMethods(path, HeadMethod, ctx => BuildQueryContext(ctx, context));
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition<TRequest>(HttpMethods.Head, path);

        return this;
    }

    public IEndpointsBuilder Post(
        string path,
        Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
    {
        var builder = _routeBuilder.MapPost(path, ctx => context?.Invoke(ctx) ?? Task.CompletedTask);
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition(HttpMethods.Post, path);

        return this;
    }

    public IEndpointsBuilder Post<TRequest>(
        string path,
        Func<TRequest, HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
        where TRequest : class
    {
        var builder = _routeBuilder.MapPost(path, ctx => BuildRequestContext(ctx, context));
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition<TRequest>(HttpMethods.Post, path);

        return this;
    }

    public IEndpointsBuilder Put(
        string path,
        Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
    {
        var builder = _routeBuilder.MapPut(path, ctx => context?.Invoke(ctx) ?? Task.CompletedTask);
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition(HttpMethods.Put, path);

        return this;
    }

    public IEndpointsBuilder Put<TRequest>(
        string path,
        Func<TRequest, HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
        where TRequest : class
    {
        var builder = _routeBuilder.MapPut(path, ctx => BuildRequestContext(ctx, context));
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition<TRequest>(HttpMethods.Put, path);

        return this;
    }

    public IEndpointsBuilder Patch(
        string path,
        Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
    {
        var builder = _routeBuilder.MapPatch(path, ctx => context?.Invoke(ctx) ?? Task.CompletedTask);
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition(HttpMethods.Patch, path);

        return this;
    }

    public IEndpointsBuilder Patch<TRequest>(
        string path,
        Func<TRequest, HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
        where TRequest : class
    {
        var builder = _routeBuilder.MapPatch(path, ctx => BuildRequestContext(ctx, context));
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition<TRequest>(HttpMethods.Patch, path);

        return this;
    }

    public IEndpointsBuilder Delete(
        string path,
        Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
    {
        var builder = _routeBuilder.MapDelete(path, ctx => context?.Invoke(ctx) ?? Task.CompletedTask);
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition(HttpMethods.Delete, path);

        return this;
    }

    public IEndpointsBuilder Delete<TRequest>(
        string path,
        Func<TRequest, HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false,
        string roles = null,
        params string[] policies)
        where TRequest : class
    {
        var builder = _routeBuilder.MapDelete(path, ctx => BuildQueryContext(ctx, context));
        
        endpoint?.Invoke(builder);
        
        ApplyAuthRolesAndPolicies(builder, auth, roles, policies);
        AddEndpointDefinition<TRequest>(HttpMethods.Delete, path);

        return this;
    }

    private static void ApplyAuthRolesAndPolicies(IEndpointConventionBuilder builder, bool auth, string roles, params string[] policies)
    {
        if (policies?.Length > 0)
        {
            builder.RequireAuthorization(policies);
            return;
        }

        var hasRoles = !string.IsNullOrWhiteSpace(roles);
        var authorize = new AuthorizeAttribute();

        if (hasRoles)
        {
            authorize.Roles = roles;
        }

        if (auth || hasRoles)
        {
            builder.RequireAuthorization(authorize);
        }
    }

    private static async Task BuildRequestContext<TRequest>(HttpContext httpContext, Func<TRequest, HttpContext, Task> context = null)
        where TRequest : class
    {
        var request = await httpContext.ReadJsonAsync<TRequest>();

        if (request is null || context is null)
        {
            return;
        }

        await context.Invoke(request, httpContext);
    }

    private static async Task BuildQueryContext<TRequest>(HttpContext httpContext, Func<TRequest, HttpContext, Task> context = null)
        where TRequest : class
    {
        var request = httpContext.ReadQuery<TRequest>();

        if (request is null || context is null)
        {
            return;
        }

        await context.Invoke(request, httpContext);
    }

    private void AddEndpointDefinition(string method, string path)
    {
        _definitions.Add(new WebApiEndpointDefinition
        {
            Method = method,
            Path = path,
            Responses =
            [
                new()
                {
                    StatusCode = (int)HttpStatusCode.OK
                }
            ]
        });
    }

    private void AddEndpointDefinition<TRequest>(string method, string path)
        => AddEndpointDefinition(method, path, typeof(TRequest), null);

    private void AddEndpointDefinition<TRequest, TResponse>(string method, string path)
        => AddEndpointDefinition(method, path, typeof(TRequest), typeof(TResponse));

    private void AddEndpointDefinition(string method, string path, Type input, Type output)
    {
        if (_definitions.Exists(d => d.Path == path && d.Method == method))
        {
            return;
        }

        _definitions.Add(new WebApiEndpointDefinition
        {
            Method = method,
            Path = path,

            Parameters =
            [
                new()
                {
                    In =
                        method.ToLowerInvariant() switch
                        {
                            "get" => Query,
                            "head" => Query,
                            "delete" => Query,
                            "post" => Body,
                            "put" => Body,
                            "patch" => Body,
                            _ => Query
                        },
                    Name = input?.Name,
                    Type = input,
                    Example = input?.GetDefaultInstance()
                }
            ],

            Responses =
            [
                new()
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Type = output,
                    Example = output?.GetDefaultInstance()
                }
            ]
        });
    }
}